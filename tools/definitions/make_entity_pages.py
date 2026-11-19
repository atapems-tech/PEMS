#!/usr/bin/env python3
"""Writes the definition files of a master-data page from a short spec (SCRUM-342/437, the page recipe in guideline 02). No per-entity code: the entity is registered once in EntityCatalogDefaults
(CLR type, permissions and roles, readable and writable properties); everything else is generated here.

For an entry of SPECS it writes into PEMS.Persistence/Definitions/System:
  data-source/core/{key}-grid.json     the rows (provider entity: fields, search, default sort, related counts)
  page/core/{key}.json                 list-page: a "new" button, a data-grid with an actions column (edit, activate/deactivate, delete)
  form/core/{key}-create.json + action/core/{key}-create.json    (handler entity.create)
  form/core/{key}-edit.json   + action/core/{key}-edit.json      (handler entity.update; the form loads the row at /f/{key}-edit/{id})
  action/core/{key}-toggle.json, action/core/{key}-delete.json   (handlers entity.toggle, entity.delete)
  data-source/core/{key}-{field}-options.json for a select field with "options" (provider entity-options)

    python3 tools/definitions/make_entity_pages.py
"""
import json, os, re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "PEMS.Persistence", "Definitions", "System")


def t(en, fa):
    return {"en": en, "fa": fa}


def kebab(name):
    return re.sub(r"([a-z0-9])([A-Z])", r"\1-\2", name).lower()


def camel(name):
    return name[0].lower() + name[1:]


def write(kind, key, obj):
    path = os.path.join(OUT, kind, "core", key + ".json")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as f:
        json.dump(obj, f, ensure_ascii=False, indent=2)


def envelope(kind, key, title, description, payload):
    return {"kind": kind, "key": key, "appKey": "core", "version": 1, "title": title, "description": description, "payload": payload}


def form_fields(spec, key):
    fields, map_, required = [], {}, []
    for f in spec["form"]:
        prop, kind, en, fa = f["prop"], f.get("kind", "text"), f["en"], f["fa"]
        name = camel(prop)
        d = {"name": name, "label": t(en, fa), "kind": kind}
        for k in ("maxLength", "minLength", "min", "max", "pattern", "span"):
            if k in f: d[k] = f[k]
        if "messages" in f: d["messages"] = dict(f["messages"])
        if f.get("required"):
            d["required"] = True
            d.setdefault("messages", {})["required"] = t(en + " is required.", fa + " الزامی است.")
            required.append(prop)
        if "options" in f:
            opt_key = key + "-" + kebab(name) + "-options"
            write("data-source", opt_key, envelope("data-source", opt_key, t(en + " options", "گزینه‌های " + fa), t("Choices for " + en.lower(), "گزینه‌ها برای " + fa), {"kind": "provider", "provider": "entity-options", "parameters": f["options"]}))
            d["optionsFrom"] = opt_key
        fields.append(d)
        map_[name] = prop
    return fields, map_, required


def action(key, title, handler, params, redirect, success):
    write("action", key, envelope("action", key, title, title, {"kind": "handler", "handler": handler, "parameters": params, "success": success, "redirect": redirect}))


def make(spec):
    key, entity = spec["key"], spec["entity"]
    cols = spec["columns"]
    fields = [c[0] for c in cols if c[3] not in ("computed", "lookup")]
    for l in spec.get("lookups", []):
        if l["foreignKey"] not in fields: fields.append(l["foreignKey"])
    search = [c[0] for c in cols if len(c) > 5 and c[5]]
    params = {"entity": entity, "fields": fields, "search": search}
    if spec.get("sort"): params["sort"] = spec["sort"]
    if spec.get("counts"): params["counts"] = spec["counts"]
    if spec.get("lookups"): params["lookups"] = spec["lookups"]
    for k in ("where", "presets", "apply"):
        if spec.get(k): params[k] = spec[k]
    write("data-source", key + "-grid", envelope("data-source", key + "-grid", t(spec["title"][0] + " rows", "ردیف‌های " + spec["title"][1]), t("The rows of the " + spec["title"][0].lower() + " grid", "ردیف‌های جدول " + spec["title"][1]),
          {"kind": "grid", "provider": "entity", "parameters": params}))

    grid = {"id": key, "url": "/d/" + key + "-grid", "idField": camel(spec["id"]), "viewsKey": key, "columns": []}   # the args of the entity-list component (SCRUM-589); saved views per grid (SCRUM-599)
    for prop, en, fa, typ, width, *_ in cols:
        c = {"key": camel(prop), "header": t(en, fa), "width": width}
        if typ == "computed": c["type"] = "number"
        elif typ == "lookup": pass
        elif typ != "text": c["type"] = typ
        grid["columns"].append(c)

    row_actions, toolbar = [], []
    if spec.get("create", True):
        grid["newUrl"] = "/f/" + key + "-create"
        grid["newLabel"] = t("New " + spec["singular"][0].lower(), spec["singular"][1] + " جدید")
        ff, map_, required = form_fields(spec, key)
        create_params = {"entity": entity, "map": map_, "required": required, "defaults": spec.get("defaults", {}), "unique": spec.get("unique", [])}
        if spec.get("create_redirect"): create_params["redirect"] = spec["create_redirect"]
        if spec.get("create_deny"): create_params["deny"] = spec["create_deny"]
        if spec.get("validate"): create_params["validate"] = {"model": spec["validate"]["model"], "action": spec["validate"]["action"], "args": spec["validate"]["args"]}
        write("form", key + "-create", envelope("form", key + "-create", t("Create " + spec["singular"][0].lower(), "ایجاد " + spec["singular"][1]), t("Create a new " + spec["singular"][0].lower(), "ایجاد " + spec["singular"][1] + " جدید"), {
            "id": key + "-create", "title": t("New " + spec["singular"][0].lower(), spec["singular"][1] + " جدید"), "submitLabel": t("Create", "ایجاد"), "actionKey": key + "-create", "columns": 2, "warnOnLeave": True, "fields": ff}))
        action(key + "-create", t("Create " + spec["singular"][0].lower(), "ایجاد " + spec["singular"][1]), "entity.create", create_params, "/p/" + key, t(spec["singular"][0] + " created.", spec["singular"][1] + " ایجاد شد."))
    if spec.get("edit", False):
        ff, map_, required = form_fields(spec, key)
        ro = spec.get("readonly_on_edit", [])
        for f in ff:
            if f["name"] in ro: f["readOnly"] = True
        edit_map = {k: v for k, v in map_.items() if k not in ro}
        edit_required = [r for r in required if camel(r) not in ro]
        edit_params = {"entity": entity, "map": edit_map, "required": edit_required, "unique": spec.get("unique", [])}
        if spec.get("validate"): edit_params["validate"] = {"model": spec["validate"]["model"], "action": spec["validate"]["action"], "args": {**spec["validate"]["args"], "ruleId": "field:id"}}
        write("form", key + "-edit", envelope("form", key + "-edit", t("Edit " + spec["singular"][0].lower(), "ویرایش " + spec["singular"][1]), t("Edit " + spec["singular"][0].lower(), "ویرایش " + spec["singular"][1]), {
            "id": key + "-edit", "title": t("Edit " + spec["singular"][0].lower(), "ویرایش " + spec["singular"][1]), "submitLabel": t("Save", "ذخیره"), "actionKey": key + "-edit", "columns": 2, "warnOnLeave": True, "fields": ff}))
        action(key + "-edit", t("Edit " + spec["singular"][0].lower(), "ویرایش " + spec["singular"][1]), "entity.update", edit_params, "/p/" + key, t(spec["singular"][0] + " saved.", spec["singular"][1] + " ذخیره شد."))
        row_actions.append({"key": "edit", "label": t("Edit", "ویرایش"), "url": "/f/" + key + "-edit/{id}", "method": "get"})
    if spec.get("toggle"):
        action(key + "-toggle", t("Activate or deactivate", "فعال یا غیرفعال کردن"), "entity.toggle", {"entity": entity, "property": spec["toggle"]}, "/p/" + key, t("Status updated.", "وضعیت به‌روز شد."))
        row_actions.append({"key": "toggle", "label": t("Activate / deactivate", "فعال / غیرفعال"), "url": "/a/" + key + "-toggle", "method": "post", "confirm": t("Change the status?", "وضعیت تغییر کند؟")})
    if spec.get("deactivate") and not spec.get("delete"):
        spec["delete"] = {"deactivate": spec["deactivate"], "label": ("Deactivate", "غیرفعال کردن")}
    if spec.get("delete"):
        d = spec["delete"]
        dp = {"entity": entity, "deny": d.get("deny", [])}
        if d.get("deactivate"): dp["deactivate"] = d["deactivate"]
        verb = d.get("label", ("Delete", "حذف"))
        action(key + "-delete", t(*verb), "entity.delete", dp, "/p/" + key, t(spec["singular"][0] + (" deactivated." if d.get("deactivate") else " deleted."), spec["singular"][1] + (" غیرفعال شد." if d.get("deactivate") else " حذف شد.")))
        ra = {"key": "delete", "label": t(*verb), "url": "/a/" + key + "-delete", "method": "post", "tone": "danger", "confirm": t(verb[0] + " this record?", "این رکورد " + verb[1] + " شود؟")}
        if d.get("showWhen"): ra["showWhen"] = d["showWhen"]
        row_actions.append(ra)
    if row_actions:
        grid["columns"].append({"key": "_actions", "header": t("Actions", "عملیات"), "type": "actions", "width": 280, "sortable": False, "filterable": False, "actions": row_actions})

    write("page", key, envelope("page", key, t(*spec["title"]), t(spec["title"][0] + " list (page definition)", "فهرست " + spec["title"][1] + " (تعریف صفحه)"), {"layout": "list-page", "route": key, "template": "entity-list", "args": grid}))
    # the details page of a record: the entity-details component over the same rows (SCRUM-600); the id comes from the request (/p/{key}-details?id=7)
    if spec.get("details", True) and cols:
        dfields = []
        for prop, en, fa, typ, width, *_ in cols:
            f = {"key": camel(prop), "label": t(en, fa)}
            if typ in ("bool", "date", "datetime", "number"): f["type"] = typ
            elif typ == "computed": f["type"] = "number"
            if prop.endswith("Description"): f["wide"] = True
            dfields.append(f)
        dargs = {"id": key + "-record", "url": "/d/" + key + "-grid", "idField": camel(spec["id"]), "fields": dfields, "backUrl": "/p/" + key, "backLabel": t("Back to the list", "بازگشت به فهرست")}
        if spec.get("edit", False):
            dargs["editUrl"] = "/f/" + key + "-edit"
            dargs["editLabel"] = t("Edit", "ویرایش")
        write("page", key + "-details", envelope("page", key + "-details", t(spec["title"][0] + " details", "جزئیات " + spec["title"][1]), t("Details of one record (page definition)", "جزئیات یک رکورد (تعریف صفحه)"),
                                                  {"layout": "detail-page", "template": "entity-details", "args": dargs}))


def variant_page(spec, key, title, apply):
    """A second page over the same rows with a preset always applied (the data source names it)."""
    base = json.load(open(os.path.join(OUT, "data-source", "core", spec["key"] + "-grid.json"), encoding="utf-8"))
    base["key"] = key + "-grid"
    base["title"] = t(title[0] + " rows", "ردیف‌های " + title[1])
    base["payload"]["parameters"]["apply"] = [apply]
    write("data-source", key + "-grid", base)
    page = json.load(open(os.path.join(OUT, "page", "core", spec["key"] + ".json"), encoding="utf-8"))
    page["key"] = key
    page["title"] = t(*title)
    page["payload"]["route"] = key
    page["payload"]["args"]["id"] = key
    page["payload"]["args"]["url"] = "/d/" + key + "-grid"
    write("page", key, page)


def unique(fields, en, fa, ignore_empty=False):
    u = {"fields": fields, "message": t(en, fa)}
    if ignore_empty: u["ignoreEmpty"] = True
    return u


def system_role_names():
    text = open(os.path.join(ROOT, "PEMS.Persistence", "Identity", "SystemRoles.cs"), encoding="utf-8-sig").read()
    return sorted(set(re.findall(r'public const string \w+ = "(\w+)";', text)))


def master(key, entity, id_, title, singular, extra_cols=None, parent=None):
    cols = [("Code", "Code", "کد", "text", 140, True), ("Name", "Name", "نام", "text", 240, True), ("Description", "Description", "توضیحات", "text", 300, True)] + (extra_cols or []) + [("IsActive", "Active", "فعال", "bool", 100)]
    form = [{"prop": "Code", "en": "Code", "fa": "کد", "required": True, "maxLength": 30}, {"prop": "Name", "en": "Name", "fa": "نام", "required": True, "maxLength": 100}]
    if parent: form.append(parent)
    form.append({"prop": "Description", "kind": "textArea", "en": "Description", "fa": "توضیحات", "maxLength": 500, "span": 2})
    return {"key": key, "entity": entity, "id": id_, "title": title, "singular": singular, "columns": cols, "form": form, "edit": True, "readonly_on_edit": ["code"], "toggle": "IsActive",
            "defaults": {"IsActive": True, "CreatedDate": "$now"}, "unique": [unique(["Code"], "This code already exists.", "این کد از قبل وجود دارد.")]}


SPECS = [
    # ---- W1 administration
    {"key": "departments", "entity": "Department", "id": "DepartmentId", "title": ("Departments", "دپارتمان‌ها"), "singular": ("Department", "دپارتمان"),
     "columns": [("Code", "Code", "کد", "text", 140, True), ("Name", "Name", "نام", "text", 260, True), ("Description", "Description", "توضیحات", "text", 320, True), ("Employees", "Active employees", "کارکنان فعال", "computed", 140), ("IsActive", "Active", "فعال", "bool", 100)],
     "sort": ["Name"], "counts": [{"name": "employees", "entity": "Employee", "foreignKey": "DepartmentId", "where": {"IsActive": True}}],
     "form": [{"prop": "Code", "en": "Code", "fa": "کد", "maxLength": 20, "pattern": "[A-Za-z0-9_-]+", "messages": {"pattern": t("The code may only contain letters, digits, hyphen and underscore.", "کد فقط می‌تواند شامل حروف انگلیسی، عدد، خط تیره و زیرخط باشد.")}},
              {"prop": "Name", "en": "Name", "fa": "نام", "required": True, "minLength": 2, "maxLength": 100}, {"prop": "Description", "kind": "textArea", "en": "Description", "fa": "توضیحات", "maxLength": 500, "span": 2}],
     "edit": True, "toggle": "IsActive", "defaults": {"IsActive": True, "CreatedDate": "$now"},
     "unique": [unique(["Name"], "A department with this name already exists.", "دپارتمانی با همین نام از قبل وجود دارد."), unique(["Code"], "A department with this code already exists.", "دپارتمانی با همین کد از قبل وجود دارد.", True)]},
    {"key": "roles", "entity": "ApplicationRole", "id": "Id", "title": ("Roles", "نقش‌ها"), "singular": ("Role", "نقش"),
     "columns": [("Name", "Name", "نام", "text", 180, True), ("DisplayName", "Display name", "نام نمایشی", "text", 200, True), ("Description", "Description", "توضیحات", "text", 280, True), ("Category", "Category", "دسته", "text", 120), ("Domain", "Domain", "حوزه", "text", 120),
                 ("IsSystemRole", "System role", "نقش سیستمی", "bool", 110), ("Users", "Users", "کاربران", "computed", 100), ("Permissions", "Permissions", "مجوزها", "computed", 110), ("SortOrder", "Order", "ترتیب", "number", 90)],
     "sort": ["SortOrder", "Name"], "counts": [{"name": "users", "entity": "IdentityUserRole", "foreignKey": "RoleId"}, {"name": "permissions", "entity": "RolePermissionGrant", "foreignKey": "RoleId", "where": {"IsActive": True}}],
     "form": [{"prop": "Name", "en": "Role name (English, no spaces)", "fa": "نام نقش (انگلیسی، بدون فاصله)", "required": True, "minLength": 2, "maxLength": 100, "pattern": "[A-Za-z][A-Za-z0-9_]*",
               "messages": {"pattern": t("Use English letters, digits and underscore, starting with a letter.", "فقط حروف انگلیسی، عدد و زیرخط؛ با حرف شروع شود.")}},
              {"prop": "DisplayName", "en": "Display name", "fa": "نام نمایشی", "maxLength": 200}, {"prop": "Description", "kind": "textArea", "en": "Description", "fa": "توضیحات", "maxLength": 500, "span": 2}],
     "unique": [unique(["Name"], "A role with this name already exists.", "نقشی با این نام از قبل وجود دارد.")], "create_redirect": "/Admin/PermissionMatrix#role-{Name}",
     "delete": {"deny": [{"when": "row.IsSystemRole or row.Name in " + json.dumps(system_role_names()).replace('"', "'"), "message": t("System roles cannot be deleted.", "نقش‌های سیستمی حذف نمی‌شوند.")}], "showWhen": "row.isSystemRole == false"}},
    {"key": "security-events", "entity": "SecurityEvent", "id": "SecurityEventId", "title": ("Security events", "رویدادهای امنیتی"), "singular": ("Security event", "رویداد امنیتی"), "create": False,
     "columns": [("OccurredAtUtc", "When (UTC)", "زمان (UTC)", "datetime", 170), ("EventType", "Event", "رویداد", "text", 170, True), ("Method", "Method", "روش", "text", 150, True), ("Result", "Result", "نتیجه", "text", 100),
                 ("FailureReason", "Failure reason", "دلیل شکست", "text", 180, True), ("ActorUserName", "Actor", "انجام‌دهنده", "text", 150, True), ("TargetUserName", "Target account", "حساب هدف", "text", 150, True), ("IpAddress", "IP address", "نشانی IP", "text", 130)],
     "sort": ["-OccurredAtUtc"], "form": []},
    # ---- W4 inventory master data
    master("uoms", "MaterialUOM", "MaterialUOMId", ("Units of measure", "واحدهای اندازه‌گیری"), ("Unit of measure", "واحد اندازه‌گیری"), extra_cols=[("ConversionFactor", "Conversion factor", "ضریب تبدیل", "number", 140)]),
    master("material-categories", "MaterialCategory", "MaterialCategoryId", ("Material categories", "دسته‌بندی‌های مواد"), ("Material category", "دسته‌بندی مواد"),
           parent={"prop": "ParentCategoryId", "kind": "select", "en": "Parent category", "fa": "دسته‌ی والد", "options": {"entity": "MaterialCategory", "value": "MaterialCategoryId", "label": "Name", "active": "IsActive"}},
           extra_cols=[("ParentCategoryId", "Parent", "والد", "number", 100)]),
    master("warehouses", "Warehouse", "WarehouseId", ("Warehouses", "انبارها"), ("Warehouse", "انبار")),
]


def simple(key, entity, id_, title, singular, columns, form, unique_rules, lookups=None, edit=True, readonly_on_edit=None, toggle="IsActive", deactivate=None, where=None, presets=None):
    return {"deactivate": deactivate, "where": where, "presets": presets, "key": key, "entity": entity, "id": id_, "title": title, "singular": singular, "columns": columns, "form": form, "edit": edit, "readonly_on_edit": readonly_on_edit or [], "toggle": toggle,
            "defaults": {"IsActive": True, "CreatedDate": "$now"}, "unique": unique_rules, "lookups": lookups or []}


def select(prop, en, fa, entity, value, label, required=False, active="IsActive"):
    d = {"prop": prop, "kind": "select", "en": en, "fa": fa, "options": {"entity": entity, "value": value, "label": label}}
    if active: d["options"]["active"] = active
    if required: d["required"] = True
    return d


DISCIPLINE = {"name": "discipline", "entity": "Discipline", "foreignKey": "DisciplineId", "label": "Name"}
SPECS += [
    # ---- W2 maintenance master data (MaintenanceMasterData permissions)
    simple("maintenance-groups", "MaintenanceGroup", "MaintenanceGroupId", ("Maintenance groups", "گروه‌های تعمیراتی"), ("Maintenance group", "گروه تعمیراتی"),
           [("Code", "Code", "کد", "text", 140, True), ("Name", "Name", "نام", "text", 240, True), ("Discipline", "Discipline", "دیسیپلین", "lookup", 180), ("Description", "Description", "توضیحات", "text", 300, True), ("IsActive", "Active", "فعال", "bool", 100)],
           [{"prop": "Code", "en": "Code", "fa": "کد", "required": True, "maxLength": 30}, {"prop": "Name", "en": "Name", "fa": "نام", "required": True, "maxLength": 100},
            select("DisciplineId", "Discipline", "دیسیپلین", "Discipline", "DisciplineId", "Name", True), {"prop": "Description", "kind": "textArea", "en": "Description", "fa": "توضیحات", "maxLength": 500, "span": 2}],
           [unique(["Name"], "A maintenance group with this name already exists.", "گروه تعمیراتی با همین نام از قبل وجود دارد."), unique(["Code"], "A maintenance group with this code already exists.", "گروه تعمیراتی با همین کد از قبل وجود دارد.")],
           lookups=[DISCIPLINE], readonly_on_edit=["code"]),
    simple("skills", "Skill", "SkillId", ("Skills", "مهارت‌ها"), ("Skill", "مهارت"),
           [("Name", "Name", "نام", "text", 260, True), ("Discipline", "Discipline", "دیسیپلین", "lookup", 180), ("Description", "Description", "توضیحات", "text", 320, True), ("IsActive", "Active", "فعال", "bool", 100)],
           [{"prop": "Name", "en": "Name", "fa": "نام", "required": True, "maxLength": 100}, select("DisciplineId", "Discipline", "دیسیپلین", "Discipline", "DisciplineId", "Name"),
            {"prop": "Description", "kind": "textArea", "en": "Description", "fa": "توضیحات", "maxLength": 500, "span": 2}],
           [unique(["Name"], "A skill with this name already exists.", "مهارتی با همین نام از قبل وجود دارد.")], lookups=[DISCIPLINE]),
    simple("qualifications", "Qualification", "QualificationId", ("Qualifications", "صلاحیت‌ها"), ("Qualification", "صلاحیت"),
           [("Name", "Name", "نام", "text", 260, True), ("Discipline", "Discipline", "دیسیپلین", "lookup", 180), ("Description", "Description", "توضیحات", "text", 320, True), ("IsActive", "Active", "فعال", "bool", 100)],
           [{"prop": "Name", "en": "Name", "fa": "نام", "required": True, "maxLength": 100}, select("DisciplineId", "Discipline", "دیسیپلین", "Discipline", "DisciplineId", "Name"),
            {"prop": "Description", "kind": "textArea", "en": "Description", "fa": "توضیحات", "maxLength": 500, "span": 2}],
           [unique(["Name"], "A qualification with this name already exists.", "صلاحیتی با همین نام از قبل وجود دارد.")], lookups=[DISCIPLINE]),
    # ---- W1 administration: audit trails (read only) and separation of duties (SCRUM-629, 630, 647)
    {"key": "audit-log", "entity": "AuditLog", "id": "AuditLogId", "title": ("Audit log", "گزارش ممیزی"), "singular": ("Audit entry", "رکورد ممیزی"), "create": False,
     "columns": [("AuditLogId", "Id", "شناسه", "number", 80), ("Timestamp", "When", "زمان", "datetime", 170), ("EntityName", "Entity", "موجودیت", "text", 170, True), ("EntityKeyValue", "Key", "کلید", "text", 120, True), ("Action", "Action", "عملیات", "text", 100),
                 ("ChangedFieldsSummary", "Changed fields", "فیلدهای تغییریافته", "text", 420), ("UserName", "User", "کاربر", "text", 150, True), ("IsAIAssisted", "AI assisted", "با کمک هوش مصنوعی", "bool", 130)],
     "sort": ["-Timestamp", "-AuditLogId"], "form": []},
    {"key": "authorization-audit", "entity": "AuthorizationAuditLog", "id": "AuthorizationAuditLogId", "title": ("Authorization audit", "ممیزی دسترسی"), "singular": ("Authorization decision", "تصمیم دسترسی"), "create": False,
     "columns": [("AuthorizationAuditLogId", "Id", "شناسه", "number", 80), ("Timestamp", "When", "زمان", "datetime", 170), ("User", "User", "کاربر", "lookup", 150), ("ResourceName", "Resource", "منبع", "text", 170, True), ("ActionName", "Action", "عملیات", "text", 120, True), ("ResourceId", "Resource id", "شناسه‌ی منبع", "text", 120, True),
                 ("Decision", "Decision", "تصمیم", "text", 100), ("Reason", "Reason", "دلیل", "text", 320), ("IpAddress", "IP address", "نشانی IP", "text", 130)],
     "lookups": [{"name": "user", "entity": "ApplicationUser", "foreignKey": "UserId", "label": "UserName"}],
     "sort": ["-Timestamp", "-AuthorizationAuditLogId"], "form": []},
    dict(simple("sod-policies", "SoDPolicy", "SoDPolicyId", ("Separation of duties", "تفکیک وظایف"), ("Separation policy", "سیاست تفکیک وظایف"),
                [("PermissionA", "Permission A", "مجوز اول", "lookup", 260), ("PermissionB", "Permission B", "مجوز دوم", "lookup", 260), ("Description", "Description", "توضیحات", "text", 320), ("IsActive", "Active", "فعال", "bool", 100)],
                [select("PermissionAId", "Permission A", "مجوز اول", "Permission", "PermissionId", "Name", True), select("PermissionBId", "Permission B", "مجوز دوم", "Permission", "PermissionId", "Name", True),
                 {"prop": "Description", "kind": "textArea", "en": "Description", "fa": "توضیحات", "maxLength": 500, "span": 2}],
                [{"fields": ["PermissionAId", "PermissionBId"], "symmetric": True, "where": {"IsActive": True}, "message": t("An active policy for this permission pair already exists.", "برای این جفت مجوز از قبل سیاست فعالی وجود دارد.")}],
                lookups=[{"name": "permissionA", "entity": "Permission", "foreignKey": "PermissionAId", "label": "Name"}, {"name": "permissionB", "entity": "Permission", "foreignKey": "PermissionBId", "label": "Name"}], edit=False),
         create_deny=[{"when": "values.permissionAId == values.permissionBId", "message": t("A permission cannot conflict with itself.", "یک مجوز نمی‌تواند با خودش تعارض داشته باشد.")}]),
    # ---- W1 administration: contract approval authority (SCRUM-632): service rules run as the validation operation of the create and edit actions
    dict(simple("contract-approval-authority", "ContractApprovalAuthorityRule", "ContractApprovalAuthorityRuleId", ("Contract approval authority", "اختیار تأیید قرارداد"), ("Approval rule", "قاعده‌ی تأیید"),
                [("ContractApprovalAuthorityRuleId", "Id", "شناسه", "number", 70), ("Priority", "Priority", "اولویت", "number", 100), ("MinValue", "From value", "از مبلغ", "number", 140), ("MaxValue", "To value", "تا مبلغ", "number", 140), ("CriticalityClass", "Criticality", "بحرانی‌بودن", "text", 120),
                 ("RequiredApproverRole", "Approver role", "نقش تأییدکننده", "text", 200, True), ("Description", "Description", "توضیحات", "text", 300, True), ("IsActive", "Active", "فعال", "bool", 100)],
                [{"prop": "MinValue", "kind": "number", "en": "From value", "fa": "از مبلغ", "min": 0}, {"prop": "MaxValue", "kind": "number", "en": "To value", "fa": "تا مبلغ", "min": 0},
                 {"prop": "CriticalityClass", "en": "Criticality class", "fa": "کلاس بحرانی‌بودن", "maxLength": 10},
                 select("RequiredApproverRole", "Approver role", "نقش تأییدکننده", "ApplicationRole", "Name", "Name", True, active=None),
                 {"prop": "Priority", "kind": "number", "en": "Priority (smaller is checked first)", "fa": "اولویت (کوچک‌تر اول بررسی می‌شود)", "min": 0},
                 {"prop": "Description", "kind": "textArea", "en": "Description", "fa": "توضیحات", "maxLength": 500, "span": 2}],
                []),
         defaults={"IsActive": True}, sort=["Priority", "-ContractApprovalAuthorityRuleId"],
         validate={"model": "ContractApprovalRule", "action": "Validate", "args": {"minValue": "field:minValue", "maxValue": "field:maxValue", "criticalityClass": "field:criticalityClass", "requiredApproverRole": "field:requiredApproverRole"}}),
    # ---- W4 storage locations (InventoryMasterData permissions)
    simple("storage-locations", "StorageLocation", "StorageLocationId", ("Storage locations", "محل‌های نگهداری"), ("Storage location", "محل نگهداری"),
           [("Code", "Code", "کد", "text", 140, True), ("Name", "Name", "نام", "text", 240, True), ("Warehouse", "Warehouse", "انبار", "lookup", 180), ("Parent", "Parent location", "محل والد", "lookup", 180), ("IsActive", "Active", "فعال", "bool", 100)],
           [{"prop": "Code", "en": "Code", "fa": "کد", "required": True, "maxLength": 30}, {"prop": "Name", "en": "Name", "fa": "نام", "required": True, "maxLength": 100},
            select("WarehouseId", "Warehouse", "انبار", "Warehouse", "WarehouseId", "Name", True), select("ParentLocationId", "Parent location", "محل والد", "StorageLocation", "StorageLocationId", "Name")],
           [unique(["WarehouseId", "Code"], "A location with this code already exists in this warehouse.", "محل نگهداری با همین کد در این انبار از قبل وجود دارد.")],
           lookups=[{"name": "warehouse", "entity": "Warehouse", "foreignKey": "WarehouseId", "label": "Name"}, {"name": "parent", "entity": "StorageLocation", "foreignKey": "ParentLocationId", "label": "Name"}], readonly_on_edit=["code", "warehouseId"]),
]

SPECS += [
    simple("spare-parts", "SparePart", "SparePartId", ("Spare parts", "قطعات یدکی"), ("Spare part", "قطعه‌ی یدکی"),
           [("PartNumber", "Part number", "شماره‌ی قطعه", "text", 160, True), ("PartName", "Name", "نام", "text", 240, True), ("Manufacturer", "Manufacturer", "سازنده", "text", 160, True), ("Model", "Model", "مدل", "text", 140),
            ("Unit", "Unit", "واحد", "text", 90), ("QuantityOnHand", "On hand", "موجودی", "number", 110), ("MinStockLevel", "Minimum", "حداقل", "number", 100), ("ReorderPoint", "Reorder point", "نقطه‌ی سفارش", "number", 120), ("StorageLocation", "Location", "محل نگهداری", "text", 160, True)],
           [{"prop": "PartNumber", "en": "Part number", "fa": "شماره‌ی قطعه", "required": True, "maxLength": 100}, {"prop": "PartName", "en": "Name", "fa": "نام", "required": True, "maxLength": 200},
            {"prop": "Manufacturer", "en": "Manufacturer", "fa": "سازنده", "maxLength": 200}, {"prop": "Model", "en": "Model", "fa": "مدل", "maxLength": 200}, {"prop": "Unit", "en": "Unit", "fa": "واحد", "maxLength": 30},
            {"prop": "StorageLocation", "en": "Storage location", "fa": "محل نگهداری", "maxLength": 200}, {"prop": "QuantityOnHand", "kind": "number", "en": "Quantity on hand", "fa": "موجودی", "min": 0},
            {"prop": "MinStockLevel", "kind": "number", "en": "Minimum stock level", "fa": "حداقل موجودی", "min": 0}, {"prop": "ReorderPoint", "kind": "number", "en": "Reorder point", "fa": "نقطه‌ی سفارش مجدد", "min": 0},
            {"prop": "Description", "kind": "textArea", "en": "Description", "fa": "توضیحات", "maxLength": 1000, "span": 2}],
           [], toggle=False, deactivate="IsActive", where={"IsActive": True}, presets={"lowStock": {"left": "QuantityOnHand", "op": "lte", "right": "ReorderPoint"}}),
]

if __name__ == "__main__":
    for s in SPECS:
        make(s)
        print("wrote", s["key"])
        if s["key"] == "spare-parts":
            variant_page(s, "spare-parts-low-stock", ("Spare parts at or below the reorder point", "قطعات در نقطه‌ی سفارش یا کمتر"), "lowStock")
            print("wrote spare-parts-low-stock")
