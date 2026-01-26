namespace Prioritizacion.Web.Models;

public sealed class Aspirante
{
    public Guid Id { get; init; }
    public Guid ConvocatoriaId { get; init; }
    public string Email { get; init; } = "";
    public string? Nombre { get; init; }
    public string? DniNie { get; init; }
    public int? NumEmpleat { get; init; }
    public string? DniNieEmmascarat { get; init; }
    public string? PrimerCognom { get; init; }
    public string? SegonCognom { get; init; }
    public string? Nom { get; init; }
    public string? TornY { get; init; }
    public DateTime? EnviadoEn { get; init; }
    public DateTime CreadoEn { get; init; }
}
