#!/usr/bin/env python3
"""Plan workbook -> text mirror and Jira import files.   (see docs/process/PLAN_JIRA_WORKFLOW.md)

    pip install openpyxl
    python tools/plan-sync/plan_sync.py export                 # plan/backlog.csv + plan/jira-import.csv + plan/jira-links.csv
    python tools/plan-sync/plan_sync.py check                  # offline consistency checks on the workbook (exit 1 on findings)

`export` is read-only on the workbook. `push`/`pull`/Jira-side `check` need Jira API access and are planned (backlog #336);
their credentials are CI secrets (JIRA_BASE_URL, JIRA_PROJECT_KEY, JIRA_USER_EMAIL, JIRA_API_TOKEN), never files in git.
"""
import csv, re, sys
from collections import Counter
from pathlib import Path

try:
    import openpyxl
except ImportError:
    sys.exit("openpyxl is required: pip install openpyxl")

ROOT = Path(__file__).resolve().parents[2]
WORKBOOK = ROOT / "plan" / "PEMS_Backlog_Roadmap.xlsx"
OUT_DIR = ROOT / "plan"

ISSUE_TYPE = {"Feature": "Story", "Bug Fix": "Bug"}                    # everything else -> Task
PRIORITY = {"P0": "Highest", "P1": "High", "P2": "Medium", "P3": "Low"}
STATUS = {"Not Started": "To Do", "In Progress": "In Progress", "Partial": "In Progress", "Done": "Done",
          "Hold": "On Hold", "Later": "Backlog", "Cancelled": "Won't Do", "Obsolete": "Won't Do"}
EPICS = {"M0": "M0 Baseline and foundation decisions", "M1": "M1 Dynamic core, components and licensing (Alpha)",
         "M2": "M2 Builders, demo and install (Beta)", "M3": "M3 Commercial release 1.0",
         "M4": "M4 Platform completion and module migration", "M5": "M5 Competitive features",
         "M6": "M6 Later", "-": "No milestone"}


def load_rows():
    wb = openpyxl.load_workbook(WORKBOOK, data_only=False)
    ws = wb["Backlog"]
    header = [c.value for c in ws[1]]
    rows = []
    for values in ws.iter_rows(min_row=2, values_only=True):
        if values[0] is None:
            continue
        rows.append(dict(zip(header, values)))
    return header, rows


def clean(v):
    return "" if v is None else str(v).strip()


def labels(r):
    out = []
    if clean(r.get("Effort")): out.append("effort-" + clean(r["Effort"]))
    if clean(r.get("Doer")): out.append("doer-" + clean(r["Doer"]).lower())
    if clean(r.get("Confirmed by PM")).lower() == "yes": out.append("pm-confirmed")
    if clean(r.get("Status")) == "Partial": out.append("partial")
    if clean(r.get("Status")) == "Later": out.append("later")
    if clean(r.get("Commercial")) == "Sales blocker": out.append("sales-blocker")
    # team-managed Jira projects (like SCRUM) have no Components: the plan area travels as a label instead
    area = re.sub(r"[^a-z0-9]+", "-", clean(r.get("Area")).lower()).strip("-")
    if area: out.append("area-" + area[:60])
    return out


def description(r):
    parts = []
    for title, key in (("Evidence", "Evidence"), ("Instruction", "Prompt"), ("Acceptance", "Requirement"), ("Report", "Report")):
        if clean(r.get(key)):
            parts.append(f"h3. {title}\n{clean(r[key])}")
    if clean(r.get("Depends")):
        parts.append("h3. Depends on\n" + clean(r["Depends"]))
    parts.append(f"Plan ID: {r['#']}   Group: {clean(r.get('Group'))}")
    return "\n\n".join(parts)


def export():
    header, rows = load_rows()
    OUT_DIR.mkdir(exist_ok=True)

    with (OUT_DIR / "backlog.csv").open("w", newline="", encoding="utf-8") as fh:        # reviewable text mirror
        w = csv.writer(fh)
        w.writerow(header)
        for r in rows:
            w.writerow([clean(r.get(h)) for h in header])

    milestones = sorted({clean(r.get("Milestone")) or "-" for r in rows})
    with (OUT_DIR / "jira-import.csv").open("w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(["Work item ID", "Work type", "Parent", "Summary", "Description", "Priority", "Status", "Component", "Fix Version",
                    "Plan ID", "Labels", "Labels", "Labels", "Labels", "Labels", "Labels"])   # repeated header = multi-value Labels
        epic_id = {}
        for i, m in enumerate(milestones, start=1):                                     # epics first (parents before children)
            epic_id[m] = i
            w.writerow([i, "Epic", "", EPICS.get(m, m), f"Milestone {m}", "", "", "", m, "", "", "", "", "", "", ""])
        next_id = len(milestones) + 1
        for r in rows:
            m = clean(r.get("Milestone")) or "-"
            lab = (labels(r) + [""] * 6)[:6]
            w.writerow([next_id, ISSUE_TYPE.get(clean(r.get("Track")), "Task"), epic_id[m], clean(r.get("Task")), description(r),
                        PRIORITY.get(clean(r.get("Priority")), "Medium"), STATUS.get(clean(r.get("Status")), "To Do"),
                        clean(r.get("Area")), m, r["#"]] + lab)
            next_id += 1

    with (OUT_DIR / "jira-links.csv").open("w", newline="", encoding="utf-8") as fh:     # for the API step (links are not CSV-importable)
        w = csv.writer(fh)
        w.writerow(["plan_id", "blocked_by_plan_id"])
        for r in rows:
            for dep in re.findall(r"#(\d+)", clean(r.get("Depends"))):
                w.writerow([r["#"], dep])

    print(f"exported {len(rows)} rows: plan/backlog.csv, plan/jira-import.csv ({len(rows) + len(milestones)} work items), plan/jira-links.csv")
    if len(rows) + len(milestones) > 1500:
        print("note: Jira recommends at most ~1,500 work items per import file; split before importing")


def check():
    _, rows = load_rows()
    problems = []
    ids = Counter(r["#"] for r in rows)
    problems += [f"duplicate Plan ID {i}" for i, n in ids.items() if n > 1]
    known = set(ids)
    for r in rows:
        pid = r["#"]
        status, evidence = clean(r.get("Status")), clean(r.get("Evidence"))
        # "Existing capabilities" rows are estimated from code (a class name is their evidence) and are listed for QA via the Test Plan
        if status == "Done" and len(evidence) < 20 and clean(r.get("Group")) != "Existing capabilities":
            problems.append(f"#{pid}: Done without evidence")
        if status not in STATUS:
            problems.append(f"#{pid}: unknown status '{status}'")
        if clean(r.get("Priority")) not in PRIORITY:
            problems.append(f"#{pid}: unknown priority '{clean(r.get('Priority'))}'")
        for dep in re.findall(r"#(\d+)", clean(r.get("Depends"))):
            if int(dep) not in known:
                problems.append(f"#{pid}: depends on unknown #{dep}")
    for p in problems:
        print("FINDING:", p)
    print(f"{len(rows)} rows checked, {len(problems)} finding(s)")
    return 1 if problems else 0


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    if cmd == "export":
        export()
    elif cmd == "check":
        sys.exit(check())
    else:
        sys.exit(__doc__)
