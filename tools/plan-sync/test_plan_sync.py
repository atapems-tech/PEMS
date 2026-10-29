"""Tests of the plan tool: python3 -m unittest discover -s tools/plan-sync   (standard library only)."""
import sys, tempfile, unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import plan_sync as ps


class PlanFilesTests(unittest.TestCase):
    def test_every_row_file_round_trips_through_parse_and_format(self):
        for path in sorted(ps.ROWS_DIR.glob("*.md")):
            text = path.read_text(encoding="utf-8")
            row = ps.parse_row(text, path.name)
            self.assertEqual(ps.format_row(row), text, path.name)

    def test_file_name_matches_id_and_ids_are_unique(self):
        _, rows = ps.load_rows()
        self.assertEqual(len({r["#"] for r in rows}), len(rows))

    def test_check_has_no_findings(self):
        self.assertEqual(ps.check(), 0)

    def test_summary_counts_every_row(self):
        _, rows = ps.load_rows()
        self.assertIn(f"Total tasks: {len(rows)}", ps.summary_text())


class EditCommandTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.old = ps.ROWS_DIR
        ps.ROWS_DIR = Path(self.tmp.name)
        # the commands print paths relative to ROOT; keep that working for a temp folder
        self.old_root = ps.ROOT
        ps.ROOT = Path(self.tmp.name)

    def tearDown(self):
        ps.ROWS_DIR, ps.ROOT = self.old, self.old_root
        self.tmp.cleanup()

    def test_new_set_report_and_show(self):
        ps.cmd_new(["900", "A test task", "area=Testing"])
        path = ps.row_path(900)
        self.assertTrue(path.exists())
        ps.cmd_set(["900", "status=In Progress", "milestone=M1"])
        ps.cmd_report(["900", "first"])
        ps.cmd_report(["900", "+second"])
        row = ps.parse_row(path.read_text(encoding="utf-8"))
        self.assertEqual((row["Status"], row["Milestone"], row["Area"]), ("In Progress", "M1", "Testing"))
        self.assertEqual(row["Report"], "first\n\nsecond")
        self.assertTrue(row["Prompt"].startswith("1)"))

    def test_set_rejects_an_unknown_status_and_a_new_duplicate(self):
        ps.cmd_new(["901", "Another"])
        with self.assertRaises(SystemExit):
            ps.cmd_set(["901", "status=Almost"])
        with self.assertRaises(SystemExit):
            ps.cmd_new(["901", "Again"])


if __name__ == "__main__":
    unittest.main()
