using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Web.Controllers;
using PEMS.Web.Helpers;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-529: the CONTRACTOR and SUB-CONTRACTOR number forms moved from VDR details to Admin > Document numbering, keeping their actions.
/// SCRUM-530: VDR details has a Files tab with every submission file and an inline preview.
/// </summary>
public class VdrNumberingAndFilesTests
{
    // ---- SCRUM-529
    [Fact]
    public void VdrDetails_NoLongerHoldsTheNumberForms_ButShowsTheVendorNumberAndTheAdminLink()
    {
        var view = Read("PEMS.Web", "Views", "VDR", "Details.cshtml");
        Assert.DoesNotContain("GenerateContractorNumber", view);
        Assert.DoesNotContain("GenerateVendorNumber", view);
        Assert.Contains("Vd_VendorFormatNo", view);
        Assert.Contains("asp-area=\"Admin\" asp-controller=\"Numbering\" asp-action=\"Index\" asp-route-vdrItemId=\"@Model.VDRItemId\"", view);
    }



    // ---- SCRUM-530
    private static VDRSubmissionDto Submission(int rev, int? main, string? mainPath, int? comment = null, string? commentName = null, int? reply = null, string? replyName = null) => new()
    {
        RevisionNo = rev, AttachmentId = main, MainFilePath = mainPath,
        CommentSheetAttachmentId = comment, CommentSheetFileName = commentName, ReplySheetAttachmentId = reply, ReplySheetFileName = replyName
    };

    [Fact]
    public void Files_ListEverySubmissionFile_NewestFirst_AndMarkWhatCanBePreviewed()
    {
        var files = VdrFiles.From([
            Submission(0, 10, "store/a/drawing-r0.pdf"),
            Submission(1, 11, "store/a/drawing-r1.pdf", 12, "comments.xlsx", 13, "reply.pdf")
        ]);
        Assert.Equal([11, 12, 13, 10], files.Select(f => f.AttachmentId));
        Assert.Equal(["Main", "CommentSheet", "ReplySheet", "Main"], files.Select(f => f.Kind));
        Assert.Equal("drawing-r1.pdf", files[0].FileName);
        Assert.False(files[1].CanPreview);   // xlsx is downloaded, never shown inline (SCRUM-202)
        Assert.True(files[2].CanPreview);
    }

    [Fact]
    public void Files_SelectTheRequestedPreviewableFile_ElseTheNewest()
    {
        var files = VdrFiles.From([Submission(1, 11, "a.pdf", 12, "c.xlsx"), Submission(0, 10, "b.pdf")]);
        Assert.Equal(10, VdrFiles.Selected(files, 10)!.AttachmentId);
        Assert.Equal(11, VdrFiles.Selected(files, 12)!.AttachmentId);    // not previewable: the newest previewable instead
        Assert.Equal(11, VdrFiles.Selected(files, 999)!.AttachmentId);   // not a file of this item
        Assert.Null(VdrFiles.Selected(VdrFiles.From([Submission(0, 1, "x.dwg")]), null));
        Assert.Empty(VdrFiles.From([Submission(0, null, null)]));
    }

    [Fact]
    public void VdrDetails_HasTheFilesTab_WithThePreviewComponent()
    {
        var view = Read("PEMS.Web", "Views", "VDR", "Details.cshtml");
        Assert.Contains("<pems-tab target=\"tab-files\"", view);
        Assert.Contains("id=\"tab-files\" role=\"tabpanel\"", view);
        Assert.Contains("<pems-file-preview id=\"vdrFilePreview\" src=\"@vdrPreviewUrl\"", view);
        Assert.Contains("Url.Action(\"Preview\", \"Files\"", view);
        Assert.Contains("asp-controller=\"Files\" asp-action=\"Download\"", view);
        Assert.DoesNotContain("<iframe", view);
    }

    // ---- SCRUM-531: upload in the Files tab
    [Theory]
    [InlineData("Main", "documents")]
    [InlineData("CommentSheet", "comment-sheets")]
    [InlineData("ReplySheet", "reply-sheets")]
    [InlineData("Other", null)]
    [InlineData(null, null)]
    public void UploadKinds_HaveTheirFolder(string? kind, string? folder) => Assert.Equal(folder, VdrFiles.FolderOf(kind));

    [Fact]
    public void UploadAction_KeepsTheUploadRules_AndOnlyTouchesASubmissionOfTheItem()
    {
        // SCRUM-761: the controller action is the definition vdr-submission-file over VdrFiles.UploadSubmissionFile (VDR.Edit floor, the upload profile of the action)
        var definition = Read("PEMS.Persistence", "Definitions", "System", "action", "core", "vdr-submission-file.json");
        Assert.Contains("\"profile\": \"attachment\"", definition);
        var source = Read("PEMS.Web", "Services", "DocumentFileOperations.cs");
        Assert.Contains("x.VDRSubmissionId == submissionId && x.VDRItemId == vdrItemId", source);
        Assert.Contains("VdrFiles.FolderOf(kind)", source);
        Assert.Equal("VDR.Edit", PEMS.Web.Definitions.OperationCatalog.Scan(typeof(PEMS.Application.Common.PemsOperationAttribute).Assembly).Get("VdrFiles", "UploadSubmissionFile")!.Permission);
    }

    [Fact]
    public void FilesTab_HasTheUploadForm()
    {
        var view = Read("PEMS.Web", "Views", "VDR", "Details.cshtml");
        var pane = view[view.IndexOf("id=\"tab-files\"", StringComparison.Ordinal)..];
        Assert.Contains("<pems-form action=\"/a/vdr-submission-file\" method=\"post\" enctype=\"multipart/form-data\"", pane);
        foreach (var field in new[] { "name=\"vdrItemId\"", "name=\"submissionId\"", "name=\"kind\"", "<pems-uploader id=\"vdrUploadFile\" name=\"file\" required" })
            Assert.Contains(field, pane);
        Assert.Contains("@foreach (var k in VdrFiles.Kinds)", pane);
    }

    private static int CountOf(string text, string part)
    {
        int n = 0, i = 0;
        while ((i = text.IndexOf(part, i, StringComparison.Ordinal)) >= 0) { n++; i += part.Length; }
        return n;
    }

    private static string Read(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.Web", "PEMS.Web.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine([dir!.FullName, .. parts]));
    }
}
