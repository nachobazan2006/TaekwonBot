using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace TaekwondoAPI.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public SmtpEmailService(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml, EmailAttachment? adjunto = null)
        {
            ValidarConfiguracion();

            var remitente = string.IsNullOrWhiteSpace(_settings.FromEmail)
                ? _settings.UserName
                : _settings.FromEmail;

            using var mensaje = new MailMessage
            {
                From = new MailAddress(remitente, _settings.FromName),
                Subject = asunto,
                Body = cuerpoHtml,
                IsBodyHtml = true
            };
            mensaje.To.Add(destinatario);

            if (adjunto is not null && adjunto.Content.Length > 0)
            {
                var stream = new MemoryStream(adjunto.Content);
                mensaje.Attachments.Add(new Attachment(stream, adjunto.FileName, adjunto.ContentType));
            }

            using var smtp = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.UserName, _settings.Password)
            };

            await smtp.SendMailAsync(mensaje);
        }

        private void ValidarConfiguracion()
        {
            if (string.IsNullOrWhiteSpace(_settings.Host) ||
                string.IsNullOrWhiteSpace(_settings.UserName) ||
                string.IsNullOrWhiteSpace(_settings.Password))
            {
                throw new InvalidOperationException("Falta configurar EmailSettings:Host, UserName o Password.");
            }
        }
    }
}
