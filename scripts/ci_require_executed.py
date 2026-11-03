#!/usr/bin/env python3
"""Fail when a dotnet test TRX report proves nothing: no passed test, or a skipped/failed one.

The MySQL tests skip themselves when ONLINESTORE_TEST_MYSQL is missing, and a skipped test
still lets `dotnet test` exit with 0. CI uses this check so "green" really means "ran on MySQL".
"""
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def summarize(trx_paths):
    counts = {"Passed": 0, "Failed": 0, "NotExecuted": 0, "Other": 0}
    for path in trx_paths:
        for result in ET.parse(path).getroot().iterfind(".//{*}UnitTestResult"):
            outcome = result.get("outcome", "")
            counts[outcome if outcome in counts else "Other"] += 1
    return counts


def main(argv):
    if len(argv) != 2:
        print("usage: ci_require_executed.py <directory with .trx files>", file=sys.stderr)
        return 2
    reports = sorted(Path(argv[1]).glob("*.trx"))
    if not reports:
        print("No .trx report found: the tests did not run.", file=sys.stderr)
        return 1
    counts = summarize(reports)
    print(", ".join(f"{name}={value}" for name, value in counts.items()))
    if counts["Passed"] == 0:
        print("No test passed: nothing was proven.", file=sys.stderr)
        return 1
    if counts["NotExecuted"] or counts["Failed"] or counts["Other"]:
        print("Some tests were skipped, failed or ended in an unknown state.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
