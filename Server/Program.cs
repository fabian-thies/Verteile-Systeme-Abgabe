using Npgsql;
using Server.Hubs;
using Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 1024L * 1024L * 50L; // 50 MB
});

builder.Services.AddSignalR();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFileManagementService, FileManagementService>();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

var app = builder.Build();

var connectionString = app.Configuration.GetConnectionString("DefaultConnection");
var dbConnected = false;
while (!dbConnected)
    try
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();
            dbConnected = true;
            Console.WriteLine("Database connection established.");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("Database connection failed: " + ex.Message);
        await Task.Delay(TimeSpan.FromSeconds(60));
    }

app.MapHub<ChatHub>("/chatHub");
app.Run();