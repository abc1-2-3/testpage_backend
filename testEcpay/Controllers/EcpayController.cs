// Controllers/EcpayController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using testEcpay.Helpers;
using testEcpay.Models;
using testEcpay.Services;

namespace testEcpay.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EcpayController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IDonationService _donationService;

    private string MerchantID  => _config["ECPay:MerchantID"]!;
    private string HashKey     => _config["ECPay:HashKey"]!;
    private string HashIV      => _config["ECPay:HashIV"]!;
    private string BaseUrl     => _config["ECPay:BaseUrl"]!;
    private string FrontendUrl => _config["Frontend:BaseUrl"] ?? "http://localhost:3000";
    private string ActionURL   => _config["ECPay:Environment"] == "Production"
        ? "https://payment.ecpay.com.tw/Cashier/AioCheckOut/V5"
        : "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5";

    public EcpayController(IConfiguration config, IDonationService donationService)
    {
        _config = config;
        _donationService = donationService;
    }

    /// <summary>
    /// 建立贊助訂單
    /// POST /api/ecpay/create-order
    /// 可選：Header 帶 Bearer JWT 則綁定 userId，不帶也可以匿名贊助
    /// </summary>
    [HttpPost("create-order")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateOrder([FromBody] DonateRequest donateRequest)
    {
        if (donateRequest.Amount <= 0 || donateRequest.Amount > 100000)
            return BadRequest(new { error = "金額需介於 1 ~ 100,000 元" });

        if (string.IsNullOrWhiteSpace(donateRequest.DonorName))
            return BadRequest(new { error = "贊助者名稱不可為空" });

        // 取得登入用戶 ID（Optional，未登入也可以贊助）
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // 產生唯一訂單號，先寫入 DB（PENDING），同時記錄 userId
        var orderId = GenerateMerchantTradeNo();
        await _donationService.CreateAsync(
            orderId, donateRequest.Amount, userId,
            donateRequest.DonorName, donateRequest.Message
        );

        // 組合綠界參數
        var order = new Dictionary<string, string>
        {
            { "MerchantID",        MerchantID },
            { "MerchantTradeNo",   orderId },
            { "MerchantTradeDate", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") },
            { "PaymentType",       "aio" },
            { "TotalAmount",       $"{donateRequest.Amount}" },
            { "TradeDesc",         "test" },
            { "ItemName",          "test" },
            { "ReturnURL",         $"{BaseUrl}/api/ecpay/notify" },
            { "ChoosePayment",     "Credit" },
            { "EncryptType",       "1" },
        };

        var checkMacValue = EcpayHelper.GenerateCheckMacValue(order, HashKey, HashIV);
        order.Add("CheckMacValue", checkMacValue);

        return Content(BuildAutoSubmitForm(ActionURL, order), "text/html; charset=utf-8");
    }

    /// <summary>
    /// 綠界付款結果 Webhook（Server-to-server）
    /// POST /api/ecpay/notify
    /// 綠界主動呼叫，必須回傳純文字 "1|OK"
    /// </summary>
    [HttpPost("notify")]
    [AllowAnonymous]
    public async Task<IActionResult> Notify([FromForm] Dictionary<string, string> data)
    {
        if (!data.TryGetValue("CheckMacValue", out var receivedMac))
            return Content("0|Missing CheckMacValue");

        var generatedMac = EcpayHelper.GenerateCheckMacValue(data, HashKey, HashIV);
        if (!string.Equals(receivedMac, generatedMac, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[ECPay Notify] 驗簽失敗 received={receivedMac} generated={generatedMac}");
            return Content("0|CheckMacValue Error");
        }

        var merchantTradeNo = data.GetValueOrDefault("MerchantTradeNo", "");
        var rtnCode         = data.GetValueOrDefault("RtnCode", "");
        var tradeAmtStr     = data.GetValueOrDefault("TradeAmt", "0");
        var customField1    = data.GetValueOrDefault("CustomField1", ""); // userId 備援

        var donation = await _donationService.GetByOrderIdAsync(merchantTradeNo);
        if (donation == null)
        {
            Console.WriteLine($"[ECPay Notify] 找不到訂單: {merchantTradeNo}");
            return Content("0|Order Not Found");
        }

        // 冪等：已處理過就直接回 OK
        if (donation.Status == DonationStatus.Paid)
            return Content("1|OK");

        // 驗證金額防篡改
        if (int.TryParse(tradeAmtStr, out var tradeAmt) && tradeAmt != donation.Amount)
        {
            Console.WriteLine($"[ECPay Notify] 金額不符 received={tradeAmt} expected={donation.Amount}");
            return Content("0|Amount Mismatch");
        }

        // userId：優先用 DB 已有的，備援才用 CustomField1
        var resolvedUserId = donation.UserId
            ?? (string.IsNullOrEmpty(customField1) ? null : customField1);

        var newStatus = rtnCode == "1" ? DonationStatus.Paid : DonationStatus.Failed;
        await _donationService.UpdateStatusAsync(merchantTradeNo, newStatus, resolvedUserId);

        Console.WriteLine($"[ECPay Notify] orderId={merchantTradeNo} status={newStatus} userId={resolvedUserId}");
        return Content("1|OK");
    }

    /// <summary>
    /// 查詢單筆訂單狀態（付款完成後前端輪詢用）
    /// GET /api/ecpay/status/{orderId}
    /// </summary>
    [HttpGet("status/{orderId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStatus(string orderId)
    {
        var donation = await _donationService.GetByOrderIdAsync(orderId);
        if (donation == null)
            return NotFound(new { error = "找不到訂單" });

        return Ok(new DonationStatusResponse(
            OrderId:   donation.OrderId,
            Amount:    donation.Amount,
            DonorName: donation.DonorName,
            Message:   donation.Message,
            Status:    donation.Status.ToString(),
            CreatedAt: donation.CreatedAt
        ));
    }

    /// <summary>
    /// 取得目前登入用戶的贊助歷史紀錄
    /// GET /api/ecpay/my-donations?page=1&pageSize=10
    /// Header: Authorization: Bearer {jwt}
    /// </summary>
    [HttpGet("my-donations")]
    [Authorize]
    public async Task<IActionResult> GetMyDonations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        pageSize = Math.Min(pageSize, 50);
        var donations = await _donationService.GetByUserIdAsync(userId, page, pageSize);

        return Ok(donations.Select(d => new
        {
            orderId   = d.OrderId,
            amount    = d.Amount,
            donorName = d.DonorName,
            message   = d.Message,
            status    = d.Status.ToString(),
            createdAt = d.CreatedAt
        }));
    }

    /// <summary>
    /// 取得最近付款成功的贊助（直播頁跑馬燈用，公開）
    /// GET /api/ecpay/recent
    /// </summary>
    [HttpGet("recent")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 20)
    {
        var donations = await _donationService.GetRecentAsync(Math.Min(limit, 50));
        return Ok(donations.Select(d => new
        {
            donorName = d.DonorName,
            amount    = d.Amount,
            message   = d.Message,
            createdAt = d.CreatedAt
        }));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GenerateMerchantTradeNo()
    {
        var ts   = DateTime.Now.ToString("yyyyMMddHHmmss");
        var rand = Random.Shared.Next(0, 1000).ToString("D3");
        return $"DON{ts}{rand}"; // 20 chars
    }

    private static string BuildAutoSubmitForm(string actionUrl, Dictionary<string, string> parameters)
    {
        var inputs = string.Join("\n", parameters.Select(kv =>
            $"    <input type=\"hidden\" name=\"{EscapeHtml(kv.Key)}\" value=\"{EscapeHtml(kv.Value)}\" />"));

        return $$"""
            <!DOCTYPE html>
            <html lang="zh-TW">
            <head>
              <meta charset="UTF-8" />
              <title>前往付款中...</title>
              <style>
                body {
                  font-family: "Noto Serif TC", serif;
                  display: flex; flex-direction: column;
                  align-items: center; justify-content: center;
                  min-height: 100vh; margin: 0;
                  background: #1a1228; color: #e8d5b7;
                }
                .rune { font-size: 48px; animation: spin 3s linear infinite; }
                p { font-size: 18px; margin-top: 16px; opacity: 0.8; }
                @keyframes spin { to { transform: rotate(360deg); } }
              </style>
            </head>
            <body>
              <div class="rune">✦</div>
              <p>正在施展魔法，導向綠界付款頁面...</p>
              <form id="ecpay" action="{{actionUrl}}" method="post">
            {{inputs}}
              </form>
              <script>
                setTimeout(function () { document.getElementById("ecpay").submit(); }, 500);
              </script>
            </body>
            </html>
            """;
    }

    private static string EscapeHtml(string s) =>
        s.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
}
