# Vendored copy of com.dbrizov.naughtyattributes-x

This is an **embedded, locally patched** copy of the OpenUPM package
`com.dbrizov.naughtyattributes-x` **2.2.1**, taken verbatim from the package cache and then
modified. It is no longer resolved from the registry — the `Packages/manifest.json` dependency
entry was removed. The OpenUPM scoped-registry scope is still listed, so reverting to the
registry copy is a one-line change once upstream ships a fix.

## Why it was vendored

Unity 6000.4 made `UnityEngine.Object.GetInstanceID()` obsolete **as an error** (`CS0619`) in
favour of `GetEntityId()`. Upstream 2.2.1 is the newest published version (2026-02-08) and has
five unguarded calls, so the package's editor assembly fails to compile on Unity 6000.5. A
`CS0619` in another package's assembly cannot be suppressed from the outside, so there was no
fix short of changing the package's own source.

## The patch

A single helper was added, and the five call sites now route through it. Nothing else differs
from upstream 2.2.1.

**Added** — `Scripts/Editor/Utility/PropertyUtility.cs`:

```csharp
public static string GetObjectKey(UnityEngine.Object obj)
{
#if UNITY_6000_4_OR_NEWER
    return obj.GetEntityId().ToString();
#else
    return obj.GetInstanceID().ToString();
#endif
}
```

The version guard keeps the file compiling on older editors, so the patch is upstreamable as-is.

**Changed** — all five sites built an `EditorPrefs` key from the instance id:

| File | Was |
| --- | --- |
| `Scripts/Editor/NaughtyInspector.cs` (×3) | `$"{target.GetInstanceID()}…"` |
| `Scripts/Editor/PropertyDrawers_SpecialCase/ReorderableListPropertyDrawer.cs` | `…targetObject.GetInstanceID() + "." + property.name` |
| `Scripts/Editor/PropertyDrawers_SpecialCase/SerializedCollectionPropertyDrawer.cs` | `…targetObject.GetInstanceID() + "." + property.name` |

Every one is a string context, so returning `string` from the helper sidesteps any question of
how `EntityId` converts to `int`.

**Behavioural note:** `EntityId.ToString()` need not match the old instance id, so previously
saved foldout and tab-selection states key differently and reset once. Both are per-session
inspector cosmetics — no project data is affected.

## Maintaining this

If upstream publishes a version with its own fix, delete this folder and restore the dependency
in `Packages/manifest.json`. If you re-vendor a newer upstream version instead, re-apply the
helper and the five call sites above — and clear the read-only attribute that the package cache
sets on every file.
