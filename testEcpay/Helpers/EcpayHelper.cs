using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace testEcpay.Helpers
{
    public class EcpayHelper
    {
        public static string GenerateCheckMacValue(Dictionary<string, string> parameters, string hashKey, string hashIV)
        {
            var sorted = parameters
        .Where(x => x.Key != "CheckMacValue")
        .OrderBy(x => x.Key, StringComparer.Ordinal)
        .Select(x => $"{x.Key}={x.Value}");

            var raw = $"HashKey={hashKey}&{string.Join("&", sorted)}&HashIV={hashIV}";
            // 加這行
            Console.WriteLine($"[CheckMac Raw] {raw}");

            // URL Encode 並處理 RFC 3986 差異
            var urlEncoded = System.Web.HttpUtility.UrlEncode(raw)
                .ToLower()
                .Replace("+", "%20")
                .Replace("%21", "!")
                .Replace("%28", "(")
                .Replace("%29", ")")
                .Replace("%2a", "*")
                .Replace("%7e", "~");

            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(urlEncoded));
                var sb = new StringBuilder();
                foreach (var b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString().ToUpper();
            }
        }
    }
}

