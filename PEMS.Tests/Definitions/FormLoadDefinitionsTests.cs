using System.Text.Json.Nodes;
using PEMS.Application.DTOs;

namespace PEMS.Tests.Definitions;

/// <summary>SCRUM-818: edit forms preload the saved values through the form-level load (a read operation); the fields of the form are the properties the operation returns.</summary>
public class FormLoadDefinitionsTests
{
    private static JsonObject Form(string key)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "PEMS.Persistence", "Definitions"))) dir = dir.Parent;
        var path = Path.Combine(dir!.FullName, "PEMS.Persistence", "Definitions", "System", "form", "core", key + ".json");
        return JsonNode.Parse(File.ReadAllText(path))!["payload"]!.AsObject();
    }

    [Theory]
    [InlineData("job-plan-edit", "JobPlan", "One")]
    [InlineData("meeting-update-minutes", "Meeting", "Head")]
    [InlineData("organization-import-confirm", "OrganizationImport", "Pending")]
    [InlineData("letter-template-edit", "LetterTemplatePage", "One")]
    public void An_edit_form_names_a_read_operation_that_exists_and_returns_its_fields(string form, string model, string action)
    {
        var load = Form(form)["load"]!.AsObject();
        Assert.Equal(model, (string)load["model"]!);
        Assert.Equal(action, (string)load["action"]!);
        var operations = PEMS.Web.Definitions.OperationCatalog.Scan(typeof(PEMS.Application.Common.PemsOperationAttribute).Assembly);
        var op = operations.Get(model, action);
        Assert.NotNull(op);
        // the returned element type carries a property for each field of the form that is preloaded (the first letter in lower case)
        var rowType = op!.Method.ReturnType.GetGenericArguments()[0].GetGenericArguments()[0];
        var props = rowType.GetProperties().Select(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..]).ToHashSet();
        var fields = Form(form)["fields"]!.AsArray().Select(f => (string)f!["name"]!).ToList();
        Assert.Contains(fields, f => props.Contains(f));
    }

    [Fact]
    public void The_job_plan_form_values_are_a_row_with_the_form_field_names()
    {
        var names = typeof(JobPlanFormRowDto).GetProperties().Select(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..]).ToHashSet();
        foreach (var f in Form("job-plan-edit")["fields"]!.AsArray().Select(f => (string)f!["name"]!)) Assert.Contains(f, names);
    }
}
