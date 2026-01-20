using Microsoft.AspNetCore.Authentication.Cookies;
using Prioritizacion.Web.Data;
using Prioritizacion.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    // Protect priorization and submit endpoints
    options.Conventions.AuthorizePage("/Priorizar");
    options.Conventions.AuthorizePage("/Confirmacion");
});

var cookieName = builder.Configuration["Auth:SessionCookieName"] ?? "hdm_priorizacion_session";

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = cookieName;
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.SlidingExpiration = true;
        if (int.TryParse(builder.Configuration["Auth:CookieDays"], out var days))
            options.ExpireTimeSpan = TimeSpan.FromDays(days);
    });

builder.Services.AddAuthorization();

builder.Services.AddSingleton<Db>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PrioritizacionService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
