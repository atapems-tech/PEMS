using System.Reflection;
using PEMS.Web.Areas.Warehouse.Controllers;

namespace PEMS.Tests.Inventory;

/// <summary>
/// SCRUM-545: the Material Master edit modal did not post category, discipline, unit, vendor and specification, but
/// <c>MaterialItemService.UpdateAsync</c> saves every parameter of the Edit action, so each save cleared those five fields.
/// Every parameter of <see cref="MaterialItemController.Edit"/> must have a field in the edit modal.
/// </summary>
public class MaterialItemEditKeepsFieldsTests
{
    private static string EditModal()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var view = File.ReadAllText(Path.Combine(dir!.FullName, "PEMS.Web", "Areas", "Warehouse", "Views", "MaterialItem", "Details.cshtml"));
        var start = view.IndexOf("id=\"editMaterialModal\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "edit modal not found");
        var end = view.IndexOf("</pems-modal>", start, StringComparison.Ordinal);
        return view[start..end];
    }

    [Fact]
    public void EveryParameterOfEdit_HasAFieldInTheEditModal()
    {
        var edit = typeof(MaterialItemController).GetMethod(nameof(MaterialItemController.Edit), BindingFlags.Public | BindingFlags.Instance)!;
        var modal = EditModal();
        var missing = edit.GetParameters().Select(p => p.Name!).Where(n => !modal.Contains($"name=\"{n}\"", StringComparison.Ordinal)).ToList();
        Assert.Empty(missing);
    }

    [Theory]
    [InlineData("categoryId", "Model.CategoryId")]
    [InlineData("disciplineId", "Model.DisciplineId")]
    [InlineData("uomId", "Model.UomId")]
    [InlineData("vendorId", "Model.VendorId")]
    [InlineData("specification", "Model.Specification")]
    public void TheFiveFields_ArePrefilledFromTheMaterial(string field, string modelValue)
    {
        var modal = EditModal();
        var at = modal.IndexOf($"name=\"{field}\"", StringComparison.Ordinal);
        var next = modal.IndexOf("</pems-col>", at, StringComparison.Ordinal);
        Assert.Contains(modelValue, modal[at..next]);
    }
}
