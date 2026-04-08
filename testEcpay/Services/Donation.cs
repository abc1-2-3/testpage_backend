// Models/Donation.cs
namespace testEcpay.Models;

public class Donation
{
    public string Id { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public int Amount { get; set; }
    public string? Message { get; set; }
    public string? DonorName { get; set; }   // 贊助者顯示名稱（可匿名）
    public string OrderId { get; set; } = string.Empty;  // MerchantTradeNo
    public DonationStatus Status { get; set; } = DonationStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum DonationStatus
{
    Pending,   // 等待付款
    Paid,      // 付款成功
    Failed     // 付款失敗
}
