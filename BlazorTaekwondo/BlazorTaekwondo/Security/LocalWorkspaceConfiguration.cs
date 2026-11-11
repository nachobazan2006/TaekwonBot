namespace BlazorTaekwondo.Security;

public static class LocalWorkspaceConfiguration
{
    public static void Configure(WebApplicationBuilder builder)
    {
        var directory = new DirectoryInfo(builder.Environment.ContentRootPath);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, ".local", "settings.json")))
            directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Falta la configuración privada local. Ejecutá el lanzador del workspace.");
        builder.Configuration.AddJsonFile(Path.Combine(directory.FullName, ".local", "settings.json"), optional: false, reloadOnChange: false);
        builder.Configuration.AddEnvironmentVariables();
        if (!builder.Configuration.GetValue<bool>("TaekwonBot:LocalOnly"))
            throw new InvalidOperationException("Este proyecto requiere modo local aislado.");
        if (!Uri.TryCreate(builder.Configuration["ApiBaseUrl"], UriKind.Absolute, out var api)
            || api.Scheme != "http" || !IsLocalHost(api.Host) || api.Port != 5087 || api.AbsolutePath != "/"
            || api.UserInfo.Length != 0 || api.Query.Length != 0 || api.Fragment.Length != 0)
            throw new InvalidOperationException("ApiBaseUrl debe apuntar únicamente a la API local en el puerto 5087.");
        foreach (var name in new[] { "ApiKey", "AdminPassword", "TaekwonBot:SigningKey" })
            if (string.IsNullOrWhiteSpace(builder.Configuration[name])) throw new InvalidOperationException("Falta configuración privada de autenticación local.");
        byte[] key;
        try { key = Convert.FromBase64String(builder.Configuration["TaekwonBot:SigningKey"]!); }
        catch (FormatException) { throw new InvalidOperationException("La configuración privada de firma local no es válida."); }
        if (key.Length < 64) throw new InvalidOperationException("La clave de firma local es demasiado corta.");
    }
    public static bool IsLocalHost(string host) => host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host is "127.0.0.1" or "::1" or "[::1]";
}
