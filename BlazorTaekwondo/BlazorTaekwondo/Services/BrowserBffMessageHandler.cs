using Microsoft.JSInterop;
using System.Net;
using System.Text;

namespace BlazorTaekwondo.Services;

public sealed class BrowserBffMessageHandler(IJSRuntime js) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.PathAndQuery.TrimStart('/') ?? throw new InvalidOperationException("Ruta local requerida.");
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var result = await js.InvokeAsync<BrowserResult>("TaekwonLocalApi.request", cancellationToken, request.Method.Method, path, body);
        return new HttpResponseMessage((HttpStatusCode)result.Status)
        {
            Content = new StringContent(result.Body ?? "", Encoding.UTF8, "application/json"), RequestMessage = request
        };
    }
    public sealed class BrowserResult
    {
        public int Status { get; set; }
        public string? Body { get; set; }
    }
}
