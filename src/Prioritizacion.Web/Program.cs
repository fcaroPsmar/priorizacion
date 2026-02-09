using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Prioritizacion.Web.Data;
using Prioritizacion.Web.Services;

var builder = WebApplication.CreateBuilder(args);
var cookieSecurePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

builder.Services.AddRazorPages(options =>
{
    // Protect priorization and submit endpoints
    options.Conventions.AuthorizePage("/Priorizar");
    options.Conventions.AuthorizePage("/Confirmacion");
    options.Conventions.AuthorizePage("/Admin", "AdminOnly");
    options.Conventions.AuthorizePage("/ImportExport", "AdminOnly");
    options.Conventions.ConfigureFilter(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "hdm_csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.Cookie.SameSite = SameSiteMode.Lax;
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
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = cookieSecurePolicy;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.SlidingExpiration = true;
        if (int.TryParse(builder.Configuration["Auth:CookieDays"], out var days))
            options.ExpireTimeSpan = TimeSpan.FromDays(days);
    })
    .AddCookie(adminCookieScheme, options =>
    {
        options.Cookie.Name = "hdm_admin_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = cookieSecurePolicy;
        options.Cookie.SameSite = SameSiteMode.Lax;
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
builder.Services.AddScoped<ImportExcelService>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddHttpsRedirection(options =>
{
    if (int.TryParse(builder.Configuration["Kestrel:Endpoints:Https:Port"], out var httpsPort))
    {
        options.HttpsPort = httpsPort;
    }
    else if (int.TryParse(builder.Configuration["HttpsRedirection:HttpsPort"], out var redirectPort))
    {
        options.HttpsPort = redirectPort;
    }
    else
    {
        options.HttpsPort = 443;
    }
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
    context.Response.Headers[HeaderNames.XFrameOptions] = "DENY";
    context.Response.Headers[HeaderNames.ReferrerPolicy] = "strict-origin-when-cross-origin";
    context.Response.Headers[HeaderNames.PermissionsPolicy] = "geolocation=(), microphone=(), camera=()";
    context.Response.Headers[HeaderNames.ContentSecurityPolicy] =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'; " +
        "img-src 'self' data:; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'";
    await next();
});

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseStatusCodePagesWithReExecute("/Error", "?statusCode={0}");
app.UseRouting();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    await next();
    if (context.User.Identity?.IsAuthenticated == true)
    {
        context.Response.Headers[HeaderNames.CacheControl] = "no-store, no-cache";
        context.Response.Headers[HeaderNames.Pragma] = "no-cache";
        context.Response.Headers[HeaderNames.Expires] = "0";
    }
});
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
