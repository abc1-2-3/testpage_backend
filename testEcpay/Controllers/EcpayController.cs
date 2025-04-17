using Microsoft.AspNetCore.Mvc;
using System.Text;
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
        public IActionResult CreateOrder([FromBody]DonateRequest donateRequest)
        {
            var order = new Dictionary<string, string>
            {
                { "MerchantID", MerchantID },
                { "MerchantTradeNo",$"TEST{DateTime.Now:yyyyMMddHHmmss}" },
                { "MerchantTradeDate",  DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")},
                { "PaymentType", "aio" },
                { "TotalAmount", $"{donateRequest.Amount}" },
                { "TradeDesc","test"},
                { "ItemName","接成功就喝珍奶" },
                { "ReturnURL","https"},
                { "ChoosePayment", "Credit"},
                { "ClientBackURL","https"},
            };
            var message = donateRequest.Message;
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
            htmlForm.AppendLine("  <div class='loading'>?? 處理中...</div>");

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


            return Content(htmlForm.ToString(), "text/html"); ;

        }
    }
}
