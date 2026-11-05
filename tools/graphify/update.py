"""Update the code graph and prune extractor stubs in one step: python tools/graphify/update.py"""
import subprocess, sys, pathlib

root = pathlib.Path(__file__).resolve().parents[2]
code = subprocess.call(["graphify", "update", "."], cwd=root)
if code != 0:
    sys.exit(code)
sys.exit(subprocess.call([sys.executable, str(root / "tools" / "graphify" / "prune.py"), str(root / "graphify-out" / "graph.json")]))
