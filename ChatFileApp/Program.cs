using ChatFileApp.Data;
using ChatFileApp.Hubs;
using ChatFileApp.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => 
    { 
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/login";
    });
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddSingleton<PluginManager>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

app.MapHub<ChatHub>("/chathub");
app.MapHub<PluginHub>("/pluginhub");

var pluginManager = app.Services.GetRequiredService<PluginManager>();
await pluginManager.InitializeAsync();

// Register each plugin's static files
foreach (var kvp in StaticAssetsMappings.PluginStaticMappings)
{
    var pluginId = kvp.Key;
    var fileProvider = kvp.Value;

    // Here we mount them under e.g. /plugins/<pluginId>/
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider,
        RequestPath = $"/plugins/{pluginId}"
    });
}

app.Run();