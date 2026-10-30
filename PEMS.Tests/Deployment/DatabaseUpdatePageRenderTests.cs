using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Tests.TestSupport;
using PEMS.Web.Areas.Admin.Controllers;

namespace PEMS.Tests.Deployment;

/// <summary>
/// SCRUM-280: Admin > Database Update with the automatic pre-update backup. When the service backs up itself the manual
/// "backup taken" box gives way to a notice (the form still posts the confirmation the controller expects); the run table
/// shows the backup file. Rendered without HTTP or database.
/// </summary>
public class DatabaseUpdatePageRenderTests
{
    private static Task<string> Render(DatabaseUpdateStatusDto model)
    {
        var controller = new DatabaseUpdateController(null!, null!);
        var result = new ViewResult { ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(controller.ViewData) { Model = model } };
        return RazorViewRenderer.RenderAsync(controller, result, "~/Areas/Admin/Views/DatabaseUpdate/Index.cshtml");
    }

    private static DatabaseUpdateStatusDto Status(DatabaseSetupState state, bool autoBackup) => new()
    {
        State = state,
        BackupBeforeUpdate = autoBackup,
        KnownMigrationCount = 124,
        AppliedMigrationCount = 122,
        PendingMigrations = state == DatabaseSetupState.UpdateAvailable ? new List<string> { "20261026101257_LicensingSeal", "20261027104305_DatabaseUpdateRunBackupFile" } : new List<string>(),
        RecentRuns = new List<DatabaseUpdateRunDto>
        {
            new() { Trigger = "Startup", Mode = "Update", Succeeded = true, MigrationsApplied = 2, BackupFile = @"D:\Backups\PEMSDB_preupdate_20261027_101500.bak" }
        }
    };

    [Fact]
    public async Task Automatic_backup_replaces_the_manual_checkbox_with_a_notice_and_still_posts_the_confirmation()
    {
        var html = await Render(Status(DatabaseSetupState.UpdateAvailable, autoBackup: true));

        Assert.Contains("data-auto-backup", html);
        Assert.Contains("type=\"hidden\" name=\"backupConfirmed\" value=\"true\"", html);
        Assert.DoesNotContain("id=\"backupConfirmed\"", html);
    }

    [Theory]
    [InlineData(DatabaseSetupState.UpdateAvailable, false)]   // backup off: the operator confirms a manual backup
    [InlineData(DatabaseSetupState.FirstRun, true)]           // an install takes no backup, so the box stays
    public async Task Without_an_automatic_backup_the_operator_confirms_one(DatabaseSetupState state, bool autoBackup)
    {
        var html = await Render(Status(state, autoBackup));

        Assert.Contains("id=\"backupConfirmed\"", html);
        Assert.DoesNotContain("data-auto-backup", html);
    }

    [Fact]
    public async Task Run_history_shows_the_backup_file()
    {
        var html = await Render(Status(DatabaseSetupState.UpToDate, autoBackup: true));

        Assert.Contains(@"PEMSDB_preupdate_20261027_101500.bak", html);
    }
}
