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

var pluginManager = app.Services.GetRequiredService<PluginManager>();
await pluginManager.InitializeAsync();

// 2) Explicitly load the Whiteboard plugin assembly
await pluginManager.LoadPluginAsync("whiteboard-plugin"); 
// or "Whiteboard", depending on which ID you have in your plugin code/metadata

// 3) Now that the plugin assembly is loaded and appended to ApplicationParts,
//    call MapRazorPages so that the new pages are recognized
app.MapRazorPages();
app.MapStaticAssets(); // or your static assets code

// 4) If needed, also map the plugin static files
foreach (var kvp in StaticAssetsMappings.PluginStaticMappings)
{
    var pluginId = kvp.Key;
    var fileProvider = kvp.Value;

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider,
        RequestPath = $"/plugins/{pluginId}"
    });
}

// 5) Continue mapping your hubs or anything else
app.MapHub<ChatHub>("/chathub");
app.MapHub<PluginHub>("/pluginhub");

app.Run();