using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RobotBombero.Server.Api;
using RobotBombero.Server.Data;

var builder = WebApplication.CreateBuilder(args);

// Puerto configurable (por defecto 5080) y accesible desde otros equipos de la red local.
// Si se indica una URL por configuración (ASPNETCORE_URLS, --urls), manda esa.
if (string.IsNullOrEmpty(builder.Configuration["urls"]))
{
    string port = Environment.GetEnvironmentVariable("ROBOTBOMBERO_PORT");
    if (string.IsNullOrWhiteSpace(port)) port = "5080";
    builder.WebHost.UseUrls("http://0.0.0.0:" + port.Trim());
}

// ConnectionStrings:Default se puede sustituir por configuración (las pruebas usan una base aislada).
// Por defecto: Server/data/robotbombero.db, relativo a la carpeta del proyecto y no al directorio de trabajo.
string connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrEmpty(connectionString))
{
    string dataDir = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "data"));
    Directory.CreateDirectory(dataDir);
    connectionString = "Data Source=" + Path.Combine(dataDir, "robotbombero.db");
}
var database = new Database(connectionString);
database.Migrate();
builder.Services.AddSingleton(database);

var app = builder.Build();

// Archivos del juego (compilación WebGL de Unity en wwwroot). Funciona también por HTTP en la red local.
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = new UnityContentTypeProvider(),
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream",
    OnPrepareResponse = ctx =>
    {
        // Los nombres de los archivos no cambian entre compilaciones: el navegador siempre pregunta si hay otra versión.
        ctx.Context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.CacheControl] = "no-cache";
        // Solo los .br se sirven ya comprimidos. Los .unityweb los descomprime Unity en JavaScript.
        if (ctx.File.Name.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
            ctx.Context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.ContentEncoding] = "br";
    }
});

PlayersApi.Map(app);
PacksApi.Map(app);
GamesApi.Map(app);
LeaderboardApi.Map(app);
app.Run();

/// <summary>Tipos de contenido de los archivos de Unity WebGL, incluidos los precomprimidos con Brotli.</summary>
internal sealed class UnityContentTypeProvider : IContentTypeProvider
{
    private readonly FileExtensionContentTypeProvider inner = new FileExtensionContentTypeProvider();

    public UnityContentTypeProvider()
    {
        inner.Mappings[".wasm"] = "application/wasm";
        inner.Mappings[".data"] = "application/octet-stream";
        inner.Mappings[".unityweb"] = "application/octet-stream";
        inner.Mappings[".symbols.json"] = "application/json";
    }

    public bool TryGetContentType(string subpath, out string contentType)
    {
        // game.wasm.br conserva el tipo del archivo de debajo (game.wasm).
        if (subpath.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
            subpath = subpath.Substring(0, subpath.Length - 3);
        return inner.TryGetContentType(subpath, out contentType);
    }
}

public partial class Program { }
