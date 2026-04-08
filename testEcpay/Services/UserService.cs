// Services/UserService.cs
using Dapper;
using testEcpay.Models;

namespace testEcpay.Services;

public interface IUserService
{
    Task<User?> GetByEmailAsync(string email);
    Task<User> UpsertAsync(string email, string? name, string? image, string? googleId);
}

public class UserService : IUserService
{
    private readonly IDbConnectionFactory _db;

    public UserService(IDbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        using var conn = await _db.CreateConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<User>(@"
            SELECT
                id         AS Id,
                email      AS Email,
                name       AS Name,
                image      AS Image,
                google_id  AS GoogleId,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM users
            WHERE email = @Email
        ", new { Email = email });
    }

    /// <summary>
    /// Upsert：找到就更新 name/image/googleId，找不到就新增
    /// </summary>
    public async Task<User> UpsertAsync(string email, string? name, string? image, string? googleId)
    {
        using var conn = await _db.CreateConnectionAsync();

        var id = Guid.NewGuid().ToString("N"); // 產生新 ID（只在 INSERT 時用）

        await conn.ExecuteAsync(@"
            INSERT INTO users (id, email, name, image, google_id, created_at, updated_at)
            VALUES (@Id, @Email, @Name, @Image, @GoogleId, NOW(), NOW())
            ON CONFLICT (email) DO UPDATE SET
                name       = EXCLUDED.name,
                image      = EXCLUDED.image,
                google_id  = COALESCE(EXCLUDED.google_id, users.google_id),
                updated_at = NOW()
        ", new { Id = id, Email = email, Name = name, Image = image, GoogleId = googleId });

        // 回傳更新後的資料
        return (await GetByEmailAsync(email))!;
    }
}
