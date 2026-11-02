using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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

PlayersApi.Map(app);
PacksApi.Map(app);
GamesApi.Map(app);
LeaderboardApi.Map(app);
app.Run();

public partial class Program { }
