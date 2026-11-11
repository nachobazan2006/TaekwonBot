using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaekwondoAPI.Data;
using TaekwondoAPI.Models;

namespace TaekwondoAPI.Controllers
{
    [ApiController]
    [Route("api/alumnos")]
    public class AlumnosController : ControllerBase
    {
        private readonly TaekwondoDbContext _context;

        public AlumnosController(TaekwondoDbContext context)
        {
            _context = context;
        }

        // =========================
        // GET → traer todos
        // =========================
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var alumnos = await _context.Alumnos.ToListAsync();
            return Ok(alumnos);
        }

        // =========================
        // POST → agregar alumno
        // =========================
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Alumno alumno)
        {
            if (await _context.Alumnos.AnyAsync(a => a.Numero == alumno.Numero))
                return BadRequest("El número ya existe");

            _context.Alumnos.Add(alumno);
            await _context.SaveChangesAsync();

            return Ok(alumno);
        }

        // =========================
        // PUT → editar alumno
        // =========================
        [HttpPut("{numero}")]
        public async Task<IActionResult> Put(string numero, [FromBody] Alumno alumnoActualizado)
        {
            var alumno = await _context.Alumnos
                .FirstOrDefaultAsync(a => a.Numero == numero);

            if (alumno == null)
                return NotFound();

            alumno.Estado = alumnoActualizado.Estado;
            alumno.FechaContacto = alumnoActualizado.FechaContacto;
            alumno.MensajeEnviado = alumnoActualizado.MensajeEnviado;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // =========================
        // DELETE → eliminar alumno
        // =========================
        [HttpDelete("{numero}")]
        public async Task<IActionResult> Delete(string numero)
        {
            var alumno = await _context.Alumnos
                .FirstOrDefaultAsync(a => a.Numero == numero);

            if (alumno == null)
                return NotFound();

            _context.Alumnos.Remove(alumno);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        [HttpPut("{numero}/marcar-enviado")]
        public async Task<IActionResult> MarcarMensajeEnviado(string numero)
        {
            var alumno = await _context.Alumnos
                .FirstOrDefaultAsync(a => a.Numero == numero);

            if (alumno == null)
                return NotFound();

            alumno.MensajeEnviado = true;

            await _context.SaveChangesAsync();

            return NoContent();
        }
        [HttpGet("pendientes")]
        public async Task<ActionResult<IEnumerable<Alumno>>> GetPendientes()
        {
            var pendientes = await _context.Alumnos
                .Where(a => !a.MensajeEnviado)
                .OrderBy(a => a.FechaContacto)
                .ToListAsync();

            return Ok(pendientes);
        }


    }
}
