using System.Text;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق سند PEMS-SEC-PERMIT-AUTH-001 بند ۲/۳: پیاده‌سازی WebAuthn/Passkey با کتابخانه‌ی
/// Fido2NetLib (RP سمت سرور). PEMS فقط CredentialId/PublicKey/SignCount را می‌بیند و ذخیره
/// می‌کند - هرگز داده‌ی بیومتریک خام را نمی‌بیند. محافظت در برابر Replay از طریق مکانیزم
/// Challenge یک‌بارمصرف + SignCount خودِ کتابخانه انجام می‌شود، نه پیاده‌سازی سفارشی.
/// </summary>
public class WebAuthnService : IWebAuthnService
{
    private readonly IFido2 _fido2;
    private readonly PEMSDbContext _context;

    public WebAuthnService(IFido2 fido2, PEMSDbContext context)
    {
        _fido2 = fido2;
        _context = context;
    }

    public async Task<string> GetRegistrationOptionsJsonAsync(int userId, string username, string displayName)
    {
        var existingCredentials = await _context.WebAuthnCredentials
            .Where(x => x.UserId == userId && x.IsActive)
            .ToListAsync();

        var excludeCredentials = existingCredentials
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToList();

        var fidoUser = new Fido2User
        {
            Name = username,
            DisplayName = displayName,
            Id = Encoding.UTF8.GetBytes(userId.ToString())
        };

        var options = _fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = fidoUser,
            ExcludeCredentials = excludeCredentials,
            AuthenticatorSelection = AuthenticatorSelection.Default,
            AttestationPreference = AttestationConveyancePreference.None
        });

        return options.ToJson();
    }

    public async Task<(bool Success, string? Error, int? CredentialDbId)> CompleteRegistrationAsync(
        int userId, string attestationResponseJson, string optionsJson, string? nickname)
    {
        try
        {
            var attestationResponse = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(attestationResponseJson);
            if (attestationResponse == null) return (false, "پاسخ نامعتبر است.", null);

            var originalOptions = CredentialCreateOptions.FromJson(optionsJson);

            var result = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = attestationResponse,
                OriginalOptions = originalOptions,
                IsCredentialIdUniqueToUserCallback = async (p, ct) =>
                {
                    var exists = await _context.WebAuthnCredentials.AnyAsync(x => x.CredentialId == p.CredentialId, ct);
                    return !exists;
                }
            });

            var credential = new WebAuthnCredential
            {
                UserId = userId,
                CredentialId = result.Id,
                PublicKey = result.PublicKey,
                SignCount = result.SignCount,
                AAGUID = result.AaGuid.ToString(),
                Nickname = nickname,
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            await _context.WebAuthnCredentials.AddAsync(credential);
            await _context.SaveChangesAsync();

            return (true, null, credential.WebAuthnCredentialId);
        }
        catch (Fido2VerificationException ex)
        {
            return (false, ex.Message, null);
        }
        catch (Exception ex)
        {
            return (false, $"ثبت Passkey ناموفق بود: {ex.Message}", null);
        }
    }

    public async Task<string?> GetAssertionOptionsJsonAsync(int userId)
    {
        var credentials = await _context.WebAuthnCredentials
            .Where(x => x.UserId == userId && x.IsActive)
            .ToListAsync();

        if (credentials.Count == 0) return null;

        var allowedCredentials = credentials
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToList();

        var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = allowedCredentials,
            UserVerification = UserVerificationRequirement.Preferred
        });

        return options.ToJson();
    }

    public async Task<(bool Success, string? Error, int? CredentialDbId)> CompleteAssertionAsync(
        int userId, string assertionResponseJson, string optionsJson)
    {
        try
        {
            var assertionResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(assertionResponseJson);
            if (assertionResponse == null) return (false, "پاسخ نامعتبر است.", null);

            var originalOptions = AssertionOptions.FromJson(optionsJson);

            var storedCredential = await _context.WebAuthnCredentials
                .FirstOrDefaultAsync(x => x.UserId == userId && x.IsActive && x.CredentialId == assertionResponse.RawId);
            if (storedCredential == null) return (false, "Passkey ثبت‌شده‌ای برای این کاربر یافت نشد.", null);

            var result = await _fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = assertionResponse,
                OriginalOptions = originalOptions,
                StoredPublicKey = storedCredential.PublicKey,
                StoredSignatureCounter = storedCredential.SignCount,
                IsUserHandleOwnerOfCredentialIdCallback = (p, ct) =>
                    Task.FromResult(Encoding.UTF8.GetString(p.UserHandle) == userId.ToString())
            });

            storedCredential.SignCount = result.SignCount;
            storedCredential.LastUsedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            return (true, null, storedCredential.WebAuthnCredentialId);
        }
        catch (Fido2VerificationException ex)
        {
            return (false, ex.Message, null);
        }
        catch (Exception ex)
        {
            return (false, $"تایید Passkey ناموفق بود: {ex.Message}", null);
        }
    }

    public async Task<bool> HasAnyCredentialAsync(int userId) =>
        await _context.WebAuthnCredentials.AnyAsync(x => x.UserId == userId && x.IsActive);

    public async Task<List<(int Id, string? Nickname, DateTime CreatedDate, DateTime? LastUsedDate)>> GetCredentialsAsync(int userId)
    {
        var creds = await _context.WebAuthnCredentials
            .Where(x => x.UserId == userId && x.IsActive)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();
        return creds.Select(c => (c.WebAuthnCredentialId, c.Nickname, c.CreatedDate, c.LastUsedDate)).ToList();
    }
}
