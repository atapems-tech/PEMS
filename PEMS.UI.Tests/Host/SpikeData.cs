using PEMS.UI.Abstractions;

namespace PEMS.UI.Tests.Host;

public static class SpikeData
{
    public static ViewDescriptor Table { get; } = new("spike-table", "Instruments",
    [
        new ColumnDescriptor("tag", "Tag"),
        new ColumnDescriptor("name", "Name"),
        new ColumnDescriptor("area", "Area"),
        new ColumnDescriptor("status", "Status", ColumnAlignment.Center),
        new ColumnDescriptor("range", "Range", ColumnAlignment.End),
        new ColumnDescriptor("owner", "Owner")
    ]);

    public static IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows(int count) =>
        Enumerable.Range(1, count).Select(i => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
        {
            ["tag"] = $"PT-{i:0000}",
            ["name"] = $"Pressure transmitter {i}",
            ["area"] = $"Unit {i % 12}",
            ["status"] = i % 7 == 0 ? "Overdue" : "Active",
            ["range"] = $"0-{i % 50 + 10} bar",
            ["owner"] = $"Team {i % 9}"
        }).ToList();

    public static FormDefinition Form { get; } = new("spike-form", "Register instrument", "/b/descriptor-form",
    [
        new FieldDescriptor("Name", "Name", Required: true, MaxLength: 50, HelpText: "Unique tag name"),
        new FieldDescriptor("Quantity", "Quantity", FieldKind.Number, Required: true),
        new FieldDescriptor("Due", "Due date", FieldKind.Date),
        new FieldDescriptor("Active", "Active", FieldKind.Checkbox),
        new FieldDescriptor("Attachment", "Datasheet", FieldKind.File)
    ]);
}
