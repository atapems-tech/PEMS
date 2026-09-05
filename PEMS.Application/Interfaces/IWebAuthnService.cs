namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق سند PEMS-SEC-PERMIT-AUTH-001: احراز هویت WebAuthn/Passkey. PEMS هرگز داده‌ی بیومتریک خام
/// را نمی‌بیند - فقط Assertion رمزنگاری‌شده که توسط Authenticator دستگاه کاربر امضا شده است.
/// options به‌صورت JSON از طریق فرم/درخواست به کلاینت و برگشت داده می‌شود (بدون نیاز به
/// Session سمت سرور) - خود options حاوی Challenge یک‌بارمصرف است که کتابخانه‌ی Fido2 در لحظه‌ی
/// اعتبارسنجی بررسی می‌کند (محافظت داخلی در برابر Replay).
/// </summary>
public interface IWebAuthnService
{
    Task<string> GetRegistrationOptionsJsonAsync(int userId, string username, string displayName);

    Task<(bool Success, string? Error, int? CredentialDbId)> CompleteRegistrationAsync(
        int userId, string attestationResponseJson, string optionsJson, string? nickname);

    Task<string?> GetAssertionOptionsJsonAsync(int userId);

    Task<(bool Success, string? Error, int? CredentialDbId)> CompleteAssertionAsync(
        int userId, string assertionResponseJson, string optionsJson);

    Task<bool> HasAnyCredentialAsync(int userId);

    Task<List<(int Id, string? Nickname, DateTime CreatedDate, DateTime? LastUsedDate)>> GetCredentialsAsync(int userId);
}
