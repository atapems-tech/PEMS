using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Entities;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-539: an empty Projects create form was saved (no code, no name), and Vendor create showed no validation box for
/// a server-side error. The project code and name are required on the server and in the browser; Vendor create has the
/// validation summary like the other create forms.
/// </summary>
public class ProjectAndVendorCreateValidationTests
{
    private static string View(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine([dir!.FullName, "PEMS.Web", "Views", .. parts]));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "Name")]
    [InlineData("PRJ-1", "")]
    public void AProjectWithoutCodeOrName_IsNotValid(string code, string name)
    {
        var p = new Project { ProjectCode = code, ProjectName = name };
        Assert.False(Validator.TryValidateObject(p, new ValidationContext(p), new List<ValidationResult>(), validateAllProperties: true));
    }

    [Fact]
    public void AProjectWithCodeAndName_IsValid()
    {
        var p = new Project { ProjectCode = "PRJ-1", ProjectName = "Name" };
        Assert.True(Validator.TryValidateObject(p, new ValidationContext(p), new List<ValidationResult>(), validateAllProperties: true));
    }

    [Theory]
    [InlineData("Create.cshtml")]
    [InlineData("Edit.cshtml")]
    public void TheProjectForms_RequireCodeAndNameInTheBrowser(string file)
    {
        var view = View("Projects", file);
        Assert.Contains("<pems-input asp-for=\"ProjectCode\" required />", view);
        Assert.Contains("<pems-input asp-for=\"ProjectName\" required />", view);
    }

    [Fact]
    public void VendorCreate_ShowsServerSideErrors()
        => Assert.Contains("<pems-validation-summary", View("Vendor", "Create.cshtml"));
}
