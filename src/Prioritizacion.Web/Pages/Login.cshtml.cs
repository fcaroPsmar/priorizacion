using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Prioritizacion.Web.Services;

namespace Prioritizacion.Web.Pages;

public class LoginModel : PageModel
{
    private readonly AuthService _auth;

    public LoginModel(AuthService auth)
    {
        _auth = auth;
    }

    [BindProperty] public string Email { get; set; } = "";
    [BindProperty] public string Codigo { get; set; } = "";
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User?.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Priorizar");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var (principal, error) = await _auth.TryLoginAsync(Email, Codigo);
        if (principal is null)
        {
            Error = error;
            return Page();
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        return RedirectToPage("/Priorizar");
    }
}
