using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Prioritizacion.Web.Data;
using Prioritizacion.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    // Protect priorization and submit endpoints
    options.Conventions.AuthorizePage("/Priorizar");
    options.Conventions.AuthorizePage("/Confirmacion");
    options.Conventions.AuthorizePage("/Admin", "AdminOnly");
});

var cookieName = builder.Configuration["Auth:SessionCookieName"] ?? "hdm_priorizacion_session";

var adminEntraSection = builder.Configuration.GetSection("AdminEntra");
var adminAuthority = BuildAdminAuthority(adminEntraSection);
var adminCookieScheme = "AdminCookie";
var adminOidcScheme = "AdminOidc";
var adminPolicyScheme = "AdminScheme";

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
    })
    .AddCookie(adminCookieScheme, options =>
    {
        options.Cookie.Name = "hdm_admin_auth";
        options.LoginPath = "/Admin";
        options.AccessDeniedPath = "/Admin";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    })
    .AddOpenIdConnect(adminOidcScheme, options =>
    {
        options.Authority = adminAuthority;
        options.ClientId = adminEntraSection["ClientId"];
        options.ClientSecret = adminEntraSection["ClientSecret"];
        options.CallbackPath = adminEntraSection["CallbackPath"] ?? "/admin/signin-oidc";
        options.SignedOutCallbackPath = adminEntraSection["SignedOutCallbackPath"] ?? "/admin/signout-callback-oidc";
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.SaveTokens = true;
        options.SignInScheme = adminCookieScheme;
        options.GetClaimsFromUserInfoEndpoint = true;
    })
    .AddPolicyScheme(adminPolicyScheme, adminPolicyScheme, options =>
    {
        options.ForwardDefaultSelector = context =>
            context.User.Identity?.IsAuthenticated == true ? adminCookieScheme : adminOidcScheme;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.AddAuthenticationSchemes(adminPolicyScheme);
        policy.RequireAuthenticatedUser();
    });
});

builder.Services.AddSingleton<Db>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PrioritizacionService>();
builder.Services.AddScoped<ConvocatoriaService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();

static string BuildAdminAuthority(IConfigurationSection adminEntraSection)
{
    var tenantId = adminEntraSection["TenantId"] ?? "";
    var instance = adminEntraSection["Instance"] ?? "https://login.microsoftonline.com/";
    instance = instance.TrimEnd('/');
    return $"{instance}/{tenantId}/v2.0";
}
