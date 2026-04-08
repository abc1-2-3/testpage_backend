// Services/DatabaseInitializer.cs
// 啟動時自動建立資料表（如果不存在）
// 不需要 Migration 工具，適合小專案快速部署

using Dapper;
using Npgsql;

namespace testEcpay.Services;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // ── Users 資料表 ─────────────────────────────────────────────────────
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS users (
                id          TEXT        PRIMARY KEY,
                email       TEXT        NOT NULL UNIQUE,
                name        TEXT,
                image       TEXT,
                google_id   TEXT        UNIQUE,
                created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
        ");

        // ── Donations 資料表 ──────────────────────────────────────────────────
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS donations (
                id          TEXT        PRIMARY KEY,
                user_id     TEXT        REFERENCES users(id) ON DELETE SET NULL,
                amount      INT         NOT NULL,
                message     TEXT,
                donor_name  TEXT,
                order_id    TEXT        NOT NULL UNIQUE,
                status      TEXT        NOT NULL DEFAULT 'Pending',
                created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
        ");

        Console.WriteLine("[DB] 資料表初始化完成");
    }
}
