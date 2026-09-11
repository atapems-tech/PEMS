using Microsoft.AspNetCore.DataProtection;
using PEMS.Application.Interfaces;

namespace PEMS.Infrastructure.FileStorage;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۱۲: اولین استفاده‌ی واقعی
/// Microsoft.AspNetCore.DataProtection در این پروژه - پیش از این هیچ رمزنگاری سمت سرور برای
/// اعتبارنامه به کار نرفته بود (حتی Ftp:Password موجود هم متن‌خام در appsettings.json است،
/// طبق ممیزی مستقیم این نشست). Purpose string مخصوص این ویژگی است تا کلیدهای رمزنگاری هرگز
/// با بخش دیگری از برنامه تداخل نکنند.
/// </summary>
public class CredentialProtector : ICredentialProtector
{
    private const string PurposeString = "PEMS.StorageProvider.Credentials.v1";
    private readonly IDataProtector _protector;

    public CredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(PurposeString);
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}
