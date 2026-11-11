using BlazorTaekwondo.Models;
using System.Net.Http.Json;

namespace BlazorTaekwondo.Services
{
    public class ReportesService
    {
        private readonly HttpClient _http;

        public string LastError { get; private set; } = string.Empty;

        public ReportesService(HttpClient http) => _http = http;

        public async Task<ReportesData?> CargarDatosAsync(string? periodoSeleccionado = null)
        {
            try
            {
                LastError = string.Empty;

                var t1 = _http.GetFromJsonAsync<List<AlumnoDto>>("api/alumnosclub");
                var t2 = _http.GetFromJsonAsync<List<CuotaDto>>("api/cuotas");
                var t3 = _http.GetFromJsonAsync<List<ExamenDto>>("api/examenes");
                await Task.WhenAll(t1, t2, t3);

                var alumnos = (await t1) ?? new();
                var cuotas = (await t2) ?? new();
                var examenes = (await t3) ?? new();

                return Build(alumnos, cuotas, examenes, periodoSeleccionado);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return null;
            }
        }

        private static ReportesData Build(
            List<AlumnoDto> alumnos,
            List<CuotaDto> cuotas,
            List<ExamenDto> examenes,
            string? periodoSeleccionado)
        {
            var hoy = DateTime.Today;
            cuotas = cuotas.Where(c => c.Estado == "active" && c.AlumnoClubId > 0 && c.PeriodoAnio > 0 && c.PeriodoMes is >= 1 and <= 12).ToList();
            var periodosDisponibles = ConstruirPeriodosDisponibles(alumnos, cuotas, hoy);
            var periodoActivo = ResolverPeriodoSeleccionado(periodoSeleccionado, periodosDisponibles, hoy);
            var inicioPeriodo = new DateTime(periodoActivo.Year, periodoActivo.Month, 1);
            var finPeriodo = inicioPeriodo.AddMonths(1).AddDays(-1);

            var cuotasPeriodo = cuotas
                .Where(c => c.PeriodoAnio == inicioPeriodo.Year && c.PeriodoMes == inicioPeriodo.Month)
                .ToList();

            var alumnosHastaPeriodo = alumnos
                .Where(a => a.FechaRegistro.Date <= finPeriodo.Date)
                .ToList();

            var altasHistoricas = alumnos
                .GroupBy(a => new DateTime(a.FechaRegistro.Year, a.FechaRegistro.Month, 1))
                .OrderBy(g => g.Key)
                .Select(g => new StatItem
                {
                    Label = $"{MesCorto(g.Key.Month)} {g.Key.Year}",
                    Cantidad = g.Count()
                })
                .ToList();

            var altasMesActual = alumnos.Count(a =>
                a.FechaRegistro.Year == inicioPeriodo.Year &&
                a.FechaRegistro.Month == inicioPeriodo.Month);

            var promedioAltasPrevias = alumnos
                .Where(a => a.FechaRegistro.Date < inicioPeriodo.Date)
                .GroupBy(a => new DateTime(a.FechaRegistro.Year, a.FechaRegistro.Month, 1))
                .Select(g => g.Count())
                .DefaultIfEmpty(0)
                .Average();

            var alumnosConCuotaPeriodo = cuotasPeriodo
                .Select(c => c.AlumnoClubId)
                .ToHashSet();

            var alumnosSinCuotaPeriodo = alumnosHastaPeriodo
                .Where(a => !string.IsNullOrWhiteSpace(a.NombreApellido))
                .Where(a => !alumnosConCuotaPeriodo.Contains(a.Id))
                .OrderBy(a => a.NombreApellido)
                .Select(a => $"{a.NombreApellido.Trim()} · {a.Horario} · {a.Graduacion}")
                .ToList();

            var cuotasPorMesContexto = cuotas
                .Where(c => new DateTime(c.PeriodoAnio, c.PeriodoMes, 1) <= finPeriodo.Date)
                .GroupBy(c => new DateTime(c.PeriodoAnio, c.PeriodoMes, 1))
                .OrderBy(g => g.Key)
                .TakeLast(6)
                .Select(g => new StatItem
                {
                    Label = $"{MesCorto(g.Key.Month)} {g.Key.Year}",
                    Valor = g.Sum(x => x.Monto),
                    Cantidad = g.Count()
                })
                .ToList();

            var altasPorMesContexto = altasHistoricas
                .TakeLast(6)
                .ToList();

            var recaudacionPorHorario = cuotasPeriodo
                .GroupBy(c => Normalizar(c.Clase))
                .Select(g => new StatItem
                {
                    Label = g.Key,
                    Valor = g.Sum(x => x.Monto),
                    Cantidad = g.Count()
                })
                .OrderByDescending(x => x.Valor)
                .ToList();

            return new ReportesData
            {
                PeriodosDisponibles = periodosDisponibles
                    .Select(p => new PeriodoFiltroItem
                    {
                        Key = $"{p.Year:D4}-{p.Month:D2}",
                        Label = $"{MesLargo(p.Month)} {p.Year}"
                    })
                    .ToList(),
                PeriodoSeleccionado = $"{inicioPeriodo.Year:D4}-{inicioPeriodo.Month:D2}",

                TotalAlumnos = alumnosHastaPeriodo.Count,
                TotalCuotas = cuotasPeriodo.Sum(c => c.Monto),
                TotalExamenes = examenes.Sum(e => e.Monto),
                TotalPagosCuotas = cuotasPeriodo.Count,
                AlumnosUnicosCuotas = cuotasPeriodo
                    .Select(c => c.AlumnoClubId)
                    .Distinct()
                    .Count(),

                CuotasPorMes = cuotasPorMesContexto,
                CuotasPorClase = cuotasPeriodo
                    .GroupBy(c => Normalizar(c.Clase))
                    .Select(g => new StatItem
                    {
                        Label = g.Key,
                        Valor = g.Sum(x => x.Monto),
                        Cantidad = g.Count()
                    })
                    .OrderByDescending(x => x.Valor)
                    .ToList(),

                ExamenesPorTipo = examenes
                    .GroupBy(e => Normalizar(e.ExamenRendir, "Sin tipo"))
                    .Select(g => new StatItem
                    {
                        Label = g.Key,
                        Valor = g.Sum(x => x.Monto),
                        Cantidad = g.Count()
                    })
                    .OrderByDescending(x => x.Cantidad)
                    .ToList(),

                ExamenesPorCinturon = examenes
                    .Select(e => new { Examen = e, Graduacion = ObtenerGraduacionExamen(e) })
                    .Where(e => !string.IsNullOrWhiteSpace(e.Graduacion))
                    .GroupBy(e => e.Graduacion)
                    .Select(g => new StatItem
                    {
                        Label = g.Key,
                        Valor = g.Sum(x => x.Examen.Monto),
                        Cantidad = g.Count()
                    })
                    .OrderByDescending(x => x.Cantidad)
                    .ToList(),

                ExamenesPorClase = examenes
                    .GroupBy(e => Normalizar(e.Clase))
                    .Select(g => new StatItem
                    {
                        Label = g.Key,
                        Valor = g.Sum(x => x.Monto),
                        Cantidad = g.Count()
                    })
                    .OrderByDescending(x => x.Cantidad)
                    .ToList(),

                AlumnosPorHorario = alumnosHastaPeriodo
                    .GroupBy(a => string.IsNullOrWhiteSpace(a.Horario) ? "Sin asignar" : a.Horario)
                    .Select(g => new StatItem
                    {
                        Label = g.Key,
                        Cantidad = g.Count()
                    })
                    .OrderBy(x => x.Label)
                    .ToList(),

                AlumnosConFichaMedica = alumnosHastaPeriodo.Count(a => EsAfirmativo(a.FichaMedica)),
                AlumnosConSocio = alumnosHastaPeriodo.Count(a => EsAfirmativo(a.Socio)),
                AltasPorMes = altasPorMesContexto,
                AltasMesActual = altasMesActual,
                PromedioAltasPrevias = decimal.Round((decimal)promedioAltasPrevias, 1),
                RankingCaptacionMeses = altasHistoricas
                    .Where(x => ParsePeriodoKey(x.Label) <= inicioPeriodo)
                    .OrderByDescending(x => x.Cantidad)
                    .ThenBy(x => x.Label)
                    .Take(5)
                    .ToList(),
                RecaudacionPorHorario = recaudacionPorHorario,
                MesActualLabel = $"{MesLargo(inicioPeriodo.Month)} {inicioPeriodo.Year}",
                AlumnosSinCuotaMesActual = alumnosSinCuotaPeriodo.Count,
                AlumnosSinCuotaMesActualDetalle = alumnosSinCuotaPeriodo
            };
        }

        private static List<DateTime> ConstruirPeriodosDisponibles(List<AlumnoDto> alumnos, List<CuotaDto> cuotas, DateTime hoy)
        {
            return alumnos
                .Select(a => new DateTime(a.FechaRegistro.Year, a.FechaRegistro.Month, 1))
                .Concat(cuotas.Select(c => new DateTime(c.PeriodoAnio, c.PeriodoMes, 1)))
                .Append(new DateTime(hoy.Year, hoy.Month, 1))
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();
        }

        private static DateTime ResolverPeriodoSeleccionado(string? periodoSeleccionado, List<DateTime> periodosDisponibles, DateTime hoy)
        {
            if (!string.IsNullOrWhiteSpace(periodoSeleccionado) &&
                DateTime.TryParse($"{periodoSeleccionado}-01", out var periodoParseado))
            {
                var periodoNormalizado = new DateTime(periodoParseado.Year, periodoParseado.Month, 1);
                if (periodosDisponibles.Contains(periodoNormalizado))
                    return periodoNormalizado;
            }

            return periodosDisponibles.FirstOrDefault(new DateTime(hoy.Year, hoy.Month, 1));
        }

        private static DateTime ParsePeriodoKey(string label)
        {
            var partes = label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length != 2 || !int.TryParse(partes[1], out var year))
                return DateTime.MinValue;

            var month = Array.IndexOf(new[] { "", "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" }, partes[0]);
            return month <= 0 ? DateTime.MinValue : new DateTime(year, month, 1);
        }

        private static string MesCorto(int m)
        {
            var nombres = new[] { "", "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
            return m >= 1 && m <= 12 ? nombres[m] : m.ToString();
        }

        private static string MesLargo(int m)
        {
            var nombres = new[]
            {
                "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
            };
            return m >= 1 && m <= 12 ? nombres[m] : m.ToString();
        }

        private static string Normalizar(string valor, string fallback = "Sin clase") =>
            string.IsNullOrWhiteSpace(valor) ? fallback : valor.Trim();

        private static string NormalizarNombre(string valor) =>
            string.IsNullOrWhiteSpace(valor) ? string.Empty : valor.Trim().ToLowerInvariant();

        private static string ObtenerGraduacionExamen(ExamenDto examen)
        {
            if (!string.IsNullOrWhiteSpace(examen.Graduacion))
                return examen.Graduacion.Trim();

            return EsValorSiNo(examen.Cinturon) ? string.Empty : Normalizar(examen.Cinturon, string.Empty);
        }

        private static bool EsValorSiNo(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return false;

            var normalizado = valor.Trim().ToLowerInvariant();
            return normalizado is "si" or "sí" or "no";
        }

        private static bool EsAfirmativo(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return false;

            var normalizado = valor.Trim().ToLowerInvariant();
            return normalizado is "si" or "sí";
        }
    }
}
