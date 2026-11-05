"""Update the code graph, prune extractor stubs and add the UI component registry links (ADR-0013) in one step: python tools/graphify/update.py"""
import subprocess, sys, pathlib

root = pathlib.Path(__file__).resolve().parents[2]
code = subprocess.call(["graphify", "update", "."], cwd=root)
if code != 0:
    sys.exit(code)
graph = str(root / "graphify-out" / "graph.json")
for step in ("prune.py", "ui_links.py"):
    code = subprocess.call([sys.executable, str(root / "tools" / "graphify" / step), graph])
    if code != 0:
        sys.exit(code)
