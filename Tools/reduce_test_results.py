#!/usr/bin/env python3
"""Reduce a Unity/NUnit3 test-results XML to a compact pass/fail summary.

The raw results file is large (every passed case, full stack traces, output
capture). This keeps only what's needed to see whether the run is green and,
if not, which tests failed and why — a few lines instead of thousands.

Usage:
    reduce_test_results.py <results.xml> [--out <file>] [--max-msg-lines N]
"""

import argparse
import sys
import xml.etree.ElementTree as ET


def _first_lines(text, count):
    if not text:
        return []
    lines = [line.strip() for line in text.strip().splitlines() if line.strip()]
    return lines[:count]


def _failure_detail(case, max_lines):
    failure = case.find("failure")
    if failure is None:
        return []
    message = failure.findtext("message") or ""
    detail = _first_lines(message, max_lines)
    if detail:
        return detail
    # No message — fall back to the first stack-trace frame for a hint.
    trace = _first_lines(failure.findtext("stack-trace") or "", 1)
    return trace


def reduce_results(xml_path, max_msg_lines):
    try:
        root = ET.parse(xml_path).getroot()
    except (ET.ParseError, FileNotFoundError, OSError) as error:
        return f"# EditMode tests — could not read results\n\n{xml_path}: {error}\n"

    # NUnit3 counts live on the <test-run> root; fall back to walking cases.
    cases = root.findall(".//test-case")
    failed_cases = [c for c in cases if c.get("result") == "Failed"]

    total = root.get("total") or str(len(cases))
    passed = root.get("passed") or str(sum(1 for c in cases if c.get("result") == "Passed"))
    failed = root.get("failed") or str(len(failed_cases))
    skipped = root.get("skipped") or str(sum(1 for c in cases if c.get("result") == "Skipped"))
    duration = root.get("duration")
    outcome = "PASSED" if failed == "0" and root.get("result") != "Failed" else "FAILED"

    lines = [f"# EditMode tests — {outcome}", ""]
    summary = f"total {total} · passed {passed} · failed {failed} · skipped {skipped}"
    if duration:
        try:
            summary += f" · {float(duration):.1f}s"
        except ValueError:
            pass
    lines.append(summary)

    if failed_cases:
        lines += ["", "## Failures", ""]
        for case in failed_cases:
            name = case.get("fullname") or case.get("name") or "<unnamed>"
            lines.append(f"- {name}")
            for detail in _failure_detail(case, max_msg_lines):
                lines.append(f"    {detail}")

    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description="Reduce NUnit3 test results to a compact summary.")
    parser.add_argument("xml", help="Path to the NUnit3 results XML.")
    parser.add_argument("--out", help="Also write the summary to this file.")
    parser.add_argument("--max-msg-lines", type=int, default=2,
                        help="Max lines of each failure message to keep (default 2).")
    args = parser.parse_args()

    summary = reduce_results(args.xml, args.max_msg_lines)
    sys.stdout.write(summary)
    if args.out:
        with open(args.out, "w", encoding="utf-8") as handle:
            handle.write(summary)


if __name__ == "__main__":
    main()
