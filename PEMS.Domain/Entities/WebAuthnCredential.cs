using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند PEMS-SEC-PERMIT-AUTH-001 بند ۳: فقط حداقل اطلاعات لازم برای احراز هویت WebAuthn/
/// Passkey ذخیره می‌شود - هرگز داده‌ی بیومتریک خام (اثر انگشت/چهره) در PEMS ذخیره یا پردازش
/// نمی‌شود؛ آن‌ها فقط توسط سیستم‌عامل/Authenticator دستگاه کاربر تایید می‌شوند و فقط Assertion
/// رمزنگاری‌شده به سرور می‌رسد.
/// </summary>
public class WebAuthnCredential : BaseEntity
{
    public int WebAuthnCredentialId { get; set; }

    public int UserId { get; set; }

    public byte[] CredentialId { get; set; } = Array.Empty<byte>();
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();
    public uint SignCount { get; set; }
    public string? AAGUID { get; set; }

    public string? Nickname { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? LastUsedDate { get; set; }
}
