// Services/DonationService.cs
using Dapper;
using testEcpay.Models;

namespace testEcpay.Services;

public interface IDonationService
{
    Task<Donation> CreateAsync(string orderId, int amount, string? userId, string? donorName, string? message);
    Task<Donation?> GetByOrderIdAsync(string orderId);
    Task<bool> UpdateStatusAsync(string orderId, DonationStatus status, string? userId = null);
    Task<IEnumerable<Donation>> GetByUserIdAsync(string userId, int page = 1, int pageSize = 10);
    Task<IEnumerable<Donation>> GetRecentAsync(int limit = 20);
}

public class DonationService : IDonationService
{
    private readonly IDbConnectionFactory _db;

    public DonationService(IDbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<Donation> CreateAsync(
        string orderId, int amount, string? userId, string? donorName, string? message)
    {
        using var conn = await _db.CreateConnectionAsync();
        var id = Guid.NewGuid().ToString("N");

        await conn.ExecuteAsync(@"
            INSERT INTO donations (id, order_id, amount, user_id, donor_name, message, status, created_at, updated_at)
            VALUES (@Id, @OrderId, @Amount, @UserId, @DonorName, @Message, 'Pending', NOW(), NOW())
        ", new { Id = id, OrderId = orderId, Amount = amount, UserId = userId, DonorName = donorName, Message = message });

        return (await GetByOrderIdAsync(orderId))!;
    }

    public async Task<Donation?> GetByOrderIdAsync(string orderId)
    {
        using var conn = await _db.CreateConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<Donation>(@"
            SELECT
                id          AS Id,
                user_id     AS UserId,
                amount      AS Amount,
                message     AS Message,
                donor_name  AS DonorName,
                order_id    AS OrderId,
                status      AS Status,
                created_at  AS CreatedAt,
                updated_at  AS UpdatedAt
            FROM donations
            WHERE order_id = @OrderId
        ", new { OrderId = orderId });
    }

    /// <summary>
    /// 更新訂單狀態
    /// userId 參數：如果 DB 裡原本是 null（匿名），但 CustomField1 有帶 userId，
    /// 這裡可以順便補上去，讓這筆贊助被正確歸戶
    /// </summary>
    public async Task<bool> UpdateStatusAsync(string orderId, DonationStatus status, string? userId = null)
    {
        using var conn = await _db.CreateConnectionAsync();
        var rows = await conn.ExecuteAsync(@"
            UPDATE donations
            SET
                status     = @Status,
                user_id    = COALESCE(user_id, @UserId),  -- 只在原本是 null 時才填入
                updated_at = NOW()
            WHERE order_id = @OrderId
        ", new { Status = status.ToString(), OrderId = orderId, UserId = userId });

        return rows > 0;
    }

    /// <summary>
    /// 取得指定用戶的贊助歷史（分頁，只顯示 PAID 的）
    /// </summary>
    public async Task<IEnumerable<Donation>> GetByUserIdAsync(string userId, int page = 1, int pageSize = 10)
    {
        using var conn = await _db.CreateConnectionAsync();
        var offset = (page - 1) * pageSize;

        return await conn.QueryAsync<Donation>(@"
            SELECT
                id          AS Id,
                user_id     AS UserId,
                amount      AS Amount,
                message     AS Message,
                donor_name  AS DonorName,
                order_id    AS OrderId,
                status      AS Status,
                created_at  AS CreatedAt,
                updated_at  AS UpdatedAt
            FROM donations
            WHERE user_id = @UserId
              AND status  = 'Paid'
            ORDER BY created_at DESC
            LIMIT @PageSize OFFSET @Offset
        ", new { UserId = userId, PageSize = pageSize, Offset = offset });
    }

    /// <summary>取得最近的贊助紀錄（供直播頁顯示用）</summary>
    public async Task<IEnumerable<Donation>> GetRecentAsync(int limit = 20)
    {
        using var conn = await _db.CreateConnectionAsync();
        return await conn.QueryAsync<Donation>(@"
            SELECT
                id          AS Id,
                user_id     AS UserId,
                amount      AS Amount,
                message     AS Message,
                donor_name  AS DonorName,
                order_id    AS OrderId,
                status      AS Status,
                created_at  AS CreatedAt,
                updated_at  AS UpdatedAt
            FROM donations
            WHERE status = 'Paid'
            ORDER BY created_at DESC
            LIMIT @Limit
        ", new { Limit = limit });
    }
}
