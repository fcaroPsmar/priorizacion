using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Prioritizacion.Web.Pages;

public class AdminModel : PageModel
{
    private const string CookieName = "hdm_admin";
    private readonly IConfiguration _config;

    public AdminModel(IConfiguration config)
    {
        _config = config;
    }

    public bool IsAuthed => Request.Cookies.ContainsKey(CookieName);

    public string? Error { get; set; }

    public void OnGet() { }

    public IActionResult OnPost([FromForm] string password)
    {
        var expected = _config["Admin:Password"] ?? "";
        if (string.IsNullOrWhiteSpace(expected) || expected == "CHANGE_ME")
        {
            Error = "La contraseña de admin no está configurada.";
            return Page();
        }

        if (!TimeConstantEquals(password, expected))
        {
            Error = "Contraseña incorrecta.";
            return Page();
        }

        Response.Cookies.Append(CookieName, "1", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });

        return RedirectToPage("/Admin");
    }

    private static bool TimeConstantEquals(string a, string b)
    {
        a ??= "";
        b ??= "";
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
