using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using Dapper;
using Prioritizacion.Web.Data;

namespace Prioritizacion.Web.Services;

public sealed class ImportExcelService
{
    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    private static readonly IReadOnlyDictionary<string, string> HeaderAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["convocatoriaid"] = "convocatoria_id",
            ["idconvocatoria"] = "convocatoria_id",
            ["idconvocat"] = "convocatoria_id",
            ["convocatoria"] = "convocatoria_id",
            ["dni"] = "dni_nie",
            ["dninie"] = "dni_nie",
            ["dni/nie"] = "dni_nie",
            ["numempleat"] = "num_empleat",
            ["numemple"] = "num_empleat",
            ["numempleo"] = "num_empleat",
            ["dni_nieemmascarat"] = "dni_nie_emmascarat",
            ["dnieniemmascarat"] = "dni_nie_emmascarat",
            ["dninieemmascarat"] = "dni_nie_emmascarat",
            ["dninieemms"] = "dni_nie_emmascarat",
            ["primercognom"] = "primer_cognom",
            ["primercog"] = "primer_cognom",
            ["primerapell"] = "primer_cognom",
            ["segoncognom"] = "segon_cognom",
            ["segoncog"] = "segon_cognom",
            ["segonapell"] = "segon_cognom",
            ["nom"] = "nom",
            ["nombre"] = "nom",
            ["email"] = "email",
            ["correo"] = "email",
            ["correoelec"] = "email",
            ["correoelect"] = "email",
            ["correoelectronico"] = "email",
            ["correuelec"] = "email",
            ["base"] = "base",
            ["posicion"] = "posicion",
            ["posicio"] = "posicion",
            ["hores"] = "hores",
            ["tornx"] = "torn_x",
            ["torn_x"] = "torn_x",
            ["torn"] = "torn_x",
            ["gfhadjudicacio"] = "gfh_adjudicacio",
            ["gfhadjudic"] = "gfh_adjudicacio",
            ["centro"] = "centro",
            ["centre"] = "centro",
            ["descripcion"] = "descripcion",
            ["descripcio"] = "descripcion",
            ["orden"] = "orden",
            ["experiencia"] = "experiencia",
            ["experienci"] = "experiencia",
            ["barempersonal"] = "barem_personal",
            ["qualificacio"] = "qualificacio",
            ["calificacio"] = "qualificacio",
            ["total"] = "total",
            ["ficherasp"] = "ficher_aspirant",
            ["ficheraspirant"] = "ficher_aspirant",
            ["pondexp"] = "pond_exp",
            ["pondbare"] = "pond_barem",
            ["pondbarem"] = "pond_barem",
            ["provacompetencial"] = "prova_competencial",
            ["provacomp"] = "prova_competencial",
            ["provacompet"] = "prova_competencial",
            ["pondprov"] = "pond_prova",
            ["pondprova"] = "pond_prova"
        };

    private readonly Db _db;
    private readonly ILogger<ImportExcelService> _logger;

    public ImportExcelService(Db db, ILogger<ImportExcelService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ImportResult> ImportAsync(IFormFile file, Guid? defaultConvocatoriaId, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return new ImportResult(false, "Debes seleccionar un fichero Excel válido.");
        }

        if (!IsExcelContentType(file.ContentType) || !HasValidExtension(file.FileName))
        {
            return new ImportResult(false, "El fichero debe ser un Excel válido.");
        }

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet is null)
        {
            return new ImportResult(false, "No se encontró ninguna hoja en el Excel.");
        }

        var headerRow = worksheet.Row(1);
        var columns = MapHeaders(headerRow);

        var missing = GetMissingColumns(columns, defaultConvocatoriaId);
        if (missing.Count > 0)
        {
            return new ImportResult(false, $"Faltan columnas requeridas en el Excel: {string.Join(", ", missing)}.");
        }

        var rowCount = 0;
        var createdAspirantes = 0;
        var createdPlazas = 0;
        var createdTokens = 0;
        var createdRelaciones = 0;
        var updatedRelaciones = 0;

        using var conn = _db.OpenConnection();
        using var tx = conn.BeginTransaction();

        var aspiranteCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var plazaCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (row.CellsUsed().All(cell => string.IsNullOrWhiteSpace(cell.GetString())))
            {
                continue;
            }

            rowCount++;
            if (rowCount > 5000)
            {
                tx.Rollback();
                return new ImportResult(false, "El Excel supera el límite de 5000 filas.");
            }

            var convocatoriaId = ReadGuid(row, columns, "convocatoria_id") ?? defaultConvocatoriaId;
            if (convocatoriaId is null || convocatoriaId == Guid.Empty)
            {
                tx.Rollback();
                return new ImportResult(false, "No se encontró la convocatoria en el Excel ni se seleccionó una en el formulario.");
            }

            var dniNie = ReadString(row, columns, "dni_nie");
            var email = ReadString(row, columns, "email")?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(dniNie) && string.IsNullOrWhiteSpace(email))
            {
                tx.Rollback();
                return new ImportResult(false, $"La fila {row.RowNumber()} no tiene DNI/NIE ni correo electrónico.");
            }

            if (!IsValidIdentifier(dniNie))
            {
                tx.Rollback();
                return new ImportResult(false, $"La fila {row.RowNumber()} tiene un DNI/NIE inválido.");
            }

            if (!IsValidEmail(email))
            {
                tx.Rollback();
                return new ImportResult(false, $"La fila {row.RowNumber()} tiene un correo inválido.");
            }

            var basePlaza = ReadString(row, columns, "base");
            var posicion = ReadString(row, columns, "posicion");
            if (string.IsNullOrWhiteSpace(basePlaza) || string.IsNullOrWhiteSpace(posicion))
            {
                tx.Rollback();
                return new ImportResult(false, $"La fila {row.RowNumber()} no tiene base o posición.");
            }

            var aspiranteKey = BuildAspiranteKey(convocatoriaId.Value, dniNie, email);
            if (!aspiranteCache.TryGetValue(aspiranteKey, out var aspiranteId))
            {
                var aspiranteResult = await GetOrCreateAspiranteAsync(conn, tx, convocatoriaId.Value, dniNie, email, row, columns);
                aspiranteId = aspiranteResult.Id;
                aspiranteCache[aspiranteKey] = aspiranteId;

                if (aspiranteResult.Created)
                {
                    createdAspirantes++;
                }
            }

            var plazaKey = $"{convocatoriaId.Value:N}|{basePlaza}|{posicion}";
            if (!plazaCache.TryGetValue(plazaKey, out var plazaId))
            {
                var plazaResult = await UpsertPlazaAsync(conn, tx, convocatoriaId.Value, basePlaza, posicion, row, columns);
                plazaId = plazaResult.Id;
                plazaCache[plazaKey] = plazaId;
                if (plazaResult.Created)
                {
                    createdPlazas++;
                }
            }

            var ordenDefecto = ReadInt(row, columns, "orden") ?? rowCount;
            var affected = await UpsertAspirantePlazaAsync(conn, tx, aspiranteId, plazaId, ordenDefecto, row, columns);
            if (affected == 1)
            {
                createdRelaciones++;
            }
            else
            {
                updatedRelaciones++;
            }

            var tokenCreated = await EnsureTokenAsync(conn, tx, aspiranteId);
            if (tokenCreated)
            {
                createdTokens++;
            }
        }

        tx.Commit();

        var summary = $"Importadas {rowCount} filas. Aspirantes nuevos: {createdAspirantes}. Plazas nuevas: {createdPlazas}. Relaciones creadas: {createdRelaciones}, actualizadas: {updatedRelaciones}. Tokens creados: {createdTokens}.";
        return new ImportResult(true, summary);
    }

    private static Dictionary<string, int> MapHeaders(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var raw = cell.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var normalized = NormalizeHeader(raw);
            if (!HeaderAliases.TryGetValue(normalized, out var canonical))
            {
                continue;
            }

            map[canonical] = cell.Address.ColumnNumber;
        }

        return map;
    }

    private static List<string> GetMissingColumns(Dictionary<string, int> columns, Guid? defaultConvocatoriaId)
    {
        var required = new List<string> { "dni_nie", "email", "base", "posicion" };
        if (defaultConvocatoriaId is null || defaultConvocatoriaId == Guid.Empty)
        {
            required.Add("convocatoria_id");
        }

        return required.Where(col => !columns.ContainsKey(col)).ToList();
    }

    private static string NormalizeHeader(string value)
    {
        var normalized = RemoveDiacritics(value.Trim().ToLowerInvariant());
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string? ReadString(IXLRow row, Dictionary<string, int> columns, string key)
    {
        if (!columns.TryGetValue(key, out var col))
        {
            return null;
        }

        var value = row.Cell(col).GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int? ReadInt(IXLRow row, Dictionary<string, int> columns, string key)
    {
        if (!columns.TryGetValue(key, out var col))
        {
            return null;
        }

        var cell = row.Cell(col);
        if (cell.DataType == XLDataType.Number)
        {
            return Convert.ToInt32(cell.GetDouble());
        }

        var raw = cell.GetString();
        if (int.TryParse(raw, NumberStyles.Integer, InvariantCulture, out var value))
        {
            return value;
        }

        if (int.TryParse(raw, NumberStyles.Integer, SpanishCulture, out value))
        {
            return value;
        }

        return null;
    }

    private static decimal? ReadDecimal(IXLRow row, Dictionary<string, int> columns, string key)
    {
        if (!columns.TryGetValue(key, out var col))
        {
            return null;
        }

        var cell = row.Cell(col);
        if (cell.DataType == XLDataType.Number)
        {
            return Convert.ToDecimal(cell.GetDouble());
        }

        var raw = cell.GetString();
        if (decimal.TryParse(raw, NumberStyles.Any, InvariantCulture, out var value))
        {
            return value;
        }

        if (decimal.TryParse(raw, NumberStyles.Any, SpanishCulture, out value))
        {
            return value;
        }

        return null;
    }

    private static Guid? ReadGuid(IXLRow row, Dictionary<string, int> columns, string key)
    {
        if (!columns.TryGetValue(key, out var col))
        {
            return null;
        }

        var raw = row.Cell(col).GetString();
        return Guid.TryParse(raw, out var value) ? value : null;
    }

    private static string BuildAspiranteKey(Guid convocatoriaId, string? dniNie, string? email)
    {
        var key = !string.IsNullOrWhiteSpace(dniNie) ? dniNie.Trim().ToUpperInvariant() : email ?? "";
        return $"{convocatoriaId:N}|{key}";
    }

    private static bool IsExcelContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return contentType.Equals("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("application/vnd.ms-excel", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasValidExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);
        return extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".xls", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 20)
        {
            return false;
        }

        return trimmed.All(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_');
    }

    private static bool IsValidEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (value.Length > 254)
        {
            return false;
        }

        return System.Net.Mail.MailAddress.TryCreate(value, out _);
    }

    private async Task<AspiranteResult> GetOrCreateAspiranteAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid convocatoriaId,
        string? dniNie,
        string? email,
        IXLRow row,
        Dictionary<string, int> columns)
    {
        Guid? existingId = null;
        if (!string.IsNullOrWhiteSpace(dniNie))
        {
            const string findByDniSql = @"
select id from aspirante
where convocatoria_id = @ConvocatoriaId and dni_nie = @DniNie
limit 1;";

            existingId = await conn.ExecuteScalarAsync<Guid?>(findByDniSql, new { ConvocatoriaId = convocatoriaId, DniNie = dniNie }, tx);
        }
        else if (!string.IsNullOrWhiteSpace(email))
        {
            const string findByEmailSql = @"
select id from aspirante
where convocatoria_id = @ConvocatoriaId and lower(email) = @Email
limit 1;";
            existingId = await conn.ExecuteScalarAsync<Guid?>(findByEmailSql, new { ConvocatoriaId = convocatoriaId, Email = email }, tx);
        }

        var nombre = BuildNombre(row, columns);
        var numEmpleat = ReadInt(row, columns, "num_empleat");
        var dniNieEmmascarat = ReadString(row, columns, "dni_nie_emmascarat");
        var primerCognom = ReadString(row, columns, "primer_cognom");
        var segonCognom = ReadString(row, columns, "segon_cognom");
        var nom = ReadString(row, columns, "nom");

        if (existingId.HasValue)
        {
            const string updateSql = @"
update aspirante
set email = coalesce(@Email, email),
    nombre = coalesce(@Nombre, nombre),
    dni_nie = coalesce(@DniNie, dni_nie),
    num_empleat = coalesce(@NumEmpleat, num_empleat),
    dni_nie_emmascarat = coalesce(@DniNieEmmascarat, dni_nie_emmascarat),
    primer_cognom = coalesce(@PrimerCognom, primer_cognom),
    segon_cognom = coalesce(@SegonCognom, segon_cognom),
    nom = coalesce(@Nom, nom)
where id = @Id;";

            await conn.ExecuteAsync(updateSql, new
            {
                Id = existingId.Value,
                Email = email,
                Nombre = nombre,
                DniNie = dniNie,
                NumEmpleat = numEmpleat,
                DniNieEmmascarat = dniNieEmmascarat,
                PrimerCognom = primerCognom,
                SegonCognom = segonCognom,
                Nom = nom
            }, tx);

            return new AspiranteResult(existingId.Value, false);
        }

        var id = Guid.NewGuid();
        const string insertSql = @"
insert into aspirante (
  id,
  convocatoria_id,
  email,
  nombre,
  dni_nie,
  num_empleat,
  dni_nie_emmascarat,
  primer_cognom,
  segon_cognom,
  nom
)
values (
  @Id,
  @ConvocatoriaId,
  @Email,
  @Nombre,
  @DniNie,
  @NumEmpleat,
  @DniNieEmmascarat,
  @PrimerCognom,
  @SegonCognom,
  @Nom
);";

        await conn.ExecuteAsync(insertSql, new
        {
            Id = id,
            ConvocatoriaId = convocatoriaId,
            Email = email ?? string.Empty,
            Nombre = nombre,
            DniNie = dniNie,
            NumEmpleat = numEmpleat,
            DniNieEmmascarat = dniNieEmmascarat,
            PrimerCognom = primerCognom,
            SegonCognom = segonCognom,
            Nom = nom
        }, tx);

        return new AspiranteResult(id, true);
    }

    private static string? BuildNombre(IXLRow row, Dictionary<string, int> columns)
    {
        var nom = ReadString(row, columns, "nom");
        var primerCognom = ReadString(row, columns, "primer_cognom");
        var segonCognom = ReadString(row, columns, "segon_cognom");
        var parts = new[] { nom, primerCognom, segonCognom }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (parts.Length == 0)
        {
            return null;
        }

        return string.Join(" ", parts);
    }

    private async Task<PlazaResult> UpsertPlazaAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid convocatoriaId,
        string basePlaza,
        string posicion,
        IXLRow row,
        Dictionary<string, int> columns)
    {
        var horas = ReadString(row, columns, "hores");
        var torn = ReadString(row, columns, "torn_x");
        var gfh = ReadString(row, columns, "gfh_adjudicacio");
        var centro = ReadString(row, columns, "centro");
        var descripcion = ReadString(row, columns, "descripcion");

        const string upsertSql = @"
insert into plaza (
  id,
  convocatoria_id,
  base,
  posicion,
  hores,
  torn_x,
  gfh_adjudicacio,
  centro,
  descripcion
)
values (
  @Id,
  @ConvocatoriaId,
  @Base,
  @Posicion,
  @Hores,
  @TornX,
  @GfhAdjudicacio,
  @Centro,
  @Descripcion
)
on conflict (convocatoria_id, base, posicion) do update
set hores = coalesce(excluded.hores, plaza.hores),
    torn_x = coalesce(excluded.torn_x, plaza.torn_x),
    gfh_adjudicacio = coalesce(excluded.gfh_adjudicacio, plaza.gfh_adjudicacio),
    centro = coalesce(excluded.centro, plaza.centro),
    descripcion = coalesce(excluded.descripcion, plaza.descripcion)
returning id, (xmax = 0) as inserted;";

        var plaza = await conn.QuerySingleAsync<PlazaRow>(upsertSql, new
        {
            Id = Guid.NewGuid(),
            ConvocatoriaId = convocatoriaId,
            Base = basePlaza,
            Posicion = posicion,
            Hores = horas,
            TornX = torn,
            GfhAdjudicacio = gfh,
            Centro = centro,
            Descripcion = descripcion
        }, tx);

        return new PlazaResult(plaza.Id, plaza.Inserted);
    }

    private static async Task<int> UpsertAspirantePlazaAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid aspiranteId,
        Guid plazaId,
        int ordenDefecto,
        IXLRow row,
        Dictionary<string, int> columns)
    {
        var experiencia = ReadDecimal(row, columns, "experiencia");
        var barem = ReadDecimal(row, columns, "barem_personal");
        var qualificacio = ReadDecimal(row, columns, "qualificacio");
        var total = ReadDecimal(row, columns, "total");
        var ficher = ReadString(row, columns, "ficher_aspirant");
        var pondExp = ReadDecimal(row, columns, "pond_exp");
        var pondBarem = ReadDecimal(row, columns, "pond_barem");
        var prova = ReadDecimal(row, columns, "prova_competencial");
        var pondProva = ReadDecimal(row, columns, "pond_prova");

        const string upsertSql = @"
insert into aspirante_plaza (
  id,
  aspirante_id,
  plaza_id,
  orden_defecto,
  bloqueada,
  experiencia,
  barem_personal,
  qualificacio,
  total,
  ficher_aspirant,
  pond_exp,
  pond_barem,
  prova_competencial,
  pond_prova
)
values (
  @Id,
  @AspiranteId,
  @PlazaId,
  @OrdenDefecto,
  false,
  @Experiencia,
  @BaremPersonal,
  @Qualificacio,
  @Total,
  @FicherAspirant,
  @PondExp,
  @PondBarem,
  @ProvaCompetencial,
  @PondProva
)
on conflict (aspirante_id, plaza_id) do update
set orden_defecto = excluded.orden_defecto,
    experiencia = coalesce(excluded.experiencia, aspirante_plaza.experiencia),
    barem_personal = coalesce(excluded.barem_personal, aspirante_plaza.barem_personal),
    qualificacio = coalesce(excluded.qualificacio, aspirante_plaza.qualificacio),
    total = coalesce(excluded.total, aspirante_plaza.total),
    ficher_aspirant = coalesce(excluded.ficher_aspirant, aspirante_plaza.ficher_aspirant),
    pond_exp = coalesce(excluded.pond_exp, aspirante_plaza.pond_exp),
    pond_barem = coalesce(excluded.pond_barem, aspirante_plaza.pond_barem),
    prova_competencial = coalesce(excluded.prova_competencial, aspirante_plaza.prova_competencial),
    pond_prova = coalesce(excluded.pond_prova, aspirante_plaza.pond_prova),
    orden_usuario = coalesce(aspirante_plaza.orden_usuario, excluded.orden_usuario)
returning (xmax = 0)::int;";

        var inserted = await conn.ExecuteScalarAsync<int>(upsertSql, new
        {
            Id = Guid.NewGuid(),
            AspiranteId = aspiranteId,
            PlazaId = plazaId,
            OrdenDefecto = ordenDefecto,
            Experiencia = experiencia,
            BaremPersonal = barem,
            Qualificacio = qualificacio,
            Total = total,
            FicherAspirant = ficher,
            PondExp = pondExp,
            PondBarem = pondBarem,
            ProvaCompetencial = prova,
            PondProva = pondProva
        }, tx);

        return inserted == 1 ? 1 : 0;
    }

    private async Task<bool> EnsureTokenAsync(IDbConnection conn, IDbTransaction tx, Guid aspiranteId)
    {
        const string existsSql = @"
select 1
from aspirante_token
where aspirante_id = @AspiranteId and revocado_en is null
limit 1;";
        var exists = await conn.ExecuteScalarAsync<int?>(existsSql, new { AspiranteId = aspiranteId }, tx);
        if (exists.HasValue)
        {
            return false;
        }

        const string insertSql = @"
insert into aspirante_token (id, aspirante_id, codigo, expira_en)
values (@Id, @AspiranteId, @Codigo, @ExpiraEn)
ON CONFLICT (codigo) DO NOTHING;";

        for (var i = 0; i < 5; i++)
        {
            var code = GenerateTokenCode();
            var rows = await conn.ExecuteAsync(insertSql, new
            {
                Id = Guid.NewGuid(),
                AspiranteId = aspiranteId,
                Codigo = code,
                ExpiraEn = DateTime.UtcNow.AddDays(15)
            }, tx);

            if (rows > 0)
            {
                return true;
            }
        }

        _logger.LogWarning("No se pudo generar un token único para el aspirante {AspiranteId}.", aspiranteId);
        return false;
    }

    private static string GenerateTokenCode()
    {
        var raw = Guid.NewGuid().ToString("N").ToUpperInvariant();
        return $"AUTO-{raw[..8]}";
    }
}

public sealed record ImportResult(bool Success, string Message);
public sealed record AspiranteResult(Guid Id, bool Created);
public sealed record PlazaResult(Guid Id, bool Created);

internal sealed record PlazaRow(Guid Id, bool Inserted);
