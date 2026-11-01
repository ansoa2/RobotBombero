using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RobotBombero.Server.Api;
using RobotBombero.Server.Data;

var builder = WebApplication.CreateBuilder(args);

// ConnectionStrings:Default se puede sustituir por configuración (las pruebas usan una base aislada).
string connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=robotbombero.db";
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
