using BlazorTaekwondo.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace BlazorTaekwondo.Services
{
    public class AlumnoService
    {
        private readonly HttpClient _http;
        private const string BaseUrl = "api/alumnosclub";

        public string LastError { get; private set; } = string.Empty;

        public AlumnoService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<AlumnoDto>> GetAlumnosAsync()
        {
            try
            {
                var response = await _http.GetAsync(BaseUrl);
                if (response.IsSuccessStatusCode)
                {
                    LastError = string.Empty;
                    return await response.Content.ReadFromJsonAsync<List<AlumnoDto>>() ?? new();
                }

                LastError = await ConstruirErrorAsync(response);
                return new();
            }
            catch (Exception ex)
            {
                LastError = $"Error al cargar alumnos: {ex.Message}";
                return new();
            }
        }

        public async Task<bool> AgregarAlumnoAsync(AlumnoDto alumno)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(BaseUrl, alumno);
                if (response.IsSuccessStatusCode)
                {
                    LastError = string.Empty;
                    return true;
                }

                LastError = await ConstruirErrorAsync(response);
                return false;
            }
            catch (Exception ex)
            {
                LastError = $"Error de conexion: {ex.Message}";
                return false;
            }
        }

        public async Task<bool> EditarAlumnoAsync(int id, AlumnoDto alumno)
        {
            try
            {
                var response = await _http.PutAsJsonAsync($"{BaseUrl}/{id}", alumno);
                if (response.IsSuccessStatusCode)
                {
                    LastError = string.Empty;
                    return true;
                }

                LastError = await ConstruirErrorAsync(response);
                return false;
            }
            catch (Exception ex)
            {
                LastError = $"Error de conexion: {ex.Message}";
                return false;
            }
        }

        public async Task<bool> BorrarAlumnoAsync(int id)
        {
            try
            {
                var response = await _http.DeleteAsync($"{BaseUrl}/{id}");
                if (response.IsSuccessStatusCode)
                {
                    LastError = string.Empty;
                    return true;
                }

                LastError = await ConstruirErrorAsync(response);
                return false;
            }
            catch (Exception ex)
            {
                LastError = $"Error de conexion: {ex.Message}";
                return false;
            }
        }

        private static async Task<string> ConstruirErrorAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("detail", out var detail))
                    {
                        var detailText = detail.GetString();
                        if (!string.IsNullOrWhiteSpace(detailText))
                        {
                            return $"HTTP {(int)response.StatusCode} - {detailText}";
                        }
                    }

                    if (doc.RootElement.TryGetProperty("title", out var title))
                    {
                        var titleText = title.GetString();
                        if (!string.IsNullOrWhiteSpace(titleText))
                        {
                            return $"HTTP {(int)response.StatusCode} - {titleText}";
                        }
                    }
                }
                catch
                {
                    // Ignore JSON parse failures and fall back to the raw body.
                }

                return $"HTTP {(int)response.StatusCode} - {body}";
            }

            return $"HTTP {(int)response.StatusCode} - {response.ReasonPhrase ?? "Sin detalle en la respuesta"}";
        }
    }
}
