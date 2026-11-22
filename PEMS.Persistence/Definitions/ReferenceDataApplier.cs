using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Definitions;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Definitions;

// SCRUM-803: the applier of shipped reference data. It writes the rows of a reference-data definition into the lookup table of its list, by the rule of ReferenceDataSync.Plan (SCRUM-516): the product's rows are
// inserted and, while nobody edited them, updated; a row the customer owns or edited is never written (a difference is reported); a row the product stopped shipping is retired (inactive), never deleted.
// What the product wrote is remembered per row in ReferenceDataRowStates (stable key and the fingerprint of the values), so "edited" is a fact and not a guess. A lookup row that already exists with exactly the
// shipped values (an install made by the old C# seeder) is adopted: from then on it is the product's row.

/// <summary>Where a list lives: the lookup entity, the property that holds the stable row key, the active flag (retire) and what a new row gets besides its values.</summary>
public sealed record ReferenceListBinding<T>(string ListKey, string KeyProperty, string? ActiveProperty = null, Action<T>? OnCreate = null) where T : class, new();

public sealed record ReferenceDataApplyResult(int Inserted, int Updated, int Adopted, int Retired, IReadOnlyList<string> UpstreamChanged)
{
    public int Unchanged { get; init; }
}

public sealed class ReferenceDataApplier(PEMSDbContext db)
{
    public async Task<ReferenceDataApplyResult> ApplyAsync<T>(string payloadJson, ReferenceListBinding<T> binding, CancellationToken ct = default) where T : class, new()
    {
        var errors = new ReferenceDataKind().Validate(payloadJson, new DefinitionLimits());
        if (errors.Count > 0) throw new InvalidOperationException($"The reference data list '{binding.ListKey}' is not valid: " + string.Join("; ", errors.Select(e => $"{e.Pointer} {e.Message}")));

        var shippedRaw = ReferenceDataKind.Rows(payloadJson);
        var names = shippedRaw.SelectMany(r => r.Values.Keys).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
        var props = names.ToDictionary(n => n, n => typeof(T).GetProperty(n, Flags) ?? throw new InvalidOperationException($"'{n}' is not a property of {typeof(T).Name} (list '{binding.ListKey}')."), StringComparer.Ordinal);
        var keyProp = typeof(T).GetProperty(binding.KeyProperty, Flags) ?? throw new InvalidOperationException($"{binding.KeyProperty} is not a property of {typeof(T).Name}.");
        var activeProp = binding.ActiveProperty is null ? null : typeof(T).GetProperty(binding.ActiveProperty, Flags) ?? throw new InvalidOperationException($"{binding.ActiveProperty} is not a property of {typeof(T).Name}.");

        // the shipped values in the form the table stores them, so a comparison never fails on "1" against "1.0"
        var shipped = shippedRaw.Select(r => new ShippedRow(r.Key, r.Owner, names.ToDictionary(n => n, n => Canonical(r.Values.GetValueOrDefault(n), props[n].PropertyType), StringComparer.Ordinal))).ToList();
        var shippedByKey = shipped.ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);

        var entities = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var e in await db.Set<T>().ToListAsync(ct))
        {
            var key = keyProp.GetValue(e) as string;
            if (key is null) continue;
            entities.TryAdd(shippedByKey.TryGetValue(key, out var s) ? s.Key : key, e);
        }
        var states = (await db.ReferenceDataRowStates.Where(s => s.ListKey == binding.ListKey).ToListAsync(ct)).ToDictionary(s => s.RowKey, StringComparer.Ordinal);

        var stored = entities.Select(kv =>
        {
            states.TryGetValue(kv.Key, out var state);
            var values = names.ToDictionary(n => n, n => Format(props[n].GetValue(kv.Value)), StringComparer.Ordinal);
            var active = activeProp is null || activeProp.GetValue(kv.Value) is not bool b || b;
            return new StoredRow(kv.Key, state?.Owner ?? DataOwners.Application, values, state?.ShippedHash, active, state is not null);
        }).ToList();

        int inserted = 0, updated = 0, adopted = 0, retired = 0, unchanged = 0;
        var upstream = new List<string>();
        var byKey = shipped.ToDictionary(r => r.Key, StringComparer.Ordinal);
        foreach (var step in ReferenceDataSync.Plan(shipped, stored))
        {
            switch (step.Action)
            {
                case RowAction.Insert:
                {
                    var row = byKey[step.Key];
                    var entity = new T();
                    keyProp.SetValue(entity, step.Key);
                    Write(entity, row, props);
                    binding.OnCreate?.Invoke(entity);
                    db.Set<T>().Add(entity);
                    db.ReferenceDataRowStates.Add(State(binding.ListKey, row));
                    inserted++;
                    break;
                }
                case RowAction.Update:
                {
                    var row = byKey[step.Key];
                    Write(entities[step.Key], row, props);
                    var state = states[step.Key];
                    state.ShippedHash = ReferenceDataSync.Fingerprint(row.Values);
                    state.Owner = row.Owner;
                    state.UpdatedAtUtc = DateTime.UtcNow;
                    updated++;
                    break;
                }
                case RowAction.Unchanged:
                    if (!states.ContainsKey(step.Key)) { db.ReferenceDataRowStates.Add(State(binding.ListKey, byKey[step.Key])); adopted++; }
                    else unchanged++;
                    break;
                case RowAction.UpstreamChanged:
                    upstream.Add($"{step.Key}: {string.Join(", ", step.ChangedFields)}");
                    break;
                case RowAction.Retire:
                    activeProp?.SetValue(entities[step.Key], false);
                    retired++;
                    break;
            }
        }

        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
        return new ReferenceDataApplyResult(inserted, updated, adopted, retired, upstream) { Unchanged = unchanged };
    }

    private static ReferenceDataRowState State(string list, ShippedRow row)
        => new() { ListKey = list, RowKey = row.Key, Owner = row.Owner, ShippedHash = ReferenceDataSync.Fingerprint(row.Values), UpdatedAtUtc = DateTime.UtcNow };

    private static void Write<T>(T entity, ShippedRow row, Dictionary<string, PropertyInfo> props) where T : class
    {
        foreach (var (name, prop) in props) prop.SetValue(entity, Parse(row.Values.GetValueOrDefault(name), prop.PropertyType));
    }

    /// <summary>Text as the property's type; missing is null (or the default of a value type).</summary>
    internal static object? Parse(string? text, Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (text is null) return underlying.IsValueType && Nullable.GetUnderlyingType(type) is null ? Activator.CreateInstance(type) : null;
        if (underlying == typeof(string)) return text;
        return Convert.ChangeType(text, underlying, CultureInfo.InvariantCulture);
    }

    /// <summary>A value as the text the comparison uses: booleans in lower case, numbers and dates in the invariant culture.</summary>
    internal static string? Format(object? value) => value switch
    {
        null => null,
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    private static string? Canonical(string? text, Type type) => Format(Parse(text, type));
}

/// <summary>SCRUM-803: the lists that ship as reference data: the definition file of each list and where its rows live.</summary>
public static class ReferenceDataCatalog
{
    public const string Folder = "reference-data";
    private static readonly string[] Lists = ["material-uom", "maintenance-type", "qc-discipline", "failure-mechanism"];

    public static IReadOnlyList<string> ListKeys => Lists;

    /// <summary>The payload of a shipped list definition (definitions-system/reference-data/core/{list}.json).</summary>
    public static string LoadPayload(string list, string? folder = null)
    {
        var path = Path.Combine(folder ?? Path.Combine(AppContext.BaseDirectory, "definitions-system"), Folder, "core", list + ".json");
        if (!File.Exists(path)) throw new FileNotFoundException($"The reference data definition '{list}' is not shipped.", path);
        return (JsonNode.Parse(File.ReadAllText(path)) as JsonObject)?["payload"]?.ToJsonString() ?? throw new InvalidOperationException($"{path} has no payload.");
    }

    /// <summary>What a converted seeder runs at startup: apply the list to its table.</summary>
    public static Task<ReferenceDataApplyResult> ApplyAsync(PEMSDbContext db, string list, string? folder = null, CancellationToken ct = default)
    {
        var applier = new ReferenceDataApplier(db);
        var payload = LoadPayload(list, folder);
        return list switch
        {
            "material-uom" => applier.ApplyAsync(payload, new ReferenceListBinding<MaterialUOM>(list, nameof(MaterialUOM.Code), nameof(MaterialUOM.IsActive), e => e.CreatedDate = DateTime.Now), ct),
            "maintenance-type" => applier.ApplyAsync(payload, new ReferenceListBinding<MaintenanceType>(list, nameof(MaintenanceType.Code), nameof(MaintenanceType.IsActive), e => { e.IsSystemDefault = true; e.CreatedDate = DateTime.Now; }), ct),
            "qc-discipline" => applier.ApplyAsync(payload, new ReferenceListBinding<QCDiscipline>(list, nameof(QCDiscipline.Code), nameof(QCDiscipline.IsActive), e => e.CreatedDate = DateTime.Now), ct),
            "failure-mechanism" => applier.ApplyAsync(payload, new ReferenceListBinding<FailureMechanism>(list, nameof(FailureMechanism.Code), nameof(FailureMechanism.IsActive), e => { e.IsSystemDefault = true; e.CreatedDate = DateTime.Now; }), ct),
            _ => throw new ArgumentException($"'{list}' is not a shipped reference data list.", nameof(list)),
        };
    }
}
