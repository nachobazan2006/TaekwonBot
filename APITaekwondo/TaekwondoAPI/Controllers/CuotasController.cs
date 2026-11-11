using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaekwondoAPI.Contracts;
using TaekwondoAPI.Data;
using TaekwondoAPI.Models;

namespace TaekwondoAPI.Controllers;

[ApiController]
[Route("api/cuotas")]
public sealed class CuotasController(TaekwondoDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await db.Cuotas.AsNoTracking().OrderByDescending(c => c.Fecha).ToListAsync());
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var payment = await db.Cuotas.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        return payment is null ? NotFound() : Ok(payment);
    }
    // Los formularios originales utilizan ahora la misma preparación y confirmación.
    [HttpPost]
    public IActionResult Post([FromBody] Cuota _) => ConfirmationRequired();
    [HttpPut("{id:int}")]
    public IActionResult Put(int id, [FromBody] Cuota _) => ConfirmationRequired();
    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id) => ConfirmationRequired();
    [HttpPost("{id:int}/enviar-recibo")]
    public IActionResult SendReceipt(int id) => Conflict(new BotError("local_external_disabled", "Los envíos externos están deshabilitados en el entorno ficticio."));
    private ConflictObjectResult ConfirmationRequired() => Conflict(new BotError("confirmation_required", "Prepará el cambio y confirmalo individualmente mediante TaekwonBot. Los registros contables no se borran."));
}
