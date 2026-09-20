using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.WorkflowPlatform.Services;

/// <summary>
/// رجیستری کنترل‌شده‌ی اکشن‌ها. فقط «نام + نوع» نگه می‌دارد؛ هیچ Delegate، اسکریپت، SQL یا Reflection
/// اجرا نمی‌کند. اکشن‌های هسته: Approve, Reject, Return, Complete (Outcome) و Assign, Notify, Escalate
/// (SideEffect). ماژول‌ها می‌توانند با نام یکتای خودشان اکشن اضافه کنند (RegisterModuleAction).
/// </summary>
public class WorkflowActionRegistry : IWorkflowActionRegistry
{
    private static readonly Regex NamePattern = new("^[A-Za-z][A-Za-z0-9]{0,49}$", RegexOptions.Compiled);
    private readonly ConcurrentDictionary<string, WorkflowActionDescriptor> _core = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<(string Module, string Name), WorkflowActionDescriptor> _module = new();

    public WorkflowActionRegistry()
    {
        Core("Start", WorkflowActionKind.Outcome, "Reserved for the start transition.");
        Core("Approve", WorkflowActionKind.Outcome, "Approve the stage.");
        Core("Reject", WorkflowActionKind.Outcome, "Reject the stage; ends the workflow as Rejected when it is the outcome.");
        Core("Return", WorkflowActionKind.Outcome, "Return to a previous stage for rework.");
        Core("Complete", WorkflowActionKind.Outcome, "Complete the stage/workflow.");
        Core("Cancel", WorkflowActionKind.Outcome, "Cancel the workflow; the instance ends as Cancelled.");
        Core("Assign", WorkflowActionKind.SideEffect, "Assignment event (not executed by the platform).");
        Core("Notify", WorkflowActionKind.SideEffect, "Notification event (extension point only).");
        Core("Escalate", WorkflowActionKind.SideEffect, "Escalation event (not executed automatically in Phase 1).");
    }

    private void Core(string name, WorkflowActionKind kind, string description)
        => _core[name] = new WorkflowActionDescriptor(name, kind, null, description);

    public void RegisterModuleAction(string module, string name, WorkflowActionKind kind, string description)
    {
        if (string.IsNullOrWhiteSpace(module)) throw new ArgumentException("Module is required.", nameof(module));
        if (!NamePattern.IsMatch(name ?? "")) throw new ArgumentException("Action name must be alphanumeric (max 50).", nameof(name));
        if (_core.ContainsKey(name)) throw new InvalidOperationException($"Action '{name}' is a core action and cannot be overridden.");
        _module[(module, name)] = new WorkflowActionDescriptor(name, kind, module, description);
    }

    public WorkflowActionDescriptor? Find(string name, string? module)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (_core.TryGetValue(name, out var c)) return c;
        return module != null && _module.TryGetValue((module, name), out var m) ? m : null;
    }

    public IReadOnlyList<WorkflowActionDescriptor> All(string? module = null)
        => _core.Values.Concat(_module.Values.Where(v => module == null || v.Module == module)).OrderBy(v => v.Name).ToList();
}

public sealed class NoOpWorkflowEventSink : IWorkflowEventSink
{
    public Task OnEventAsync(WorkflowRuntimeEvent evt, CancellationToken ct = default) => Task.CompletedTask;
}
