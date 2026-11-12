"""Bakes the pushpin that pricks the card reveal's balloon (CardRevealView): a map pin, its plastic head and
steel needle turned on a lathe, held at the angle it jabs in at.

Rendered as the pops are (bake_pops.py's surface material and scene): not a picture but the pin's surface,
normal and occlusion, for BalloonParty/Sprite/LitFlipbook to light with the scene's light, so it sits in the
board's light and cel look beside the balloon. The game tints the head and the needle apart, so each is a
cell of its own in the sheet's top half (its bottom half, the layer behind in a pop sheet, is empty): the
head on the left, the needle on the right, cut where the head covers it.

Usage (Blender 5.2, headless):
    blender -b --factory-startup -P Tools/art/balloon_pop/bake_pin.py -- [--out Assets/Sprites/UI/CardReveal/pin.png]

The bake ends by printing the pin settings CardRevealSettings needs to match it.
"""
import math
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Matrix

sys.path.insert(0, str(Path(__file__).resolve().parent))
import bake_pops  # noqa: E402  (beside this script, found by the path above)

CELL = 256
SAMPLES = 64
# The square each cell shows, in the pop's units (its frame is bake_pops.ORTHO_SCALE across), and where in it
# the needle's tip is, from the bottom left: the game hangs the pin from that point.
PIN_VIEW = 1.0
TIP = (0.25, 0.15)
# The pin leans toward the camera (it comes in from in front of the balloon) and to the right, its head up
# and right of its tip: TILT_TOWARD is the lean toward the camera, LEAN the angle on screen from straight up.
TILT_TOWARD = 30.0
LEAN = 35.0
# A map pin's profile, from the needle's tip up, as (radius, height) in the pop's units: about a third of the
# balloon's height, a little larger than life, so it reads at the reveal's size.
NEEDLE = [(0.0, 0.0), (0.006, 0.012), (0.012, 0.045), (0.014, 0.42), (0.0, 0.42)]
HEAD = [(0.0, 0.40), (0.1, 0.40), (0.106, 0.425), (0.064, 0.452), (0.05, 0.5), (0.047, 0.58), (0.062, 0.625),
        (0.096, 0.665), (0.101, 0.7), (0.089, 0.74), (0.052, 0.766), (0.0, 0.773)]


def parse_out():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(argv) >= 2 and argv[0] == "--out":
        return Path(argv[1])
    return Path(__file__).resolve().parents[3] / "Assets" / "Sprites" / "UI" / "CardReveal" / "pin.png"


def lathe(name, profile, material, pose):
    """Turns the profile around the pin's axis; its edges sharper than 40 degrees stay hard."""
    bm = bmesh.new()
    verts = [bm.verts.new((r, 0.0, z)) for r, z in profile]
    edges = [bm.edges.new((a, b)) for a, b in zip(verts, verts[1:])]
    bmesh.ops.spin(bm, geom=verts + edges, cent=(0.0, 0.0, 0.0), axis=(0.0, 0.0, 1.0), steps=48,
                   angle=math.tau, use_merge=True)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        f.smooth = True
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.set_sharp_from_angle(angle=math.radians(40.0))
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    obj.matrix_world = pose
    bpy.context.collection.objects.link(obj)
    return obj


def bled(cell, steps=8):
    """The surface data spread outward into the empty texels around the pin, coverage kept, as
    pack_sheets.py bleeds a pop's frames: the game filters the sheet, and empty texels holding black would
    bend the edge's normals and ring the outline in the wrong tone."""
    rgb, known = cell[..., :3].copy(), cell[..., 3] > 0.0
    for _ in range(steps):
        total = np.zeros_like(rgb)
        count = np.zeros(known.shape, dtype=np.float32)
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (-1, 1), (1, -1), (1, 1)):
            shifted = np.roll(known, (dy, dx), axis=(0, 1))
            total += np.roll(rgb, (dy, dx), axis=(0, 1)) * shifted[..., None]
            count += shifted
        grow = ~known & (count > 0)
        rgb[grow] = total[grow] / count[grow][:, None]
        known = known | grow
    rgb[~known] = (0.5, 0.5, 1.0)
    out = cell.copy()
    out[..., :3] = rgb
    return out


def render_cell(scene, path):
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    image = bpy.data.images.load(str(path))
    image.colorspace_settings.name = "Non-Color"
    pixels = np.array(image.pixels[:], dtype=np.float32).reshape(CELL, CELL, 4)
    bpy.data.images.remove(image)
    return bled(pixels)


def main():
    out = parse_out()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bake_pops.setup_scene(CELL, SAMPLES)
    material = bake_pops.surface_material()

    # Tip at the origin; tilted toward the camera (which looks along +y), then leant right on screen.
    pose = Matrix.Rotation(math.radians(LEAN), 4, "Y") @ Matrix.Rotation(math.radians(TILT_TOWARD), 4, "X")
    needle = lathe("Needle", NEEDLE, material, pose)
    head = lathe("Head", HEAD, material, pose)

    camera = scene.camera
    camera.data.ortho_scale = PIN_VIEW
    camera.location = ((0.5 - TIP[0]) * PIN_VIEW, -bake_pops.CAMERA_DISTANCE, (0.5 - TIP[1]) * PIN_VIEW)
    camera.data.clip_start = 0.1
    camera.data.clip_end = bake_pops.CAMERA_DISTANCE * 3.0

    work = out.parent / "pin_cells"
    work.mkdir(parents=True, exist_ok=True)
    needle.hide_render = True
    head_cell = render_cell(scene, work / "head.png")
    needle.hide_render = False
    head.is_holdout = True
    needle_cell = render_cell(scene, work / "needle.png")
    for cell in work.iterdir():
        cell.unlink()
    work.rmdir()

    # Rows count from the bottom: the top half holds the cells, as a pop sheet's front layer.
    sheet = np.zeros((CELL * 2, CELL * 2, 4), dtype=np.float32)
    sheet[CELL:, :CELL] = head_cell
    sheet[CELL:, CELL:] = needle_cell
    image = bpy.data.images.new("Pin", CELL * 2, CELL * 2, alpha=True)
    image.colorspace_settings.name = "Non-Color"
    image.pixels.foreach_set(sheet.ravel())
    image.filepath_raw = str(out)
    image.file_format = "PNG"
    out.parent.mkdir(parents=True, exist_ok=True)
    image.save()
    print(f"wrote {out}")

    print("CardRevealSettings for this pin:")
    print(f"  Pin Tip     ({TIP[0]:g}, {TIP[1]:g})")
    print(f"  Pin Size    {PIN_VIEW / bake_pops.ORTHO_SCALE:.6g}")
    print(f"  Pin Lean    {LEAN:g}", flush=True)


main()
