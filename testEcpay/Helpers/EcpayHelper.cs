// Helpers/EcpayHelper.cs
// 改用 Uri.EscapeDataString，在 Linux（Railway）上行為一致
// 原本的 System.Web.HttpUtility.UrlEncode 在 Linux 會把整串 encode，
// 導致 = 和 & 也被 encode，造成 CheckMacValue Error

using System.Security.Cryptography;
using System.Text;

namespace testEcpay.Helpers;

public static class EcpayHelper
{
    public static string GenerateCheckMacValue(
        Dictionary<string, string> parameters,
        string hashKey,
        string hashIV)
    {
        // 1. 排除 CheckMacValue，依 key 字母順序（Ordinal）排序
        var sorted = parameters
            .Where(x => x.Key != "CheckMacValue" && x.Key != "EncryptType")
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}={x.Value}");

        // 2. 組成原始字串
        var raw = $"HashKey={hashKey}&{string.Join("&", sorted)}&HashIV={hashIV}";

        // 3. 對整串做 URL Encode
        //    用 Uri.EscapeDataString 確保跨平台（Linux/Windows）行為一致
        //    它把空白 encode 成 %20（不是 +），符合綠界要求
        var encoded = EncodeForEcpay(raw).ToLower();

        // 4. SHA256 → 大寫 HEX
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(encoded));
        return Convert.ToHexString(bytes); // 已是大寫
    }

    /// <summary>
    /// 綠界的 URL Encode 規則：
    /// 對整個字串 encode，但保留 ! ( ) * ~ 這些字元不 encode
    /// 空白 encode 成 %20
    /// </summary>
    private static string EncodeForEcpay(string input)
    {
        var sb = new StringBuilder();

        foreach (char c in input)
        {
            // 不 encode 的字元：字母、數字、- _ . ! * ( ) ~
            if (IsUnreserved(c))
            {
                sb.Append(c);
            }
            else
            {
                // 其他全部 encode 成 %XX（大寫，之後 ToLower 統一變小寫）
                var bytes = Encoding.UTF8.GetBytes(c.ToString());
                foreach (var b in bytes)
                    sb.Append($"%{b:X2}");
            }
        }

        return sb.ToString();
    }

    private static bool IsUnreserved(char c) =>
        (c >= 'A' && c <= 'Z') ||
        (c >= 'a' && c <= 'z') ||
        (c >= '0' && c <= '9') ||
        c == '-' || c == '_' || c == '.' ||
        c == '!' || c == '*' || c == '(' || c == ')' || c == '~';
}
