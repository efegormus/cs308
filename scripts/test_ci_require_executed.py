import tempfile
import unittest
from pathlib import Path

from ci_require_executed import main

TRX = """<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>{rows}</Results></TestRun>"""


def report(directory, *outcomes):
    rows = "".join(f'<UnitTestResult testName="t{i}" outcome="{o}" />' for i, o in enumerate(outcomes))
    (Path(directory) / "tests.trx").write_text(TRX.format(rows=rows))


class RequireExecutedTests(unittest.TestCase):
    def run_main(self, *outcomes):
        with tempfile.TemporaryDirectory() as directory:
            if outcomes:
                report(directory, *outcomes)
            return main(["ci_require_executed.py", directory])

    def test_all_passed_succeeds(self):
        self.assertEqual(self.run_main("Passed", "Passed"), 0)

    def test_skipped_test_fails(self):
        self.assertEqual(self.run_main("Passed", "NotExecuted"), 1)

    def test_only_skipped_fails(self):
        self.assertEqual(self.run_main("NotExecuted", "NotExecuted"), 1)

    def test_failed_test_fails(self):
        self.assertEqual(self.run_main("Passed", "Failed"), 1)

    def test_missing_report_fails(self):
        self.assertEqual(self.run_main(), 1)

    def test_wrong_usage_is_reported(self):
        self.assertEqual(main(["ci_require_executed.py"]), 2)


if __name__ == "__main__":
    unittest.main()
