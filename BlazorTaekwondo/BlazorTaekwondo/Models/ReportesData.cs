namespace BlazorTaekwondo.Models
{
    public class ReportesData
    {
        // ── KPIs ────────────────────────────────────────────────────
        public int TotalAlumnos { get; set; }
        public decimal TotalCuotas { get; set; }
        public decimal TotalExamenes { get; set; }
        public decimal TotalRecaudacion => TotalCuotas + TotalExamenes;
        public int TotalPagosCuotas { get; set; }
        public int AlumnosUnicosCuotas { get; set; }
        public int TotalExamenesRendidos => ExamenesPorTipo.Sum(x => x.Cantidad);

        // ── Cuotas ──────────────────────────────────────────────────
        public List<StatItem> CuotasPorMes { get; set; } = new();
        public List<StatItem> CuotasPorClase { get; set; } = new();

        // ── Exámenes ────────────────────────────────────────────────
        public List<StatItem> ExamenesPorTipo { get; set; } = new();
        public List<StatItem> ExamenesPorCinturon { get; set; } = new();
        public List<StatItem> ExamenesPorClase { get; set; } = new();

        // ── Padrón ──────────────────────────────────────────────────
        public List<StatItem> AlumnosPorHorario { get; set; } = new();
        public int AlumnosConFichaMedica { get; set; }
        public int AlumnosConSocio { get; set; }
        public List<StatItem> AltasPorMes { get; set; } = new();
        public int AltasMesActual { get; set; }
        public decimal PromedioAltasPrevias { get; set; }
        public List<StatItem> RankingCaptacionMeses { get; set; } = new();
        public List<StatItem> RecaudacionPorHorario { get; set; } = new();
        public string MesActualLabel { get; set; } = string.Empty;
        public int AlumnosSinCuotaMesActual { get; set; }
        public List<string> AlumnosSinCuotaMesActualDetalle { get; set; } = new();
        public List<PeriodoFiltroItem> PeriodosDisponibles { get; set; } = new();
        public string PeriodoSeleccionado { get; set; } = string.Empty;
    }

    public class StatItem
    {
        public string Label { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public int Cantidad { get; set; }
    }

    public class PeriodoFiltroItem
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
