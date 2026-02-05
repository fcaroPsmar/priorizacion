using System.Security.Claims;
using Dapper;
using Prioritizacion.Web.Data;

namespace Prioritizacion.Web.Services;

public sealed class AuthService
{
    private const int MaxEmailLength = 254;
    private const int MaxCodeLength = 32;
    private const int MaxFailedAttempts = 5;
    private static readonly System.Text.RegularExpressions.Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private readonly Db _db;

    public AuthService(Db db)
    {
        _db = db;
    }

    public async Task<(ClaimsPrincipal? principal, string? error)> TryLoginAsync(string email, string codigo)
    {
        email = (email ?? string.Empty).Trim().ToLowerInvariant();
        codigo = (codigo ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(codigo))
            return (null, "Debes indicar correo y código.");

        if (email.Length > MaxEmailLength || codigo.Length > MaxCodeLength)
            return (null, "Credenciales inválidas.");

        if (!EmailRegex.IsMatch(email) || !System.Net.Mail.MailAddress.TryCreate(email, out _))
            return (null, "Correo electrónico inválido.");

        using var conn = _db.OpenConnection();

        const string attemptsSql = @"
select intentos_fallidos
from aspirante_token
where codigo = @Codigo
limit 1;";
        var failedAttempts = await conn.ExecuteScalarAsync<int?>(attemptsSql, new { Codigo = codigo });
        if (failedAttempts is >= MaxFailedAttempts)
        {
            return (null, "El código se ha bloqueado por demasiados intentos fallidos.");
        }

        // Validación: token válido + convocatoria abierta + aspirante no enviado
        const string sql = @"
select t.aspirante_id as AspiranteId
from vw_tokens_validos t
join aspirante a on a.id = t.aspirante_id
where t.codigo = @Codigo and lower(a.email) = @Email
limit 1;";

        var aspiranteId = await conn.ExecuteScalarAsync<Guid?>(sql, new { Codigo = codigo, Email = email });

        if (aspiranteId is null)
        {
            // Registrar intento fallido (si existe el código)
            const string failSql = @"
update aspirante_token
set intentos_fallidos = intentos_fallidos + 1
where codigo = @Codigo;";
            await conn.ExecuteAsync(failSql, new { Codigo = codigo });
            return (null, "Código o correo incorrectos, la convocatoria está cerrada o el código ha caducado.");
        }

        // Auditoría
        const string okSql = @"
update aspirante_token
set ultimo_acceso_en = now(), intentos_fallidos = 0
where codigo = @Codigo;";
        await conn.ExecuteAsync(okSql, new { Codigo = codigo });

        var claims = new List<Claim>
        {
            new("aspirante_id", aspiranteId.Value.ToString()),
            new(ClaimTypes.Email, email)
        };

        var identity = new ClaimsIdentity(claims, "cookie");
        return (new ClaimsPrincipal(identity), null);
    }

    public static Guid GetAspiranteId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("aspirante_id");
        return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
    }
}
