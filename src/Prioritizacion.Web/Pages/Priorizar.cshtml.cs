using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Prioritizacion.Web.Models;
using Prioritizacion.Web.Services;

namespace Prioritizacion.Web.Pages;

public class PriorizarModel : PageModel
{
    private readonly PrioritizacionService _svc;

    public PriorizarModel(PrioritizacionService svc)
    {
        _svc = svc;
    }

    public IReadOnlyList<PriorizarItem> Items { get; private set; } = Array.Empty<PriorizarItem>();

    [BindProperty] public string OrderedIds { get; set; } = "";
    public string? Error { get; set; }
    public string? Success { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostGuardarAsync()
    {
        var aspiranteId = AuthService.GetAspiranteId(User);
        var ids = ParseIds(OrderedIds);
        var (ok, error) = await _svc.GuardarOrdenAsync(aspiranteId, ids);
        if (!ok) Error = error;
        else Success = "Orden guardado correctamente.";

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostReiniciarAsync()
    {
        var aspiranteId = AuthService.GetAspiranteId(User);
        var (ok, error) = await _svc.ReiniciarAsync(aspiranteId);
        if (!ok) Error = error;
        else Success = "Orden reiniciado al valor por defecto.";

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostEnviarAsync()
    {
        var aspiranteId = AuthService.GetAspiranteId(User);
        var ids = ParseIds(OrderedIds);
        var (saved, saveError) = await _svc.GuardarOrdenAsync(aspiranteId, ids);
        if (!saved)
        {
            Error = saveError;
            await LoadAsync();
            return Page();
        }

        var (ok, error) = await _svc.EnviarAsync(aspiranteId);
        if (!ok)
        {
            Error = error;
            await LoadAsync();
            return Page();
        }

        // Cerrar sesión tras enviar
        await HttpContext.SignOutAsync();
        return RedirectToPage("/Confirmacion");
    }

    private async Task LoadAsync()
    {
        var aspiranteId = AuthService.GetAspiranteId(User);
        Items = await _svc.GetItemsAsync(aspiranteId);
        OrderedIds = string.Join(",", Items.Select(i => i.PlazaId));
    }

    private static List<Guid> ParseIds(string raw)
    {
        var list = new List<Guid>();
        if (string.IsNullOrWhiteSpace(raw)) return list;

        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(part, out var id)) list.Add(id);
        }
        return list;
    }
}
