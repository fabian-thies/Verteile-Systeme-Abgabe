/***** Program.cs *****/
using ChatApp.Components;
using ChatApp.Data;
using ChatApp.Hubs;
using ChatApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Comments in English: Configure Serilog for logging
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/ChatApp.log", rollingInterval: RollingInterval.Day)
    .Enrich.WithProperty("Application", "ChatApp")
    .CreateLogger();

// Comments in English: Configure EF Core + Identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Comments in English: Add support for Razor Pages (needed for form posts if we want them) 
builder.Services.AddRazorPages();

// Comments in English: Blazor server components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR();
builder.Host.UseSerilog();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

// Comments in English: Remove or disable antiforgery unless you add tokens in your HTML form
// app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();          // <--- Enable Razor Pages
app.MapStaticAssets();

// Comments in English: Map the Blazor app
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Comments in English: Map SignalR hub
app.MapHub<ChatHub>("/chathub");

// Comments in English: Minimal-API endpoint for Login (classic POST)
app.MapPost("/login", async (HttpContext context, SignInManager<ApplicationUser> signInManager) =>
{
    var form = context.Request.Form;
    var email = form["Email"].ToString();
    var password = form["Password"].ToString();

    Log.Information("Logging in with email {Email}", email);
    Log.Information("Logging in with password {Password}", password);

    var result = await signInManager.PasswordSignInAsync(email, password, false, false);

    Log.Information("Login results: {Result}", result);

    if (result.Succeeded)
    {
        // Comments in English: On success, set the auth cookie and redirect to the main page
        return Results.Redirect("/");
    }
    else
    {
        Log.Error("Login failed");
        // Comments in English: Could also redirect somewhere else
        return Results.Redirect("/login?error=1");
    }
});

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Server started at {Time}", DateTime.Now);

app.Run();
