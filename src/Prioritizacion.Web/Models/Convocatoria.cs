namespace Prioritizacion.Web.Models;

public sealed class Convocatoria
{
    public Guid Id { get; init; }
    public string Codigo { get; init; } = "";
    public string Nombre { get; init; } = "";
    public DateTime? FechaInicio { get; init; }
    public DateTime? FechaFin { get; init; }
    public bool Activa { get; init; }
    public DateTime? AccesoDesde { get; init; }
    public DateTime? AccesoHasta { get; init; }
}
