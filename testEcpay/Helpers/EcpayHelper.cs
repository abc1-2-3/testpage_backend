using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace testEcpay.Helpers
{
    public class EcpayHelper
    {
        public static string GenerateCheckMacValue(Dictionary<string, string> parameters, string hashKey, string hashIV)
        {
            // 依照 key 排序
            var sorted = parameters.OrderBy(p => p.Key).ToList();
            var raw = new StringBuilder($"HashKey={hashKey}");
            foreach (var kv in sorted)
            {
                raw.Append($"&{kv.Key}={kv.Value}");
            }
            raw.Append($"&HashIV={hashIV}");

            var urlEncoded = HttpUtility.UrlEncode(raw.ToString()).ToLower();
            urlEncoded = urlEncoded.Replace("%21", "!")
                                   .Replace("%28", "(")
                                   .Replace("%29", ")")
                                   .Replace("%2a", "*")
                                   .Replace("%2d", "-")
                                   .Replace("%2e", ".")
                                   .Replace("%5f", "_");

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(urlEncoded));
            return BitConverter.ToString(hash).Replace("-", "").ToUpper();
        }
    }
}

