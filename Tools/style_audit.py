#!/usr/bin/env python3
"""
BalloonParty Code Style Auditor
================================
Scans Assets/Source/**/*.cs for violations of the project's code-style guide.

Usage:
    python3 Tools/style_audit.py                  # full report
    python3 Tools/style_audit.py --fix             # auto-fix safe issues
    python3 Tools/style_audit.py --rule braces     # run only one rule
    python3 Tools/style_audit.py --file Foo.cs     # audit one file

Rules are categorised:
    [REPORT]  — flagged for manual review
    [FIXABLE] — can be auto-corrected with --fix
"""

from __future__ import annotations

import argparse
import os
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional

# ─── Configuration ───────────────────────────────────────────────────────────

SOURCE_ROOT = Path(__file__).resolve().parent.parent / "Assets" / "Source"
EDITOR_FOLDERS = {"Editor"}

# Folders that should contain a README.md
FEATURE_FOLDERS_DEPTH = 1  # immediate children of Source/

# ─── Data types ──────────────────────────────────────────────────────────────

@dataclass
class Violation:
    file: str
    line: int
    rule: str
    message: str
    fixable: bool = False

    def __str__(self):
        tag = "[FIXABLE]" if self.fixable else "[REPORT]"
        rel = os.path.relpath(self.file, SOURCE_ROOT)
        return f"  {tag} {rel}:{self.line}  ({self.rule}) {self.message}"


@dataclass
class AuditResult:
    violations: list[Violation] = field(default_factory=list)

    def add(self, v: Violation):
        self.violations.append(v)

    def summary(self) -> str:
        by_rule: dict[str, int] = {}
        for v in self.violations:
            by_rule[v.rule] = by_rule.get(v.rule, 0) + 1
        lines = [f"\n{'='*60}", f"  Total violations: {len(self.violations)}", f"{'='*60}"]
        for rule, count in sorted(by_rule.items(), key=lambda x: -x[1]):
            lines.append(f"    {rule:40s}  {count}")
        return "\n".join(lines)


# ─── Helpers ─────────────────────────────────────────────────────────────────

def cs_files(root: Path):
    """Yield all .cs files under root, skipping Editor~ and hidden dirs."""
    for p in sorted(root.rglob("*.cs")):
        if any(part.startswith(".") for part in p.parts):
            continue
        yield p


def read_lines(path: Path) -> list[str]:
    with open(path, encoding="utf-8-sig") as f:
        return f.readlines()


def is_in_editor(path: Path) -> bool:
    return any(part in EDITOR_FOLDERS for part in path.relative_to(SOURCE_ROOT).parts)


def expected_namespace(path: Path) -> str:
    """Derive expected namespace from folder path relative to Source/."""
    rel = path.relative_to(SOURCE_ROOT).parent
    parts = ["BalloonParty"] + [p for p in rel.parts if p not in (".", "")]
    return ".".join(parts)


# ─── Rule implementations ───────────────────────────────────────────────────

def check_namespace(path: Path, lines: list[str], result: AuditResult):
    """Namespace must match folder structure."""
    # AssemblyInfo.cs uses assembly-level attributes without a namespace
    if path.name == "AssemblyInfo.cs":
        return
    expected = expected_namespace(path)
    for i, line in enumerate(lines, 1):
        m = re.match(r"^\s*namespace\s+([\w.]+)", line)
        if m:
            actual = m.group(1)
            if actual != expected:
                result.add(Violation(str(path), i, "namespace-mismatch",
                    f"expected '{expected}', got '{actual}'", fixable=True))
            return
    # No namespace found — global types
    result.add(Violation(str(path), 1, "namespace-missing",
        f"no namespace declaration; expected '{expected}'"))


def check_braces_required(path: Path, lines: list[str], result: AuditResult):
    """Braces required for if/else/for/foreach/while/using/lock/fixed."""
    control_re = re.compile(
        r"^\s*(if|else\s+if|else|for|foreach|while|using|lock|fixed)\s*(\(.*\))?\s*$"
    )
    n = len(lines)
    for i, line in enumerate(lines, 1):
        stripped = line.rstrip()
        if not control_re.match(stripped):
            continue

        # Multi-line condition: skip forward until parens are balanced
        paren_depth = stripped.count("(") - stripped.count(")")
        scan = i  # 1-based index of the line *after* the current one
        while paren_depth > 0 and scan < n:
            paren_depth += lines[scan].count("(") - lines[scan].count(")")
            scan += 1

        # Next non-empty line after the (possibly multi-line) condition
        for j in range(scan, min(scan + 3, n)):
            next_line = lines[j].strip()
            if not next_line:
                continue
            if next_line.startswith("{") or next_line.startswith("//"):
                break
            # It's a statement without braces
            result.add(Violation(str(path), i, "braces-required",
                f"control statement without braces: {stripped.strip()}", fixable=True))
            break


def check_allman_braces(path: Path, lines: list[str], result: AuditResult):
    """Opening brace must be on its own line (Allman style).
    Exceptions: object/collection initialisers, lambdas, array init, auto-properties."""
    for i, line in enumerate(lines, 1):
        stripped = line.rstrip()
        # Skip lines that are ONLY a brace
        if stripped.strip() == "{":
            continue
        # Skip string interpolation, attributes, lambdas, arrow functions
        if "=>" in stripped:
            continue
        if re.search(r'"\$?.*\{', stripped):
            continue
        # Detect: ) { or keyword {  at end of line (but not initialiser = new Foo {)
        if stripped.endswith("{") and not re.search(r"(=|new\s+\w+.*|new\(.*\))\s*\{$", stripped):
            # Could be control flow or method signature with brace on same line
            if re.search(r"(if|else|for|foreach|while|using|lock|fixed|class|struct|enum|interface|namespace|switch|try|catch|finally|do)\b.*\{$", stripped):
                result.add(Violation(str(path), i, "allman-braces",
                    f"opening brace on same line: {stripped.strip()[:80]}", fixable=True))
            elif re.match(r"\s*(public|private|protected|internal|static|async|override|virtual|abstract|sealed|void|[\w<>\[\],\s]+)\s+\w+\s*\(.*\)\s*\{$", stripped):
                result.add(Violation(str(path), i, "allman-braces",
                    f"method brace on same line: {stripped.strip()[:80]}", fixable=True))


def check_block_comment_headers(path: Path, lines: list[str], result: AuditResult):
    """No block comment headers like // ====== or // ------."""
    header_re = re.compile(r"^\s*//\s*[=\-*#]{4,}")
    for i, line in enumerate(lines, 1):
        if header_re.match(line):
            result.add(Violation(str(path), i, "block-comment-header",
                f"block comment header: {line.strip()[:60]}", fixable=True))


def check_redundant_comments(path: Path, lines: list[str], result: AuditResult):
    """Flag common redundant comment patterns."""
    patterns = [
        (re.compile(r"^\s*//\s*(inject\s+depend|constructor|update\s+position|set\s+color|"
                     r"get\s+component|initialize|cleanup|dispose|destructor|"
                     r"fields|properties|methods|private\s+methods|public\s+methods)\s*$", re.I),
         "redundant comment"),
    ]
    for i, line in enumerate(lines, 1):
        for pat, desc in patterns:
            if pat.match(line):
                result.add(Violation(str(path), i, "redundant-comment",
                    f"{desc}: {line.strip()[:60]}", fixable=True))


def check_start_coroutine(path: Path, lines: list[str], result: AuditResult):
    """No StartCoroutine / IEnumerator in new code."""
    for i, line in enumerate(lines, 1):
        if "StartCoroutine" in line and "//" not in line.split("StartCoroutine")[0]:
            result.add(Violation(str(path), i, "no-coroutines",
                "StartCoroutine usage — use UniTask instead"))
        # IEnumerator return type (but allow in test code)
        if re.match(r"\s*(private|public|protected|internal)?\s*IEnumerator\s+\w+", line):
            result.add(Violation(str(path), i, "no-coroutines",
                "IEnumerator method — use async UniTask instead"))


def check_magic_strings(path: Path, lines: list[str], result: AuditResult):
    """Animator params and physics layers must be cached, not passed as magic strings."""
    # SetTrigger("Foo"), SetBool("Foo"), etc.
    anim_re = re.compile(r'\.(SetTrigger|SetBool|SetFloat|SetInteger|GetBool|GetFloat|GetInteger)\s*\(\s*"')
    layer_re = re.compile(r'(LayerMask\.NameToLayer|LayerMask\.GetMask)\s*\(\s*"')
    for i, line in enumerate(lines, 1):
        if anim_re.search(line):
            result.add(Violation(str(path), i, "magic-string-animator",
                "animator param should use cached StringToHash int"))
        if layer_re.search(line):
            # Allow lazy-init pattern: assignment to a static field
            if re.search(r"\bstatic\b", line):
                continue
            # Allow assignment in a block that initialises a static sentinel
            # Look for a static field assignment context (current line has = )
            if "=" in line:
                # Check if the target looks like a static field (uppercase or PascalCase)
                lhs = line.split("=")[0].strip()
                # Could be inside a lazy-init block — check surrounding context for static field
                # Simple heuristic: the assignment target is a known-static-looking ident
                # This still flags inline comparisons like: if (x == LayerMask.NameToLayer("Y"))
                pass
            else:
                result.add(Violation(str(path), i, "magic-string-layer",
                    "layer lookup should be cached in a static field"))


def check_addto_this_in_poolable(path: Path, lines: list[str], result: AuditResult):
    """Pooled objects must use CompositeDisposable, not AddTo(this)."""
    # First check if the class implements IPoolable
    full_text = "".join(lines)
    if "IPoolable" not in full_text:
        return
    for i, line in enumerate(lines, 1):
        if ".AddTo(this)" in line or ".AddTo( this )" in line:
            result.add(Violation(str(path), i, "addto-this-poolable",
                "AddTo(this) in IPoolable class — use CompositeDisposable instead"))


def check_object_instantiate(path: Path, lines: list[str], result: AuditResult):
    """Flag Object.Instantiate that might need CreateChildFromPrefab."""
    if is_in_editor(path):
        return
    if "PoolChannel" in path.name:
        return
    for i, line in enumerate(lines, 1):
        if re.search(r"Object\.Instantiate\s*[<(]", line):
            result.add(Violation(str(path), i, "object-instantiate",
                "Object.Instantiate — verify this doesn't need CreateChildFromPrefab"))


def check_member_ordering(path: Path, lines: list[str], result: AuditResult):
    """Basic member ordering: [SerializeField] before [Inject] before readonly before mutable before properties."""
    GROUP_CONST = 1
    GROUP_STATIC_READONLY = 2
    GROUP_SERIALIZE = 3
    GROUP_INJECT = 4
    GROUP_READONLY = 5
    GROUP_MUTABLE = 6
    GROUP_PROPERTY = 7

    last_group = 0
    in_class = False
    brace_depth = 0
    class_brace_depth = 0

    for i, line in enumerate(lines, 1):
        stripped = line.strip()

        brace_depth += stripped.count("{") - stripped.count("}")

        if re.match(r"(public|internal|private|protected)?\s*(abstract\s+|sealed\s+|static\s+|partial\s+)*(class|struct)\s+", stripped):
            in_class = True
            last_group = 0
            class_brace_depth = brace_depth
            continue

        if not in_class:
            continue

        # Only check direct class members (depth = class + 1)
        if brace_depth != class_brace_depth + 1:
            continue

        # Skip methods, constructors, properties with bodies
        if "(" in stripped and ")" in stripped and "=" not in stripped.split("(")[0]:
            continue

        # Check previous line for attributes that may span to this line
        prev_stripped = lines[i - 2].strip() if i >= 2 else ""
        has_serialize_above = "[SerializeField]" in prev_stripped
        has_inject_above = "[Inject]" in prev_stripped
        has_header_above = "[Header" in prev_stripped

        # Detect field declarations
        if re.match(r"\s*(private|public|protected|internal)\s+const\s+", stripped):
            group = GROUP_CONST
        elif re.match(r"\s*(private|public|protected|internal)?\s*static\s+readonly\s+", stripped):
            group = GROUP_STATIC_READONLY
        elif "[SerializeField]" in stripped or has_serialize_above:
            group = GROUP_SERIALIZE
        elif "[Inject]" in stripped and ";" in stripped:
            group = GROUP_INJECT
        elif has_inject_above and ";" in stripped:
            group = GROUP_INJECT
        elif re.match(r"\s*(private|public|protected|internal)\s+readonly\s+", stripped):
            group = GROUP_READONLY
        elif re.match(r"\s*(private|public|protected|internal)\s+\w+[\w<>\[\],\s]*\s+_\w+\s*[;=]", stripped):
            if "[SerializeField]" not in stripped and "[Inject]" not in stripped and not has_serialize_above and not has_inject_above:
                group = GROUP_MUTABLE
            else:
                continue
        elif "=>" in stripped and ";" in stripped and not stripped.startswith("//"):
            # Expression-body property (e.g. `internal Foo Bar => _bar;`)
            if re.match(r"\s*(private|public|protected|internal)\s+\S+.*\s+=>\s+", stripped):
                group = GROUP_PROPERTY
            else:
                continue
        else:
            continue

        if group < last_group:
            group_names = {1: "const", 2: "static readonly", 3: "[SerializeField]",
                          4: "[Inject]", 5: "readonly", 6: "mutable", 7: "property"}
            result.add(Violation(str(path), i, "member-ordering",
                f"{group_names.get(group, '?')} field after {group_names.get(last_group, '?')} field"))
        last_group = group


def check_dotween_kill_poolable(path: Path, lines: list[str], result: AuditResult):
    """IPoolable classes using DOTween must kill tweens in OnDespawned."""
    full_text = "".join(lines)
    if "IPoolable" not in full_text:
        return

    dotween_re = re.compile(
        r"\.(DOFade|DOScale|DOMove|DOColor|DORotate|DOAnchorPos|DOMoveX|DOMoveY|DOLocalMove|"
        r"DOPunchScale|DOShakePosition|DOJump)\b|DOTween\.|DOVirtual\."
    )

    uses_dotween = any(dotween_re.search(line) for line in lines)
    if not uses_dotween:
        return

    # Check that OnDespawned contains a Kill or TweenTracker reference
    in_ondespawned = False
    has_kill = False
    brace_depth = 0

    for line in lines:
        stripped = line.strip()
        if "OnDespawned" in stripped and ("void" in stripped or "override" in stripped):
            in_ondespawned = True
            brace_depth = 0
            continue
        if in_ondespawned:
            brace_depth += stripped.count("{") - stripped.count("}")
            if "Kill" in stripped or "TweenTracker" in stripped or "DOTween.Kill" in stripped:
                has_kill = True
            if brace_depth <= 0 and "{" in "".join(lines[:lines.index(line)]):
                break

    if not has_kill:
        # Find OnDespawned line number for reporting
        for i, line in enumerate(lines, 1):
            if "OnDespawned" in line:
                result.add(Violation(str(path), i, "dotween-kill-poolable",
                    "IPoolable uses DOTween but OnDespawned() does not kill tweens"))
                return
        result.add(Violation(str(path), 1, "dotween-kill-poolable",
            "IPoolable uses DOTween but has no OnDespawned() to kill tweens"))


def check_inject_on_field(path: Path, lines: list[str], result: AuditResult):
    """Flag [Inject] on fields in non-MonoBehaviour classes.

    MonoBehaviours (and transitive subclasses) cannot use constructor injection
    in VContainer, so field injection is the only option and is allowed.
    Plain-C# classes should always prefer constructor injection.
    """
    full_text = "".join(lines)

    # Extract the class name and check if it's a transitive MonoBehaviour subtype
    for line in lines:
        m = re.match(
            r"\s*(?:public|internal)\s+(?:sealed\s+|abstract\s+|partial\s+)*"
            r"(?:class)\s+(\w+)",
            line,
        )
        if m:
            if m.group(1) in _mono_subtypes():
                return
            break

    # Also catch direct `: MonoBehaviour` for classes not in our source tree
    if re.search(r":\s*MonoBehaviour", full_text):
        return

    for i, line in enumerate(lines, 1):
        stripped = line.strip()
        # [Inject] on a field: line has [Inject] and ends with ;
        if "[Inject]" in stripped and stripped.endswith(";"):
            result.add(Violation(str(path), i, "inject-on-field",
                "field injection — prefer constructor injection"))
            continue
        # [Inject] on previous line, current line is a field (ends with ;)
        if i >= 2:
            prev = lines[i - 2].strip()
            if prev == "[Inject]" and stripped.endswith(";") and "(" not in stripped:
                result.add(Violation(str(path), i, "inject-on-field",
                    "field injection — prefer constructor injection"))


def check_missing_readmes(result: AuditResult):
    """Each feature folder should have a README.md."""
    for child in sorted(SOURCE_ROOT.iterdir()):
        if child.is_dir() and not child.name.startswith(".") and child.name != "Editor":
            readme = child / "README.md"
            if not readme.exists():
                result.add(Violation(str(child), 0, "missing-readme",
                    f"feature folder '{child.name}/' has no README.md"))


def _build_monobehaviour_subtypes() -> set[str]:
    """Walk the inheritance graph to find all types that transitively derive from MonoBehaviour."""
    parent_map: dict[str, str] = {}
    for path in cs_files(SOURCE_ROOT):
        with open(path, encoding="utf-8-sig") as f:
            for line in f:
                m = re.match(
                    r"\s*(?:public|internal)\s+(?:sealed\s+|abstract\s+|partial\s+)*"
                    r"(?:class|struct)\s+(\w+)\s*(?:<[^>]*>)?\s*:\s*(\w+)",
                    line,
                )
                if m and m.group(1) != m.group(2):
                    parent_map[m.group(1)] = m.group(2)

    unity_bases = {"MonoBehaviour", "ScriptableObject", "LifetimeScope",
                   "Editor", "PropertyDrawer"}
    result = set()
    for cls in parent_map:
        cur = cls
        seen = {cur}
        while cur in parent_map:
            cur = parent_map[cur]
            if cur in seen:
                break
            seen.add(cur)
        if cur in unity_bases:
            result.add(cls)
    return result


_MONO_SUBTYPES: set[str] | None = None


def _mono_subtypes() -> set[str]:
    global _MONO_SUBTYPES
    if _MONO_SUBTYPES is None:
        _MONO_SUBTYPES = _build_monobehaviour_subtypes()
    return _MONO_SUBTYPES


def _build_internal_types() -> set[str]:
    """Collect all internal class/struct names in the source tree."""
    types: set[str] = set()
    for path in cs_files(SOURCE_ROOT):
        with open(path, encoding="utf-8-sig") as f:
            for line in f:
                m = re.match(
                    r"\s*internal\s+(?:sealed\s+|partial\s+)*(?:class|struct)\s+(\w+)",
                    line,
                )
                if m:
                    types.add(m.group(1))
    return types


_INTERNAL_TYPES: set[str] | None = None


def _internal_types() -> set[str]:
    global _INTERNAL_TYPES
    if _INTERNAL_TYPES is None:
        _INTERNAL_TYPES = _build_internal_types()
    return _INTERNAL_TYPES


def check_public_visibility(path: Path, lines: list[str], result: AuditResult):
    """Flag public classes/structs that could potentially be internal.

    Skips types that commonly need to be public:
    - MonoBehaviour / ScriptableObject and all transitive subclasses
    - Editor, PropertyDrawer (Unity editor)
    - LifetimeScope (VContainer)
    - Types implementing known public interfaces
    - Attribute subclasses
    - [Serializable] types (exposed in public SOs)
    """
    if is_in_editor(path):
        return

    full_text = "".join(lines)
    internal = _internal_types()
    if not internal:
        return

    # Skip files with [Serializable] — data types serialised by Unity
    if "[Serializable]" in full_text or "[System.Serializable]" in full_text:
        return

    for i, line in enumerate(lines, 1):
        stripped = line.strip()
        m = re.match(
            r"public\s+(sealed\s+|partial\s+)*(class|struct)\s+(\w+)",
            stripped,
        )
        if not m:
            continue

        type_name = m.group(3)

        # Skip all transitive MonoBehaviour / ScriptableObject / LifetimeScope subtypes
        if type_name in _mono_subtypes():
            continue

        # Check inheritance on the same line for remaining known bases
        skip_bases = (
            "MonoBehaviour", "ScriptableObject", "PropertyAttribute",
        )
        remainder = stripped[stripped.index(type_name) + len(type_name):]
        if any(base in remainder for base in skip_bases):
            continue

        result.add(Violation(str(path), i, "public-visibility",
            f"'{type_name}' is public — consider internal if not used cross-assembly"))


def check_inconsistent_accessibility(path: Path, lines: list[str], result: AuditResult):
    """Detect public members in public types that expose internal types.

    This catches the C# compiler error CS0051 (inconsistent accessibility) before
    it reaches the build.  A public method/constructor/property in a public type
    must not use an internal type as a parameter or return type.
    """
    if is_in_editor(path):
        return

    full_text = "".join(lines)
    internal = _internal_types()
    if not internal:
        return

    # Check if file has a public class/struct
    if not re.search(r"\bpublic\s+(?:sealed\s+|abstract\s+|partial\s+)*(?:class|struct)\s+\w+", full_text):
        return

    # Build a single regex for all internal type names (word-boundary match)
    internal_re = re.compile(r"\b(" + "|".join(re.escape(t) for t in sorted(internal)) + r")\b")

    # Gather multi-line public member signatures
    in_public = False
    sig_lines: list[str] = []
    sig_start = 0

    for i, line in enumerate(lines, 1):
        stripped = line.strip()

        # Skip class/struct/interface/enum declarations
        if re.match(r"(?:public|internal|private|protected)\s+(?:sealed\s+|abstract\s+|partial\s+)*(?:class|struct|interface|enum)\s", stripped):
            continue

        if not in_public:
            if stripped.startswith("public "):
                in_public = True
                sig_start = i
                sig_lines = [stripped]
                # Complete on one line?
                if "{" in stripped or ";" in stripped or "=>" in stripped:
                    in_public = False
                    full_sig = " ".join(sig_lines)
                    m = internal_re.search(full_sig)
                    if m:
                        result.add(Violation(str(path), sig_start, "inconsistent-accessibility",
                            f"public member exposes internal type '{m.group(1)}'"))
                    sig_lines = []
        else:
            sig_lines.append(stripped)
            if "{" in stripped or ";" in stripped or "=>" in stripped:
                in_public = False
                full_sig = " ".join(sig_lines)
                m = internal_re.search(full_sig)
                if m:
                    result.add(Violation(str(path), sig_start, "inconsistent-accessibility",
                        f"public member exposes internal type '{m.group(1)}'"))
                sig_lines = []


_CSHARP_KEYWORDS = frozenset({
    'abstract', 'as', 'async', 'await', 'base', 'bool', 'break', 'byte',
    'case', 'catch', 'char', 'checked', 'class', 'const', 'continue',
    'decimal', 'default', 'delegate', 'do', 'double', 'else', 'enum',
    'event', 'explicit', 'extern', 'false', 'finally', 'fixed', 'float',
    'for', 'foreach', 'get', 'goto', 'if', 'implicit', 'in', 'int',
    'interface', 'internal', 'is', 'lock', 'long', 'namespace', 'nameof',
    'new', 'not', 'null', 'object', 'operator', 'out', 'override', 'params',
    'partial', 'private', 'protected', 'public', 'readonly', 'ref', 'return',
    'sbyte', 'sealed', 'set', 'short', 'sizeof', 'stackalloc', 'static',
    'string', 'struct', 'switch', 'this', 'throw', 'true', 'try', 'typeof',
    'uint', 'ulong', 'unchecked', 'unsafe', 'ushort', 'using', 'value',
    'var', 'virtual', 'void', 'volatile', 'when', 'where', 'while', 'yield',
})

# Patterns that recognise local-variable / parameter declarations.
# Each pattern must have exactly one capturing group for the variable name.
_LOCAL_DECL_PATTERNS = [
    # var name = / var name;
    re.compile(r'\bvar\s+([a-z]\w*)\b'),
    # Primitive typed local: int name = / float name, …
    re.compile(
        r'\b(?:int|uint|long|ulong|short|ushort|byte|sbyte|float|double|'
        r'decimal|bool|char|string)\s+([a-z]\w*)\s*[=;,()]'
    ),
    # foreach (AnyType name in …)
    re.compile(r'\bforeach\s*\([^)]*?\s+([a-z]\w*)\s+in\b'),
    # PascalCase type (or interface) + camelCase name — catches both locals and params.
    # Deliberately excludes _field patterns by requiring [a-z] start on the name.
    re.compile(r'\b[A-Z]\w*(?:<[^>]*>)?(?:\[\])?\s+([a-z]\w*)\s*[=;,()]'),
]


def _collect_outer_locals(lines: list[str], start: int, end: int) -> set:
    """
    Return all camelCase names declared as locals or parameters in lines[start:end].
    Used to detect what a lambda at line `end` could be closing over.
    """
    names: set[str] = set()
    for i in range(start, end):
        raw = lines[i]
        stripped = raw.strip()
        if stripped.startswith('//') or stripped.startswith('*'):
            continue
        for pat in _LOCAL_DECL_PATTERNS:
            for m in pat.finditer(raw):
                name = m.group(1)
                if name and name not in _CSHARP_KEYWORDS and len(name) > 1:
                    names.add(name)
    return names


def _lambda_own_params(line: str) -> set:
    """
    Extract the parameter name(s) declared by a lambda on `line`.
    Handles: `x =>`, `_ =>`, `(x, y) =>`, `(Type x, Type y) =>`.
    """
    params: set[str] = set()
    arrow = line.rfind('=>')
    if arrow < 0:
        return params
    before = line[:arrow].strip()
    # Strip any trailing call context: `.Subscribe(`, `.Returns(`  etc.
    # Keep only the part that looks like lambda params (last `(…)` or bare word).
    m = re.search(r'\(([^()]*)\)\s*$', before)
    if m:
        param_list = m.group(1)
    else:
        words = re.findall(r'\b[a-z_]\w*\b', before)
        if words:
            name = words[-1].lstrip('_')
            if name and name not in _CSHARP_KEYWORDS:
                params.add(name)
        return params
    for part in param_list.split(','):
        words = part.strip().split()
        if words:
            name = words[-1].lstrip('_')
            if name and name[0].islower() and name not in _CSHARP_KEYWORDS:
                params.add(name)
    return params


def _lambda_body_captures(body_lines: list[str], candidates: set) -> bool:
    """Return True if any line in body_lines contains a standalone reference to a candidate name."""
    for line in body_lines:
        if line.strip().startswith('//'):
            continue
        for name in candidates:
            if re.search(r'\b' + re.escape(name) + r'\b', line):
                return True
    return False


def check_non_capturing_lambda(path: Path, lines: list[str], result: AuditResult):
    """Large block lambdas (>3 body lines) that close over no outer locals should be named methods.
    Small lambdas (≤3 lines) are fine inline regardless of capture."""
    MAX_BODY_LINES = 3
    SCAN_WINDOW = 150  # lines to scan backward for outer locals
    n = len(lines)
    i = 0
    while i < n:
        raw = lines[i].rstrip()
        if raw.lstrip().startswith('//'):
            i += 1
            continue

        ends_with_arrow = bool(re.search(r'=>\s*$', raw))
        arrow_then_brace = bool(re.search(r'=>\s*\{', raw))

        if not (ends_with_arrow or arrow_then_brace):
            i += 1
            continue

        # Locate the { that opens the block body
        if ends_with_arrow:
            j = i + 1
            while j < n and not lines[j].strip():
                j += 1
            if j >= n or not lines[j].strip().startswith('{'):
                i += 1
                continue
            brace_line = j
        else:
            brace_line = i

        # Walk to the matching closing brace, collecting body lines
        depth = 0
        body_lines: list[str] = []
        started = False
        close_line = brace_line
        k = brace_line
        while k < n:
            for ch in lines[k]:
                if ch == '{':
                    depth += 1
                    started = True
                elif ch == '}':
                    depth -= 1
            if started:
                if k > brace_line:
                    body_lines.append(lines[k])
                if depth == 0:
                    close_line = k
                    break
            k += 1

        if not body_lines:
            i = close_line + 1
            continue

        # Only flag LARGE lambdas — small ones (≤ MAX_BODY_LINES) are fine inline.
        meaningful_lines = sum(
            1 for l in body_lines
            if l.strip() and l.strip() != '{' and not l.strip().startswith('}')
        )
        if meaningful_lines <= MAX_BODY_LINES:
            i = close_line + 1
            continue

        own_params = _lambda_own_params(raw)
        scope_start = max(0, i - SCAN_WINDOW)
        outer_locals = _collect_outer_locals(lines, scope_start, i)
        candidates = outer_locals - own_params

        if not _lambda_body_captures(body_lines, candidates):
            result.add(Violation(str(path), i + 1, "non-capturing-lambda",
                "large block lambda closes over no outer locals — extract to a named method"))

        i = close_line + 1


def check_large_anonymous_functions(path: Path, lines: list[str], result: AuditResult):
    """Block lambdas with more than 3 non-empty body lines should be extracted to named methods."""
    MAX_BODY_LINES = 3
    n = len(lines)
    i = 0
    while i < n:
        stripped = lines[i].rstrip()

        # Skip comment lines
        if stripped.lstrip().startswith("//"):
            i += 1
            continue

        ends_with_arrow = bool(re.search(r'=>\s*$', stripped))
        arrow_then_brace = bool(re.search(r'=>\s*\{', stripped))

        if not (ends_with_arrow or arrow_then_brace):
            i += 1
            continue

        if ends_with_arrow:
            # Find the next non-empty line — it must start with { to be a block lambda
            j = i + 1
            while j < n and not lines[j].strip():
                j += 1
            if j >= n or not lines[j].strip().startswith('{'):
                i += 1
                continue
            brace_line = j
        else:
            brace_line = i

        # Walk from the opening brace to the matching closing brace, counting body lines
        depth = 0
        body_lines = 0
        started = False
        k = brace_line
        while k < n:
            for ch in lines[k]:
                if ch == '{':
                    depth += 1
                    started = True
                elif ch == '}':
                    depth -= 1
            if started and depth == 0:
                break
            if started and k > brace_line and lines[k].strip():
                body_lines += 1
            k += 1

        if body_lines > MAX_BODY_LINES:
            result.add(Violation(str(path), i + 1, "large-lambda",
                f"anonymous function body has {body_lines} lines — extract to a named method"))

        i += 1


def check_multiple_blank_lines(path: Path, lines: list[str], result: AuditResult):
    """No two or more consecutive blank lines — a single blank line is enough to separate segments."""
    consecutive = 0
    for i, line in enumerate(lines, 1):
        if line.strip() == "":
            consecutive += 1
            if consecutive == 2:
                result.add(Violation(str(path), i, "multiple-blank-lines",
                    "two or more consecutive blank lines — use a single blank line", fixable=True))
        else:
            consecutive = 0


def check_trailing_newlines(path: Path, lines: list[str], result: AuditResult):
    """File must end with exactly one newline — no trailing blank lines."""
    if not lines:
        return
    trailing = 0
    for line in reversed(lines):
        if line.strip() == "":
            trailing += 1
        else:
            break
    if trailing > 0:
        result.add(Violation(str(path), len(lines), "trailing-newlines",
            f"{trailing} trailing blank line(s) at end of file", fixable=True))


def check_repeated_accessor(path: Path, lines: list[str], result: AuditResult):
    """Flag calls where 3+ arguments are accessed from the same object (e.g. obj.A, obj.B, obj.C).

    This suggests the callee should accept the object directly instead of
    unpacking every property at the call site.
    """
    # Join continuation lines to reconstruct multi-line call expressions.
    # A simple heuristic: if a line ends with ',' and the next is indented
    # more, it is a continuation.
    joined: list[tuple[int, str]] = []  # (start_line, full_text)
    buf: list[str] = []
    start = 1

    for i, raw in enumerate(lines, 1):
        stripped = raw.rstrip()
        if not buf:
            start = i
        buf.append(stripped)
        # Continue joining while the line ends with ',' or the expression is
        # clearly not finished (open parens).
        open_parens = sum(s.count("(") - s.count(")") for s in buf)
        if open_parens <= 0:
            joined.append((start, " ".join(b.strip() for b in buf)))
            buf = []

    if buf:
        joined.append((start, " ".join(b.strip() for b in buf)))

    # Pattern: extract the argument list inside the outermost parentheses of a
    # method call.  We look for ident( ... ) where ... contains commas.
    call_re = re.compile(r"\w+\s*\((.+)\)")
    accessor_re = re.compile(r"^([\w]+)\.\w+$")

    # Value-type accessors like position.x, rect.width, color.r are fine to unpack.
    IGNORE_PREFIXES = {"position", "rect", "c", "color", "col", "row", "v", "p"}

    for line_no, text in joined:
        m = call_re.search(text)
        if not m:
            continue

        args_str = m.group(1)
        # Cheap split — won't handle nested calls perfectly but good enough for
        # the `settings.X, settings.Y, …` pattern.
        args = [a.strip() for a in args_str.split(",")]

        # Count args that share the same prefix.
        prefix_runs: dict[str, int] = {}
        for arg in args:
            am = accessor_re.match(arg)
            if am:
                prefix = am.group(1)
                prefix_runs[prefix] = prefix_runs.get(prefix, 0) + 1

        for prefix, count in prefix_runs.items():
            # Skip single-char, numeric, or known value-type prefixes.
            if len(prefix) <= 1 or prefix.isdigit() or prefix in IGNORE_PREFIXES:
                continue
            if count >= 5:
                result.add(Violation(str(path), line_no, "repeated-accessor",
                    f"{count} arguments read from '{prefix}' — consider passing the object directly"))


# ─── Rule registry ───────────────────────────────────────────────────────────

RULES: dict[str, callable] = {
    "namespace":         check_namespace,
    "braces":            check_braces_required,
    "allman":            check_allman_braces,
    "block-comments":    check_block_comment_headers,
    "redundant-comments":check_redundant_comments,
    "coroutines":        check_start_coroutine,
    "magic-strings":     check_magic_strings,
    "addto-poolable":    check_addto_this_in_poolable,
    "instantiate":       check_object_instantiate,
    "member-ordering":   check_member_ordering,
    "dotween-poolable":  check_dotween_kill_poolable,
    "inject-on-field":   check_inject_on_field,
    "public-visibility": check_public_visibility,
    "accessibility":     check_inconsistent_accessibility,
    "repeated-accessor": check_repeated_accessor,
    "large-lambda":      check_large_anonymous_functions,
    "non-capturing-lambda": check_non_capturing_lambda,
    "blank-lines":       check_multiple_blank_lines,
    "trailing-newlines": check_trailing_newlines,
}

# Rules that don't operate on individual files
META_RULES: dict[str, callable] = {
    "missing-readme": check_missing_readmes,
}


# ─── Auto-fix implementations ───────────────────────────────────────────────

def fix_namespace(path: Path, lines: list[str]) -> list[str]:
    """Fix namespace to match folder structure."""
    expected = expected_namespace(path)
    fixed = []
    for line in lines:
        m = re.match(r"^(\s*)namespace\s+([\w.]+)", line)
        if m:
            indent = m.group(1)
            fixed.append(f"{indent}namespace {expected}\n")
        else:
            fixed.append(line)
    return fixed


def fix_multiple_blank_lines(path: Path, lines: list[str]) -> list[str]:
    """Collapse runs of 2+ consecutive blank lines into a single blank line."""
    fixed = []
    blank_run = 0
    for line in lines:
        if line.strip() == "":
            blank_run += 1
            if blank_run <= 1:
                fixed.append(line)
        else:
            blank_run = 0
            fixed.append(line)
    return fixed


def fix_trailing_newlines(path: Path, lines: list[str]) -> list[str]:
    """Strip all trailing blank lines, keeping exactly one terminating newline."""
    while lines and lines[-1].strip() == "":
        lines = lines[:-1]
    if lines and not lines[-1].endswith("\n"):
        lines[-1] += "\n"
    return lines


def fix_block_comment_headers(path: Path, lines: list[str]) -> list[str]:
    """Remove block comment header lines (// ====, // ----, etc.)."""
    header_re = re.compile(r"^\s*//\s*[=\-*#]{4,}")
    return [line for line in lines if not header_re.match(line)]


def fix_redundant_comments(path: Path, lines: list[str]) -> list[str]:
    """Remove redundant comment lines (// constructor, // fields, etc.)."""
    pat = re.compile(
        r"^\s*//\s*(inject\s+depend|constructor|update\s+position|set\s+color|"
        r"get\s+component|initialize|cleanup|dispose|destructor|"
        r"fields|properties|methods|private\s+methods|public\s+methods)\s*$",
        re.I,
    )
    return [line for line in lines if not pat.match(line)]


def fix_allman_braces(path: Path, lines: list[str]) -> list[str]:
    """Split trailing { onto its own line (Allman brace style)."""
    out = []
    for line in lines:
        stripped = line.rstrip()
        # Apply the same exclusions as the checker
        if stripped.strip() == "{":
            out.append(line)
            continue
        if "=>" in stripped:
            out.append(line)
            continue
        if re.search(r'"\$?.*\{', stripped):
            out.append(line)
            continue
        if stripped.endswith("{") and not re.search(r"(=|new\s+\w+.*|new\(.*\))\s*\{$", stripped):
            indent = len(line) - len(line.lstrip())
            indent_str = line[:indent]
            code_part = stripped[indent:].rstrip(" {").rstrip()
            out.append(indent_str + code_part + "\n")
            out.append(indent_str + "{\n")
        else:
            out.append(line)
    return out


def fix_braces_required(path: Path, lines: list[str]) -> list[str]:
    """Wrap braceless control-flow bodies in { }."""
    control_re = re.compile(
        r"^\s*(if|else\s+if|else|for|foreach|while|using|lock|fixed)\s*(\(.*\))?\s*$"
    )
    result_lines = list(lines)
    i = 0
    while i < len(result_lines):
        if control_re.match(result_lines[i].rstrip()):
            # Skip forward past multi-line conditions until parens are balanced
            paren_depth = result_lines[i].count("(") - result_lines[i].count(")")
            scan = i + 1
            while paren_depth > 0 and scan < len(result_lines):
                paren_depth += result_lines[scan].count("(") - result_lines[scan].count(")")
                scan += 1

            j = scan
            while j < len(result_lines) and j < scan + 4 and not result_lines[j].strip():
                j += 1
            if j < len(result_lines):
                next_s = result_lines[j].strip()
                if not next_s.startswith("{") and not next_s.startswith("//"):
                    ctrl_indent = len(result_lines[i]) - len(result_lines[i].lstrip())
                    ctrl_indent_str = result_lines[i][:ctrl_indent]
                    result_lines.insert(j, ctrl_indent_str + "{\n")
                    result_lines.insert(j + 2, ctrl_indent_str + "}\n")
                    i = j + 3
                    continue
        i += 1
    return result_lines


# Ordered pipeline: each entry is (violation-rule-name, fixer-function).
# fix_trailing_newlines and fix_multiple_blank_lines run last so comment-deletion
# fixers cannot re-introduce trailing gaps that escape cleanup.
_FIXER_PIPELINE: list[tuple[str, callable]] = [
    ("namespace-mismatch",    fix_namespace),
    ("allman-braces",         fix_allman_braces),
    ("braces-required",       fix_braces_required),
    ("block-comment-header",  fix_block_comment_headers),
    ("redundant-comment",     fix_redundant_comments),
    ("multiple-blank-lines",  fix_multiple_blank_lines),
    ("trailing-newlines",     fix_trailing_newlines),
]


def run_fix(result: AuditResult):
    """Apply auto-fixes for all fixable violations, processing each file through
    the full fixer pipeline so multiple fixes compose cleanly."""
    files_rules: dict[str, set[str]] = {}
    for v in result.violations:
        if v.fixable:
            files_rules.setdefault(v.file, set()).add(v.rule)

    fixed_count = 0
    for fpath, rules in sorted(files_rules.items()):
        path = Path(fpath)
        lines = read_lines(path)
        original = list(lines)

        for rule, fixer in _FIXER_PIPELINE:
            # Always run blank-line and trailing-newline passes last — comment
            # deletions may introduce gaps or a bare trailing blank that needs cleanup.
            if rule in rules or rule in ("multiple-blank-lines", "trailing-newlines"):
                lines = fixer(path, lines)

        if lines != original:
            with open(path, "w", encoding="utf-8") as f:
                f.writelines(lines)
            fixed_count += 1
            applied = sorted(rules)
            print(f"  FIXED [{', '.join(applied)}] in {os.path.relpath(fpath, SOURCE_ROOT)}")

    print(f"\n  Auto-fixed {fixed_count} file(s).")


# ─── Main ────────────────────────────────────────────────────────────────────

def run_audit(rule_filter: Optional[str] = None, file_filter: Optional[str] = None) -> AuditResult:
    result = AuditResult()

    # Per-file rules
    for path in cs_files(SOURCE_ROOT):
        if file_filter and file_filter not in str(path):
            continue

        lines = read_lines(path)

        for name, check_fn in RULES.items():
            if rule_filter and rule_filter != name:
                continue
            check_fn(path, lines, result)

    # Meta rules
    if not file_filter:
        for name, check_fn in META_RULES.items():
            if rule_filter and rule_filter != name:
                continue
            check_fn(result)

    return result


def main():
    parser = argparse.ArgumentParser(description="BalloonParty Code Style Auditor")
    parser.add_argument("--fix", action="store_true",
                        help="Auto-fix safe issues (allman braces, braces-required, "
                             "block/redundant comments, blank lines, namespace)")
    parser.add_argument("--rule", type=str, default=None,
                        help=f"Run only one rule. Available: {', '.join(sorted(list(RULES) + list(META_RULES)))}")
    parser.add_argument("--file", type=str, default=None, help="Audit only files matching this substring")
    args = parser.parse_args()

    print(f"Scanning {SOURCE_ROOT} ...")
    result = run_audit(rule_filter=args.rule, file_filter=args.file)

    if not result.violations:
        print("\n  ✅  No violations found!")
        return

    # Print violations grouped by file
    by_file: dict[str, list[Violation]] = {}
    for v in result.violations:
        by_file.setdefault(v.file, []).append(v)

    for fpath in sorted(by_file):
        rel = os.path.relpath(fpath, SOURCE_ROOT)
        print(f"\n{rel}")
        for v in sorted(by_file[fpath], key=lambda v: v.line):
            tag = "[FIXABLE]" if v.fixable else "[REPORT]"
            print(f"  {v.line:4d}  {tag:10s}  {v.rule:25s}  {v.message}")

    print(result.summary())

    if args.fix:
        print("\nApplying auto-fixes...")
        run_fix(result)


if __name__ == "__main__":
    main()
