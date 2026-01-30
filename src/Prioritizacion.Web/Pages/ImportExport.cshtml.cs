using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Prioritizacion.Web.Models;
using Prioritizacion.Web.Services;

namespace Prioritizacion.Web.Pages;

public sealed class ImportExportModel : PageModel
{
    private readonly ConvocatoriaService _convocatoriaService;
    private readonly ImportExcelService _importExcelService;

    public ImportExportModel(ConvocatoriaService convocatoriaService, ImportExcelService importExcelService)
    {
        _convocatoriaService = convocatoriaService;
        _importExcelService = importExcelService;
    }

    public IReadOnlyList<Convocatoria> Convocatorias { get; private set; } = Array.Empty<Convocatoria>();

    [BindProperty]
    public Guid? SelectedConvocatoriaId { get; set; }

    [BindProperty]
    public IFormFile? ExcelFile { get; set; }

    public ImportResult? Result { get; private set; }

    public async Task OnGetAsync()
    {
        Convocatorias = await _convocatoriaService.GetAllAsync();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Convocatorias = await _convocatoriaService.GetAllAsync();
        if (ExcelFile is null)
        {
            Result = new ImportResult(false, "Debes seleccionar un fichero Excel.");
            return Page();
        }

        Result = await _importExcelService.ImportAsync(ExcelFile, SelectedConvocatoriaId, cancellationToken);
        return Page();
    }
}
