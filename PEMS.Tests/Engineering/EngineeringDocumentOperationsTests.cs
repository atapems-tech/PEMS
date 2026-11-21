using System.Text.Json.Nodes;
using PEMS.Application.Definitions;
using PEMS.Domain.Entities;
using PEMS.Persistence.Seed;
using PEMS.Tests.TestSupport;
using PEMS.Web.Definitions;
using PEMS.Web.Services;

namespace PEMS.Tests.Engineering;

/// <summary>
/// SCRUM-690: the lifecycle commands of the engineering document details page are operations. The status rules and their messages (a rule that refuses never reaches a service), the reason a freeze needs, the
/// permission floors of the old controller, and the separation-of-duties guard of the approval (the setting of the shipped action) are kept.
/// </summary>
public class EngineeringDocumentOperationsTests
{
    private static (EngineeringDocumentOperations Ops, PEMS.Persistence.Context.PEMSDbContext Ctx) Create(string name)
    {
        var ctx = PemsDbContextFactory.CreateInMemory("EngDocOps_" + name);
        // a refused rule never reaches a service, so the services are not needed here
        return (new EngineeringDocumentOperations(ctx, null!, null!, null!, null!, null!, null!), ctx);
    }

    private static async Task<int> AddAsync(PEMS.Persistence.Context.PEMSDbContext ctx, string status)
    {
        var doc = new EngineeringDocument { DocumentNo = "D-" + Guid.NewGuid().ToString("N")[..6], Title = "T", DocumentType = "Datasheet", ProjectId = 1, Status = status, IsActive = true };
        ctx.EngineeringDocuments.Add(doc);
        await ctx.SaveChangesAsync();
        return doc.DocumentId;
    }

    private static readonly List<string> Roles = ["EngineeringManager"];

    [Fact]
    public async Task A_missing_document_gives_a_message_for_every_status_rule()
    {
        var (ops, _) = Create(nameof(A_missing_document_gives_a_message_for_every_status_rule));

        Assert.False((await ops.SubmitAsync(99, null, null, 1, Roles, null, null)).Success);
        Assert.False((await ops.CheckAsync(99, null, 1, Roles, null, null)).Success);
        Assert.False((await ops.ApproveAsync(99, null, 1, Roles, null, null)).Success);
        Assert.False((await ops.RejectAsync(99, null, 1, Roles, null, null)).Success);
        Assert.False((await ops.IssueAsync(99, null, "InternalDCC", null, null, null, null, null, null, 1, Roles, null, null)).Success);
        Assert.False((await ops.ReviseAndResubmitAsync(99, null, 1, Roles, null, null)).Success);
    }

    [Theory]
    [InlineData("Approved", "Document cannot be submitted in current status")]
    [InlineData("Checked", "Document cannot be submitted in current status")]
    public async Task Only_a_draft_or_a_rejected_document_is_submitted(string status, string message)
    {
        var (ops, ctx) = Create("submit_" + status);
        var id = await AddAsync(ctx, status);

        var result = await ops.SubmitAsync(id, null, null, 1, Roles, null, null);

        Assert.Equal((false, message), result);
    }

    [Fact]
    public async Task The_review_steps_refuse_a_document_in_the_wrong_status_with_the_old_messages()
    {
        var (ops, ctx) = Create(nameof(The_review_steps_refuse_a_document_in_the_wrong_status_with_the_old_messages));
        var draft = await AddAsync(ctx, "Draft");
        var submitted = await AddAsync(ctx, "Submitted");

        Assert.Equal("Only submitted documents can be checked", (await ops.CheckAsync(draft, null, 1, Roles, null, null)).Error);
        Assert.Equal("Only checked documents can be approved", (await ops.ApproveAsync(submitted, null, 1, Roles, null, null)).Error);
        Assert.Equal("Only submitted or checked documents can be rejected", (await ops.RejectAsync(draft, null, 1, Roles, null, null)).Error);
        Assert.Equal("Only approved documents can be issued", (await ops.IssueAsync(draft, null, "InternalDCC", null, null, null, null, null, null, 1, Roles, null, null)).Error);
        Assert.Equal("Only submitted or checked documents can be returned for revision", (await ops.ReviseAndResubmitAsync(draft, null, 1, Roles, null, null)).Error);
    }

    [Fact]
    public async Task A_freeze_needs_its_reason_and_an_idc_answer_needs_an_employee_with_a_discipline()
    {
        var (ops, _) = Create(nameof(A_freeze_needs_its_reason_and_an_idc_answer_needs_an_employee_with_a_discipline));

        Assert.False((await ops.FreezeAsync(1, "  ", 1)).Success);
        Assert.False((await ops.UnfreezeAsync(1, null, 1)).Success);
        Assert.False((await ops.IdcRespondAsync(1, "Approve", null, null, 4, null, null)).Success);
        Assert.False((await ops.IdcRespondAsync(1, "Approve", null, 1, null, null, null)).Success);
    }

    // ------------------------------------------------------------ the floors and the guard of the old controller
    private static readonly OperationCatalog Catalog = OperationCatalog.Scan(typeof(PEMS.Application.Common.PemsOperationAttribute).Assembly);

    [Theory]
    [InlineData("Freeze", "EngineeringDocuments.Freeze")]
    [InlineData("Unfreeze", "EngineeringDocuments.Unfreeze")]
    [InlineData("Hold", "EngineeringDocuments.Hold")]
    [InlineData("ReleaseHold", "EngineeringDocuments.ReleaseHold")]
    [InlineData("Approve", "EngineeringDocuments.Approve")]
    public void TheOperationsKeepTheFloorOfTheOldController(string action, string permission)
        => Assert.Equal(permission, Catalog.Get("EngineeringDocument", action)!.Permission);

    [Fact]
    public void TheApproveActionKeepsTheSeparationOfDutiesGuard()
    {
        var shipped = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, SystemDefinitionSeeder.FolderName, "action", "core", "eng-doc-approve.json")))!["payload"]!;
        var guard = (JsonObject)shipped["parameters"]!["guard"]!;

        Assert.Equal("EngineeringDocuments", (string?)guard["resource"]);
        Assert.Equal("Approve", (string?)guard["action"]);
        Assert.Equal("sod", (string?)guard["mode"]);
        Assert.Equal("field:id", (string?)guard["resourceId"]);
    }
}
