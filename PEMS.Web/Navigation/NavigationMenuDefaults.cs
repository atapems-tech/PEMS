using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Web.Security;

namespace PEMS.Web.Navigation;

/// <summary>
/// کاتالوگ پیش‌فرض سایدبار داده‌محور: عیناً همان ساختار، ترتیب، آیکون و کلید بومی‌سازی _SidebarMenu.cshtml (سایدبار قدیمی که پشت پرچم
/// Features:DynamicSidebar:Enabled باقی مانده). قاعده‌ی نمایش هر بخش همان دروازه‌ی قبلی است (NavigationVisibilityResolver / پرچم‌های
/// مجوزمحور)؛ قاعده‌ی هر پیوند علاوه بر آن همان مجوزی است که کنترلر مقصد در Action همین پیوند واقعاً اعمال می‌کند (از سورس خوانده
/// شده، نه اختراعی). پیوندی که کنترلرش هیچ دروازه‌ای ندارد Always می‌ماند (تصمیم مالک: مثل امروز نمایان).
/// </summary>
public static class NavigationMenuDefaults
{
    /// <summary>
    /// کلیدهای سیستمیِ حذف‌شده از منو. همگام‌سازی پیش‌فرض‌ها ردیف قدیمی آن‌ها را در دیتابیس غیرفعال می‌کند (حذف نمی‌شود).
    /// «برچسب‌ها» از منو رفت: درخت برچسب حالا داخل کارتابل نامه‌نگاری (پنل کناری و تنظیمات) مدیریت می‌شود.
    /// </summary>
    public static readonly IReadOnlyList<string> RetiredKeys = new[]
    {
        "proj.tags",
        // SCRUM-423: تدارکات، قرارداد و انبار زیر بخش واحد «تدارکات و زنجیره‌ی تأمین» (sec.procurement) رفتند. والدِ ردیف موجود را
        // همگام‌سازی عوض نمی‌کند (مالکیت مدیر)، پس کلیدها تازه‌اند و ردیف‌های قدیمی غیرفعال می‌شوند.
        "eng.package", "eng.tender", "eng.mr",
        "sec.contract", "contract.contracts", "contract.vendors", "contract.po", "contract.milestones", "contract.claims",
        "sec.inventory", "wh.dashboard", "wh.material", "wh.irn", "wh.ledger", "wh.sr", "wh.replenishment", "wh.masterdata",
        "wh.partcatalogue", "wh.partcatalogue.admin",
    };

    private const string S = NavigationMenuItemKinds.Section;
    private const string H = NavigationMenuItemKinds.Subheading;
    private const string L = NavigationMenuItemKinds.Link;
    private const string Always = NavigationVisibilityRules.Always;

    private static NavigationMenuItemDefinition Link(string key, string? parent, int order, string titleKey, string icon, string controller,
        string action = "Index", string? area = null, string rule = Always, bool isDefault = false, string? badge = null, string? extraActive = null)
        => new(key, parent, L, order, titleKey, icon, rule, area, controller, action, IsDefaultForController: isDefault, ExtraActiveControllers: extraActive, BadgeKey: badge);

    private static NavigationMenuItemDefinition Section(string key, int order, string titleKey, string icon, string rule, string extraActive, string? activeArea = null)
        => new(key, null, S, order, titleKey, icon, rule, ExtraActiveControllers: extraActive, ActiveArea: activeArea);

    private static NavigationMenuItemDefinition Sub(string key, string parent, int order, string titleKey)
        => new(key, parent, H, order, titleKey, null, Always);

    private static NavigationMenuItemDefinition ComingSoon(string key, string parent, int order, string titleKey, string icon, string title, string section, string rule = Always)
        => new(key, parent, L, order, titleKey, icon, rule,
            ExternalPath: "/ComingSoon?title=" + Uri.EscapeDataString(title) + "&section=" + Uri.EscapeDataString(section));

    public static IReadOnlyList<NavigationMenuItemDefinition> All { get; } = Build();

    private static List<NavigationMenuItemDefinition> Build() => new()
    {
        // ---- سطح بالا
        Link("home", null, 10, "Dashboard", "bi-speedometer2", "Home"),
        Link("exec.dashboard", null, 15, "Exec_NavTitle", "bi-graph-up-arrow", "ExecutiveDashboard", rule: "Perm:EngineeringDocuments.View"),
        Link("mywork", null, 20, "My Work", "bi-list-check", "MyWork", badge: NavigationBadges.MyWorkPending),
        Link("quicksetup", null, 30, "Quick Setup", "bi-rocket-takeoff", "QuickSetup", rule: "Module:QuickSetup"),
        Link("importcenter", null, 40, "IC_Title", "bi-cloud-arrow-up", "ImportCenter", rule: "Module:ImportCenter",
            extraActive: string.Join(",", ImportCatalog.StandaloneImportControllers)),

        // ---- مدیریت مهندسی
        Section("sec.eng", 100, "EngineeringManagement", "bi-building", "Module:EngineeringManagement",
            "EngineeringDashboard,Projects,EngineeringDocuments,DocumentRegister,DocumentRevisions,DocumentWorkflow,VDR,Transmittal,MDB"),
        Link("eng.dashboard", "sec.eng", 10, "EngineeringDashboard", "bi-speedometer2", "EngineeringDashboard"),
        Link("eng.projects", "sec.eng", 20, "Projects", "bi-kanban", "Projects"),
        Link("eng.documents", "sec.eng", 30, "EngineeringDocuments", "bi-file-earmark-text", "EngineeringDocuments"),
        Link("eng.register", "sec.eng", 40, "DocumentRegister", "bi-journal-bookmark", "DocumentRegister"),
        Link("eng.revisions", "sec.eng", 50, "Document Revision", "bi-clock-history", "DocumentRevisions"),
        Link("eng.docworkflow", "sec.eng", 60, "Document Workflow", "bi-diagram-2", "DocumentWorkflow"),
        Link("eng.vdr", "sec.eng", 70, "VDR", "bi-journal-check", "VDR"),
        Link("eng.transmittal", "sec.eng", 80, "Transmittal", "bi-send", "Transmittal"),
        Link("eng.mdb", "sec.eng", 90, "MDB", "bi-collection", "MDB"),
        Link("eng.disciplines", "sec.eng", 130, "Disciplines", "bi-diagram-3", "Discipline", rule: "Perm:DisciplineWorkspace.View", isDefault: true),

        // ---- هوش مصنوعی و دانش
        Section("sec.ai", 200, "AI & Knowledge", "bi-robot", "Module:AIIntelligence",
            "EngineeringDocumentIntelligence,KnowledgeBase,AISearch,ChatAssistant,AIInsights"),
        Link("ai.docintel", "sec.ai", 10, "DocumentIntelligence", "bi-diagram-3-fill", "EngineeringDocumentIntelligence", rule: "Perm:EngineeringDocuments.View"),
        Link("ai.kb", "sec.ai", 20, "KnowledgeBaseSearch", "bi-stars", "KnowledgeBase"),
        Link("ai.search", "sec.ai", 30, "AI Search", "bi-search-heart", "AISearch"),
        Link("ai.chat", "sec.ai", 40, "Engineering Chat Assistant", "bi-chat-dots", "ChatAssistant"),
        Link("ai.copilot", "sec.ai", 50, "Engineering Copilot", "bi-stars", "AIInsights"),

        // ---- مکاتبات و کنترل مدارک
        Section("sec.correspondence", 300, "CorrespondenceDocControl", "bi-envelope-paper", "Module:CorrespondenceDocControl", "Letter,DCC,TechnicalQuery"),
        Link("corr.letters", "sec.correspondence", 10, "Correspondence", "bi-envelope-paper", "Letter", isDefault: true),
        Link("corr.referrals", "sec.correspondence", 20, "MyReferrals", "bi-person-check", "Letter", "MyReferrals"),
        Link("corr.dcc", "sec.correspondence", 30, "DCC", "bi-building-gear", "DCC"),
        Link("corr.tq", "sec.correspondence", 40, "TQ / TSR / RFI", "bi-question-circle", "TechnicalQuery"),

        // ---- کنترل کیفیت
        Section("sec.quality", 400, "QualityControl", "bi-patch-check", "Module:QualityControl", "QC,MOC"),
        Link("quality.qc", "sec.quality", 10, "QC / QA", "bi-patch-check", "QC"),
        Link("quality.moc", "sec.quality", 20, "MOC", "bi-arrow-repeat", "MOC"),

        // ---- تدارکات و زنجیره‌ی تأمین (SCRUM-423): خرید، قرارداد، فروشنده و انبار در یک بخش، مثل CMMS. هر پیوند همان دروازه‌ی
        // قبلی‌اش را دارد به‌اضافه‌ی ماژولِ بخشِ قبلی‌اش (مهندسی/قرارداد/انبار)، پس هیچ نقشی پیوندی بیشتر یا کمتر نمی‌بیند؛ بخش وقتی
        // دیده می‌شود که کاربر یکی از آن ماژول‌ها را ببیند.
        Section("sec.procurement", 500, "Nav_ProcurementSupplyChain", "bi-cart3", "Module:EngineeringManagement|Module:ContractManagement|Module:Inventory|Flag:PartCatalogue",
            "ProcurementPackage,Tender,MaterialRequisition,Contract,Vendor,PurchaseOrder,Inventory,MaterialItem,IrnReceipt,InventoryMasterData,InventoryTransaction,StoreRequisition,Replenishment,PartCatalogueRequest,PartCatalogueAdmin"),
        Link("proc.vendors", "sec.procurement", 10, "Vendors", "bi-building", "Vendor", rule: "Module:ContractManagement&Perm:Vendors.View"),
        Sub("proc.sub.purchasing", "sec.procurement", 20, "Nav_Proc_Purchasing"),
        // SCRUM-471: the procurement cartable (pre-procurement queue), first under Purchasing
        Link("proc.preprocurement", "proc.sub.purchasing", 5, "Ppr_Title", "bi-inbox", "PreProcurement", rule: "Perm:PreProcurement.View"),
        Link("proc.mr", "proc.sub.purchasing", 10, "Material Requisition", "bi-cart-check", "MaterialRequisition", rule: "Module:EngineeringManagement&Perm:MaterialRequisition.View"),
        Link("proc.package", "proc.sub.purchasing", 20, "ProcurementPackage", "bi-folder2-open", "ProcurementPackage", rule: "Module:EngineeringManagement&Perm:ProcurementPackage.View"),
        Link("proc.tender", "proc.sub.purchasing", 30, "Tender", "bi-clipboard-check", "Tender", rule: "Module:EngineeringManagement&Perm:Tender.View"),
        Link("proc.po", "proc.sub.purchasing", 40, "Purchase Orders", "bi-receipt", "PurchaseOrder", rule: "Module:ContractManagement&Perm:PurchaseOrder.View"),
        Sub("proc.sub.contracts", "sec.procurement", 30, "Contracts"),
        Link("proc.contracts", "proc.sub.contracts", 10, "Contracts", "bi-file-earmark-ruled", "Contract", rule: "Module:ContractManagement&Perm:Contracts.View"),
        ComingSoon("proc.milestones", "proc.sub.contracts", 20, "Contract Milestones", "bi-flag", "Contract Milestones", "Contract Management", "Module:ContractManagement"),
        ComingSoon("proc.claims", "proc.sub.contracts", 30, "Claims", "bi-exclamation-diamond", "Claims", "Contract Management", "Module:ContractManagement"),
        Sub("proc.sub.warehouse", "sec.procurement", 40, "Warehouse"),
        Link("proc.wh.dashboard", "proc.sub.warehouse", 10, "Dashboard", "bi-speedometer2", "Inventory", area: "Warehouse", rule: "Module:Inventory&Perm:MaterialItem.View"),
        Link("proc.wh.material", "proc.sub.warehouse", 20, "Material Master", "bi-box-seam", "MaterialItem", area: "Warehouse", rule: "Module:Inventory&Perm:MaterialItem.View"),
        Link("proc.wh.irn", "proc.sub.warehouse", 30, "Incoming Receipts (IRN)", "bi-truck", "IrnReceipt", area: "Warehouse", rule: "Module:Inventory&Perm:IRN.View"),
        Link("proc.wh.ledger", "proc.sub.warehouse", 40, "Transaction Ledger", "bi-list-ul", "InventoryTransaction", area: "Warehouse", rule: "Module:Inventory&Perm:MaterialItem.View"),
        Link("proc.wh.sr", "proc.sub.warehouse", 50, "Store Requisitions", "bi-clipboard-check", "StoreRequisition", area: "Warehouse", rule: "Module:Inventory&Perm:StoreRequisition.View", badge: NavigationBadges.StoreRequisitionsAwaitingApproval),
        Link("proc.wh.replenishment", "proc.sub.warehouse", 60, "Repl_Menu", "bi-cart-plus", "Replenishment", area: "Warehouse", rule: "Module:Inventory&Flag:Replenishment"),
        Link("proc.wh.masterdata", "proc.sub.warehouse", 70, "Master Data", "bi-collection", "InventoryMasterData", area: "Warehouse", rule: "Module:Inventory&Perm:InventoryMasterData.View"),
        Link("proc.wh.partcatalogue", "proc.sub.warehouse", 80, "Pc_Menu", "bi-journal-plus", "PartCatalogueRequest", area: "Warehouse", rule: "Flag:PartCatalogue", badge: NavigationBadges.MyWorkPartCatalogue),
        Link("proc.wh.partcatalogue.admin", "proc.sub.warehouse", 90, "Pc_Title_Admin", "bi-sliders", "PartCatalogueAdmin", area: "Warehouse", rule: "Flag:PartCatalogueAdmin"),

        // ---- کنترل پروژه
        Section("sec.projctrl", 600, "ProjectControls", "bi-sliders", "Module:ProjectControls", "Task,TaskManager,Tag,Report,Meeting"),
        Link("proj.tasks", "sec.projctrl", 10, "Tasks", "bi-check2-square", "Task", rule: "Perm:Task.View", badge: NavigationBadges.TasksOpen),
        Link("proj.taskmanager", "sec.projctrl", 15, "Nav_TaskManager", "bi-kanban", "TaskManager", rule: "Perm:Task.View"),
        Link("proj.meetings", "sec.projctrl", 20, "Meetings", "bi-people", "Meeting"),
        Link("proj.reports", "sec.projctrl", 30, "Reports", "bi-graph-up", "Report"),

        // ---- CMMS (قاعده‌ی هر آیتم همان IsCmmsItemVisible: مجوز + Override مدیر)
        Section("sec.cmms", 700, "CmmsIntegration", "bi-tools", "Module:CmmsIntegration",
            "Equipment,Instrument,Asset,MaintenanceStrategy,FailureModeCatalog,StrategyImport,MaintenancePlan,MaintenanceCalendar,JobPlan,WorkRequest,WorkOrder,SparePart,Team,Crew,ShiftHandover,MaintenanceMasterData,EquipmentClassification,RcmFmea,EquipmentCriticality,CriticalityAssessment,FunctionalLocation,Barcode,Reliability,Inspection,Kpi,Permit,CmmsReadiness,CmmsOperations"),
        Link("cmms.equipment", "sec.cmms", 10, "Equipment", "bi-gear", "Equipment", rule: "CmmsItem:Equipment"),
        Link("cmms.instrument", "sec.cmms", 20, "Instrument Passport", "bi-cpu", "Instrument", rule: "CmmsItem:Instrument"),
        Link("cmms.asset", "sec.cmms", 30, "Assets", "bi-diagram-2", "Asset", rule: "CmmsItem:Asset"),
        Sub("cmms.sub.planning", "sec.cmms", 40, "Maintenance Planning"),
        Link("cmms.strategy", "cmms.sub.planning", 10, "Maintenance Strategy", "bi-diagram-2", "MaintenanceStrategy", rule: "CmmsItem:MaintenanceStrategy"),
        Link("cmms.plan", "cmms.sub.planning", 20, "Maintenance Plan", "bi-calendar-check", "MaintenancePlan", rule: "CmmsItem:MaintenancePlan"),
        Link("cmms.backlog", "cmms.sub.planning", 30, "Maintenance Backlog", "bi-hourglass-split", "CmmsOperations", "Backlog", rule: "CmmsItem:CmmsOperations"),
        Link("cmms.calendar", "cmms.sub.planning", 40, "Maintenance Calendar", "bi-calendar3-week", "MaintenanceCalendar", rule: "CmmsItem:MaintenanceCalendar"),
        Link("cmms.jobplan", "sec.cmms", 50, "Job Plans", "bi-list-check", "JobPlan", rule: "CmmsItem:JobPlan"),
        Link("cmms.workrequest", "sec.cmms", 60, "Work Requests", "bi-clipboard-plus", "WorkRequest", rule: "CmmsItem:WorkRequest"),
        Link("cmms.workorder", "sec.cmms", 70, "Work Order", "bi-clipboard-check", "WorkOrder", rule: "CmmsItem:WorkOrder", badge: NavigationBadges.WorkOrdersOpen),
        Link("cmms.sparepart", "sec.cmms", 80, "Spare Parts", "bi-tools", "SparePart", rule: "CmmsItem:SparePart"),
        Link("cmms.team", "sec.cmms", 90, "Teams", "bi-people", "Team", rule: "CmmsItem:Team"),
        Link("cmms.crew", "sec.cmms", 100, "Crews", "bi-people-fill", "Crew", rule: "CmmsItem:Crew"),
        Link("cmms.shifthandover", "sec.cmms", 110, "Shift Handovers", "bi-arrow-left-right", "ShiftHandover", rule: "CmmsItem:ShiftHandover"),
        Link("cmms.masterdata", "sec.cmms", 120, "Maintenance Master Data", "bi-collection", "MaintenanceMasterData", rule: "CmmsItem:MaintenanceMasterData"),
        Link("cmms.classification", "sec.cmms", 130, "Equipment Classification", "bi-diagram-3-fill", "EquipmentClassification", rule: "CmmsItem:EquipmentClassification"),
        Link("cmms.readiness", "sec.cmms", 140, "CMMS Readiness", "bi-clipboard-check", "CmmsReadiness", rule: "CmmsItem:CmmsReadiness"),
        Link("cmms.operations", "sec.cmms", 150, "CMMS Operations", "bi-speedometer2", "CmmsOperations", "Dashboard", rule: "CmmsItem:CmmsOperations", isDefault: true),
        Link("cmms.rcm", "sec.cmms", 160, "RCM / FMEA", "bi-clipboard2-pulse", "RcmFmea", rule: "CmmsItem:RcmFmea"),
        Link("cmms.criticality", "sec.cmms", 170, "Criticality Analysis", "bi-shield-exclamation", "EquipmentCriticality", rule: "CmmsItem:EquipmentCriticality"),
        Link("cmms.assessment", "sec.cmms", 180, "Criticality Assessment", "bi-clipboard2-check", "CriticalityAssessment", "Dashboard", rule: "CmmsItem:CriticalityAssessment"),
        Link("cmms.reliability", "sec.cmms", 190, "Reliability", "bi-graph-up-arrow", "Reliability", "Dashboard", rule: "CmmsItem:Reliability"),
        Link("cmms.inspection", "sec.cmms", 200, "Inspections", "bi-clipboard-pulse", "Inspection", rule: "CmmsItem:Inspection"),
        Link("cmms.kpi", "sec.cmms", 210, "KPI Dashboard", "bi-speedometer2", "Kpi", rule: "CmmsItem:Kpi"),
        Link("cmms.permit", "sec.cmms", 220, "Permits", "bi-shield-check", "Permit", rule: "CmmsItem:Permit"),
        Link("cmms.fl", "sec.cmms", 230, "Functional Locations", "bi-diagram-3", "FunctionalLocation", rule: "CmmsItem:FunctionalLocation"),
        Link("cmms.barcode", "sec.cmms", 240, "Barcode", "bi-upc-scan", "Barcode", rule: "CmmsItem:Barcode"),

        // ---- عملیات و تولید
        Section("sec.operations", 800, "Operations & Production", "bi-diagram-3-fill", "Module:Operations", "Production,ProductionOrder,ProductionRun"),
        Link("ops.dashboard", "sec.operations", 10, "Dashboard", "bi-speedometer2", "Production", "Dashboard", rule: "Perm:Production.View"),
        Link("ops.orders", "sec.operations", 20, "Production Orders", "bi-clipboard-data", "ProductionOrder", rule: "Perm:Production.View"),

        // ---- مدیریت سیستم (برای دارنده‌ی DisciplineMaster بدون Administration فقط زیرگروه سازمان/دیسیپلین دیده می‌شود)
        Section("sec.admin", 1000, "Administration", "bi-gear", "Module:Administration|Flag:DisciplineMaster",
            "Settings,Users,Roles,PermissionMatrix,AuditLog,RoutingRules,ContractApprovalAuthority,PurchaseOrderAuthority,Department,Employee,Authorization,SoDPolicy,AuthorizationAudit,NavigationVisibility,NavigationMenu,Organization,OrganizationImport,DatabaseUpdate,License,Intake,OCRProcessing,AIExtraction,TableExtraction,Relationship",
            activeArea: "Admin"),
        Link("admin.settings", "sec.admin", 10, "Settings", "bi-gear", "Settings", rule: "Module:Administration"),
        // SCRUM-40: الگوهای شماره‌گذاری اسناد تدارکات (کنترلر فقط SystemAdmin)
        Link("admin.numbering", "sec.admin", 15, "Num_Title", "bi-123", "Numbering", area: "Admin", rule: "Module:Administration&Role:SystemAdmin"),
        Sub("admin.sub.access", "sec.admin", 20, "User & Access"),
        Link("admin.users", "admin.sub.access", 10, "Users", "bi-people", "Users", area: "Admin", rule: "Module:Administration"),
        Link("admin.roles", "admin.sub.access", 20, "Roles", "bi-shield-check", "Roles", area: "Admin", rule: "Module:Administration"),
        Link("admin.workflow", "admin.sub.access", 30, "Workflow Management", "bi-diagram-3", "WorkflowManagement", area: "Admin", rule: "Module:Administration"),
        Link("admin.navmenu", "admin.sub.access", 35, "Nav_Admin_Title", "bi-menu-button-wide", "NavigationMenu", area: "Admin", rule: "Module:Administration&Role:SystemAdmin"),
        Link("admin.help", "admin.sub.access", 40, "Help Administration", "bi-life-preserver", "Help", area: "Admin", rule: "Module:Administration"),
        Sub("admin.sub.org", "sec.admin", 30, "Organization"),
        Link("admin.departments", "admin.sub.org", 10, "Departments", "bi-building", "Department", rule: "Module:Administration"),
        Link("admin.employees", "admin.sub.org", 20, "Employees", "bi-people", "Employee", rule: "Module:Administration"),
        Link("admin.disciplines", "admin.sub.org", 30, "Disc_Master_Nav", "bi-sliders", "Discipline", "Manage", rule: "Module:Administration|Flag:DisciplineMaster"),
        Link("admin.positions", "admin.sub.org", 40, "Org_Positions_Nav", "bi-person-badge", "Organization", "Positions", area: "Admin", rule: "Module:Administration"),
        Link("admin.org", "admin.sub.org", 50, "Org_Title", "bi-diagram-3", "Organization", area: "Admin", rule: "Module:Administration", isDefault: true),
        Sub("admin.sub.ai", "sec.admin", 40, "AI Administration"),
        Link("admin.intake", "admin.sub.ai", 10, "AIIntakeQueue", "bi-inboxes", "Intake", rule: "Module:Administration"),
        Link("admin.ocr", "admin.sub.ai", 20, "OCR Processing", "bi-badge-ad", "OCRProcessing", rule: "Module:Administration&Perm:OCRProcessing.View"),
        Link("admin.aiextraction", "admin.sub.ai", 30, "AI Extraction", "bi-cpu", "AIExtraction", rule: "Module:Administration&Perm:AIExtraction.View"),
        Link("admin.tables", "admin.sub.ai", 40, "Table Extraction", "bi-table", "TableExtraction", rule: "Module:Administration&Perm:TableExtraction.View"),
        Link("admin.relationship", "admin.sub.ai", 50, "RelationshipExplorer", "bi-diagram-3", "Relationship", rule: "Module:Administration&Perm:Relationship.View"),
        Link("admin.routing", "admin.sub.ai", 60, "AI Routing Rules", "bi-signpost-split", "RoutingRules", area: "Admin", rule: "Module:Administration"),
        Link("admin.contractauthority", "admin.sub.ai", 70, "Contract Approval Authority", "bi-shield-check", "ContractApprovalAuthority", area: "Admin", rule: "Module:Administration"),
        // SCRUM-470: the PO authority table
        Link("admin.poauthority", "admin.sub.ai", 75, "Po_AuthorityTitle", "bi-receipt-cutoff", "PurchaseOrderAuthority", area: "Admin", rule: "Module:Administration"),
        Sub("admin.sub.monitoring", "sec.admin", 50, "System Monitoring"),
        Link("admin.auditlog", "admin.sub.monitoring", 10, "AuditLog", "bi-clock-history", "AuditLog", area: "Admin", rule: "Module:Administration"),
        Link("admin.authaudit", "admin.sub.monitoring", 20, "Authorization Audit", "bi-journal-check", "AuthorizationAudit", area: "Admin", rule: "Module:Administration"),
        Link("admin.dbupdate", "admin.sub.monitoring", 25, "Database Update", "bi-database-up", "DatabaseUpdate", area: "Admin", rule: "Module:Administration"),
        Link("admin.license", "admin.sub.monitoring", 26, "Licence", "bi-key", "License", area: "Admin", rule: "Module:Administration"),
        Link("admin.performance", "admin.sub.monitoring", 30, "Performance Testing", "bi-speedometer2", "Performance", rule: "Module:Administration"),
        new("admin.hangfire", "admin.sub.monitoring", L, 40, "Background Jobs Dashboard", "bi-hdd-stack", "Module:Administration",
            ExternalPath: "/hangfire", OpenInNewTab: true),
        Link("admin.projectintel", "admin.sub.monitoring", 50, "Project Intelligence", "bi-graph-up-arrow", "ProjectIntelligence", area: "Admin", rule: "Module:Administration"),
    };
}
