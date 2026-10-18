using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Common;
using PEMS.Persistence.Context;
using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Interfaces;
using PEMS.WorkflowPlatform.Services;

namespace PEMS.Persistence.Workflow;

/// <summary>
/// Phase 3B - ثابت‌ها و ابزارهای مشترک گردش‌کار Work Order (مرجع: WORKFLOW_PLATFORM_PHASE3A_WORK_ORDER_GOVERNANCE.md).
/// دو فرآیند جدا با هویت جدا (Architecture A): چرخه‌عمر (WorkOrderLifecycle) و بازبینی چند-دیسیپلینه (WorkOrderMultiDisciplineReview).
/// </summary>
public static class WorkOrderWorkflow
{
    public const string Module = "WorkOrder";
    public const string LifecycleEntityType = "WorkOrderLifecycle";
    public const string LifecycleContext = "Standard";
    public const string ReviewEntityType = "WorkOrderMultiDisciplineReview";
    public const string ReviewContext = "MultiDisciplineReview";
    public const string LegacyReviewEntityType = "WorkOrder";     // Instance های قبل از Phase 3B (بدون Binding)
    public const string LifecycleDefinitionName = "Work Order Lifecycle";
    public const string ParticipantPrefix = "Permission:";
    // طبق «PEMS Authorization Architecture Completion» فاز ۳ بند ۶: این یک استثنای کسب‌وکاری
    // تک‌منظوره است (نه الگوی رو به گسترش) - فقط در دقیقاً یک شاخه (استثنای Verify/FinalApprove
    // برای مدیر نگهداری روی WO هایی که سازمان مبدأشان خودِ Maintenance است، خط ۱۹۷ همین فایل)
    // مصرف می‌شود. جایگزینی آن با یک Permission/Scope عمومی به یک لایه‌ی Policy تازه نیاز داشت
    // بدون اینکه مشکل واقعی‌ای حل کند - طبق دستور صریح: «Do not create a generic Policy Engine
    // merely to remove one string constant». هیچ ماژول دیگری امروز به همین مفهوم «Override مدیر»
    // نیاز ندارد؛ اگر ماژول دومی همین الگو را خواست، آن‌جا دلیل کافی برای تعمیم خواهد بود.
    public const string ManagerRole = "MNT.Manager";

    public static string LifecycleKey(int workOrderId) => $"{Module}|{LifecycleEntityType}|{workOrderId}";

    /// <summary>مجوزهای «وظیفه‌ای» گردش‌کار Work Order. هر گذار دقیقاً یکی از آن‌ها را الزامی می‌کند؛ قوانین سازمان/دیسیپلین/SoD روی همین کد کلید می‌خورند.</summary>
    public static class Perm
    {
        public const string View = "WorkOrder.View";
        public const string Create = "WorkOrder.Create";
        public const string Plan = "WorkOrder.Plan";
        public const string Prepare = "WorkOrder.Prepare";
        public const string Assign = "WorkOrder.Assign";
        public const string Issue = "WorkOrder.Issue";
        // طبق «PEMS — Work Order Governance Remediation» F2: Issue/Release (اختیار Operations) و شروع
        // واقعی کار (اختیار مجری نگهداری) دو رویداد جدا با دو صاحب‌اختیار جدا هستند - قبلاً یک کلیک
        // (Issue، Approve) هر دو را هم‌زمان انجام می‌داد و StartDate را هم ست می‌کرد.
        public const string Start = "WorkOrder.Start";
        public const string Execute = "WorkOrder.Execute";
        public const string Hold = "WorkOrder.Hold";
        public const string Verify = "WorkOrder.Verify";
        public const string FinalApprove = "WorkOrder.FinalApprove";
        public const string Reject = "WorkOrder.Reject";
        public const string CancelPreIssue = "WorkOrder.CancelPreIssue";
        public const string CancelExecution = "WorkOrder.CancelExecution";

        public static readonly string[] Workflow =
            { Create, Plan, Prepare, Assign, Issue, Start, Execute, Hold, Verify, FinalApprove, Reject, CancelPreIssue, CancelExecution };
    }

    /// <summary>رجیستری اکشن‌ها: اکشن‌های هسته + «Hold» و «Comment» به‌عنوان اکشن ماژول WorkOrder (فقط نام؛ هیچ کدی اجرا نمی‌شود).</summary>
    public static IWorkflowActionRegistry CreateRegistry()
    {
        var r = new WorkflowActionRegistry();
        Register(r);
        return r;
    }

    public static void Register(IWorkflowActionRegistry registry)
    {
        registry.RegisterModuleAction(Module, "Hold", WorkflowActionKind.Outcome, "Put the Work Order on hold.");
        registry.RegisterModuleAction(Module, "Comment", WorkflowActionKind.Outcome, "Discipline review comment.");
    }

    public static string T(string fa, string en)
        => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa" ? fa : en;

    /// <summary>
    /// همه‌ی Instance های بازبینی چند-دیسیپلینه‌ی یک Work Order، بدون «جدیدترین بر اساس نوع»: نوع اختصاصی جدید + Instance های Legacy که
    /// صریحاً با تعریف (Category=MultiDisciplineReview) شناسایی می‌شوند - نه صرفاً BusinessObjectType. جدیدترین اول.
    /// </summary>
    public static async Task<List<WorkflowInstance>> ReviewInstancesAsync(PEMSDbContext db, int workOrderId, bool runningOnly)
    {
        var key = $"{Module}|{ReviewEntityType}|{workOrderId}";
        var current = db.WorkflowInstances.Where(i => i.EntityKey == key);
        var legacy = from i in db.WorkflowInstances
                     join v in db.WorkflowVersions on i.WorkflowVersionId equals v.WorkflowVersionId
                     join d in db.WorkflowDefinitions on v.WorkflowDefinitionId equals d.WorkflowDefinitionId
                     where i.Module == Module && i.BusinessObjectType == LegacyReviewEntityType && i.BusinessObjectId == workOrderId && d.Category == ReviewContext
                     select i;
        var all = await current.Concat(legacy).ToListAsync();
        return all.Where(i => !runningOnly || i.Status == "Running").GroupBy(i => i.WorkflowInstanceId).Select(g => g.First())
            .OrderByDescending(i => i.StartDate).ThenByDescending(i => i.WorkflowInstanceId).ToList();
    }

    /// <summary>دامنه‌ی سازمانی مؤثر یک Department: نزدیک‌ترین خودش/والد که مقدار دارد؛ وگرنه null (نامشخص).</summary>
    public static async Task<string?> ResolveDepartmentDomainAsync(PEMSDbContext db, int departmentId)
    {
        int? current = departmentId;
        for (var depth = 0; depth < 12 && current.HasValue; depth++)
        {
            var d = await db.Departments.AsNoTracking().Where(x => x.DepartmentId == current.Value)
                .Select(x => new { x.OrganizationDomain, x.ParentDepartmentId }).FirstOrDefaultAsync();
            if (d == null) return null;
            if (!string.IsNullOrWhiteSpace(d.OrganizationDomain)) return d.OrganizationDomain;
            current = d.ParentDepartmentId;
        }
        return null;
    }

    /// <summary>هویت سازمانی کارمند = (دامنه از سلسله‌مراتب Department، دیسیپلین). null اگر کارمند/دامنه نامشخص باشد.</summary>
    public static Task<(string? Domain, int? DisciplineId)> ResolveEmployeeAsync(PEMSDbContext db, int? employeeId) =>
        // از مرز مرکزی IOrganizationResolver عبور می‌کند (رفتار دقیقاً همان؛ اثبات: OrganizationResolverFacadeTests.Characterization_*).
        new PEMS.Persistence.Services.OrganizationResolver(db).GetDomainAndDisciplineAsync(employeeId);

    /// <summary>پیوند «Permission:X» برای هر مجوز گردش‌کار که این نقش‌ها دارند (همان الگوی Work Request).</summary>
    public static async Task<List<string>> ExpandRolesAsync(IAccessControlService access, IReadOnlyList<string> roles)
    {
        var expanded = new List<string>(roles ?? Array.Empty<string>());
        var baseRoles = expanded.ToList();
        foreach (var p in Perm.Workflow)
        {
            var dot = p.IndexOf('.');
            if ((await access.AuthorizeForRolesAsync(baseRoles, p[..dot], p[(dot + 1)..])).Allowed)
                expanded.Add(ParticipantPrefix + p);
        }
        return expanded;
    }
}

/// <summary>
/// هوک مجوز مخصوص Work Order (روی همان نقطه‌ی توسعه‌ی IWorkflowAuthorizationHook؛ چارچوب مجوز جدیدی نیست): ابتدا مجوز نقش
/// (AccessControl، مثل Work Request)، سپس سه لایه‌ی سازمانی: دامنه‌ی سازمانی، مالکیت دیسیپلین، و تفکیک وظایف (SoD) از
/// روی WorkflowHistory. همه سمت سرور، Fail-closed، و بدون هیچ تغییر حالت در صورت رد.
/// </summary>
public class WorkOrderAuthorizationHook : IWorkflowAuthorizationHook
{
    private readonly PEMSDbContext _db;
    private readonly AccessControlWorkflowAuthorizationHook _permissionHook;

    public WorkOrderAuthorizationHook(PEMSDbContext db, IAccessControlService access)
    {
        _db = db;
        _permissionHook = new AccessControlWorkflowAuthorizationHook(access);
    }

    public async Task<WorkflowAuthorizationResult> AuthorizeAsync(WorkflowAuthorizationContext c, CancellationToken ct = default)
    {
        var baseResult = await _permissionHook.AuthorizeAsync(c, ct);
        if (!baseResult.Allowed) return baseResult;

        // فقط چرخه‌عمر Work Order قوانین سازمانی/SoD دارد؛ بازبینی چند-دیسیپلینه مسیر خودش را دارد
        if (c.Instance.Module != WorkOrderWorkflow.Module || c.Instance.BusinessObjectType != WorkOrderWorkflow.LifecycleEntityType)
            return baseResult;

        var perm = c.Transition.RequiredPermission ?? "";
        if (!c.ActorEmployeeId.HasValue)
            return Deny("کارمند مرتبط با کاربر مشخص نیست.", "No employee is linked to the current user.");

        var (org, disc) = await WorkOrderWorkflow.ResolveEmployeeAsync(_db, c.ActorEmployeeId);
        if (org == null)
            return Deny("دامنه‌ی سازمانی شما مشخص نیست (دپارتمان بدون دامنه).", "Your organization domain is not defined (department has no organization domain).");

        var wo = await _db.WorkOrders.AsNoTracking().Where(x => x.WorkOrderId == c.Instance.BusinessObjectId)
            .Select(x => new { x.OriginatingOrganization, x.OriginatingDisciplineId, x.ResponsibleDisciplineId }).FirstOrDefaultAsync(ct);
        if (wo == null) return Deny("Work Order یافت نشد.", "Work Order not found.");

        var actor = c.ActorEmployeeId.Value;
        var isManager = c.ActorRoles.Contains(WorkOrderWorkflow.ManagerRole);

        // ---------- سازمان / دیسیپلین ----------
        switch (perm)
        {
            case WorkOrderWorkflow.Perm.Plan:
            case WorkOrderWorkflow.Perm.Prepare:
            case WorkOrderWorkflow.Perm.Reject:
            case WorkOrderWorkflow.Perm.CancelPreIssue:
            case WorkOrderWorkflow.Perm.CancelExecution:
                if (org != OrganizationDomains.Maintenance) return Deny("این اقدام فقط برای سازمان نگهداری و تعمیرات مجاز است.", "This action is restricted to the Maintenance organization.");
                break;
            case WorkOrderWorkflow.Perm.Create:
                break; // ایجاد/ارسال: هر دامنه‌ی معتبر با مجوز Create؛ دامنه‌ی مبدأ از خودِ ایجادکننده می‌آید
            case WorkOrderWorkflow.Perm.Hold:
            case WorkOrderWorkflow.Perm.Start:
            case WorkOrderWorkflow.Perm.Execute:
                if (org != OrganizationDomains.Maintenance) return Deny("این اقدام فقط برای سازمان نگهداری و تعمیرات مجاز است.", "This action is restricted to the Maintenance organization.");
                if (!wo.ResponsibleDisciplineId.HasValue) return Deny("دیسیپلین مسئول نگهداری هنوز تعیین نشده است.", "The responsible maintenance discipline has not been set.");
                if (disc != wo.ResponsibleDisciplineId) return Deny("شما عضو دیسیپلین مسئول نگهداری این Work Order نیستید.", "You are not a member of this Work Order's responsible maintenance discipline.");
                break;
            case WorkOrderWorkflow.Perm.Issue:
                if (org != OrganizationDomains.Operations) return Deny("صدور/آزادسازی فقط با مرجع عملیات (Operations) انجام می‌شود.", "Issue / release is performed only by the Operations authority.");
                break;
            case WorkOrderWorkflow.Perm.Verify:
            case WorkOrderWorkflow.Perm.FinalApprove:
                if (wo.OriginatingOrganization == null || !wo.OriginatingDisciplineId.HasValue)
                    return Deny("دیسیپلین مبدأ این Work Order ثبت نشده است.", "The originating discipline of this Work Order is not recorded.");
                if (org != wo.OriginatingOrganization)
                    return Deny("تأیید/تصویب فقط با دیسیپلین مبدأ (سازمان مبدأ) مجاز است.", "Verification / approval belongs to the originating discipline (originating organization).");
                if (disc != wo.OriginatingDisciplineId && !(isManager && wo.OriginatingOrganization == OrganizationDomains.Maintenance))
                    return Deny("شما عضو دیسیپلین مبدأ این Work Order نیستید.", "You are not a member of this Work Order's originating discipline.");
                break;
            default:
                return Deny("مجوز گذار نامعتبر است.", "Invalid transition permission.");
        }

        // ---------- تفکیک وظایف (از تاریخچه‌ی گردش‌کار همین Instance) ----------
        var prior = await PriorActorsAsync(c.Instance, ct);
        HashSet<int> Actors(params string[] perms) => perms.SelectMany(p => prior.GetValueOrDefault(p) ?? new HashSet<int>()).ToHashSet();
        var planners = Actors(WorkOrderWorkflow.Perm.Plan, WorkOrderWorkflow.Perm.Prepare);
        var issuers = Actors(WorkOrderWorkflow.Perm.Issue);
        // طبق F2: Start Work هم بخشی از «اجرا» محسوب می‌شود - کسی که کار را شروع کرده، دقیقاً مثل
        // کسی که آن را اجرا/تکمیل کرده، نباید بعداً تأیید/تصویب کار خودش را انجام دهد.
        var executors = Actors(WorkOrderWorkflow.Perm.Execute, WorkOrderWorkflow.Perm.Start);
        var verifiers = Actors(WorkOrderWorkflow.Perm.Verify);

        switch (perm)
        {
            case WorkOrderWorkflow.Perm.Start:
            case WorkOrderWorkflow.Perm.Execute:
                if (planners.Contains(actor)) return Deny("تفکیک وظایف: برنامه‌ریز نمی‌تواند مجری همان Work Order باشد.", "Separation of duties: the planner cannot execute the same Work Order.");
                if (issuers.Contains(actor)) return Deny("تفکیک وظایف: صادرکننده (Operations) نمی‌تواند مجری همان Work Order باشد.", "Separation of duties: the Operations issuer cannot execute the same Work Order.");
                break;
            case WorkOrderWorkflow.Perm.Issue:
                if (executors.Contains(actor)) return Deny("تفکیک وظایف: مجری نمی‌تواند صادرکننده‌ی همان Work Order باشد.", "Separation of duties: the executor cannot issue the same Work Order.");
                break;
            case WorkOrderWorkflow.Perm.Verify:
                if (executors.Contains(actor)) return Deny("تفکیک وظایف: مجری نمی‌تواند تکمیل کار خودش را تأیید کند.", "Separation of duties: the executor cannot verify their own completion.");
                break;
            case WorkOrderWorkflow.Perm.FinalApprove:
                if (executors.Contains(actor)) return Deny("تفکیک وظایف: مجری نمی‌تواند تصویب نهایی کند.", "Separation of duties: the executor cannot give final approval.");
                if (verifiers.Contains(actor)) return Deny("تفکیک وظایف: تأییدکننده نمی‌تواند تصویب نهایی کند.", "Separation of duties: the verifier cannot give final approval.");
                if (c.Instance.InitiatedById == actor) return Deny("تفکیک وظایف: ایجادکننده نمی‌تواند Work Order را ببندد.", "Separation of duties: the creator cannot close the Work Order.");
                break;
        }
        return WorkflowAuthorizationResult.Allow();
    }

    /// <summary>مجموعه‌ی کارمندانی که تاکنون هر «مجوز وظیفه‌ای» را روی این Instance اجرا کرده‌اند (گذار ⇄ تاریخچه).</summary>
    private async Task<Dictionary<string, HashSet<int>>> PriorActorsAsync(WorkflowInstance instance, CancellationToken ct)
    {
        var history = await _db.WorkflowHistories.AsNoTracking()
            .Where(h => h.WorkflowInstanceId == instance.WorkflowInstanceId && h.UserId != null).ToListAsync(ct);
        var transitions = await _db.WorkflowTransitions.AsNoTracking()
            .Where(t => t.WorkflowVersionId == instance.WorkflowVersionId && t.RequiredPermission != null).ToListAsync(ct);
        var result = new Dictionary<string, HashSet<int>>();
        foreach (var h in history)
        {
            var t = transitions.FirstOrDefault(x => x.FromStageId == h.FromStageId && x.ActionType == h.ActionType && x.ToStageId == h.ToStageId);
            if (t == null) continue;
            if (!result.TryGetValue(t.RequiredPermission!, out var set)) result[t.RequiredPermission!] = set = new HashSet<int>();
            set.Add(h.UserId!.Value);
        }
        return result;
    }

    private static WorkflowAuthorizationResult Deny(string fa, string en) => WorkflowAuthorizationResult.Deny(WorkOrderWorkflow.T(fa, en));
}
