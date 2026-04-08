// Models/User.cs
namespace testEcpay.Models;

public class User
{
    public string Id { get; set; } = string.Empty;       // CUID 或 UUID
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Image { get; set; }
    public string? GoogleId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
