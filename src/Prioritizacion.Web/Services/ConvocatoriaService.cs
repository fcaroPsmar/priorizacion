using Dapper;
using Prioritizacion.Web.Data;
using Prioritizacion.Web.Models;

namespace Prioritizacion.Web.Services;

public sealed class ConvocatoriaService
{
    private readonly Db _db;

    public ConvocatoriaService(Db db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Convocatoria>> GetAllAsync()
    {
        using var conn = _db.OpenConnection();
        const string sql = @"
select
  id,
  codigo as Codigo,
  nombre as Nombre,
  fecha_inicio as FechaInicio,
  fecha_fin as FechaFin,
  activa as Activa,
  acceso_desde as AccesoDesde,
  acceso_hasta as AccesoHasta
from convocatoria
order by creado_en desc;";

        var rows = await conn.QueryAsync<Convocatoria>(sql);
        return rows.ToList();
    }

    public async Task<Convocatoria> CreateAsync(ConvocatoriaInput input)
    {
        using var conn = _db.OpenConnection();
        const string sql = @"
insert into convocatoria (codigo, nombre, fecha_inicio, fecha_fin, activa, acceso_desde, acceso_hasta)
values (@Codigo, @Nombre, @FechaInicio, @FechaFin, @Activa, @AccesoDesde, @AccesoHasta)
returning
  id,
  codigo as Codigo,
  nombre as Nombre,
  fecha_inicio as FechaInicio,
  fecha_fin as FechaFin,
  activa as Activa,
  acceso_desde as AccesoDesde,
  acceso_hasta as AccesoHasta;";

        return await conn.QuerySingleAsync<Convocatoria>(sql, input);
    }

    public async Task<Convocatoria?> UpdateAsync(Guid id, ConvocatoriaInput input)
    {
        using var conn = _db.OpenConnection();
        const string sql = @"
update convocatoria
set codigo = @Codigo,
    nombre = @Nombre,
    fecha_inicio = @FechaInicio,
    fecha_fin = @FechaFin,
    activa = @Activa,
    acceso_desde = @AccesoDesde,
    acceso_hasta = @AccesoHasta
where id = @Id
returning
  id,
  codigo as Codigo,
  nombre as Nombre,
  fecha_inicio as FechaInicio,
  fecha_fin as FechaFin,
  activa as Activa,
  acceso_desde as AccesoDesde,
  acceso_hasta as AccesoHasta;";

        return await conn.QuerySingleOrDefaultAsync<Convocatoria>(sql, new
        {
            Id = id,
            input.Codigo,
            input.Nombre,
            input.FechaInicio,
            input.FechaFin,
            input.Activa,
            input.AccesoDesde,
            input.AccesoHasta
        });
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var conn = _db.OpenConnection();
        const string sql = "delete from convocatoria where id = @Id;";
        var rows = await conn.ExecuteAsync(sql, new { Id = id });
        return rows > 0;
    }
}

public sealed record ConvocatoriaInput(
    string Codigo,
    string Nombre,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    bool Activa,
    DateTime? AccesoDesde,
    DateTime? AccesoHasta
);
