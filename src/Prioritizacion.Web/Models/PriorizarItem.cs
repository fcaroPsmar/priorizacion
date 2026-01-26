namespace Prioritizacion.Web.Models;

public sealed class PriorizarItem
{
    public Guid PlazaId { get; init; }
    public string Base { get; init; } = "";
    public string Posicion { get; init; } = "";
    public string? Hores { get; init; }
    public string? TornX { get; init; }
    public string? GfhAdjudicacio { get; init; }
    public string? Centre { get; init; }
    public int Orden { get; set; }

    public string Titulo => $"{Base}-{Posicion}";
}
