using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaekwondoAPI.Data;
using TaekwondoAPI.Models;

namespace TaekwondoAPI.Controllers
{
    [ApiController]
    [Route("api/configuracionmensajes")]
    public class ConfiguracionMensajesController : ControllerBase
    {
        private readonly TaekwondoDbContext _db;

        public ConfiguracionMensajesController(TaekwondoDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var config = await _db.ConfiguracionMensajes.FirstOrDefaultAsync();

            if (config == null)
            {
                config = new ConfiguracionMensajes
                {
                    DiasRecordatorio = 7,
                    ReenviarAutomaticamente = true
                };

                _db.ConfiguracionMensajes.Add(config);
                await _db.SaveChangesAsync();
            }

            return Ok(config);
        }

        [HttpPut]
        public async Task<IActionResult> Put([FromBody] ConfiguracionMensajes nuevaConfig)
        {
            if (nuevaConfig.DiasRecordatorio < 1 || nuevaConfig.DiasRecordatorio > 31)
            {
                return BadRequest(new
                {
                    ok = false,
                    error = "La cantidad de días debe estar entre 1 y 31."
                });
            }

            var config = await _db.ConfiguracionMensajes.FirstOrDefaultAsync();

            if (config == null)
            {
                config = new ConfiguracionMensajes
                {
                    DiasRecordatorio = nuevaConfig.DiasRecordatorio,
                    ReenviarAutomaticamente = nuevaConfig.ReenviarAutomaticamente
                };

                _db.ConfiguracionMensajes.Add(config);
            }
            else
            {
                config.DiasRecordatorio = nuevaConfig.DiasRecordatorio;
                config.ReenviarAutomaticamente = nuevaConfig.ReenviarAutomaticamente;
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                mensaje = "Configuración guardada correctamente."
            });
        }
    }
}