using System.Text.RegularExpressions;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-543: date-and-time fields used the native <c>type="datetime-local"</c> input, so a Persian user got a Gregorian picker.
/// They now use <c>&lt;pems-date time="true"&gt;</c> (Jalali date and time on a Persian page, ISO <c>yyyy-MM-ddTHH:mm</c> posted).
/// No view may write a <c>datetime-local</c> input by hand again.
/// </summary>
public class NoNativeDateTimeInputTests
{
    private static string Web()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "PEMS.Web");
    }

    [Fact]
    public void NoView_WritesADatetimeLocalInput()
    {
        var offenders = Directory.EnumerateFiles(Web(), "*.cshtml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.Combine("bin", ""), StringComparison.OrdinalIgnoreCase) && !f.Contains(Path.Combine("obj", ""), StringComparison.OrdinalIgnoreCase))
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"type=""datetime-local"""))
            .Select(Path.GetFileName).ToList();
        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("Letter", "Index.cshtml", "id=\"ctReminderAt\"")]
    [InlineData("Meeting", "Create.cshtml", "name=\"meetingDate\"")]
    [InlineData("Meeting", "Index.cshtml", "name=\"meetingDate\"")]
    [InlineData("Permit", "Details.cshtml", "name=\"testedAt\"")]
    [InlineData("Reliability", "ConditionMonitoring.cshtml", "name=\"readingDate\"")]
    [InlineData("ShiftHandover", "Create.cshtml", "name=\"handoverDate\"")]
    [InlineData("WorkOrder", "Details.cshtml", "name=\"plannedStart\"")]
    [InlineData("WorkOrder", "Details.cshtml", "name=\"plannedFinish\"")]
    public void TheEightFields_UseTheDateTimeMode(string folder, string file, string field)
        => Assert.Matches("<pems-date time=\"true\"[^>]*" + Regex.Escape(field), File.ReadAllText(Path.Combine(Web(), "Views", folder, file)));

    [Fact]
    public void TheLetterReminderScript_ReadsAndWritesThroughPemsDate()
    {
        var js = File.ReadAllText(Path.Combine(Web(), "wwwroot", "js", "pems-cartable.js"));
        Assert.Contains("window.PemsDate.setValue(remAt, iso)", js);
        Assert.Contains("window.PemsDate.value(remAt)", js);
    }
}
