using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TaekwondoAPI.Data;
using TaekwondoAPI.Services;

namespace TaekwondoAPI.Controllers
{
    [ApiController]
    [Route("api/notificaciones")]
    public class NotificacionesController : ControllerBase
    {
        private readonly TaekwondoDbContext _db;
        private readonly TwilioWhatsAppService _twilio;
        private readonly ILogger<NotificacionesController> _logger;

        public NotificacionesController(
            TaekwondoDbContext db,
            TwilioWhatsAppService twilio,
            ILogger<NotificacionesController> logger)
        {
            _db = db;
            _twilio = twilio;
            _logger = logger;
        }

        // POST api/notificaciones/enviar-pendientes?dryRun=false
        [HttpPost("enviar-pendientes")]
        [EnableRateLimiting("notificaciones")]
        public async Task<IActionResult> EnviarPendientes([FromQuery] bool dryRun = false)
        {
            var hoy = DateTime.UtcNow.Date;

            var config = await _db.ConfiguracionMensajes.FirstOrDefaultAsync();

            if (config == null)
            {
                config = new Models.ConfiguracionMensajes
                {
                    DiasRecordatorio = 7,
                    ReenviarAutomaticamente = true
                };

                _db.ConfiguracionMensajes.Add(config);
                await _db.SaveChangesAsync();
            }

            int dias = config.DiasRecordatorio;
            bool reenviarAutomaticamente = config.ReenviarAutomaticamente;

            var alumnosPendientes = await _db.Alumnos
                .Where(a => a.Estado.ToLower() == "pendiente")
                .OrderBy(a => a.FechaContacto)
                .ToListAsync();

            List<Models.Alumno> candidatos;

            if (reenviarAutomaticamente)
            {
                candidatos = alumnosPendientes
                    .Where(a =>
                        (a.FechaUltimoMensaje == null && a.FechaContacto.Date.AddDays(dias) <= hoy)
                        ||
                        (a.FechaUltimoMensaje != null && a.FechaUltimoMensaje.Value.Date.AddDays(dias) <= hoy)
                    )
                    .ToList();
            }
            else
            {
                candidatos = alumnosPendientes
                    .Where(a =>
                        !a.MensajeEnviado &&
                        a.FechaContacto.Date.AddDays(dias) <= hoy
                    )
                    .ToList();
            }

            if (candidatos.Count == 0)
            {
                return Ok(new
                {
                    ok = true,
                    dryRun,
                    diasRecordatorio = dias,
                    reenviarAutomaticamente,
                    total = 0,
                    candidatos = Array.Empty<object>(),
                    enviados = Array.Empty<string>(),
                    errores = Array.Empty<object>()
                });
            }

            // DRY RUN: no manda, solo muestra lo que haría
            if (dryRun)
            {
                var debugCandidatos = candidatos.Select(a =>
                {
                    var dbg = NormalizarE164AR_Debug(a.Numero);

                    return new
                    {
                        numeroOriginal = a.Numero,
                        a.FechaContacto,
                        a.FechaUltimoMensaje,
                        a.Estado,
                        a.MensajeEnviado,
                        dbg.onlyDigits,
                        dbg.length,
                        toE164 = dbg.toE164,
                        proximoEnvioDesde =
                            a.FechaUltimoMensaje == null
                                ? a.FechaContacto.Date.AddDays(dias)
                                : a.FechaUltimoMensaje.Value.Date.AddDays(dias)
                    };
                }).ToList();

                return Ok(new
                {
                    ok = true,
                    dryRun = true,
                    diasRecordatorio = dias,
                    reenviarAutomaticamente,
                    total = debugCandidatos.Count,
                    candidatos = debugCandidatos
                });
            }

            if (!_twilio.IsConfiguredForTemplate())
            {
                return BadRequest(new
                {
                    ok = false,
                    error = "Twilio no está listo para enviar templates.",
                    detalle = "Falta TemplateSid (Content SID HX...) o credenciales en Azure Environment Variables."
                });
            }

            var enviados = new List<string>();
            var errores = new List<object>();

            foreach (var a in candidatos)
            {
                try
                {
                    var toE164 = NormalizarE164AR(a.Numero);
                    await _twilio.EnviarTemplateAsync(toE164, new { });

                    a.MensajeEnviado = true;
                    a.FechaUltimoMensaje = DateTime.UtcNow;
                    enviados.Add(a.Numero);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al enviar WhatsApp al número {Numero}", a.Numero);
                    errores.Add(new { numero = a.Numero, error = "Error al enviar el mensaje." });
                }
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                dryRun = false,
                diasRecordatorio = dias,
                reenviarAutomaticamente,
                total = candidatos.Count,
                enviados,
                errores
            });
        }

        private static (string onlyDigits, int length, string toE164) NormalizarE164AR_Debug(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new Exception("Número vacío");

            var onlyDigits = new string(input.Where(char.IsDigit).ToArray());
            var len = onlyDigits.Length;

            string toE164;

            if (len == 12 && onlyDigits.StartsWith("549"))
                toE164 = "+" + onlyDigits;
            else if (len == 10)
                toE164 = "+549" + onlyDigits;
            else if (len == 13 && onlyDigits.StartsWith("54"))
                toE164 = "+" + onlyDigits;
            else
                throw new Exception($"Formato de número no reconocido.");

            return (onlyDigits, len, toE164);
        }

        private static string NormalizarE164AR(string input)
        {
            var dbg = NormalizarE164AR_Debug(input);
            return dbg.toE164;
        }
    }
}
