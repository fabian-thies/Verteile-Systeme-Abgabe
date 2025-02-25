using ChatApp.Components;
using ChatApp.Hubs;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/ChatApp.log", rollingInterval: RollingInterval.Day)
    .Enrich.WithProperty("Application", "ChatApp")
    .CreateLogger();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();
builder.Services.AddScoped<AuthenticationService>();

builder.Host.UseSerilog();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<ChatHub>("/chathub");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Server started at {Time}", DateTime.Now);

app.Run();