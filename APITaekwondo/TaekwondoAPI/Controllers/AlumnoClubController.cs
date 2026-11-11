using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaekwondoAPI.Data;
using TaekwondoAPI.Models;

[ApiController]
[Route("api/alumnosclub")]
public class AlumnosClubController : ControllerBase
{
    private readonly TaekwondoDbContext _context;
    private readonly ILogger<AlumnosClubController> _logger;

    public AlumnosClubController(TaekwondoDbContext context, ILogger<AlumnosClubController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
        => Ok(await _context.AlumnosClub.ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] AlumnoClub alumno)
    {
        var validationError = ValidarAlumno(alumno);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        alumno.NombreApellido = alumno.NombreApellido.Trim();
        alumno.Graduacion = alumno.Graduacion?.Trim() ?? string.Empty;
        alumno.Horario = alumno.Horario.Trim();
        alumno.FichaMedica = NormalizarSiNo(alumno.FichaMedica);
        alumno.Socio = NormalizarSiNo(alumno.Socio);
        alumno.Gmail = alumno.Gmail?.Trim() ?? string.Empty;
        alumno.FechaRegistro = NormalizarFecha(alumno.FechaRegistro);

        try
        {
            _context.AlumnosClub.Add(alumno);
            await _context.SaveChangesAsync();
            return Ok(alumno);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error al crear AlumnoClub {@Alumno}", alumno);
            return Problem(
                title: "Error al guardar el alumno",
                detail: ex.InnerException?.Message ?? ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al crear AlumnoClub {@Alumno}", alumno);
            return Problem(
                title: "Error inesperado al guardar el alumno",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Put(int id, [FromBody] AlumnoClub alumno)
    {
        var validationError = ValidarAlumno(alumno);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var existente = await _context.AlumnosClub.FindAsync(id);
        if (existente == null) return NotFound();

        existente.NombreApellido = alumno.NombreApellido.Trim();
        existente.Graduacion = alumno.Graduacion?.Trim() ?? string.Empty;
        existente.Horario = alumno.Horario.Trim();
        existente.FichaMedica = NormalizarSiNo(alumno.FichaMedica);
        existente.Socio = NormalizarSiNo(alumno.Socio);
        existente.Gmail = alumno.Gmail?.Trim() ?? string.Empty;
        existente.FechaRegistro = NormalizarFecha(alumno.FechaRegistro);

        try
        {
            await _context.SaveChangesAsync();
            return NoContent();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error al actualizar AlumnoClub {Id} {@Alumno}", id, alumno);
            return Problem(
                title: "Error al actualizar el alumno",
                detail: ex.InnerException?.Message ?? ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al actualizar AlumnoClub {Id} {@Alumno}", id, alumno);
            return Problem(
                title: "Error inesperado al actualizar el alumno",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var alumno = await _context.AlumnosClub.FindAsync(id);
        if (alumno == null) return NotFound();

        _context.AlumnosClub.Remove(alumno);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static string? ValidarAlumno(AlumnoClub alumno)
    {
        if (string.IsNullOrWhiteSpace(alumno.NombreApellido))
        {
            return "El nombre y apellido es obligatorio.";
        }

        if (string.IsNullOrWhiteSpace(alumno.Horario))
        {
            return "El horario es obligatorio.";
        }

        return null;
    }

    private static string NormalizarSiNo(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? "No" : valor.Trim();
    }

    private static DateTime NormalizarFecha(DateTime fecha)
    {
        if (fecha == default)
        {
            fecha = DateTime.Today;
        }

        return fecha.Kind switch
        {
            DateTimeKind.Utc => fecha,
            DateTimeKind.Local => fecha.ToUniversalTime(),
            _ => DateTime.SpecifyKind(fecha, DateTimeKind.Utc)
        };
    }
}
