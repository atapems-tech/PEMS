namespace PEMS.Application.Interfaces;

/// <summary>
/// The designers as operations (SCRUM-439 page, 604 form, 606 action, 607 data source, 608 menu): one small set of edits of the open draft of any definition, addressed by <c>reference</c> (<c>appKey:kind:key</c>) and by
/// JSON Pointer, plus typed helpers that build the right item for a form field, a page instance, a menu item and an action. The draft is opened when there is none; every edit goes through the definition service
/// (If-Match, kind validation at validate and publish), so a designer is a stored page and form over these operations, never a compiled screen. The result is <c>(Success, Error)</c> with the first problem.
/// </summary>
public interface IDefinitionDesigner
{
    [PEMS.Application.Common.PemsOperation("Designer", "SetValue", "Definitions.Edit")]
    Task<(bool Success, string? Error)> SetValueAsync(string reference, string pointer, string valueJson, string actor);

    [PEMS.Application.Common.PemsOperation("Designer", "AddItem", "Definitions.Edit")]
    Task<(bool Success, string? Error)> AddItemAsync(string reference, string arrayPointer, string itemJson, string actor);

    [PEMS.Application.Common.PemsOperation("Designer", "RemoveItem", "Definitions.Edit")]
    Task<(bool Success, string? Error)> RemoveItemAsync(string reference, string pointer, string actor);

    [PEMS.Application.Common.PemsOperation("Designer", "MoveItem", "Definitions.Edit")]
    Task<(bool Success, string? Error)> MoveItemAsync(string reference, string arrayPointer, int from, int to, string actor);

    /// <summary>Removes the row a designer grid shows: <paramref name="rowId"/> is the <c>rowId</c> of the row (reference|pointer|index).</summary>
    [PEMS.Application.Common.PemsOperation("Designer", "RemoveRow", "Definitions.Edit")]
    Task<(bool Success, string? Error)> RemoveRowAsync(string rowId, string actor);

    /// <summary>Moves the row a designer grid shows one place <c>up</c> or <c>down</c>.</summary>
    [PEMS.Application.Common.PemsOperation("Designer", "MoveRow", "Definitions.Edit")]
    Task<(bool Success, string? Error)> MoveRowAsync(string rowId, string direction, string actor);

    /// <summary>A field of a form (SCRUM-604): <c>{ name, kind, label: { en, fa }, required }</c> at the end of <c>/fields</c>.</summary>
    [PEMS.Application.Common.PemsOperation("Designer", "AddFormField", "Definitions.Edit")]
    Task<(bool Success, string? Error)> AddFormFieldAsync(string reference, string name, string kind, string titleEn, string titleFa, string required, string actor);

    /// <summary>A component instance in a region of a page (SCRUM-439): <c>{ id, component, options }</c> at the end of <c>/regions/{region}</c>.</summary>
    [PEMS.Application.Common.PemsOperation("Designer", "AddPageInstance", "Definitions.Edit")]
    Task<(bool Success, string? Error)> AddPageInstanceAsync(string reference, string region, string id, string component, string? optionsJson, string actor);

    /// <summary>An item of a menu (SCRUM-608): <c>{ key, parent, kind: link, order, title: { en, fa }, page }</c> at the end of <c>/items</c>.</summary>
    [PEMS.Application.Common.PemsOperation("Designer", "AddMenuItem", "Definitions.Edit")]
    Task<(bool Success, string? Error)> AddMenuItemAsync(string reference, string itemKey, string? parent, string titleEn, string titleFa, string page, int order, string actor);

    /// <summary>A new action definition that runs an operation (SCRUM-606): handler operation.invoke with model, action and the arguments; created as a definition, published through the workbench.</summary>
    [PEMS.Application.Common.PemsOperation("Designer", "CreateAction", "Definitions.Create")]
    Task<(bool Success, string? Error)> CreateActionAsync(string key, string? appKey, string model, string action, string? argsJson, string successEn, string successFa, string? redirect, string actor);
}
