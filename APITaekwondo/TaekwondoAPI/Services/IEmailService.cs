namespace TaekwondoAPI.Services
{
    public interface IEmailService
    {
        Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml, EmailAttachment? adjunto = null);
    }
}
