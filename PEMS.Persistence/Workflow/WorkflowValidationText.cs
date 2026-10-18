namespace PEMS.Persistence.Workflow;

/// <summary>
/// Phase 4 - پیام‌های Validator پلتفرم فقط فارسی‌اند؛ برای UI انگلیسی معادل عمومی هر «کد» اینجا نگهداری می‌شود (منطق اعتبارسنجی
/// تکرار نشده - فقط متن نمایشی). مرحله/گذار مرتبط در UI جداگانه لینک می‌شود.
/// </summary>
public static class WorkflowValidationText
{
    private static readonly Dictionary<string, string> En = new()
    {
        ["VERSION_NOT_FOUND"] = "The version was not found.",
        ["NO_STAGES"] = "The version has no stages.",
        ["DUPLICATE_STAGE_NAME"] = "Two stages share the same name.",
        ["STAGE_NAME_EMPTY"] = "A stage name is empty.",
        ["DUPLICATE_STAGE_ORDER"] = "Several stages share the same order; the display order is ambiguous.",
        ["START_MISSING"] = "No start transition (From = none) is defined.",
        ["START_MULTIPLE"] = "More than one start transition is defined; the start must be unique.",
        ["START_NO_TARGET"] = "The start transition has no target stage.",
        ["BROKEN_REFERENCE"] = "A transition refers to a stage that is not in this version.",
        ["EMPTY_TRANSITION"] = "A transition has neither source nor target.",
        ["SELF_LOOP"] = "A transition points back to the same stage.",
        ["DUPLICATE_TRANSITION"] = "Duplicate transition (same action and priority from the same stage); the choice would be ambiguous.",
        ["NO_TERMINAL"] = "No terminal transition (To = end) exists; the workflow can never finish.",
        ["UNREACHABLE_STAGE"] = "The stage cannot be reached from the start.",
        ["DEAD_END"] = "The stage has no outgoing transition.",
        ["NO_APPROVE_TRANSITION"] = "The stage has no Approve transition.",
        ["CATALOG_UNAVAILABLE"] = "The role/employee/permission catalog is unavailable; references were not checked.",
        ["STAGE_NO_PARTICIPANT"] = "The stage has no fixed responsible role or employee.",
        ["PERMISSION_UNKNOWN"] = "The referenced permission does not exist in the permission catalog.",
        ["ROLE_UNKNOWN"] = "The referenced role does not exist.",
        ["EMPLOYEE_UNKNOWN"] = "The referenced employee does not exist.",
        ["ESCALATION_ROLE_UNKNOWN"] = "The escalation role does not exist.",
        ["PERMISSION_FORMAT"] = "A permission must have the form Resource.Action.",
        ["SOD_ON_START"] = "Separation of duties has no effect on the start transition.",
        ["ACTION_NOT_REGISTERED"] = "The action is not registered in the action registry.",
        ["ACTION_NOT_TRANSITION"] = "The action is a side-effect action and cannot be a transition.",
        ["CONDITION_INVALID"] = "The transition condition is invalid.",
        ["STAGE_TYPE_INVALID"] = "The stage type is invalid.",
        ["APPROVAL_STRATEGY_INVALID"] = "The approval strategy is invalid.",
        ["MIN_COUNT_MISSING"] = "The MinimumCount strategy needs a minimum approval count.",
        ["MIN_COUNT_TOO_HIGH"] = "The minimum approval count exceeds the number of roles.",
        ["MANDATORY_ROLES_ALIAS"] = "MandatoryRolesOnly currently behaves like AllApprovalsRequired.",
        ["SLA_INVALID"] = "The stage timeout must be positive.",
        ["ESCALATION_NO_ROLE"] = "The stage is escalatable but has no escalation role.",
        ["ESCALATION_NO_SLA"] = "The stage is escalatable but has no timeout.",
        ["DEFINITION_KEY_MISSING"] = "The definition needs a Module and a Category.",
        ["DEFINITION_INACTIVE"] = "The definition is archived / inactive.",
        ["LEGACY_AMBIGUITY"] = "Another active definition has the same Module and Category.",
        ["BINDING_PIN_MISMATCH"] = "A binding is pinned to a version of a different definition.",
    };

    public static string Localize(string code, string faMessage)
        => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa" ? faMessage : (En.TryGetValue(code, out var e) ? e : code);
}
