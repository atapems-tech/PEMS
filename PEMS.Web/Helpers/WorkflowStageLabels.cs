namespace PEMS.Web.Helpers;

/// <summary>
/// نام نمایشی مرحله‌ی گردش‌کار: ۱) نام فارسی ثبت‌شده در تعریف مرحله (NameFa) ۲) کلید منبع «MyWork_Stage_*» ۳) نام ذخیره‌شده.
/// فقط نمایش است؛ نام مرحله‌ی ذخیره‌شده (Name) که موتور و شرط‌ها با آن کار می‌کنند هیچ‌وقت تغییر نمی‌کند.
/// </summary>
public static class WorkflowStageLabels
{
    public const string KeyPrefix = "MyWork_Stage_";

    /// <summary>«Operations Issue / Release» → «MyWork_Stage_OperationsIssueRelease» (فقط حروف و ارقام).</summary>
    public static string ResourceKey(string? stageName)
    {
        var chars = (stageName ?? string.Empty).Where(char.IsLetterOrDigit).ToArray();
        return KeyPrefix + new string(chars);
    }
}
