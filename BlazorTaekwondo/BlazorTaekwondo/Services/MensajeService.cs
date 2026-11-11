using BlazorTaekwondo.Models;
using System.Net.Http.Json;

namespace BlazorTaekwondo.Services
{
    public class MensajeService
    {
        private readonly HttpClient _http;
        private const string ContactosUrl = "api/alumnos";
        private const string ConfigUrl = "api/configuracionmensajes";
        private const string NotificacionesUrl = "api/notificaciones/enviar-pendientes";

        public MensajeService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<ContactoMensajeDto>> GetContactosAsync()
        {
            return await _http.GetFromJsonAsync<List<ContactoMensajeDto>>(ContactosUrl) ?? new();
        }

        public async Task<bool> AgregarContactoAsync(ContactoMensajeDto contacto)
        {
            var response = await _http.PostAsJsonAsync(ContactosUrl, contacto);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> EditarContactoAsync(string numeroOriginal, ContactoMensajeDto contacto)
        {
            var response = await _http.PutAsJsonAsync($"{ContactosUrl}/{numeroOriginal}", contacto);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> EliminarContactoAsync(string numero)
        {
            var response = await _http.DeleteAsync($"{ContactosUrl}/{numero}");
            return response.IsSuccessStatusCode;
        }

        public async Task<ConfiguracionMensajesDto> GetConfiguracionAsync()
        {
            return await _http.GetFromJsonAsync<ConfiguracionMensajesDto>(ConfigUrl) ?? new();
        }

        public async Task<bool> GuardarConfiguracionAsync(ConfiguracionMensajesDto config)
        {
            var response = await _http.PutAsJsonAsync(ConfigUrl, config);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> EnviarMensajesPendientesAsync()
        {
            var response = await _http.PostAsync(NotificacionesUrl, null);
            return response.IsSuccessStatusCode;
        }
    }
}
