#!/usr/bin/env python3
"""Pick the tests a session should run for what it changed: the fewest tests that catch the regressions we have actually had (SCRUM-464, guideline chapter 05 "Test budget").

    python tools/test-gates/gates.py             # print the commands for the changed files (committed since origin/main + uncommitted + new)
    python tools/test-gates/gates.py --run       # print and run them, stop at the first failure
    python tools/test-gates/gates.py --base main --files PEMS.Web/Views/Task/Index.cshtml   # explicit base or file list

How it chooses (all rules are in this file, standard library only):
  1. build and UI tests whenever code, views, components, resources or the guideline changed (cheap, catch Razor compile errors, golden manifests, ratchet, registry);
  2. guard classes by kind of file: a Persian-key check for any view or resource change, theme and token checks for stylesheets, the list-page tests for pems-list-*,
     the API tests for /api and the pipeline, the error-handling tests for Diagnostics;
  3. the exact test classes that mention a changed file (its path or its controller or class name), found by reading PEMS.Tests; classes of the slow families
     (SQL Server, concurrency, migrations ...) are never chosen, they are listed as left to the end-of-sprint QA;
  4. the very large suites (whole Web namespace, full PEMS.Tests, SQL families) are never chosen: the end-of-sprint QA runs them, each task keeps its own line d2 (guideline chapter 07 section 2b).
A filter names a class as `FullyQualifiedName~.ClassName.` so it cannot match a longer class name or a namespace.
"""
import argparse, os, re, subprocess, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
TESTS = os.path.join(ROOT, 'PEMS.Tests')
SLOW = re.compile(r'Sql|LiveDb|Concurrency|Migration|Numbering|Atomicity|Retry|RealSql|Hangfire|HttpTests|Smoke', re.I)
# Measured on 2026-11-04 (one class per `dotnet test`, build done; each invocation also costs 8 to 16 s of start-up, so classes go into ONE invocation):
# DynamicSidebarTests did not finish in 100 s, MrRegisteredPartTests took 18 s. Everything else the script picks was under 5 s. Add a class here when it measures over 10 s.
SLOW_MEASURED = {'DynamicSidebarTests', 'MrRegisteredPartTests'}

# (path regex, label, guard test classes in PEMS.Tests, why)
GUARDS = [
    (r'\.cshtml$|SharedResource.*\.resx$|Localizer', 'Persian keys and the shell', ['LocalizationKeyCoverageTests', 'ShellAccessibilityTests'],
     'every constant Localizer key needs a Persian entry; layouts keep their contract'),
    (r'wwwroot/(css/.*|tokens)\.css$|tokens\.css$|pems-themes|pems-glass', 'themes and tokens', ['ThemeContrastTests', 'ThemeSystemTests', 'PremiumGlassSystemTests'],
     'no literal hex in the four shared sheets, theme blocks and contrast pairs'),
    (r'pems-list\.(js|css)|PemsListTagHelpers|Views/.*/Index\.cshtml$', 'list pages', ['ListPageComponentTests', 'CmmsListPagesTests'],
     'the list-page frame and the pages built from it'),
    (r'PEMS\.Web/Api/|PEMS\.Web/Program\.cs$', 'API and pipeline', None, 'conformance, policy, snapshot (namespace PEMS.Tests.Api)'),
    (r'PEMS\.Web/Diagnostics/|PemsExceptions|Controllers/HomeController\.cs$|StatusPage', 'error handling', ['TraceIdTests', 'ErrorMapperTests', 'ShellAccessibilityTests'],
     'trace id, taxonomy and status pages'),
]
UI_TESTS_WHEN = re.compile(r'PEMS\.UI/|PEMS\.Web/Views/|\.cshtml$|wwwroot/|\.resx$|tools/UiAudit|docs/guidelines|PEMS\.UI\.Tests/')
BUILD_WHEN = re.compile(r'\.(cs|cshtml|razor|csproj|resx)$')


def git(*args):
    return subprocess.run(['git', *args], cwd=ROOT, capture_output=True, text=True).stdout.splitlines()


def changed_files(base):
    files = set(git('diff', '--name-only', f'{base}...HEAD')) | set(git('diff', '--name-only', 'HEAD')) | set(git('ls-files', '--others', '--exclude-standard'))
    return sorted(f for f in files if f and '/bin/' not in f and '/obj/' not in f)


def test_classes():
    """{class name: (file, source text)} for every test class of PEMS.Tests."""
    found = {}
    for dirpath, dirs, names in os.walk(TESTS):
        dirs[:] = [d for d in dirs if d not in ('bin', 'obj')]
        for n in names:
            if not n.endswith('.cs'):
                continue
            path = os.path.join(dirpath, n)
            text = open(path, encoding='utf-8-sig', errors='ignore').read()
            for m in re.finditer(r'public\s+(?:sealed\s+|static\s+|partial\s+)*class\s+(\w+)', text):
                if re.search(r'\[(Fact|Theory)', text):
                    found[m.group(1)] = (os.path.relpath(path, ROOT).replace('\\', '/'), text)
    return found


def names_for(file):
    """The strings a test would use to refer to a changed file."""
    out = {file.replace('\\', '/')}
    parts = file.replace('\\', '/').split('/')
    if 'Views' in parts:                                  # Views/Task/Index.cshtml, Areas/Warehouse/Views/X/Y.cshtml
        out.add('/'.join(parts[parts.index('Views'):]))
    stem = os.path.splitext(parts[-1])[0]
    if file.endswith('Controller.cs'):
        out.add(stem)
    if file.endswith('.cs') and stem not in ('Program',):
        out.add(stem)
    return {o for o in out if len(o) > 6}


def filter_for(classes):
    return '|'.join(f'FullyQualifiedName~.{c}.' for c in sorted(classes))


def plan(files):
    steps, skipped, why = [], [], []
    if any(BUILD_WHEN.search(f) for f in files):
        steps.append(('dotnet build PEMS.Tests', 'compiles the views, the components AND the test project (the test classes below run with --no-build, so a stale test dll would hide a broken test)'))
    if any(UI_TESTS_WHEN.search(f) for f in files):
        steps.append(('dotnet test PEMS.UI.Tests', 'components, golden manifests, script contracts, ratchet, guideline registry'))
    chosen = {}
    api = False
    for rx, label, classes, reason in GUARDS:
        if any(re.search(rx, f) for f in files):
            if classes is None:
                api = True
            else:
                for c in classes:
                    chosen.setdefault(c, label)
    classes_all = test_classes()
    for f in files:
        for key in names_for(f):
            for cls, (path, text) in classes_all.items():
                if key in text and cls not in chosen:
                    if cls in SLOW_MEASURED or SLOW.search(cls) or SLOW.search(path):
                        if cls not in skipped:
                            skipped.append(cls)
                    else:
                        chosen[cls] = f'mentions {os.path.basename(f)}'
    if chosen:
        steps.append((f'dotnet test PEMS.Tests --no-build --filter "{filter_for(chosen)}"', f'{len(chosen)} classes: ' + ', '.join(sorted(chosen))))
    if api:
        steps.append(('dotnet test PEMS.Tests --no-build --filter "FullyQualifiedName~PEMS.Tests.Api"', 'API conformance, policy, snapshot'))
    return steps, skipped


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--base', default='origin/main')
    ap.add_argument('--files', nargs='*')
    ap.add_argument('--run', action='store_true')
    args = ap.parse_args()
    files = [f.replace('\\', '/') for f in (args.files if args.files is not None else changed_files(args.base))]
    if not files:
        print('No changed files: nothing to run.')
        return 0
    print(f'{len(files)} changed file(s) against {args.base}.\n')
    steps, skipped = plan(files)
    print('Run (in this order, each within about two minutes):')
    for i, (cmd, why) in enumerate(steps, 1):
        print(f'  {i}. {cmd}\n       {why}')
    if not steps:
        print('  (documents or plan files only: no automated test applies)')
    print('\nLeave to the end-of-sprint QA, list on this task as `not done - end-of-sprint QA` (guideline chapter 07 section 2b line d2):')
    print('  - dotnet test PEMS.Tests --filter "FullyQualifiedName~PEMS.Tests.Web"   (7 to 11 minutes)')
    print('  - dotnet test PEMS.slnx   and the SQL Server families')
    if skipped:
        print('  - slow classes that mention your files: ' + ', '.join(sorted(skipped)))
    if args.run:
        for cmd, _ in steps:
            print(f'\n$ {cmd}')
            if subprocess.run(cmd, cwd=ROOT, shell=True).returncode != 0:
                print('Stopped at the first failure.')
                return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
