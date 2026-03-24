using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Web;
using testEcpay.Helpers;
using testEcpay.Model;
namespace testEcpay.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EcpayController : ControllerBase
    {
        private const string MerchantID = "3002607";
        private const string HashKey = "pwFHCqoQZGmho4w6";
        private const string HashIV = "EkRm7iFT261dpevs";
        private const string ActionURL = "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5";

        [HttpPost("CreateOrder")]
        public IActionResult CreateOrder([FromBody] DonateRequest donateRequest)
        {
            var order = new Dictionary<string, string>
            {
                { "MerchantID", MerchantID },
                { "MerchantTradeNo", $"TEST{DateTime.Now:yyyyMMddHHmmss}" },
                { "MerchantTradeDate", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") },
                { "PaymentType", "aio" },
                { "TotalAmount", $"{donateRequest.Amount}" },
                { "TradeDesc", "直播贊助" },
                { "ItemName", "直播贊助" },
                { "ReturnURL", "https://ernestina-inferible-eerily.ngrok-free.dev/api/Ecpay/Callback" },
                { "ChoosePayment", "Credit" },
                { "ClientBackURL", "http://localhost:3000/donate" },
            };

            var checkMacValue = EcpayHelper.GenerateCheckMacValue(order, HashKey, HashIV);
            order.Add("CheckMacValue", checkMacValue);

            var htmlForm = new StringBuilder();
            htmlForm.AppendLine("<!DOCTYPE html>");
            htmlForm.AppendLine("<html lang='zh-TW'>");
            htmlForm.AppendLine("<head>");
            htmlForm.AppendLine("  <meta charset='UTF-8' />");
            htmlForm.AppendLine("  <title>前往付款中...</title>");
            htmlForm.AppendLine("  <style>");
            htmlForm.AppendLine("    body { font-family: sans-serif; text-align: center; padding-top: 100px; }");
            htmlForm.AppendLine("    .loading { font-size: 20px; margin-top: 20px; animation: blink 1s infinite; }");
            htmlForm.AppendLine("    @keyframes blink { 0%, 100% { opacity: 1; } 50% { opacity: 0.5; } }");
            htmlForm.AppendLine("  </style>");
            htmlForm.AppendLine("</head>");
            htmlForm.AppendLine("<body>");
            htmlForm.AppendLine("  <p>正在為您導向綠界付款頁面，請稍候...</p>");
            htmlForm.AppendLine("  <div class='loading'>⏳ 處理中...</div>");

            htmlForm.AppendLine($"  <form id='ecpay' action='{ActionURL}' method='post'>");
            foreach (var kv in order)
            {
                htmlForm.AppendLine($"    <input type='hidden' name='{kv.Key}' value='{kv.Value}' />");
            }
            htmlForm.AppendLine("  </form>");

            htmlForm.AppendLine("  <script>");
            htmlForm.AppendLine("    setTimeout(function() { document.getElementById('ecpay').submit(); }, 100);");
            htmlForm.AppendLine("  </script>");
            htmlForm.AppendLine("</body>");
            htmlForm.AppendLine("</html>");

            return Content(htmlForm.ToString(), "text/html; charset=utf-8");
        }
        [HttpPost("callback")]
        [AllowAnonymous]
        public IActionResult Callback([FromForm] Dictionary<string, string> data)
        {
            // 1. 驗證 CheckMacValue
             if (!data.TryGetValue("CheckMacValue", out var checkMacValue))
                return BadRequest("缺少 CheckMacValue");

            var generatedMac = EcpayHelper.GenerateCheckMacValue(data, HashKey, HashIV);
            if (!string.Equals(checkMacValue, generatedMac, StringComparison.OrdinalIgnoreCase))
                return Content("0|CheckMacValue 驗證失敗");

            // 2. 檢查交易是否成功
            if (data.TryGetValue("RtnCode", out var rtnCode) && rtnCode == "1")
            {
                // 3. 這裡可以根據 MerchantTradeNo 更新訂單狀態
                var merchantTradeNo = data["MerchantTradeNo"];
                // TODO: 更新訂單狀態為已付款

                // 4. 回傳 1|OK 給綠界
                return Content("1|OK");
            }

            // 交易失敗
            return Content("0|交易失敗");
        }
    }
}
