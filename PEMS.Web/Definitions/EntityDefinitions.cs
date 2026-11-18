using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Definitions;
using PEMS.Application.Grids;
using PEMS.Persistence.Context;
using PEMS.UI.Abstractions;

namespace PEMS.Web.Definitions;

// SCRUM-437 / SCRUM-342 (the waves): entity-backed definitions. A list, an option list and a create action for a plain master-data entity are JSON only: the entity is registered here once, with the
// permissions that guard it and the properties a definition may read and write; the generic provider and handlers below do the rest. A definition can only reach what the catalog registers, and the
// catalog's permissions are checked by the code, not by the definition (a definition cannot widen access).

/// <summary>A registered entity: its CLR type, the permission to read and to create ("Module.Action", null = any signed-in user), and the properties a definition may use.</summary>
public sealed record EntityRegistration(string Name, Type ClrType, string? ReadPermission, string? CreatePermission, IReadOnlySet<string> Readable, IReadOnlySet<string> Writable);

public sealed class EntityCatalog
{
    private readonly Dictionary<string, EntityRegistration> _entities = new(StringComparer.Ordinal);

    /// <param name="readable">The property names a grid or an option list may expose.</param>
    /// <param name="writable">The property names a create action may set.</param>
    public EntityCatalog Add<T>(string? readPermission, string? createPermission, string[] readable, string[] writable) where T : class
    {
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var name in readable.Concat(writable))
            if (!props.Contains(name)) throw new InvalidOperationException($"{typeof(T).Name} has no property '{name}'.");
        _entities[typeof(T).Name] = new EntityRegistration(typeof(T).Name, typeof(T), readPermission, createPermission, readable.ToHashSet(StringComparer.Ordinal), writable.ToHashSet(StringComparer.Ordinal));
        return this;
    }

    public EntityRegistration? Get(string? name) => name is not null && _entities.TryGetValue(name, out var e) ? e : null;
    public IReadOnlyCollection<string> Names => _entities.Keys;

    /// <summary>The entities of the product that definitions may use. Add one line per master-data entity (and the permissions that guard it); the definition files do the rest.</summary>
    public static EntityCatalog Default() => new EntityCatalog()
        .Add<PEMS.Domain.Entities.MaterialUOM>(null, "InventoryMasterData.Create", ["MaterialUOMId", "Code", "Name", "Description", "ConversionFactor", "IsActive", "CreatedDate"], ["Code", "Name", "Description", "ConversionFactor", "IsActive", "CreatedDate"])
        .Add<PEMS.Domain.Entities.MaterialCategory>(null, "InventoryMasterData.Create", ["MaterialCategoryId", "Code", "Name", "Description", "ParentCategoryId", "IsActive", "CreatedDate"], ["Code", "Name", "Description", "ParentCategoryId", "IsActive", "CreatedDate"])
        .Add<PEMS.Domain.Entities.Warehouse>(null, "InventoryMasterData.Create", ["WarehouseId", "Code", "Name", "Description", "IsActive", "CreatedDate"], ["Code", "Name", "Description", "IsActive", "CreatedDate"]);

    internal static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}

internal static class EntityJson
{
    public static string? Str(JsonNode? n) => n is JsonValue v && v.GetValueKind() == JsonValueKind.String ? (string?)v : null;

    public static IReadOnlyList<string> Names(JsonNode? n) => n is JsonArray a ? a.Select(x => Str(x) ?? "").ToList() : [];

    public static bool Supported(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        return t == typeof(string) || t == typeof(int) || t == typeof(long) || t == typeof(decimal) || t == typeof(double) || t == typeof(bool) || t == typeof(DateTime);
    }
}

/// <summary>Grid rows of a registered entity: <c>{ "kind": "grid", "provider": "entity", "parameters": { "entity": "MaterialUOM", "fields": ["Code", "Name"], "search": ["Code", "Name"] } }</c>.</summary>
public sealed class EntityGridProvider(IServiceScopeFactory scopes, EntityCatalog catalog) : IGridDataProvider
{
    public string Key => "entity";
    public IReadOnlyCollection<string>? AllowedRoles => null;

    public IReadOnlyList<DefinitionFieldError> ValidateParameters(JsonObject? parameters)
    {
        var errors = new List<DefinitionFieldError>();
        var entity = catalog.Get(EntityJson.Str(parameters?["entity"]));
        if (entity is null) return [new("/entity", "unknown-entity", "Def_Err_UnknownEntity", $"parameters.entity must be one of: {string.Join(", ", catalog.Names)}.")];
        var fields = EntityJson.Names(parameters!["fields"]);
        if (fields.Count == 0) errors.Add(new("/fields", "required", "Def_Err_Required", "A grid needs the list of fields it exposes."));
        foreach (var f in fields.Concat(EntityJson.Names(parameters["search"])).Distinct())
            if (!entity.Readable.Contains(f)) errors.Add(new("/fields", "not-readable", "Def_Err_NotReadable", $"'{f}' is not a readable property of {entity.Name}."));
        foreach (var s in EntityJson.Names(parameters["search"]))
            if (entity.ClrType.GetProperty(s)?.PropertyType != typeof(string)) errors.Add(new("/search", "not-text", "Def_Err_Type", $"'{s}' is not a text property; only text properties are searched."));
        return errors;
    }

    public async Task<(IReadOnlyList<object> Items, int Total)> QueryAsync(GridDataRequest request, CancellationToken ct)
    {
        var entity = catalog.Get(request.Parameters.GetValueOrDefault("entity")) ?? throw new GridQueryException("unknown entity");
        using var scope = scopes.CreateScope();
        if (!await scope.ServiceProvider.GetRequiredService<IDefinitionAccess>().CanOpenAsync(entity.ReadPermission, request.Roles, ct)) throw new UnauthorizedAccessException();
        var db = scope.ServiceProvider.GetRequiredService<PEMSDbContext>();
        var fields = (JsonNode.Parse(request.Parameters.GetValueOrDefault("fields") ?? "[]") as JsonArray ?? []).Select(x => EntityJson.Str(x) ?? "").ToArray();
        var search = (JsonNode.Parse(request.Parameters.GetValueOrDefault("search") ?? "[]") as JsonArray ?? []).Select(x => EntityJson.Str(x) ?? "").ToHashSet(StringComparer.Ordinal);
        var method = typeof(EntityGridProvider).GetMethod(nameof(Query), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(entity.ClrType);
        return await (Task<(IReadOnlyList<object>, int)>)method.Invoke(null, [db, request.Grid, fields, search, entity, ct])!;
    }

    private static async Task<(IReadOnlyList<object> Items, int Total)> Query<T>(PEMSDbContext db, GridRequest request, string[] fields, HashSet<string> search, EntityRegistration entity, CancellationToken ct) where T : class
    {
        var gridFields = new GridFields<T>();
        var add = typeof(GridFields<T>).GetMethod("Add")!;
        foreach (var name in fields)
        {
            var pi = typeof(T).GetProperty(name)!;
            var p = Expression.Parameter(typeof(T), "x");
            var lambda = Expression.Lambda(Expression.Property(p, pi), p);
            add.MakeGenericMethod(pi.PropertyType).Invoke(gridFields, [EntityCatalog.Camel(name), lambda, search.Contains(name)]);
        }
        var query = db.Set<T>().AsNoTracking();
        var total = await query.ApplyGridFilters(request, gridFields).CountAsync(ct);
        var rows = await query.ApplyGridFilters(request, gridFields).ApplyGridSort(request, gridFields).Paged(request).ToListAsync(ct);
        var props = fields.Select(f => (Name: EntityCatalog.Camel(f), Prop: typeof(T).GetProperty(f)!)).ToList();
        var items = rows.Select(r =>
        {
            var d = new Dictionary<string, object?>();
            foreach (var (name, prop) in props) d[name] = prop.GetValue(r) is DateTime dt ? dt.ToString("s", CultureInfo.InvariantCulture) : prop.GetValue(r);
            return (object)d;
        }).ToList();
        return (items, total);
    }
}

/// <summary>The options of a field from a registered entity: <c>{ "kind": "provider", "provider": "entity-options", "parameters": { "entity": "MaterialCategory", "value": "MaterialCategoryId", "label": "Name", "active": "IsActive" } }</c>.</summary>
public sealed class EntityOptionsProvider(IServiceScopeFactory scopes, EntityCatalog catalog) : IDataSourceProvider
{
    public string Key => "entity-options";
    public IReadOnlyCollection<string>? AllowedRoles => null;

    public IReadOnlyList<DefinitionFieldError> ValidateParameters(JsonObject? parameters)
    {
        var entity = catalog.Get(EntityJson.Str(parameters?["entity"]));
        if (entity is null) return [new("/entity", "unknown-entity", "Def_Err_UnknownEntity", $"parameters.entity must be one of: {string.Join(", ", catalog.Names)}.")];
        var errors = new List<DefinitionFieldError>();
        foreach (var k in new[] { "value", "label" })
            if (!entity.Readable.Contains(EntityJson.Str(parameters![k]) ?? "")) errors.Add(new("/" + k, "not-readable", "Def_Err_NotReadable", $"parameters.{k} must be a readable property of {entity.Name}."));
        if (parameters!["active"] is not null && entity.ClrType.GetProperty(EntityJson.Str(parameters["active"]) ?? "")?.PropertyType != typeof(bool)) errors.Add(new("/active", "not-bool", "Def_Err_Type", "parameters.active must be a bool property."));
        return errors;
    }

    public async Task<IReadOnlyList<FieldOption>> LoadAsync(DataSourceRequest request, CancellationToken ct)
    {
        var entity = catalog.Get(request.Parameters.GetValueOrDefault("entity")) ?? throw new InvalidOperationException("unknown entity");
        using var scope = scopes.CreateScope();
        if (!await scope.ServiceProvider.GetRequiredService<IDefinitionAccess>().CanOpenAsync(entity.ReadPermission, request.Roles, ct)) return [];
        var db = scope.ServiceProvider.GetRequiredService<PEMSDbContext>();
        var method = typeof(EntityOptionsProvider).GetMethod(nameof(Load), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(entity.ClrType);
        return await (Task<IReadOnlyList<FieldOption>>)method.Invoke(null, [db, request.Parameters.GetValueOrDefault("value")!, request.Parameters.GetValueOrDefault("label")!, request.Parameters.GetValueOrDefault("active"), ct])!;
    }

    private static async Task<IReadOnlyList<FieldOption>> Load<T>(PEMSDbContext db, string value, string label, string? active, CancellationToken ct) where T : class
    {
        IQueryable<T> query = db.Set<T>().AsNoTracking();
        if (!string.IsNullOrEmpty(active))
        {
            var p = Expression.Parameter(typeof(T), "x");
            query = query.Where(Expression.Lambda<Func<T, bool>>(Expression.Property(p, active), p));
        }
        var vp = typeof(T).GetProperty(value)!; var lp = typeof(T).GetProperty(label)!;
        var rows = await query.Take(2000).ToListAsync(ct);
        return rows.Select(r => new FieldOption { Value = Convert.ToString(vp.GetValue(r), CultureInfo.InvariantCulture) ?? "", Label = TranslatableText.Literal(Convert.ToString(lp.GetValue(r), CultureInfo.InvariantCulture)) })
            .OrderBy(o => o.Label.ToString(), StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}

/// <summary>
/// <c>entity.create</c>: creates a row of a registered entity from the form values. Parameters: <c>entity</c>, <c>map</c> (form field to property), <c>required</c> (properties that must not be empty),
/// <c>defaults</c> (property to a literal, <c>"$now"</c> or <c>"$utcnow"</c>), <c>unique</c> (a list of <c>{ fields, message }</c>: no row may have the same values, text compared without case).
/// </summary>
public sealed class EntityCreateAction(IServiceScopeFactory scopes, EntityCatalog catalog) : IActionHandler
{
    public string Key => "entity.create";
    public IReadOnlyCollection<string>? AllowedRoles => null;

    public IReadOnlyList<DefinitionFieldError> ValidateParameters(JsonObject? parameters)
    {
        var entity = catalog.Get(EntityJson.Str(parameters?["entity"]));
        if (entity is null) return [new("/entity", "unknown-entity", "Def_Err_UnknownEntity", $"parameters.entity must be one of: {string.Join(", ", catalog.Names)}.")];
        var errors = new List<DefinitionFieldError>();
        if (parameters!["map"] is not JsonObject map || map.Count == 0) errors.Add(new("/map", "required", "Def_Err_Required", "map is required: form field to property."));
        else foreach (var (field, prop) in map)
        {
            var name = EntityJson.Str(prop) ?? "";
            var pi = entity.ClrType.GetProperty(name);
            if (!entity.Writable.Contains(name) || pi is null || !EntityJson.Supported(pi.PropertyType)) errors.Add(new("/map/" + field, "not-writable", "Def_Err_NotWritable", $"'{name}' is not a writable property of {entity.Name}."));
        }
        foreach (var r in EntityJson.Names(parameters["required"])) if (!entity.Writable.Contains(r)) errors.Add(new("/required", "not-writable", "Def_Err_NotWritable", $"'{r}' is not a writable property."));
        if (parameters["defaults"] is JsonObject defaults)
            foreach (var (name, v) in defaults)
            {
                var pi = entity.ClrType.GetProperty(name);
                if (pi is null || !EntityJson.Supported(pi.PropertyType)) errors.Add(new("/defaults/" + name, "not-writable", "Def_Err_NotWritable", $"'{name}' is not a settable property of {entity.Name}."));
                else if (Convert(pi.PropertyType, v) is NotConvertible) errors.Add(new("/defaults/" + name, "wrong-type", "Def_Err_Type", $"The default of '{name}' does not fit its type."));
            }
        if (parameters["unique"] is JsonArray unique)
            for (var i = 0; i < unique.Count; i++)
            {
                if (unique[i] is not JsonObject u) { errors.Add(new($"/unique/{i}", "wrong-type", "Def_Err_Type", "A unique rule is an object.")); continue; }
                foreach (var f in EntityJson.Names(u["fields"])) if (entity.ClrType.GetProperty(f) is null) errors.Add(new($"/unique/{i}/fields", "unknown-field", "Def_Err_UnknownField", $"'{f}' is not a property of {entity.Name}."));
                DataSourceDefinitionKind.CheckLabel(u["message"], $"/unique/{i}/message", errors);
            }
        return errors;
    }

    private sealed class NotConvertible;

    /// <summary>A form value or a default converted to the type of a property: null for empty, <see cref="NotConvertible"/> when it does not fit.</summary>
    private static object? Convert(Type type, JsonNode? json) => json is null ? null : Convert(type, json is JsonValue v && v.GetValueKind() == JsonValueKind.String ? (string?)v : json.ToJsonString());

    private static object? Convert(Type type, string? text)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (target == typeof(string)) return string.IsNullOrWhiteSpace(text) ? (Nullable.GetUnderlyingType(type) is null && type == typeof(string) ? text?.Trim() ?? "" : null) : text.Trim();
        if (text is "$now" && target == typeof(DateTime)) return DateTime.Now;
        if (text is "$utcnow" && target == typeof(DateTime)) return DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(text)) return Nullable.GetUnderlyingType(type) is null && type.IsValueType ? Activator.CreateInstance(type) : null;
        if (target == typeof(bool)) return string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) ? true : string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) ? false : new NotConvertible();
        if (target == typeof(DateTime)) return DateTime.TryParse(FormEngine.NormalizeDigits(text), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : new NotConvertible();
        if (!FormEngine.TryNumber(text, out var n)) return new NotConvertible();
        try { return System.Convert.ChangeType(n, target, CultureInfo.InvariantCulture); } catch (Exception ex) when (ex is OverflowException or InvalidCastException) { return new NotConvertible(); }
    }

    private static TranslatableText Msg(string en, string fa) => TranslatableText.Of(("en", en), ("fa", fa));

    public async Task<ActionOutcome> ExecuteAsync(ActionInvocation invocation, CancellationToken ct)
    {
        var p = invocation.Parameters;
        var entity = catalog.Get(EntityJson.Str(p?["entity"]));
        if (entity is null) return ActionOutcome.Refused(Msg("The action is not set up.", "کنش درست تنظیم نشده است."));
        using var scope = scopes.CreateScope();
        if (!await scope.ServiceProvider.GetRequiredService<IDefinitionAccess>().CanOpenAsync(entity.CreatePermission, invocation.Roles, ct)) return ActionOutcome.Refused(Msg("You may not create this.", "اجازه‌ی ایجاد این مورد را ندارید."));
        var db = scope.ServiceProvider.GetRequiredService<PEMSDbContext>();

        var instance = Activator.CreateInstance(entity.ClrType)!;
        var map = (JsonObject)p!["map"]!;
        foreach (var (field, prop) in map)
        {
            var pi = entity.ClrType.GetProperty(EntityJson.Str(prop)!)!;
            var value = Convert(pi.PropertyType, invocation.Values.GetValueOrDefault(field));
            if (value is NotConvertible) return ActionOutcome.Refused(field, Msg("The value is not valid.", "مقدار معتبر نیست."));
            pi.SetValue(instance, value);
        }
        foreach (var name in EntityJson.Names(p["required"]))
        {
            var pi = entity.ClrType.GetProperty(name)!;
            var v = pi.GetValue(instance);
            if (v is null || v is string s && string.IsNullOrWhiteSpace(s))
                return ActionOutcome.Refused(map.FirstOrDefault(m => EntityJson.Str(m.Value) == name).Key ?? name, Msg("This field is required.", "این فیلد الزامی است."));
        }
        if (p["defaults"] is JsonObject defaults)
            foreach (var (name, v) in defaults) { var pi = entity.ClrType.GetProperty(name)!; pi.SetValue(instance, Convert(pi.PropertyType, v)); }

        if (p["unique"] is JsonArray unique)
            foreach (var u in unique.OfType<JsonObject>())
            {
                var fields = EntityJson.Names(u["fields"]);
                var exists = await (Task<bool>)typeof(EntityCreateAction).GetMethod(nameof(Exists), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(entity.ClrType).Invoke(null, [db, instance, fields, ct])!;
                if (exists)
                {
                    var message = u["message"]?.Deserialize<TranslatableText>(PemsJson.Options) ?? Msg("This already exists.", "این مورد از قبل وجود دارد.");
                    var field = map.FirstOrDefault(m => fields.Contains(EntityJson.Str(m.Value) ?? "")).Key;
                    return field is null ? ActionOutcome.Refused(message) : ActionOutcome.Refused(field, message);
                }
            }

        db.Add(instance);
        await db.SaveChangesAsync(ct);
        return ActionOutcome.Done();
    }

    private static async Task<bool> Exists<T>(PEMSDbContext db, object instance, IReadOnlyList<string> fields, CancellationToken ct) where T : class
    {
        var x = Expression.Parameter(typeof(T), "x");
        Expression? all = null;
        foreach (var name in fields)
        {
            var pi = typeof(T).GetProperty(name)!;
            var left = Expression.Property(x, pi);
            var value = pi.GetValue(instance);
            Expression eq;
            if (pi.PropertyType == typeof(string))
            {
                var lower = (value as string ?? "").Trim().ToLowerInvariant();
                eq = Expression.Equal(Expression.Call(Expression.Coalesce(left, Expression.Constant("")), typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!), Expression.Constant(lower));
            }
            else eq = Expression.Equal(left, Expression.Constant(value, pi.PropertyType));
            all = all is null ? eq : Expression.AndAlso(all, eq);
        }
        return all is not null && await db.Set<T>().AnyAsync(Expression.Lambda<Func<T, bool>>(all, x), ct);
    }
}
