using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// Code-first database installer/updater. Decides whether the database needs an install (empty) or an
/// update (pending migrations on an existing schema), applies the migrations and records the run.
/// Used by the startup bootstrap and by the Admin "Database Update" page; one implementation for both.
/// </summary>
public interface IDatabaseUpdateService
{
    Task<DatabaseUpdateStatusDto> GetStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Applies the install or the pending updates. Safe to call repeatedly: when nothing is pending it
    /// returns NothingToDo. A schema without migration history is never modified.
    /// </summary>
    /// <param name="trigger">"Startup" or "Admin".</param>
    /// <param name="performedBy">User name for admin runs.</param>
    Task<DatabaseUpdateResultDto> ApplyAsync(string trigger, string? performedBy, CancellationToken ct = default);
}
