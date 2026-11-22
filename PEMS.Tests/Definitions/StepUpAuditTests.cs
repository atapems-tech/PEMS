using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Tests.TestSupport;
using PEMS.Web.Definitions;

namespace PEMS.Tests.Definitions;

/// <summary>SCRUM-819: the step-up writes an audit record for a passed confirmation as well as for a denial.</summary>
public class StepUpAuditTests
{
    private sealed class FakeWebAuthn(bool accept) : IWebAuthnService
    {
        public Task<string> GetRegistrationOptionsJsonAsync(int userId, string username, string displayName) => Task.FromResult("{}");
        public Task<(bool Success, string? Error, int? CredentialDbId)> CompleteRegistrationAsync(int userId, string optionsJson, string attestationJson, string? nickname) => Task.FromResult<(bool, string?, int?)>((true, null, 1));
        public Task<string?> GetAssertionOptionsJsonAsync(int userId) => Task.FromResult<string?>("{\"challenge\":\"x\"}");
        public Task<(bool Success, string? Error, int? CredentialDbId)> CompleteAssertionAsync(int userId, string assertionJson, string optionsJson) => Task.FromResult<(bool, string?, int?)>(accept ? (true, null, 1) : (false, "bad answer", null));
        public Task<bool> HasAnyCredentialAsync(int userId) => Task.FromResult(true);
        public Task<List<(int Id, string? Nickname, DateTime CreatedDate, DateTime? LastUsedDate)>> GetCredentialsAsync(int userId) => Task.FromResult(new List<(int, string?, DateTime, DateTime?)>());
    }

    private static (WebAuthnStepUpVerifier Verifier, PEMSDbContext Db) Build(bool accept)
    {
        var db = PemsDbContextFactory.CreateInMemory("StepUpAudit_" + Guid.NewGuid());
        db.Users.Add(new ApplicationUser { Id = 5, UserName = "ali", NormalizedUserName = "ALI", FullName = "Ali", IsActive = true });
        db.SaveChanges();
        var services = new ServiceCollection().AddSingleton(db).AddSingleton<IWebAuthnService>(new FakeWebAuthn(accept)).BuildServiceProvider();
        return (new WebAuthnStepUpVerifier(services.GetRequiredService<IServiceScopeFactory>()), db);
    }

    [Fact]
    public async Task A_passed_confirmation_is_audited_as_an_allow()
    {
        var (verifier, db) = Build(accept: true);
        var result = await verifier.VerifyAsync(new StepUpRequest("ali", "Permit", "Approve", "{\"a\":1}", "{\"o\":1}", "10.0.0.1"), default);
        Assert.Equal(StepUpStatus.Passed, result.Status);
        var row = await db.AuthorizationAuditLogs.SingleAsync();
        Assert.Equal((5, "Permit", "Approve", "Allow", "10.0.0.1"), (row.UserId, row.ResourceName, row.ActionName, row.Decision, row.IpAddress));
    }

    [Fact]
    public async Task A_refused_answer_is_audited_as_a_denial_and_never_as_an_allow()
    {
        var (verifier, db) = Build(accept: false);
        var result = await verifier.VerifyAsync(new StepUpRequest("ali", "Permit", "Approve", "{\"a\":1}", "{\"o\":1}", null), default);
        Assert.Equal(StepUpStatus.Denied, result.Status);
        Assert.DoesNotContain(await db.AuthorizationAuditLogs.ToListAsync(), r => r.Decision == "Allow");
    }
}
