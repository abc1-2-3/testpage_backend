// Services/DonationService.cs
using Dapper;
using testEcpay.Models;

namespace testEcpay.Services;

public interface IDonationService
{
    Task<Donation> CreateAsync(string orderId, int amount, string? userId, string? donorName, string? message);
    Task<Donation?> GetByOrderIdAsync(string orderId);
    Task<bool> UpdateStatusAsync(string orderId, DonationStatus status);
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

    public async Task<bool> UpdateStatusAsync(string orderId, DonationStatus status)
    {
        using var conn = await _db.CreateConnectionAsync();
        var rows = await conn.ExecuteAsync(@"
            UPDATE donations
            SET status = @Status, updated_at = NOW()
            WHERE order_id = @OrderId
        ", new { Status = status.ToString(), OrderId = orderId });

        return rows > 0;
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
