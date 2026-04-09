// Services/PendingOrderCleanupService.cs
// .NET 背景服務，每小時自動清理超過 24 小時還是 PENDING 的廢單

using Dapper;

namespace testEcpay.Services;

public class PendingOrderCleanupService : BackgroundService
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<PendingOrderCleanupService> _logger;

    // 每小時執行一次
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    // 超過幾小時的 PENDING 視為廢單
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromHours(24);

    public PendingOrderCleanupService(
        IDbConnectionFactory db,
        ILogger<PendingOrderCleanupService> logger)
    {
        _db = db;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[CleanupService] 廢單清理服務已啟動，每 {Interval} 執行一次", Interval);

        // 啟動時先執行一次，之後每隔 Interval 再執行
        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupAsync();
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CleanupAsync()
    {
        try
        {
            using var conn = await _db.CreateConnectionAsync();

            var deleted = await conn.ExecuteAsync(@"
                UPDATE donations
                SET status     = 'Failed',
                    updated_at = NOW()
                WHERE status     = 'Pending'
                  AND created_at < NOW() - @Threshold
            ", new { Threshold = StaleThreshold });

            if (deleted > 0)
                _logger.LogInformation("[CleanupService] 已標記 {Count} 筆逾時廢單為 Failed", deleted);
        }
        catch (Exception ex)
        {
            // 清理失敗不影響主程式，只記 log
            _logger.LogError(ex, "[CleanupService] 清理廢單時發生錯誤");
        }
    }
}
