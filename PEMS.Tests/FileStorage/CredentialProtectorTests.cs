using Microsoft.AspNetCore.DataProtection;
using PEMS.Infrastructure.FileStorage;

namespace PEMS.Tests.FileStorage;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۱۲: تست چرخه‌ی کامل رمزنگاری/رمزگشایی
/// رمز عبور Storage Provider - مقدار ذخیره‌شده هرگز نباید متن‌خام باشد.
/// </summary>
public class CredentialProtectorTests
{
    [Fact]
    public void Protect_ThenUnprotect_ReturnsOriginalPlaintext()
    {
        var protector = new CredentialProtector(new EphemeralDataProtectionProvider());
        const string plaintext = "MySecretFtpPassword123!";

        var protectedValue = protector.Protect(plaintext);
        var roundTripped = protector.Unprotect(protectedValue);

        Assert.Equal(plaintext, roundTripped);
    }

    [Fact]
    public void Protect_ReturnedValue_IsNeverThePlaintext()
    {
        var protector = new CredentialProtector(new EphemeralDataProtectionProvider());
        const string plaintext = "AnotherSecret!";

        var protectedValue = protector.Protect(plaintext);

        Assert.NotEqual(plaintext, protectedValue);
        Assert.DoesNotContain(plaintext, protectedValue);
    }

    [Fact]
    public void Unprotect_TamperedValue_Throws()
    {
        var protector = new CredentialProtector(new EphemeralDataProtectionProvider());
        var protectedValue = protector.Protect("Whatever");
        var tampered = protectedValue[..^2] + "xx";

        Assert.ThrowsAny<Exception>(() => protector.Unprotect(tampered));
    }
}
