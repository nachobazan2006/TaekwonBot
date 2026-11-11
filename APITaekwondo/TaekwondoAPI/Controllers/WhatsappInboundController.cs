using Microsoft.AspNetCore.Mvc;
using System.Text;
using TaekwondoAPI.Filters;

namespace TaekwondoAPI.Controllers
{
    [ApiController]
    [Route("api/whatsapp")]
    [SkipApiKeyAuth] // Twilio llama este webhook directamente, sin nuestra API key
    public class WhatsAppInboundController : ControllerBase
    {
        private readonly string _contactPhone;

        public WhatsAppInboundController(IConfiguration config)
        {
            _contactPhone = config["App:ContactPhone"] ?? "sin número configurado";
        }

        [HttpPost("inbound")]
        public IActionResult Inbound()
        {
            string respuestaAutomatica =
                $"Para más información, comunicarse al número de contacto 📞: {_contactPhone}\n" +
                "¡Te esperamos!";

            string twiml =
                $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Response>
    <Message>{System.Security.SecurityElement.Escape(respuestaAutomatica)}</Message>
</Response>";

            return Content(twiml, "text/xml", Encoding.UTF8);
        }
    }
}
