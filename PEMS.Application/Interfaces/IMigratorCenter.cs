namespace PEMS.Application.Interfaces;

/// <summary>What the migrator center answers: <c>Succeeded</c> and a <c>Message</c> in plain words (shown by the action that ran the operation).</summary>
public sealed record MigratorResult(bool Succeeded, string Message);

/// <summary>
/// The migrator center (SCRUM-582) as operations of a stored page: upload a signed package and see what the migrator would do with it, before anything is changed. Read-only: the apply step with its backup and history is
/// SCRUM-807. The package format and the detector are <c>MigrationPackage</c> (SCRUM-581); the trusted vendor keys are the configuration <c>Migrator:TrustedKeys</c> (key id to public key, base64), never a file of the repository.
/// </summary>
public interface IMigratorCenter
{
    [PEMS.Application.Common.PemsOperation("Migrator", "Inspect", Roles = "SystemAdmin")]
    Task<MigratorResult> InspectAsync(byte[] package, string actor);
}
