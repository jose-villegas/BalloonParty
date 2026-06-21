#!/usr/bin/env python3
"""Tests for style_audit.py — fixture snippets paired with the violations they must (and must
not) produce. Pure-Python, no pytest. Run:  python3 Tools/test_style_audit.py

Each check is exercised in isolation: a snippet is fed to one rule's check function and the
violation lines for that rule are compared against expectations. Globals that depend on a full
tree scan (lifecycle types, editor-referenced names, internal types) are stubbed per test so the
suite is hermetic and fast."""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import style_audit as A  # noqa: E402

_results = {"pass": 0, "fail": 0}


def _violations(rule, snippet, *, path="Foo/Bar.cs", lifecycle=None, editor_names=None,
                internal=None, mono=None):
    if lifecycle is not None:
        A._LIFECYCLE_TYPES = set(lifecycle)
    if editor_names is not None:
        A._EDITOR_REFERENCED_NAMES = set(editor_names)
    if internal is not None:
        A._INTERNAL_TYPES = set(internal)
    if mono is not None:
        A._MONO_SUBTYPES = set(mono)
    lines = snippet.splitlines(keepends=True)
    res = A.AuditResult()
    A.RULES[rule](A.SOURCE_ROOT / path, lines, res)
    return res


def expect_lines(label, rule, snippet, want_lines, **kw):
    # Only one check runs, so every violation came from it — no need to match the
    # registry key against the (sometimes different) Violation.rule string.
    res = _violations(rule, snippet, **kw)
    got = sorted(v.line for v in res.violations)
    _record(label, got, sorted(want_lines))


def expect_severity(label, rule, snippet, want_sev, **kw):
    res = _violations(rule, snippet, **kw)
    sev = res.violations[0].severity if res.violations else None
    _record(label, sev, want_sev)


def _record(label, got, want):
    ok = got == want
    _results["pass" if ok else "fail"] += 1
    print(f"  [{'ok  ' if ok else 'FAIL'}] {label}")
    if not ok:
        print(f"          got={got}  want={want}")


def expect_fix_output(label, fixfn, snippet, want):
    fixed = "".join(fixfn(A.SOURCE_ROOT / "Foo/Bar.cs", snippet.splitlines(keepends=True)))
    _record(label, fixed, want)


def expect_fix_clears(label, fixfn, rule, snippet):
    # Round-trip: a fixer's output must pass the matching check — the strongest guard that
    # detection and fix share one predicate and can't drift.
    fixed = fixfn(A.SOURCE_ROOT / "Foo/Bar.cs", snippet.splitlines(keepends=True))
    res = A.AuditResult()
    A.RULES[rule](A.SOURCE_ROOT / "Foo/Bar.cs", fixed, res)
    _record(label, [v.line for v in res.violations], [])


# ── member-ordering ───────────────────────────────────────────────────────────

# Regression: a nested `readonly struct` must NOT be read as a readonly field after a property.
expect_lines("member-ordering: nested readonly struct is not a field", "member-ordering", """\
namespace N
{
    internal class Bus
    {
        private readonly int _x = 0;
        internal int X => _x;
        internal readonly struct Impact
        {
            internal readonly int A;
        }
    }
}
""", [])

expect_lines("member-ordering: mutable field before readonly is flagged", "member-ordering", """\
namespace N
{
    internal class C
    {
        private bool _active;
        private readonly int _count = 0;
    }
}
""", [6])

# ── method-ordering (warning severity) ────────────────────────────────────────

expect_severity("method-ordering is a warning", "method-ordering", """\
namespace N
{
    internal class V
    {
        public void A() { }
        private void Awake() { }
    }
}
""", "warning", lifecycle={"V"})

expect_lines("method-ordering: lifecycle after method (MonoBehaviour)", "method-ordering", """\
namespace N
{
    internal class V
    {
        public void DoThing() { }
        private void Awake() { }
    }
}
""", [6], lifecycle={"V"})

expect_lines("method-ordering: correct order is clean", "method-ordering", """\
namespace N
{
    internal class V
    {
        private void Awake() { }
        private void Update() { }
        public void DoThing() { }
    }
}
""", [], lifecycle={"V"})

expect_lines("method-ordering: lifecycle call-order (Update before Awake)", "method-ordering", """\
namespace N
{
    internal class V
    {
        private void Update() { }
        private void Awake() { }
    }
}
""", [6], lifecycle={"V"})

# In a non-MonoBehaviour, Start()/Update() are plain methods, not lifecycle — so no flag.
expect_lines("method-ordering: Start() in plain class is not lifecycle", "method-ordering", """\
namespace N
{
    internal class Controller
    {
        public void DoThing() { }
        public void Start() { }
    }
}
""", [], lifecycle=set())

expect_lines("method-ordering: constructor after method is flagged", "method-ordering", """\
namespace N
{
    internal class C
    {
        public void DoThing() { }
        public C() { }
    }
}
""", [6], lifecycle=set())

# ── braces / allman / namespace / coroutines / magic-strings ──────────────────

expect_lines("braces-required: braceless if", "braces", """\
namespace N
{
    internal class C
    {
        private void M()
        {
            if (true)
                return;
        }
    }
}
""", [7])

expect_lines("allman: opening brace on same line", "allman", """\
namespace N
{
    internal class C {
    }
}
""", [3])

expect_lines("namespace: mismatch flagged", "namespace", """\
namespace Wrong.Namespace
{
    internal class C
    {
    }
}
""", [1], path="Foo/Bar.cs")

expect_lines("coroutines: IEnumerator usage", "coroutines", """\
namespace N
{
    internal class C
    {
        private IEnumerator Run()
        {
            yield return null;
        }
    }
}
""", [5])

expect_lines("magic-strings: uncached SetTrigger literal", "magic-strings", """\
namespace N
{
    internal class C
    {
        private void M()
        {
            _animator.SetTrigger("Jump");
        }
    }
}
""", [7])

# Comment/string stripping: a SetTrigger mentioned only in a comment must NOT flag.
expect_lines("magic-strings: commented example is ignored", "magic-strings", """\
namespace N
{
    internal class C
    {
        private void M()
        {
            // old: _animator.SetTrigger("Jump");
            var x = 1;
        }
    }
}
""", [])

# A brace inside a string initializer must not corrupt member-ordering's brace depth — without
# the code-view the "}" would close the class early and the readonly-after-mutable goes unseen.
expect_lines("member-ordering: brace in string doesn't corrupt depth", "member-ordering", """\
namespace N
{
    internal class C
    {
        private readonly string _x = "}";
        private bool _active;
        private readonly int _y = 0;
    }
}
""", [7])

# ── severity model ────────────────────────────────────────────────────────────

expect_severity("braces is an error", "braces", """\
namespace N
{
    internal class C
    {
        private void M()
        {
            if (true)
                return;
        }
    }
}
""", "error")

expect_severity("public-visibility is a warning", "public-visibility", """\
namespace N
{
    public class Unreferenced
    {
    }
}
""", "warning", internal={"Dummy"}, mono=set(), editor_names=set())

expect_lines("public-visibility: skipped when referenced cross-assembly", "public-visibility", """\
namespace N
{
    public class Shared
    {
    }
}
""", [], internal={"Dummy"}, mono=set(), editor_names={"Shared"})

# ── fixers (shared predicates: fix output must pass the matching check) ────────

expect_fix_output("fix braces: wraps a braceless if", A.fix_braces_required,
                  "if (x)\n    Foo();\n",
                  "if (x)\n{\n    Foo();\n}\n")

expect_fix_clears("round-trip: braces fix clears braces check", A.fix_braces_required, "braces",
                  "if (x)\n    Foo();\n")

expect_fix_clears("round-trip: allman fix clears allman check", A.fix_allman_braces, "allman",
                  "internal class C {\n}\n")

expect_fix_clears("round-trip: block-comment fix clears its check",
                  A.fix_block_comment_headers, "block-comments",
                  "// ======== Section ========\nvar x = 1;\n")

expect_fix_clears("round-trip: redundant-comment fix clears its check",
                  A.fix_redundant_comments, "redundant-comments",
                  "// constructor\npublic C() { }\n")


# ── cross-assembly helpers ────────────────────────────────────────────────────


def _helper_tests():
    # _code_view / _decommented preserve line count and blank the right things.
    sample = ['class C\n', '{\n', '    /* a\n', '       b */\n',
              '    var s = @"x{\n', '    y}";\n', '}\n']
    _record("code_view preserves line count", len(A._code_view(sample)), len(sample))
    _record("code_view blanks string quotes", '"' in "".join(A._code_view(sample)), False)
    _record("decommented keeps string content", '"' in "".join(A._decommented(sample)), True)

    label = "strip: type name only in a comment/string is not 'referenced'"
    cleaned = A._strip_comments_and_strings(
        '// MentionedInComment\nvar s = "AlsoString";\nvar c = new RealUse<int>();\n')
    import re
    ident = re.compile(r"[A-Za-z_]\w*")
    new_before = re.compile(r"\bnew\s+$")
    found = set()
    for m in ident.finditer(cleaned):
        t = m.group()
        if not t[0].isupper():
            continue
        st, en = m.start(), m.end()
        if st > 0 and cleaned[st - 1] == ".":
            continue
        if en < len(cleaned) and cleaned[en] == "(" and not new_before.search(cleaned[max(0, st - 12):st]):
            continue
        found.add(t)
    _record(label, ("MentionedInComment" in found, "AlsoString" in found, "RealUse" in found),
             (False, False, True))


def main():
    print("style_audit tests\n")
    _helper_tests()
    print(f"\n  {_results['pass']} passed, {_results['fail']} failed")
    sys.exit(1 if _results["fail"] else 0)


if __name__ == "__main__":
    main()
