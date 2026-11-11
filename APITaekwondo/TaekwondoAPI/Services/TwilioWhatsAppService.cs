using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace TaekwondoAPI.Services
{
    public class TwilioWhatsAppService
    {
        private readonly string _sid;
        private readonly string _token;
        private readonly string _from;
        private readonly string _templateSid;

        public TwilioWhatsAppService(IConfiguration config)
        {
            _sid = config["Twilio:AccountSid"] ?? "";
            _token = config["Twilio:AuthToken"] ?? "";
            _from = config["Twilio:WhatsAppFrom"] ?? "";
            _templateSid = config["Twilio:TemplateSid"] ?? "";
        }

        public bool IsConfiguredBasic()
            => !string.IsNullOrWhiteSpace(_sid)
            && !string.IsNullOrWhiteSpace(_token)
            && !string.IsNullOrWhiteSpace(_from);

        public bool IsConfiguredForTemplate()
        {
            return !string.IsNullOrWhiteSpace(_templateSid)
                && !string.IsNullOrWhiteSpace(_from)
                && !string.IsNullOrWhiteSpace(_sid)
                && !string.IsNullOrWhiteSpace(_token);
        }

        private void EnsureClient()
        {
            if (!IsConfiguredBasic())
                throw new InvalidOperationException("Twilio no está configurado: faltan AccountSid/AuthToken/WhatsAppFrom.");

            TwilioClient.Init(_sid, _token);
        }

        public async Task<string> EnviarTemplateAsync(string toE164, object variables)
        {
            if (!IsConfiguredForTemplate())
                throw new InvalidOperationException("Twilio no está listo para templates: falta TemplateSid (Content SID HX...).");

            EnsureClient();

            var to = new PhoneNumber($"whatsapp:{toE164}");
            var from = new PhoneNumber(_from);
            var varsJson = JsonSerializer.Serialize(variables);

            var msg = await MessageResource.CreateAsync(
                to: to,
                from: from,
                contentSid: _templateSid,
                contentVariables: varsJson
            );

            return msg.Sid;
        }

        public async Task<string> EnviarTextoAsync(string toE164, string body)
        {
            EnsureClient();

            var msg = await MessageResource.CreateAsync(
                to: new PhoneNumber($"whatsapp:{toE164}"),
                from: new PhoneNumber(_from),
                body: body
            );

            return msg.Sid;
        }
    }
}