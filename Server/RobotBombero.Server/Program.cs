using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using RobotBombero.Server.Data;

var builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=robotbombero.db";
var database = new Database(connectionString);
database.Migrate();

var app = builder.Build();
app.Run();

public partial class Program { }
