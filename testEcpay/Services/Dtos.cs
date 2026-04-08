// Models/Dtos.cs
// 所有 Request / Response 的資料傳輸物件

namespace testEcpay.Models;

// ── Auth ─────────────────────────────────────────────────────────────────────

/// <summary>Next.js 呼叫 /api/auth/login 時傳入的資料</summary>
public record LoginRequest(
    string Email,
    string? Name,
    string? Image,
    string? GoogleId
);

/// <summary>登入成功後回傳給前端的 JWT + 使用者資訊</summary>
public record LoginResponse(
    string Token,
    string UserId,
    string Email,
    string? Name,
    string? Image
);

// ── Payment ───────────────────────────────────────────────────────────────────

/// <summary>前端建立贊助訂單時傳入的資料</summary>
public record DonateRequest(
    int Amount,
    string DonorName,
    string? Message
);

/// <summary>查詢訂單狀態的回應</summary>
public record DonationStatusResponse(
    string OrderId,
    int Amount,
    string? DonorName,
    string? Message,
    string Status,
    DateTime CreatedAt
);
