using System.Data;
using Npgsql;

namespace Prioritizacion.Web.Data;

public sealed class Db
{
    private readonly string _cs;

    public Db(IConfiguration config)
    {
        _cs = config.GetConnectionString("Default")
              ?? throw new InvalidOperationException("Connection string 'Default' no configurada.");
    }

    public IDbConnection OpenConnection()
    {
        var conn = new NpgsqlConnection(_cs);
        conn.Open();
        return conn;
    }
}
