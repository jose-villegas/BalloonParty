// Renders one balloon on the board alone, under neutral noon light, on a transparent background: the source
// picture for make_shatter_sheet.py. Whatever the time of day, the render sees white light from the upper left
// with no local lights (the scene-light globals are swapped for it and put back), so the sheet carries no tint:
// the shards' material lights them at runtime, and a tint baked in would land on top of it. Sent to the open
// Editor in Play mode with
//   unity command script eval --file <this, placeholders filled in> --result-only
// Not part of the Unity project (it lives outside Assets/); the Pipeline package compiles it on each call.
//
// __MODEL__     the balloon's model type name, e.g. GoldenBalloonModel; the first one on the board is used
// __RENDERERS__ comma-separated renderer names to draw (a name ending in * matches as a prefix), e.g. Body,Shine*:
//               the body and its highlights, not its baked shadow (the sheet bakes its own) or anything around it
// __OUT__       where to write the PNG (use forward slashes)
//
// The chosen renderers move to a spare layer for one render of a temporary orthographic camera centred on the
// balloon, then go back, so nothing on the board changes. 1024 px over one world unit: a balloon (about 0.64
// units) comes out about 650 px across, the scale the sheet draws it at.
var modelName = "__MODEL__";
var rendererNames = "__RENDERERS__".Split(',');
var outPath = "__OUT__";
const int Pixels = 1024;
const float HalfExtent = 0.5f;
const int SpareLayer = 31;

bool Wanted(string name)
{
    foreach (var pattern in rendererNames)
    {
        var p = pattern.Trim();
        if (p.EndsWith("*") ? name.StartsWith(p.TrimEnd('*')) : name == p)
        {
            return true;
        }
    }

    return false;
}

BalloonParty.Balloon.View.BalloonView balloon = null;
foreach (var view in UnityEngine.Object.FindObjectsByType<BalloonParty.Balloon.View.BalloonView>())
{
    if (view.Model != null && view.Model.GetType().Name == modelName)
    {
        balloon = view;
        break;
    }
}

if (balloon == null)
{
    return "no " + modelName + " on the board: spawn one first (the Spawn Balloon cheat)";
}

var picked = new System.Collections.Generic.List<(UnityEngine.GameObject go, int layer)>();
var centre = balloon.transform.position;
foreach (var r in balloon.GetComponentsInChildren<UnityEngine.Renderer>())
{
    if (r.enabled && Wanted(r.name))
    {
        picked.Add((r.gameObject, r.gameObject.layer));
        r.gameObject.layer = SpareLayer;
    }
}

if (picked.Count == 0)
{
    return "none of " + string.Join(",", rendererNames) + " found on the " + modelName;
}

var camGo = new UnityEngine.GameObject("ShatterSourceCamera");
var cam = camGo.AddComponent<UnityEngine.Camera>();
cam.orthographic = true;
cam.orthographicSize = HalfExtent;
cam.aspect = 1f;
cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
cam.backgroundColor = new UnityEngine.Color(0f, 0f, 0f, 0f);
cam.cullingMask = 1 << SpareLayer;
cam.allowHDR = false;
cam.allowMSAA = false;
camGo.transform.position = new UnityEngine.Vector3(centre.x, centre.y, centre.z - 10f);

var lightDir = UnityEngine.Shader.PropertyToID("_SceneLightDir");
var lightColor = UnityEngine.Shader.PropertyToID("_SceneLightColor");
var fieldOn = UnityEngine.Shader.PropertyToID("_SceneLightFieldOn");
var liveDir = UnityEngine.Shader.GetGlobalVector(lightDir);
var liveColor = UnityEngine.Shader.GetGlobalColor(lightColor);
var liveField = UnityEngine.Shader.GetGlobalFloat(fieldOn);
var rt = new UnityEngine.RenderTexture(Pixels, Pixels, 24, UnityEngine.RenderTextureFormat.ARGB32);
try
{
    // Noon: the toward-light direction at 135 degrees, where the day/night gradient is white.
    UnityEngine.Shader.SetGlobalVector(lightDir, new UnityEngine.Vector4(-0.70710678f, 0.70710678f, 0f, 0f));
    UnityEngine.Shader.SetGlobalColor(lightColor, UnityEngine.Color.white);
    UnityEngine.Shader.SetGlobalFloat(fieldOn, 0f);
    cam.targetTexture = rt;
    cam.Render();
    cam.targetTexture = null;
}
finally
{
    UnityEngine.Shader.SetGlobalVector(lightDir, liveDir);
    UnityEngine.Shader.SetGlobalColor(lightColor, liveColor);
    UnityEngine.Shader.SetGlobalFloat(fieldOn, liveField);

    foreach (var (go, layer) in picked)
    {
        go.layer = layer;
    }

    UnityEngine.Object.DestroyImmediate(camGo);
}

var active = UnityEngine.RenderTexture.active;
UnityEngine.RenderTexture.active = rt;
var tex = new UnityEngine.Texture2D(Pixels, Pixels, UnityEngine.TextureFormat.RGBA32, false);
tex.ReadPixels(new UnityEngine.Rect(0, 0, Pixels, Pixels), 0, 0);
tex.Apply();
UnityEngine.RenderTexture.active = active;
rt.Release();
UnityEngine.Object.DestroyImmediate(rt);
System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
UnityEngine.Object.DestroyImmediate(tex);
return "captured " + picked.Count + " renderers of the " + modelName + " to " + outPath;
