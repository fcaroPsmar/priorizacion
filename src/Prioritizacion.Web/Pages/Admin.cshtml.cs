using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Prioritizacion.Web.Services;

namespace Prioritizacion.Web.Pages;

public class AdminModel : PageModel
{
    private readonly ConvocatoriaService _convocatoriaService;

    public AdminModel(ConvocatoriaService convocatoriaService)
    {
        _convocatoriaService = convocatoriaService;
    }

    public string ConvocatoriasJson { get; private set; } = "[]";

    public async Task OnGetAsync()
    {
        var convocatorias = await _convocatoriaService.GetAllAsync();
        ConvocatoriasJson = JsonSerializer.Serialize(convocatorias, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public async Task<IActionResult> OnPostCreateAsync([FromBody] ConvocatoriaRequest request)
    {
        if (!IsValidRequest(request))
            return BadRequest("Código y nombre son obligatorios.");

        var created = await _convocatoriaService.CreateAsync(ToInput(request));
        return new JsonResult(created);
    }

    public async Task<IActionResult> OnPostUpdateAsync([FromBody] ConvocatoriaRequest request)
    {
        if (!request.Id.HasValue)
            return BadRequest("Id es obligatorio.");

        if (!IsValidRequest(request))
            return BadRequest("Código y nombre son obligatorios.");

        var updated = await _convocatoriaService.UpdateAsync(request.Id.Value, ToInput(request));
        if (updated is null)
            return NotFound();

        return new JsonResult(updated);
    }

    public async Task<IActionResult> OnPostDeleteAsync([FromBody] ConvocatoriaDeleteRequest request)
    {
        if (request.Id == Guid.Empty)
            return BadRequest("Id es obligatorio.");

        var deleted = await _convocatoriaService.DeleteAsync(request.Id);
        return new JsonResult(new { ok = deleted });
    }

    private static bool IsValidRequest(ConvocatoriaRequest request)
        => !string.IsNullOrWhiteSpace(request.Codigo) && !string.IsNullOrWhiteSpace(request.Nombre);

    private static ConvocatoriaInput ToInput(ConvocatoriaRequest request)
        => new(request.Codigo.Trim(), request.Nombre.Trim(), request.FechaInicio, request.FechaFin, request.Activa,
            request.AccesoDesde, request.AccesoHasta);
}

public sealed record ConvocatoriaRequest(
    Guid? Id,
    string Codigo,
    string Nombre,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    bool Activa,
    DateTime? AccesoDesde,
    DateTime? AccesoHasta
);

public sealed record ConvocatoriaDeleteRequest(Guid Id);
