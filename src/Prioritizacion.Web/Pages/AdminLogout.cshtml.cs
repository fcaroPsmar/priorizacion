using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Prioritizacion.Web.Pages;

public class AdminLogoutModel : PageModel
{
    public IActionResult OnGet()
    {
        return SignOut(new AuthenticationProperties
        {
            RedirectUri = Url.Page("/Index")
        }, "AdminCookie", "AdminOidc");
    }
}
