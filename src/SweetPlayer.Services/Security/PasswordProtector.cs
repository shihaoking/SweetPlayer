using System.Text;

namespace SweetPlayer.Services.Security;

/// <summary>
/// 凭据保护器：用于持久化前对密码做简单可逆编码。
/// 后续可替换为 Windows Data Protection API 实现真正的加密。
/// </summary>
public interface IPasswordProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedText);
}

/// <summary>
/// 基于 Base64 的临时实现。仅用于避免明文持久化，并非安全加密。
/// </summary>
public sealed class Base64PasswordProtector : IPasswordProtector
{
    private const string Prefix = "b64:";

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(plaintext);
        return Prefix + Convert.ToBase64String(bytes);
    }

    public string Unprotect(string protectedText)
    {
        if (string.IsNullOrEmpty(protectedText))
        {
            return string.Empty;
        }

        if (!protectedText.StartsWith(Prefix, StringComparison.Ordinal))
        {
            // 兼容历史明文数据
            return protectedText;
        }

        try
        {
            var bytes = Convert.FromBase64String(protectedText[Prefix.Length..]);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            return protectedText;
        }
    }
}
