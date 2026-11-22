using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common;
using PEMS.Application.Definitions;
using PEMS.Application.Uploads;
using PEMS.Web.Definitions;

namespace PEMS.Tests.Definitions;

public interface IFileTestService
{
    [PemsOperation("FileTest", "One")]
    Task<bool> OneAsync(OperationFile? attachment);

    [PemsOperation("FileTest", "Many")]
    Task<bool> ManyAsync(IReadOnlyList<OperationFile> files);
}

public sealed class FileTestService : IFileTestService
{
    public static string? Last;
    public Task<bool> OneAsync(OperationFile? attachment) { Last = attachment is null ? "none" : attachment.FileName + ":" + attachment.Bytes.Length; return Task.FromResult(true); }
    public Task<bool> ManyAsync(IReadOnlyList<OperationFile> files) { Last = string.Join(",", files.Select(f => f.FileName)); return Task.FromResult(true); }
}

/// <summary>SCRUM-685: an operation may take an optional file (OperationFile?) or every file of a field (IReadOnlyList of OperationFile); the upload profile of the action checks each one.</summary>
public sealed class OperationFileTests
{
    private static readonly OperationCatalog Operations = OperationCatalog.Scan(typeof(OperationFileTests).Assembly);

    private sealed class Access : IDefinitionAccess
    {
        public Task<bool> CanOpenAsync(string? permission, IReadOnlyCollection<string> roles, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class Guard : IUploadGuard
    {
        public Task<UploadCheckResult> CheckAsync(UploadProfile profile, string? fileName, string? declaredContentType, long length, Func<Stream> openRead, CancellationToken cancellationToken = default)
            => Task.FromResult(UploadCheckResult.Accept(UploadPolicy.SafeFileName(fileName)));
    }

    private static UploadedFile File(string field, string name, int length) => new(field, name, null, length, () => new MemoryStream(new byte[length]));

    private static Task<ActionOutcome> Run(string action, string arg, string source, params UploadedFile[] files)
        => new OperationInvokeAction(new ServiceCollection().AddSingleton<IDefinitionAccess>(new Access()).AddSingleton<IUploadGuard>(new Guard()).AddScoped<IFileTestService, FileTestService>()
                .AddScoped(_ => PEMS.Tests.TestSupport.PemsDbContextFactory.CreateInMemory("file-" + Guid.NewGuid())).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), Operations)
            .ExecuteAsync(new ActionInvocation(new Dictionary<string, string?>(), "en", ["Planner"], "hamid",
                new JsonObject { ["model"] = "FileTest", ["action"] = action, ["args"] = new JsonObject { [arg] = source }, ["upload"] = new JsonObject { ["profile"] = "attachment", ["maxFileMb"] = 5 } }, Files: files), CancellationToken.None);

    [Fact]
    public async Task An_optional_file_is_null_when_none_is_chosen_and_empty_parts_do_not_count()
    {
        Assert.True((await Run("One", "attachment", "file:attachment")).Ok);
        Assert.Equal("none", FileTestService.Last);
        Assert.True((await Run("One", "attachment", "file:attachment", File("attachment", "", 0))).Ok);
        Assert.Equal("none", FileTestService.Last);
    }

    [Fact]
    public async Task A_chosen_file_reaches_the_operation_with_its_name_and_bytes()
    {
        Assert.True((await Run("One", "attachment", "file:attachment", File("attachment", "a.pdf", 12))).Ok);
        Assert.Equal("a.pdf:12", FileTestService.Last);
    }

    [Fact]
    public async Task Every_file_of_the_field_is_passed_to_a_list_parameter()
    {
        Assert.True((await Run("Many", "files", "file:files", File("files", "a.pdf", 3), File("files", "b.pdf", 4), File("other", "c.pdf", 5))).Ok);
        Assert.Equal("a.pdf,b.pdf", FileTestService.Last);
    }

    [Fact]
    public async Task A_file_over_the_limit_is_refused_and_the_operation_is_not_called()
    {
        FileTestService.Last = null;
        var outcome = await Run("One", "attachment", "file:attachment", File("attachment", "big.pdf", 6 * 1024 * 1024));
        Assert.False(outcome.Ok);
        Assert.Null(FileTestService.Last);
    }
}
