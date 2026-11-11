using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using TaekwondoAPI.Data;
using TaekwondoAPI.Models;

namespace TaekwondoAPI.Controllers
{
    [ApiController]
    [Route("api/examenes")]
    public class ExamenesController : ControllerBase
    {
        private readonly TaekwondoDbContext _db;

        public ExamenesController(TaekwondoDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            await EnsureExamenesSchemaAsync();

            var lista = await _db.Examenes
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            return Ok(lista);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            await EnsureExamenesSchemaAsync();

            var item = await _db.Examenes.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Examen examen)
        {
            await EnsureExamenesSchemaAsync();

            _db.Examenes.Add(examen);
            await _db.SaveChangesAsync();
            return Ok(examen);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Put(int id, [FromBody] Examen examen)
        {
            await EnsureExamenesSchemaAsync();

            var existente = await _db.Examenes.FindAsync(id);
            if (existente == null) return NotFound();

            existente.NombreApellido = examen.NombreApellido;
            existente.ExamenRendir = examen.ExamenRendir;
            existente.Madera = examen.Madera;
            existente.Cinturon = examen.Cinturon;
            existente.Graduacion = examen.Graduacion;
            existente.Monto = examen.Monto;
            existente.Clase = examen.Clase;
            existente.FechaExamen = examen.FechaExamen;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await EnsureExamenesSchemaAsync();

            var existente = await _db.Examenes.FindAsync(id);
            if (existente == null) return NotFound();

            _db.Examenes.Remove(existente);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private async Task EnsureExamenesSchemaAsync()
        {
            var connection = _db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Examenes') WHERE name = 'Graduacion'";

                var result = await checkCommand.ExecuteScalarAsync();
                var exists = Convert.ToInt32(result) > 0;
                if (exists)
                {
                    return;
                }

                await using var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE Examenes ADD COLUMN Graduacion TEXT NOT NULL DEFAULT ''";
                await alterCommand.ExecuteNonQueryAsync();
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }
    }
}
