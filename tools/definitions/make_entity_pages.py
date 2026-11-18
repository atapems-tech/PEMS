#!/usr/bin/env python3
"""Writes the definition files of a master-data list + create pair from a short spec (SCRUM-342, the page recipe in guideline 02).

For every spec entry it writes into PEMS.Persistence/Definitions/System: data-source/core/{key}-grid.json, page/core/{key}.json, form/core/{key}-create.json, action/core/{key}-create.json (handler entity.create)
and, for a select field with "options", data-source/core/{key}-{field}-options.json (provider entity-options). The entity itself is registered once in Program.cs (EntityCatalog) with the permissions that guard it.

    python3 tools/definitions/make_entity_pages.py            # writes the files of every spec below
"""
import json, os, re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "PEMS.Persistence", "Definitions", "System")


def t(en, fa):
    return {"en": en, "fa": fa}


GRID_TEXTS = None


def grid_texts():
    global GRID_TEXTS
    if GRID_TEXTS is None:
        page = json.load(open(os.path.join(OUT, "page", "core", "departments.json"), encoding="utf-8"))
        GRID_TEXTS = page["payload"]["regions"]["content"][0]["options"]["texts"]
    return GRID_TEXTS


def write(kind, key, obj):
    path = os.path.join(OUT, kind, "core", key + ".json")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as f:
        json.dump(obj, f, ensure_ascii=False, indent=2)


def envelope(kind, key, title, description, payload):
    return {"kind": kind, "key": key, "appKey": "core", "version": 1, "title": title, "description": description, "payload": payload}


def make(spec):
    key, entity = spec["key"], spec["entity"]
    cols = spec["columns"]
    fields = [c[0] for c in cols]
    search = [c[0] for c in cols if len(c) > 5 and c[5]]
    write("data-source", key + "-grid", envelope("data-source", key + "-grid", t(spec["title"][0] + " rows", "ردیف‌های " + spec["title"][1]), t("The rows of the " + spec["title"][0].lower() + " grid", "ردیف‌های جدول " + spec["title"][1]),
          {"kind": "grid", "provider": "entity", "parameters": {"entity": entity, "fields": fields + [spec["id"]], "search": search}}))

    grid = {"id": key, "url": "/d/" + key + "-grid", "idField": spec["idField"], "selectable": False, "columns": [], "texts": grid_texts()}
    for prop, en, fa, typ, width, *_ in cols:
        c = {"key": prop[0].lower() + prop[1:], "header": t(en, fa), "width": width}
        if typ != "text": c["type"] = typ
        grid["columns"].append(c)
    page = envelope("page", key, t(*spec["title"]), t(spec["title"][0] + " list (page definition)", "فهرست " + spec["title"][1] + " (تعریف صفحه)"), {
        "layout": "list-page", "route": key,
        "regions": {"toolbar": [{"id": "new", "component": "button", "options": {"label": t("New " + spec["singular"][0], spec["singular"][1] + " جدید"), "tone": "primary", "href": "/f/" + key + "-create"}}],
                    "content": [{"id": "grid", "component": "data-grid", "options": grid}]}})
    write("page", key, page)

    form_fields, map_, required = [], {}, []
    for f in spec["form"]:
        prop, kind, en, fa = f["prop"], f.get("kind", "text"), f["en"], f["fa"]
        name = prop[0].lower() + prop[1:]
        d = {"name": name, "label": t(en, fa), "kind": kind}
        for k in ("maxLength", "minLength", "min", "max", "pattern", "span"):
            if k in f: d[k] = f[k]
        if f.get("required"):
            d["required"] = True
            d["messages"] = {"required": t(en + " is required.", fa + " الزامی است.")}
            required.append(prop)
        if "options" in f:
            opt_key = key + "-" + re.sub(r"([a-z0-9])([A-Z])", r"\1-\2", name).lower() + "-options"
            write("data-source", opt_key, envelope("data-source", opt_key, t(en + " options", "گزینه‌های " + fa), t("Choices for " + en.lower(), "گزینه‌ها برای " + fa),
                  {"kind": "provider", "provider": "entity-options", "parameters": f["options"]}))
            d["optionsFrom"] = opt_key
        form_fields.append(d)
        map_[name] = prop
    write("form", key + "-create", envelope("form", key + "-create", t("Create " + spec["singular"][0], "ایجاد " + spec["singular"][1]), t("Create a new " + spec["singular"][0].lower(), "ایجاد " + spec["singular"][1] + " جدید"), {
        "id": key + "-create", "title": t("New " + spec["singular"][0], spec["singular"][1] + " جدید"), "submitLabel": t("Create", "ایجاد"), "actionKey": key + "-create", "columns": 2, "warnOnLeave": True, "fields": form_fields}))
    params = {"entity": entity, "map": map_, "required": required, "defaults": spec.get("defaults", {}), "unique": spec.get("unique", [])}
    write("action", key + "-create", envelope("action", key + "-create", t("Create " + spec["singular"][0], "ایجاد " + spec["singular"][1]), t("Creates the row", "ردیف را ثبت می‌کند"), {
        "kind": "handler", "handler": "entity.create", "parameters": params, "success": t(spec["singular"][0] + " created.", spec["singular"][1] + " ایجاد شد."), "redirect": "/p/" + key}))


def unique(fields, en, fa):
    return {"fields": fields, "message": t(en, fa)}


SPECS = [
    {"key": "uoms", "entity": "MaterialUOM", "id": "MaterialUOMId", "idField": "materialUOMId", "title": ("Units of measure", "واحدهای اندازه‌گیری"), "singular": ("Unit of measure", "واحد اندازه‌گیری"),
     "columns": [("Code", "Code", "کد", "text", 140, True), ("Name", "Name", "نام", "text", 240, True), ("Description", "Description", "توضیحات", "text", 300, True), ("ConversionFactor", "Conversion factor", "ضریب تبدیل", "number", 140), ("IsActive", "Active", "فعال", "bool", 100)],
     "form": [{"prop": "Code", "en": "Code", "fa": "کد", "required": True, "maxLength": 30}, {"prop": "Name", "en": "Name", "fa": "نام", "required": True, "maxLength": 100},
              {"prop": "ConversionFactor", "kind": "number", "en": "Conversion factor", "fa": "ضریب تبدیل"}, {"prop": "Description", "kind": "textArea", "en": "Description", "fa": "توضیحات", "maxLength": 500, "span": 2}],
     "defaults": {"IsActive": True, "CreatedDate": "$now"}, "unique": [unique(["Code"], "A unit with this code already exists.", "واحدی با همین کد از قبل وجود دارد.")]},
    {"key": "material-categories", "entity": "MaterialCategory", "id": "MaterialCategoryId", "idField": "materialCategoryId", "title": ("Material categories", "دسته‌بندی‌های مواد"), "singular": ("Material category", "دسته‌بندی مواد"),
     "columns": [("Code", "Code", "کد", "text", 140, True), ("Name", "Name", "نام", "text", 240, True), ("Description", "Description", "توضیحات", "text", 300, True), ("ParentCategoryId", "Parent", "والد", "number", 100), ("IsActive", "Active", "فعال", "bool", 100)],
     "form": [{"prop": "Code", "en": "Code", "fa": "کد", "required": True, "maxLength": 30}, {"prop": "Name", "en": "Name", "fa": "نام", "required": True, "maxLength": 100},
              {"prop": "ParentCategoryId", "kind": "select", "en": "Parent category", "fa": "دسته‌ی والد", "options": {"entity": "MaterialCategory", "value": "MaterialCategoryId", "label": "Name", "active": "IsActive"}},
              {"prop": "Description", "kind": "textArea", "en": "Description", "fa": "توضیحات", "maxLength": 500, "span": 2}],
     "defaults": {"IsActive": True, "CreatedDate": "$now"}, "unique": [unique(["Code"], "A category with this code already exists.", "دسته‌بندی با همین کد از قبل وجود دارد.")]},
    {"key": "warehouses", "entity": "Warehouse", "id": "WarehouseId", "idField": "warehouseId", "title": ("Warehouses", "انبارها"), "singular": ("Warehouse", "انبار"),
     "columns": [("Code", "Code", "کد", "text", 140, True), ("Name", "Name", "نام", "text", 260, True), ("Description", "Description", "توضیحات", "text", 320, True), ("IsActive", "Active", "فعال", "bool", 100)],
     "form": [{"prop": "Code", "en": "Code", "fa": "کد", "required": True, "maxLength": 30}, {"prop": "Name", "en": "Name", "fa": "نام", "required": True, "maxLength": 100},
              {"prop": "Description", "kind": "textArea", "en": "Description", "fa": "توضیحات", "maxLength": 500, "span": 2}],
     "defaults": {"IsActive": True, "CreatedDate": "$now"}, "unique": [unique(["Code"], "A warehouse with this code already exists.", "انباری با همین کد از قبل وجود دارد.")]},
]

if __name__ == "__main__":
    for s in SPECS:
        make(s)
        print("wrote", s["key"])
