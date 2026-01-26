namespace Prioritizacion.Web.Models;

public sealed class AspirantePlaza
{
    public Guid Id { get; init; }
    public Guid AspiranteId { get; init; }
    public Guid PlazaId { get; init; }
    public int OrdenDefecto { get; init; }
    public int? OrdenUsuario { get; init; }
    public bool Bloqueada { get; init; }
    public decimal? Experiencia { get; init; }
    public decimal? Unnamed9 { get; init; }
    public decimal? BaremPersonal { get; init; }
    public decimal? Unnamed11 { get; init; }
    public decimal? Qualificacio { get; init; }
    public decimal? Unnamed13 { get; init; }
    public decimal? Total { get; init; }
    public string? FicherAspirant { get; init; }
    public decimal? PondExp { get; init; }
    public decimal? PondBarem { get; init; }
    public decimal? ProvaCompetencial { get; init; }
    public decimal? PondProva { get; init; }
    public DateTime CreadoEn { get; init; }
    public DateTime ModificadoEn { get; init; }
}
