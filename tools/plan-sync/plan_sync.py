#!/usr/bin/env python3
"""Plan as markdown files -> tracker (Jira) import files, checks, summary and cheap edits.   (see docs/process/PLAN_TRACKER_WORKFLOW.md)

    python tools/plan-sync/plan_sync.py check                          # offline consistency checks on plan/rows (exit 1 on findings)
    python tools/plan-sync/plan_sync.py summary                        # counts by status, priority, area, group, milestone (was the Summary sheet)
    python tools/plan-sync/plan_sync.py show 435                       # print one row
    python tools/plan-sync/plan_sync.py set 435 status="In Progress"   # change front matter fields of one row (validated)
    python tools/plan-sync/plan_sync.py report 435 "text"              # replace the Report section ("-" reads stdin; "+text" appends a paragraph)
    python tools/plan-sync/plan_sync.py new 447 "Task title" area="..." group="..."   # new row file from the template (id = Jira key number)
    python tools/plan-sync/plan_sync.py export                         # plan/jira-import.csv + plan/jira-links.csv (git-ignored, for the Jira import)

The plan is one markdown file per backlog row in plan/rows (front matter fields, then Evidence, Prompt, Requirement, Report sections) and
text pages for the other former Excel sheets in plan/sheets. Edit a file directly, or use set/report/new; both end in the same files.
The plan is tracker-free; everything tracker-specific (field names, sprint import, assignees) lives in this tool.
`push`/`pull`/tracker-side `check` need API access and are planned (backlog #336); their credentials are CI secrets
(JIRA_BASE_URL, JIRA_PROJECT_KEY, JIRA_USER_EMAIL, JIRA_API_TOKEN), never files in git.
Assignees come from plan/members.json ({"Member A": "a@company.com", ...}); copy plan/members.example.json. The real file is git-ignored.
"""
import csv, json, re, sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ROWS_DIR = ROOT / "plan" / "rows"
SHEETS_DIR = ROOT / "plan" / "sheets"
OUT_DIR = ROOT / "plan"
MEMBERS = OUT_DIR / "members.json"

ISSUE_TYPE = {"Feature": "Story", "Bug Fix": "Bug"}                    # everything else -> Task
PRIORITY = {"P0": "Highest", "P1": "High", "P2": "Medium", "P3": "Low"}
STATUS = {"Not Started": "To Do", "In Progress": "In Progress", "Partial": "In Progress", "Done": "Done",
          "Hold": "On Hold", "Later": "Backlog", "Cancelled": "Won't Do", "Obsolete": "Won't Do"}
EPICS = {"M0": "M0 Baseline and foundation decisions", "M1": "M1 Dynamic core, components and licensing (Alpha)",
         "M2": "M2 Builders, demo and install (Beta)", "M3": "M3 Commercial release 1.0",
         "M4": "M4 Platform completion and module migration", "M5": "M5 Competitive features",
         "M6": "M6 Later", "-": "No milestone"}
LABEL_COLUMNS = 6


FIELDS = [("#", "id"), ("Task", "task"), ("Doer", "doer"), ("Area", "area"), ("Track", "track"), ("Status", "status"),
          ("Priority", "priority"), ("Milestone", "milestone"), ("Effort", "effort"), ("Commercial", "commercial"),
          ("Depends", "depends"), ("Reference (Titan)", "reference_titan"), ("Old Ref", "old_ref"), ("Group", "group"),
          ("Confirmed by PM", "confirmed_by_pm"), ("Sprint", "sprint"), ("Owner", "owner")]
SECTIONS = ["Evidence", "Prompt", "Requirement", "Report"]
HEADER = [h for h, _ in FIELDS[:1]] + ["Doer", "Area", "Task", "Track", "Status", "Priority", "Evidence", "Prompt", "Requirement", "Report",
                                       "Milestone", "Effort", "Commercial", "Depends", "Reference (Titan)", "Old Ref", "Group",
                                       "Confirmed by PM", "Sprint", "Owner"]
KEY_OF = dict(FIELDS)
NAME_OF = {k: h for h, k in FIELDS}
SECTION_RE = re.compile(r"(?m)^## (" + "|".join(SECTIONS) + r")[ \t]*$")


def row_path(pid):
    return ROWS_DIR / f"{int(pid):04d}.md"


def parse_row(text, source="row"):
    """One row file: `---` front matter (key: value lines), then `## Evidence|Prompt|Requirement|Report` sections."""
    lines = text.replace("\r\n", "\n").split("\n")
    if not lines or lines[0].strip() != "---":
        raise ValueError(f"{source}: front matter must start with ---")
    end = next((i for i in range(1, len(lines)) if lines[i].strip() == "---"), None)
    if end is None:
        raise ValueError(f"{source}: front matter is not closed")
    row = {}
    for ln in lines[1:end]:
        if not ln.strip():
            continue
        key, sep, value = ln.partition(":")
        key = key.strip()
        if not sep or key not in NAME_OF:
            raise ValueError(f"{source}: unknown or malformed front matter line '{ln}'")
        row[NAME_OF[key]] = value.strip()
    body = "\n".join(lines[end + 1:])
    parts = SECTION_RE.split(body)                       # [preamble, name, text, name, text, ...]
    if parts[0].strip():
        raise ValueError(f"{source}: text before the first section")
    for name, content in zip(parts[1::2], parts[2::2]):
        row[name] = content.strip()
    row["#"] = int(row["#"])
    return row


def format_row(row):
    out = ["---"]
    for h, k in FIELDS:
        v = clean(row.get(h))
        if v:
            out.append(f"{k}: {v}")
    out.append("---")
    for name in SECTIONS:
        v = clean(row.get(name))
        if v:
            out += ["", f"## {name}", "", v]
    return "\n".join(out) + "\n"


def load_rows():
    rows = []
    for path in sorted(ROWS_DIR.glob("*.md")):
        row = parse_row(path.read_text(encoding="utf-8"), path.name)
        if path.name != f"{row['#']:04d}.md":
            raise ValueError(f"{path.name}: file name does not match id {row['#']}")
        rows.append(row)
    return HEADER, sorted(rows, key=lambda r: r["#"])


def clean(v):
    return "" if v is None else str(v).strip()


def load_members():
    if MEMBERS.exists():
        return json.loads(MEMBERS.read_text(encoding="utf-8"))
    return {}


def sprint_of(r):
    s = clean(r.get("Sprint"))
    return "" if not s or "stretch" in s.lower() else s


def labels(r):
    out = []
    if clean(r.get("Effort")): out.append("effort-" + clean(r["Effort"]))
    if clean(r.get("Doer")): out.append("doer-" + clean(r["Doer"]).lower())
    if clean(r.get("Confirmed by PM")).lower() == "yes": out.append("pm-confirmed")
    if clean(r.get("Status")) == "Partial": out.append("partial")
    if clean(r.get("Status")) == "Later": out.append("later")
    if clean(r.get("Commercial")) == "Sales blocker": out.append("sales-blocker")
    if "stretch" in clean(r.get("Sprint")).lower(): out.append("sprint-1-stretch")
    # team-managed Jira spaces have no Components: the plan area travels as a label instead
    area = re.sub(r"[^a-z0-9]+", "-", clean(r.get("Area")).lower()).strip("-")
    if area: out.append("area-" + area[:60])
    return out


def summary(r):
    return f"[#{r['#']}] {clean(r.get('Task'))}"


def description(r, titles):
    head = (f"*Plan ID:* #{r['#']}   *Area:* {clean(r.get('Area'))}   *Group:* {clean(r.get('Group'))}   "
            f"*Milestone:* {clean(r.get('Milestone')) or '-'}   *Priority:* {clean(r.get('Priority'))}   "
            f"*Effort:* {clean(r.get('Effort'))}   *Owner:* {clean(r.get('Owner')) or '-'}   *Doer:* {clean(r.get('Doer'))}")
    parts = [head, "*Step 0:* read AGENTS.md and docs/guidelines/README.md, then the related Jira issues, before starting."]
    for title, key in (("Why", "Evidence"), ("What to do", "Prompt"), ("Done when", "Requirement")):
        if clean(r.get(key)):
            text = clean(r[key])
            if key == "Requirement" and text.lower().startswith("done when:"):
                text = text[len("done when:"):].strip()          # the section header already says "Done when"
            parts.append(f"h3. {title}\n{text}")
    deps = re.findall(r"#(\d+)", clean(r.get("Depends")))
    if deps:
        parts.append("h3. Depends on\n" + "\n".join(f"* [#{d}] {titles.get(int(d), '(unknown)')}" for d in deps))
    if clean(r.get("Report")):
        parts.append(f"h3. Report\n{clean(r['Report'])}")
    return "\n\n".join(parts)


def export():
    header, rows = load_rows()
    OUT_DIR.mkdir(exist_ok=True)
    members = load_members()
    titles = {int(r["#"]): clean(r.get("Task")) for r in rows}

    milestones = sorted({clean(r.get("Milestone")) or "-" for r in rows})
    with (OUT_DIR / "jira-import.csv").open("w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(["Work item ID", "Work type", "Parent", "Summary", "Description", "Priority", "Status", "Fix Version", "Plan ID",
                    "Sprint", "Assignee"] + ["Labels"] * LABEL_COLUMNS)                  # repeated header = multi-value Labels
        epic_id = {}
        for i, m in enumerate(milestones, start=1):                                     # epics first (parents before children)
            epic_id[m] = i
            w.writerow([i, "Epic", "", EPICS.get(m, m), f"Milestone {m}", "", "", m, "", "", ""] + [""] * LABEL_COLUMNS)
        next_id = len(milestones) + 1
        for r in rows:
            m = clean(r.get("Milestone")) or "-"
            lab = (labels(r) + [""] * LABEL_COLUMNS)[:LABEL_COLUMNS]
            w.writerow([next_id, ISSUE_TYPE.get(clean(r.get("Track")), "Task"), epic_id[m], summary(r), description(r, titles),
                        PRIORITY.get(clean(r.get("Priority")), "Medium"), STATUS.get(clean(r.get("Status")), "To Do"), m, r["#"],
                        sprint_of(r), members.get(clean(r.get("Owner")), "")] + lab)
            next_id += 1

    with (OUT_DIR / "jira-links.csv").open("w", newline="", encoding="utf-8") as fh:     # for the API step (links are not CSV-importable)
        w = csv.writer(fh)
        w.writerow(["plan_id", "blocked_by_plan_id"])
        for r in rows:
            for dep in re.findall(r"#(\d+)", clean(r.get("Depends"))):
                w.writerow([r["#"], dep])

    in_sprint = sum(1 for r in rows if sprint_of(r))
    print(f"exported {len(rows)} rows: plan/jira-import.csv ({len(rows) + len(milestones)} work items, "
          f"{in_sprint} in the sprint), plan/jira-links.csv")
    if not members:
        print("note: plan/members.json not found - Assignee left empty (copy plan/members.example.json)")
    if len(rows) + len(milestones) > 1500:
        print("note: Jira recommends at most ~1,500 work items per import file; split before importing")


def dependency_findings(rows):
    """Plan audit 2026-10-28: a row must not depend on a row planned in a LATER milestone, and dependencies must not form a cycle."""
    def stage(r):
        m = re.fullmatch(r"M(\d)", clean(r.get("Milestone")))
        return int(m.group(1)) if m else None
    by_id = {r["#"]: r for r in rows}
    deps = {r["#"]: [d for d in (int(x) for x in re.findall(r"#(\d+)", clean(r.get("Depends")))) if d in by_id and d != r["#"]] for r in rows}
    out = []
    for pid, ds in deps.items():
        for d in ds:
            a, b = stage(by_id[pid]), stage(by_id[d])
            if a is not None and b is not None and b > a:
                out.append(f"#{pid}: planned in M{a} but depends on #{d} planned in M{b}")
    state = {}
    def visit(n, path):
        state[n] = 1
        for d in deps[n]:
            if state.get(d) == 1:
                out.append("dependency cycle: " + " -> ".join(f"#{x}" for x in path + [n, d]))
            elif d not in state:
                visit(d, path + [n])
        state[n] = 2
    for n in deps:
        if n not in state:
            visit(n, [])
    return out


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
        if clean(r.get("Sprint")):                                  # a planned row must be unambiguous
            if not clean(r.get("Owner")):
                problems.append(f"#{pid}: planned in {clean(r.get('Sprint'))} without an Owner")
            if len(clean(r.get("Prompt"))) < 60 or not clean(r.get("Requirement")).lower().startswith("done when"):
                problems.append(f"#{pid}: planned in a sprint but 'What to do' is short or 'Done when' is missing")
    problems += dependency_findings(rows)
    for p in problems:
        print("FINDING:", p)
    print(f"{len(rows)} rows checked, {len(problems)} finding(s)")
    return 1 if problems else 0


def find_row(pid):
    path = row_path(pid)
    if not path.exists():
        sys.exit(f"no row #{pid} (expected {path.relative_to(ROOT)})")
    return path, parse_row(path.read_text(encoding="utf-8"), path.name)


def write_row(path, row):
    path.write_text(format_row(row), encoding="utf-8", newline="\n")


def validate_field(name, value):
    if name == "Status" and value not in STATUS:
        sys.exit(f"unknown status '{value}' (one of: {', '.join(STATUS)})")
    if name == "Priority" and value not in PRIORITY:
        sys.exit(f"unknown priority '{value}' (one of: {', '.join(PRIORITY)})")
    if "\n" in value:
        sys.exit(f"{name}: front matter values are one line; use a section for long text")


def cmd_set(args):
    if len(args) < 2:
        sys.exit("usage: set <id> key=value [key=value ...]   (keys: " + ", ".join(k for _, k in FIELDS[1:]) + ")")
    path, row = find_row(args[0])
    for pair in args[1:]:
        key, sep, value = pair.partition("=")
        if not sep or key not in NAME_OF or key == "id":
            sys.exit(f"bad field '{pair}' (keys: " + ", ".join(k for _, k in FIELDS[1:]) + ")")
        validate_field(NAME_OF[key], value.strip())
        row[NAME_OF[key]] = value.strip()
    write_row(path, row)
    print(f"updated {path.relative_to(ROOT)}")


def cmd_report(args):
    if len(args) < 2:
        sys.exit('usage: report <id> "text" | report <id> - (stdin) | report <id> "+text" (append a paragraph)')
    path, row = find_row(args[0])
    text = sys.stdin.read() if args[1] == "-" else args[1]
    if text.startswith("+"):
        text = (clean(row.get("Report")) + "\n\n" + text[1:].strip()).strip()
    row["Report"] = text.strip()
    write_row(path, row)
    print(f"updated {path.relative_to(ROOT)}")


def cmd_new(args):
    if len(args) < 2 or not args[0].isdigit():
        sys.exit('usage: new <id> "Task title" [key=value ...]   (id = the Jira key number)')
    path = row_path(args[0])
    if path.exists():
        sys.exit(f"{path.relative_to(ROOT)} already exists")
    row = {"#": int(args[0]), "Task": args[1], "Doer": "Claude", "Status": "Not Started", "Priority": "P1", "Track": "Docs"}
    for pair in args[2:]:
        key, sep, value = pair.partition("=")
        if not sep or key not in NAME_OF or key == "id":
            sys.exit(f"bad field '{pair}'")
        validate_field(NAME_OF[key], value.strip())
        row[NAME_OF[key]] = value.strip()
    row.update({"Evidence": "Why: ...", "Prompt": "1) ...", "Requirement": "Done when: ..."})
    write_row(path, row)
    print(f"created {path.relative_to(ROOT)}; fill Evidence, Prompt, Requirement, then run check")


def cmd_show(args):
    path, _ = find_row(args[0] if args else "")
    sys.stdout.write(path.read_text(encoding="utf-8"))


def summary_text():
    _, rows = load_rows()
    n = lambda pred: sum(1 for r in rows if pred(r))
    closed = ("Done", "Later", "Cancelled", "Obsolete")
    out = [f"Total tasks: {len(rows)}", f"Owner tasks (Doer = User): {n(lambda r: clean(r.get('Doer')) == 'User')}", "",
           "Status x priority", "| Status | P0 | P1 | P2 | P3 | Total |", "| --- | --- | --- | --- | --- | --- |"]
    for st in STATUS:
        cells = [n(lambda r: clean(r.get("Status")) == st and clean(r.get("Priority")) == p) for p in PRIORITY]
        out.append(f"| {st} | " + " | ".join(map(str, cells)) + f" | {sum(cells)} |")
    out += ["", "Area x priority (remaining = not Done/Later/Cancelled/Obsolete)", "| Area | Total | P0 | P1 | P2 | P3 | Remaining | Done |",
            "| --- | --- | --- | --- | --- | --- | --- | --- |"]
    for area in dict.fromkeys(clean(r.get("Area")) for r in rows):
        mine = [r for r in rows if clean(r.get("Area")) == area]
        pr = [sum(1 for r in mine if clean(r.get("Priority")) == p) for p in PRIORITY]
        rem = sum(1 for r in mine if clean(r.get("Status")) not in closed)
        out.append(f"| {area} | {len(mine)} | " + " | ".join(map(str, pr)) + f" | {rem} | {sum(1 for r in mine if clean(r.get('Status')) == 'Done')} |")
    out += ["", "Group (related tasks decided and planned together)", "| Group | Total | Done | Later | Remaining | Confirmed by PM |", "| --- | --- | --- | --- | --- | --- |"]
    for g in dict.fromkeys(clean(r.get("Group")) for r in rows):
        mine = [r for r in rows if clean(r.get("Group")) == g]
        done = sum(1 for r in mine if clean(r.get("Status")) == "Done")
        later = sum(1 for r in mine if clean(r.get("Status")) == "Later")
        rem = sum(1 for r in mine if clean(r.get("Status")) not in ("Done", "Later", "Cancelled", "Obsolete"))
        out.append(f"| {g} | {len(mine)} | {done} | {later} | {rem} | {sum(1 for r in mine if clean(r.get('Confirmed by PM')) == 'Yes')} |")
    open_ = lambda r: clean(r.get("Status")) not in closed
    out += ["", f"Confirmed by PM (all rows): {n(lambda r: clean(r.get('Confirmed by PM')) == 'Yes')}",
            f"P0 rows confirmed by PM: {n(lambda r: clean(r.get('Confirmed by PM')) == 'Yes' and clean(r.get('Priority')) == 'P0')}",
            f"Open P0 rows not yet confirmed by PM: {n(lambda r: clean(r.get('Confirmed by PM')) == 'No' and clean(r.get('Priority')) == 'P0' and open_(r))}",
            f"Rows on Hold: {n(lambda r: clean(r.get('Status')) == 'Hold')}",
            f"Remaining P0 tasks (not Done/Later/Cancelled/Obsolete): {n(lambda r: clean(r.get('Priority')) == 'P0' and open_(r))}",
            f"Remaining 'Sales blocker' tasks: {n(lambda r: clean(r.get('Commercial')) == 'Sales blocker' and clean(r.get('Status')) != 'Done')}",
            f"Open decisions (Decision Needed): {n(lambda r: clean(r.get('Track')) == 'Decision Needed' and clean(r.get('Status')) != 'Done')}",
            "", "Tasks per milestone", "| Milestone | Tasks |", "| --- | --- |"]
    for m in sorted({clean(r.get("Milestone")) or "-" for r in rows}):
        out.append(f"| {m} | {n(lambda r: (clean(r.get('Milestone')) or '-') == m)} |")
    return "\n".join(out)


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    if cmd == "export":
        export()
    elif cmd == "check":
        sys.exit(check())
    elif cmd == "summary":
        print(summary_text())
    elif cmd == "set":
        cmd_set(sys.argv[2:])
    elif cmd == "report":
        cmd_report(sys.argv[2:])
    elif cmd == "new":
        cmd_new(sys.argv[2:])
    elif cmd == "show":
        cmd_show(sys.argv[2:])
    else:
        sys.exit(__doc__)
