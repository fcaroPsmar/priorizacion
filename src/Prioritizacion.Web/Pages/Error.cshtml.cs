using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Prioritizacion.Web.Pages;

public sealed class ErrorModel : PageModel
{
    public int? StatusCode { get; private set; }

    public void OnGet([FromQuery] int? statusCode = null)
    {
        StatusCode = statusCode;
        if (statusCode.HasValue)
        {
            Response.StatusCode = statusCode.Value;
        }
    }
}
