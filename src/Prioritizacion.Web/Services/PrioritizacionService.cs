using Dapper;
using Prioritizacion.Web.Data;
using Prioritizacion.Web.Models;

namespace Prioritizacion.Web.Services;

public sealed class PrioritizacionService
{
    private readonly Db _db;

    public PrioritizacionService(Db db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PriorizarItem>> GetItemsAsync(Guid aspiranteId)
    {
        using var conn = _db.OpenConnection();

        const string sql = @"
select
  ap.plaza_id as PlazaId,
  p.base as Base,
  p.posicion as Posicion,
  p.centro as Centre,
  coalesce(ap.orden_usuario, ap.orden_defecto) as Orden
from aspirante_plaza ap
join plaza p on p.id = ap.plaza_id
where ap.aspirante_id = @AspiranteId
  and ap.bloqueada = false
order by coalesce(ap.orden_usuario, ap.orden_defecto) asc;";

        var items = (await conn.QueryAsync<PriorizarItem>(sql, new { AspiranteId = aspiranteId })).ToList();

        // Normalizar orden 1..N
        for (var i = 0; i < items.Count; i++)
            items[i].Orden = i + 1;

        return items;
    }

    public async Task<(bool ok, string? error)> GuardarOrdenAsync(Guid aspiranteId, IReadOnlyList<Guid> plazaIdsEnOrden)
    {
        if (plazaIdsEnOrden.Count == 0)
            return (false, "No hay plazas para guardar.");

        using var conn = _db.OpenConnection();
        using var tx = conn.BeginTransaction();

        // Bloquear si ya enviado o convocatoria fuera de ventana
        const string checkSql = @"
select 1
from aspirante a
join convocatoria c on c.id = a.convocatoria_id
where a.id = @AspiranteId
  and a.enviado_en is null
  and c.activa = true
  and (c.acceso_desde is null or now() >= c.acceso_desde)
  and (c.acceso_hasta is null or now() <= c.acceso_hasta)
limit 1;";

        var can = await conn.ExecuteScalarAsync<int?>(checkSql, new { AspiranteId = aspiranteId }, tx);
        if (can is null)
        {
            tx.Rollback();
            return (false, "No se puede guardar: convocatoria cerrada o ya enviada.");
        }

        const string blockSql = @"
update aspirante_plaza
set bloqueada = true,
    orden_usuario = null
where aspirante_id = @AspiranteId
  and not (plaza_id = any(@PlazaIds));";

        await conn.ExecuteAsync(blockSql, new { AspiranteId = aspiranteId, PlazaIds = plazaIdsEnOrden.ToArray() }, tx);

        const string updSql = @"
        update aspirante_plaza
        set orden_usuario = @Orden,
            bloqueada = false
        where aspirante_id = @AspiranteId and plaza_id = @PlazaId;";

        for (var i = 0; i < plazaIdsEnOrden.Count; i++)
        {
            await conn.ExecuteAsync(updSql, new { Orden = i + 1, AspiranteId = aspiranteId, PlazaId = plazaIdsEnOrden[i] }, tx);
        }

        tx.Commit();
        return (true, null);
    }

    public async Task<(bool ok, string? error)> ReiniciarAsync(Guid aspiranteId)
    {
        using var conn = _db.OpenConnection();
        using var tx = conn.BeginTransaction();

        const string checkSql = @"
select 1
from aspirante a
join convocatoria c on c.id = a.convocatoria_id
where a.id = @AspiranteId
  and a.enviado_en is null
  and c.activa = true
  and (c.acceso_desde is null or now() >= c.acceso_desde)
  and (c.acceso_hasta is null or now() <= c.acceso_hasta)
limit 1;";

        var can = await conn.ExecuteScalarAsync<int?>(checkSql, new { AspiranteId = aspiranteId }, tx);
        if (can is null)
        {
            tx.Rollback();
            return (false, "No se puede reiniciar: convocatoria cerrada o ya enviada.");
        }

        const string sql = @"
update aspirante_plaza
set orden_usuario = null,
    bloqueada = false
where aspirante_id = @AspiranteId;";

        await conn.ExecuteAsync(sql, new { AspiranteId = aspiranteId }, tx);
        tx.Commit();
        return (true, null);
    }

    public async Task<(bool ok, string? error)> EnviarAsync(Guid aspiranteId)
    {
        using var conn = _db.OpenConnection();
        using var tx = conn.BeginTransaction();

        // Solo se puede enviar si está abierta (por si acaso)
        const string checkSql = @"
select a.convocatoria_id
from aspirante a
join convocatoria c on c.id = a.convocatoria_id
where a.id = @AspiranteId
  and a.enviado_en is null
  and c.activa = true
  and (c.acceso_desde is null or now() >= c.acceso_desde)
  and (c.acceso_hasta is null or now() <= c.acceso_hasta)
limit 1;";

        var convocatoriaId = await conn.ExecuteScalarAsync<Guid?>(checkSql, new { AspiranteId = aspiranteId }, tx);
        if (convocatoriaId is null)
        {
            tx.Rollback();
            return (false, "No se puede enviar: convocatoria cerrada o ya enviada.");
        }

        // Marcar envío
        const string setSql = @"
update aspirante
set enviado_en = now()
where id = @AspiranteId and enviado_en is null;";

        var rows = await conn.ExecuteAsync(setSql, new { AspiranteId = aspiranteId }, tx);
        if (rows != 1)
        {
            tx.Rollback();
            return (false, "No se ha podido enviar (posible envío duplicado).");
        }

        // Revocar tokens
        const string revokeSql = @"
update aspirante_token
set revocado_en = now()
where aspirante_id = @AspiranteId and revocado_en is null;";

        await conn.ExecuteAsync(revokeSql, new { AspiranteId = aspiranteId }, tx);

        tx.Commit();
        return (true, null);
    }
}
