#!/usr/bin/env python3
"""Static UI pattern audit (read-only, stdlib only).

Scans PEMS.Web/**/*.cshtml and classifies every view by UI pattern, counts hand-written markup that a shared
component should own, and proposes a target component set and a migration wave.

    python tools/UiAudit/pattern-audit.py            # writes docs/design/ui-pattern-audit.csv and prints the summary
    python tools/UiAudit/pattern-audit.py --md       # also (re)writes docs/design/UI_PATTERN_AUDIT.md

It complements the browser audit (tools/UiAudit/audit.mjs, runtime checks) with a source-level inventory.
"""
import csv, re, sys
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WEB = ROOT / "PEMS.Web"
OUT_CSV = ROOT / "docs" / "design" / "ui-pattern-audit.csv"
OUT_MD = ROOT / "docs" / "design" / "UI_PATTERN_AUDIT.md"

# Domain -> migration wave (W1 is the pilot: Admin + Account + shared shell).
WAVE_RULES = [
    ("W1 Admin, account, shell", r"^(Admin/|Account/|Shared/|Home/|Settings/|Error|Notifications?/|Help/|Setup/|QuickSetup/|MyWork/|Search|GlobalSearch/|Profile/)"),
    ("W2 CMMS / maintenance / reliability", r"(Cmms|WorkOrder|WorkRequest|Maintenance|Equipment|Asset|Rcm|Reliab|Criticality|Inspection|Kpi|FunctionalLocation|Permit|Instrument|Failure|Calibration|Shift|Jobplan|JobPlan|Meter|Barcode|Spare|Crew|Team|Skill|Condition|PM)"),
    ("W3 Engineering documents, VDR, letters", r"(Document|Engineering|Vdr|VDR|Letter|Transmittal|Mdb|MDR|Revision|Import|Intake|OCR|AIExtraction|AISearch|AIInsights|Chat|Relationship|TableExtraction|Moc|MOC|Tq|Technical|Meeting|Task)"),
    ("W4 Procurement, inventory, warehouse", r"(Procure|Contract|Tender|Material|Inventory|Warehouse|Stock|Irn|IRN|Vendor|PartCatalogue|Requisition|Purchase|Replenish|Mrs|MRS)"),
    ("W5 Operations, production, organization, other", r".*"),
]

RE = {
    "table": re.compile(r"<table\b", re.I),
    "form": re.compile(r"<form\b", re.I),
    "input": re.compile(r"<(input|select|textarea)\b", re.I),
    "form_control": re.compile(r'class="[^"]*\bform-(control|select)\b', re.I),
    "asp_for": re.compile(r"\basp-for=", re.I),
    "modal": re.compile(r'class="[^"]*\bmodal\b[^"]*fade|class="modal fade', re.I),
    "offcanvas": re.compile(r'class="[^"]*\boffcanvas\b', re.I),
    "tabs": re.compile(r"nav-tabs|data-bs-toggle=\"tab\"|data-bs-toggle='tab'", re.I),
    "chart": re.compile(r"<canvas\b|pems-charts|new Chart\(", re.I),
    "kpi": re.compile(r"_KpiCard|kpi-card|class=\"[^\"]*\bkpi\b", re.I),
    "pagination": re.compile(r"_Pagination|\bpagination\b", re.I),
    "tree": re.compile(r"\btree\b|org-chart|treeview", re.I),
    "file": re.compile(r'type="file"', re.I),
    "badge": re.compile(r'class="[^"]*\bbadge\b|\bvs-pill\b', re.I),
    "inline_style": re.compile(r'\sstyle="', re.I),
    "style_block": re.compile(r"<style\b", re.I),
    "script_block": re.compile(r"<script\b(?![^>]*\bsrc=)", re.I),
    "vs": re.compile(r"\bvs-[a-z]", re.I),
    "page_header": re.compile(r"_PageHeader|vs-header|admin-hero", re.I),
    "empty_state": re.compile(r"empty-state|no (records|data|items)|\bEmptyState\b", re.I),
    "skeleton": re.compile(r"skeleton|placeholder-glow|spinner-border|_PemsLoadingOverlay", re.I),
    "cdn": re.compile(r"https?://(cdn\.|cdnjs|unpkg|fonts\.googleapis|code\.jquery|stackpath)", re.I),
    "partial": re.compile(r"<partial\b|Html\.(Partial|RenderPartial)", re.I),
}


def wave_of(key: str) -> str:
    for name, pattern in WAVE_RULES:
        if re.search(pattern, key):
            return name
    return WAVE_RULES[-1][0]


def classify(f: dict, is_partial: bool) -> str:
    if is_partial:
        return "partial"
    if f["kpi"] or f["chart"]:
        return "dashboard"
    if f["table"] and (f["form"] or f["pagination"] or f["input"]):
        return "list"
    if f["table"]:
        return "table-only"
    if f["tabs"] or (f["badge"] and not f["form"]):
        return "detail"
    if f["form"] and f["input"] >= 3:
        return "form"
    if f["form"]:
        return "form-small"
    return "other"


TARGETS = {
    "dashboard": "PageShell + KpiTile + Chart + DataTable",
    "list": "PageShell + FilterBar + DataTable + Pagination + EmptyState + StatusBadge",
    "table-only": "DataTable + EmptyState",
    "detail": "PageShell + InfoGrid + Tabs + StatusBadge",
    "form": "PageShell + FormSection + Field components + ActionBar",
    "form-small": "Field components + ActionBar",
    "partial": "review: absorb into a component or keep",
    "other": "PageShell",
}


def main() -> None:
    rows = []
    for path in sorted(WEB.rglob("*.cshtml")):
        rel = path.relative_to(WEB).as_posix()
        if rel.startswith(("bin/", "obj/", "wwwroot/")):
            continue
        text = path.read_text(encoding="utf-8-sig", errors="replace")
        f = {k: len(rx.findall(text)) for k, rx in RE.items()}
        parts = rel.split("/")
        if parts[0] == "Areas":
            area, key = parts[1], "/".join(parts[1:2] + parts[3:])
        else:
            area, key = "", "/".join(parts[1:]) if parts[0] == "Views" else rel
        key = key.removesuffix(".cshtml")
        is_partial = Path(rel).name.startswith("_")
        if Path(rel).name in ("_ViewImports.cshtml", "_ViewStart.cshtml"):
            continue
        pattern = classify(f, is_partial)
        wave = "W1 Admin, account, shell" if area == "Admin" else wave_of(key)
        rows.append({
            "view": rel, "area": area or "(root)", "pattern": pattern, "wave": wave, "target": TARGETS[pattern],
            "lines": text.count("\n") + 1,
            "inputs": f["input"], "form_control": f["form_control"], "asp_for": f["asp_for"],
            "tables": f["table"], "modals": f["modal"], "offcanvas": f["offcanvas"], "tabs": f["tabs"],
            "badges": f["badge"], "inline_style_attrs": f["inline_style"], "style_blocks": f["style_block"],
            "inline_scripts": f["script_block"], "uses_vs": int(f["vs"] > 0), "has_page_header": int(f["page_header"] > 0),
            "has_empty_state": int(f["empty_state"] > 0), "has_loading": int(f["skeleton"] > 0), "cdn_refs": f["cdn"],
        })

    OUT_CSV.parent.mkdir(parents=True, exist_ok=True)
    with OUT_CSV.open("w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)

    pages = [r for r in rows if r["pattern"] != "partial"]
    by_pattern = Counter(r["pattern"] for r in rows)
    by_wave = defaultdict(Counter)
    for r in pages:
        by_wave[r["wave"]][r["pattern"]] += 1
    tot = lambda k: sum(r[k] for r in rows)
    summary = {
        "views_total": len(rows), "pages": len(pages), "partials": by_pattern["partial"],
        "by_pattern": dict(by_pattern), "waves": {w: dict(c) for w, c in sorted(by_wave.items())},
        "hand_written_inputs": tot("inputs"), "asp_for_bindings": tot("asp_for"), "tables": tot("tables"),
        "modals": tot("modals"), "offcanvas": tot("offcanvas"), "badges": tot("badges"),
        "inline_style_attrs": tot("inline_style_attrs"), "views_with_inline_style": sum(1 for r in rows if r["inline_style_attrs"]),
        "views_with_style_block": sum(1 for r in rows if r["style_blocks"]), "views_with_inline_script": sum(1 for r in rows if r["inline_scripts"]),
        "views_using_vs": sum(r["uses_vs"] for r in rows), "pages_with_page_header": sum(r["has_page_header"] for r in pages),
        "pages_with_empty_state": sum(r["has_empty_state"] for r in pages), "pages_with_loading_state": sum(r["has_loading"] for r in pages),
        "cdn_refs": tot("cdn_refs"),
    }
    css = sorted((WEB / "wwwroot" / "css").glob("*.css"))
    js = sorted((WEB / "wwwroot" / "js").glob("*.js"))
    summary["css_files"], summary["js_files"] = len(css), len(js)
    summary["css_lines"] = sum(len(p.read_text(encoding="utf-8-sig", errors="replace").splitlines()) for p in css)
    summary["css_hex_colors"] = sum(len(re.findall(r"#[0-9a-fA-F]{3,8}\b", p.read_text(encoding="utf-8-sig", errors="replace"))) for p in css)
    summary["css_pems_tokens_used"] = sum(len(re.findall(r"var\(--pems-", p.read_text(encoding="utf-8-sig", errors="replace"))) for p in css)
    summary["css_physical_lr"] = sum(len(re.findall(r"\b(margin|padding)-(left|right)\s*:|\b(left|right)\s*:\s*[-\d]", p.read_text(encoding="utf-8-sig", errors="replace"))) for p in css)

    for k, v in summary.items():
        print(f"{k}: {v}")

    if "--md" in sys.argv:
        write_md(summary, rows)

    # SCRUM-20 / #42: the product is offline. A view that references a CDN host fails the audit (exit code 1),
    # so any pipeline that runs this script stops (the same check also runs as OfflineAssetsTests).
    if summary["cdn_refs"]:
        for r in rows:
            if r["cdn_refs"]:
                print(f"ERROR: {r['view']} references a CDN host ({r['cdn_refs']}x); vendor the file under wwwroot/lib", file=sys.stderr)
        sys.exit(1)


def write_md(s: dict, rows: list) -> None:
    pages = [r for r in rows if r["pattern"] != "partial"]
    worst = sorted(pages, key=lambda r: -(r["inputs"] + r["tables"] * 5 + r["inline_style_attrs"]))[:15]
    lines = [
        "# UI pattern audit (static)", "",
        "Generated by `python tools/UiAudit/pattern-audit.py --md` from the Razor source. Per-view data: "
        "[ui-pattern-audit.csv](ui-pattern-audit.csv) (view, pattern, target component set, migration wave, hand-written markup counts). "
        "Runtime/browser checks are separate (`tools/UiAudit/audit.mjs`).", "",
        "## Totals", "", "| Metric | Value |", "|---|---|",
    ]
    for k in ["views_total", "pages", "partials", "hand_written_inputs", "asp_for_bindings", "tables", "modals", "offcanvas", "badges",
              "inline_style_attrs", "views_with_inline_style", "views_with_style_block", "views_with_inline_script", "views_using_vs",
              "pages_with_page_header", "pages_with_empty_state", "pages_with_loading_state", "cdn_refs",
              "css_files", "css_lines", "css_hex_colors", "css_pems_tokens_used", "css_physical_lr", "js_files"]:
        lines.append(f"| {k.replace('_', ' ')} | {s[k]} |")
    lines += ["", "## Pages by pattern", "", "| Pattern | Views | Target components |", "|---|---|---|"]
    for p, n in sorted(s["by_pattern"].items(), key=lambda kv: -kv[1]):
        lines.append(f"| {p} | {n} | {TARGETS[p]} |")
    lines += ["", "## Pages by migration wave", "", "| Wave | Pages | Patterns |", "|---|---|---|"]
    for w, c in s["waves"].items():
        lines.append(f"| {w} | {sum(c.values())} | " + ", ".join(f"{k} {v}" for k, v in sorted(c.items(), key=lambda kv: -kv[1])) + " |")
    lines += ["", "## Heaviest pages (inputs + 5 x tables + inline styles)", "", "| View | Pattern | Inputs | Tables | Inline styles |", "|---|---|---|---|---|"]
    for r in worst:
        lines.append(f"| {r['view']} | {r['pattern']} | {r['inputs']} | {r['tables']} | {r['inline_style_attrs']} |")
    OUT_MD.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    print("wrote", OUT_MD)


if __name__ == "__main__":
    main()
