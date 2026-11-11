"""Bakes balloon pops for the card reveal (Assets/Source/Plans/PLAN-CardReveal.md section 5).

Each pop is a cloth simulation of a balloon's rubber tearing open from a puncture. The balloon is
modelled already inflated, so every pop starts from the same rest frame. Cracks are grown across the
mesh from the puncture (seeded random walks that branch) and the mesh is cut along them. A release
front then sweeps out from the puncture: behind it the rubber's stored stretch lets go (cloth
shrinking), so the flaps snap back toward the knot, which is pinned, and buckle and curl as they do.

Rendered with Cycles, orthographic, on a transparent background, in neutral greys so the game can
tint the balloon. One PNG per frame; pack_sheets.py packs them into sheets and a contact sheet.

Usage (Blender 5.2, headless):
    blender -b --factory-startup -P Tools/art/balloon_pop/bake_pops.py -- --seeds 0 --out Tools/art/balloon_pop/out

Options after "--":
    --seeds 0,3,7     the pops to bake (each seed is one pop; default 0)
    --out DIR         where frames/pop_NN/ go (default: out/ beside this script)
    --frames 24       frames per pop
    --size 512        rendered frame size in px (pack_sheets.py scales the sheets down)
    --samples 32      Cycles samples per frame
    --detail 6        icosphere subdivisions of the rubber (5 for quick looks)
    --blend           also save each pop's .blend next to its frames, to inspect it
"""
import argparse
import math
import random
import sys
import time
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

SIM_STEPS_PER_FRAME = 2
TIME_SCALE = 0.03
FALL_START = 26
FALL_ACCELERATION = 0.008
FRONT_START = 1
NECK_Z = -1.08
CRACK_STOP_Z = -0.78


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--seeds", default="0")
    parser.add_argument("--out", default=str(Path(__file__).resolve().parent / "out"))
    parser.add_argument("--frames", type=int, default=24)
    parser.add_argument("--size", type=int, default=512)
    parser.add_argument("--samples", type=int, default=32)
    parser.add_argument("--detail", type=int, default=6)
    parser.add_argument("--blend", action="store_true")
    return parser.parse_args(argv)


def smoothstep(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


def balloon_point(unit):
    """Shapes a point on the unit sphere into the balloon: an egg, wider above, gathered into the neck."""
    x, y, z = unit
    below = max(0.0, -z)
    pinch = 1.0 - 0.22 * below ** 2 - 0.45 * smoothstep(0.8, 1.0, below)
    return Vector((x * pinch, y * pinch, z * 1.12 - 0.1 * below ** 4))


def build_rubber(rng, detail, params):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=detail, radius=1.0)
    units = {v.index: v.co.normalized() for v in bm.verts}
    for v in bm.verts:
        v.co = balloon_point(units[v.index])
    bm.normal_update()
    bm.verts.ensure_lookup_table()

    cut = grow_cracks(bm, units, rng, params)
    heal_loose_pieces(bm, units, cut)
    bmesh.ops.split_edges(bm, edges=list(cut))
    for f in bm.faces:
        f.smooth = True

    mesh = bpy.data.meshes.new("Rubber")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("Rubber", mesh)
    bpy.context.collection.objects.link(obj)

    pin = obj.vertex_groups.new(name="pin")
    shrink = obj.vertex_groups.new(name="shrink")
    pinned = [v.index for v in mesh.vertices if v.co.z < NECK_Z]
    pin.add(pinned, 1.0, "REPLACE")
    shrink.add(list(range(len(mesh.vertices))), 0.0, "REPLACE")
    return obj, len(cut)


def grow_cracks(bm, units, rng, params):
    """Random walks over the mesh's edges from the puncture, each edge walked becoming a cut. A walk
    keeps roughly to its heading, wanders a little, may branch, and stops where it meets another
    crack, runs out of length, or reaches the neck (so every flap stays attached at the knot)."""
    puncture_dir = params["puncture"]
    start = max(bm.verts, key=lambda v: units[v.index].dot(puncture_dir))
    cut = set()

    def can_enter(v):
        return units[v.index].z > CRACK_STOP_Z

    def flatten(v, d):
        n = v.normal
        t = d - n * d.dot(n)
        return t.normalized() if t.length > 1e-6 else None

    def walk(v, heading, budget, depth):
        heading = flatten(v, heading)
        travelled = 0.0
        while heading is not None and travelled < budget:
            best = None
            best_score = 0.35
            for e in v.link_edges:
                if e in cut:
                    continue
                u = e.other_vert(v)
                if not can_enter(u):
                    continue
                step = u.co - v.co
                score = step.normalized().dot(heading)
                if score > best_score:
                    best, best_score = (e, u, step), score
            if best is None:
                return
            e, u, step = best
            cut.add(e)
            travelled += step.length
            if any(other in cut for other in u.link_edges if other is not e):
                return
            turn = rng.gauss(0.0, params["wander"])
            heading = Matrix.Rotation(turn, 3, u.normal) @ (heading * 0.7 + step.normalized() * 0.3)
            heading = flatten(u, heading)
            v = u
            if depth < 2 and rng.random() < params["branching"]:
                side = rng.choice((-1.0, 1.0)) * math.radians(rng.uniform(30.0, 65.0))
                walk(v, Matrix.Rotation(side, 3, v.normal) @ heading, (budget - travelled) * rng.uniform(0.3, 0.6),
                     depth + 1)

    base = rng.uniform(0.0, math.tau)
    count = params["cracks"]
    seed_heading = flatten(start, Vector((0.0, 0.0, 1.0))) or flatten(start, Vector((1.0, 0.0, 0.0)))
    for i in range(count):
        angle = base + math.tau * i / count + rng.gauss(0.0, 0.25)
        heading = Matrix.Rotation(angle, 3, start.normal) @ seed_heading
        walk(start, heading, rng.uniform(*params["crack_length"]), 0)
    return cut


def heal_loose_pieces(bm, units, cut):
    """Cracks that meet can close a loop and cut a piece loose, which would hang in the air with no
    gravity. Each such piece is stitched back by one edge of its rim, so every flap stays attached."""
    while True:
        seen = set()
        loose = None
        for face in bm.faces:
            if face in seen:
                continue
            piece, stack, anchored = [], [face], False
            seen.add(face)
            while stack:
                f = stack.pop()
                piece.append(f)
                for e in f.edges:
                    if e in cut:
                        continue
                    for g in e.link_faces:
                        if g not in seen:
                            seen.add(g)
                            stack.append(g)
                anchored = anchored or any(units[v.index].z <= CRACK_STOP_Z for v in f.verts)
            if not anchored:
                loose = piece
                break
        if loose is None:
            return
        rim = [e for f in loose for e in f.edges if e in cut]
        cut.discard(rim[len(rim) // 2])


def pop_params(rng):
    # Where it's punctured: anywhere on the body above the neck, the side more often than the top.
    z = rng.uniform(-0.45, 0.95)
    a = rng.uniform(0.0, math.tau)
    r = math.sqrt(max(0.0, 1.0 - z * z))
    return {
        "puncture": Vector((r * math.cos(a), r * math.sin(a), z)).normalized(),
        "cracks": rng.randint(2, 4),
        "crack_length": (2.2, 4.2),
        "wander": math.radians(rng.uniform(5.0, 9.0)),
        "branching": rng.uniform(0.015, 0.04),
        "shrink": rng.uniform(0.55, 0.68),
        "bending": rng.uniform(0.02, 0.08),
        "front_frames": rng.randint(8, 12),
    }


def add_release_front(obj, params, puncture_point, sim_frames):
    """Weights the shrink group by distance from the puncture, the release front sweeping outward:
    rubber the front has passed shrinks fully, rubber ahead of it not at all. Keyed every frame, so
    the curve's interpolation never matters."""
    target = bpy.data.objects.new("Puncture", None)
    target.location = puncture_point
    bpy.context.collection.objects.link(target)

    front = obj.modifiers.new("ReleaseFront", "VERTEX_WEIGHT_PROXIMITY")
    front.vertex_group = "shrink"
    front.target = target
    front.proximity_mode = "OBJECT"
    front.falloff_type = "LINEAR"

    width = 0.45
    span = 3.0
    for frame in range(1, sim_frames + 1):
        t = (frame - FRONT_START) / params["front_frames"]
        radius = -width if t < 0 else min(1.0, t) * (span + width)
        # min_dist maps to weight 0 (the front's leading edge), max_dist to weight 1 (behind it).
        front.min_dist = max(radius, 1e-4)
        front.max_dist = max(radius - width, 0.0)
        front.keyframe_insert("min_dist", frame=frame)
        front.keyframe_insert("max_dist", frame=frame)


def add_cloth(obj, params, sim_frames):
    cloth = obj.modifiers.new("Cloth", "CLOTH")
    s = cloth.settings
    s.quality = 12
    s.mass = 0.15
    s.air_damping = 2.0
    s.tension_stiffness = 40.0
    s.compression_stiffness = 40.0
    s.shear_stiffness = 20.0
    s.bending_stiffness = params["bending"]
    s.tension_damping = 4.0
    s.compression_damping = 4.0
    s.shear_damping = 4.0
    s.bending_damping = 0.2
    s.vertex_group_mass = "pin"
    s.pin_stiffness = 2.0
    s.vertex_group_shrink = "shrink"
    s.shrink_min = 0.0
    s.shrink_max = params["shrink"]
    s.time_scale = TIME_SCALE
    s.effector_weights.gravity = 0.0

    c = cloth.collision_settings
    c.use_collision = False
    c.use_self_collision = True
    c.self_distance_min = 0.006
    c.self_friction = 5.0
    c.collision_quality = 3

    cloth.point_cache.frame_start = 1
    cloth.point_cache.frame_end = sim_frames

    # Stitches each crack shut while its two sides still touch, so it's invisible until it opens.
    weld = obj.modifiers.new("CloseCracks", "WELD")
    weld.merge_threshold = 0.002

    solid = obj.modifiers.new("Thickness", "SOLIDIFY")
    solid.thickness = 0.008
    solid.offset = 0.0


def add_knot(material):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=False, segments=24, radius1=0.05, radius2=0.11, depth=0.22,
                          matrix=Matrix.Translation((0.0, 0.0, -1.22)))
    knot = bmesh.new()
    bmesh.ops.create_uvsphere(knot, u_segments=20, v_segments=10, radius=1.0)
    bmesh.ops.scale(knot, vec=(0.085, 0.085, 0.06), verts=knot.verts)
    bmesh.ops.translate(knot, vec=(0.0, 0.0, -1.35), verts=knot.verts)
    mesh = bpy.data.meshes.new("Knot")
    for f in bm.faces:
        f.smooth = True
    bm.to_mesh(mesh)
    bm.free()
    knot_mesh = bpy.data.meshes.new("KnotBall")
    for f in knot.faces:
        f.smooth = True
    knot.to_mesh(knot_mesh)
    knot.free()
    objects = []
    for m in (mesh, knot_mesh):
        m.materials.append(material)
        obj = bpy.data.objects.new(m.name, m)
        bpy.context.collection.objects.link(obj)
        objects.append(obj)
    return objects


def add_fall(objects, sim_frames):
    """Once the rubber has gathered at the knot, the knot drops out of the frame and the shreds trail
    after it (the cloth's pinned neck moves with its object), leaving the stage to the slip."""
    for frame in range(1, sim_frames + 1):
        t = max(0, frame - FALL_START)
        for obj in objects:
            obj.location.z = -0.5 * FALL_ACCELERATION * t * t
            obj.keyframe_insert("location", index=2, frame=frame)


def rubber_material():
    """Neutral grey rubber, tinted in game: a soft subsurface body and one sharp highlight (the coat)."""
    mat = bpy.data.materials.new("Rubber")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (0.62, 0.62, 0.62, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.42
    bsdf.inputs["Subsurface Weight"].default_value = 0.25
    bsdf.inputs["Subsurface Radius"].default_value = (0.05, 0.05, 0.05)
    bsdf.inputs["Coat Weight"].default_value = 0.7
    bsdf.inputs["Coat Roughness"].default_value = 0.06
    return mat


def setup_scene(size, samples, frames):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    scene.render.fps = 30
    scene.frame_start = 1
    scene.frame_end = frames * SIM_STEPS_PER_FRAME

    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.5, 0.5, 0.5, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
    scene.world = world

    # Lit from the top left, like the UI's sun.
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.5
    sun.data.angle = math.radians(4.0)
    sun.rotation_euler = (math.radians(50.0), 0.0, math.radians(-35.0))
    bpy.context.collection.objects.link(sun)

    cam_data = bpy.data.cameras.new("Camera")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 3.1
    cam = bpy.data.objects.new("Camera", cam_data)
    cam.location = (0.0, -10.0, -0.12)
    cam.rotation_euler = (math.radians(90.0), 0.0, 0.0)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam
    return scene


def bake(seed, args):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rng = random.Random(seed)
    params = pop_params(rng)
    scene = setup_scene(args.size, args.samples, args.frames)
    material = rubber_material()

    obj, cut_edges = build_rubber(rng, args.detail, params)
    obj.data.materials.append(material)
    knot = add_knot(material)
    sim_frames = args.frames * SIM_STEPS_PER_FRAME
    add_release_front(obj, params, balloon_point(params["puncture"]), sim_frames)
    add_cloth(obj, params, sim_frames)
    add_fall([obj] + knot, sim_frames)

    out = Path(args.out) / "frames" / f"pop_{seed:02d}"
    out.mkdir(parents=True, exist_ok=True)
    print(f"pop {seed}: {len(obj.data.vertices)} verts, {cut_edges} edges cut, {params['cracks']} cracks", flush=True)

    started = time.time()
    for frame in range(1, sim_frames + 1):
        scene.frame_set(frame)
    print(f"pop {seed}: simulated {sim_frames} frames in {time.time() - started:.0f}s", flush=True)

    if args.blend:
        bpy.ops.wm.save_as_mainfile(filepath=str(out / f"pop_{seed:02d}.blend"))

    started = time.time()
    for i in range(args.frames):
        scene.frame_set(1 + i * SIM_STEPS_PER_FRAME)
        scene.render.filepath = str(out / f"{i:02d}.png")
        bpy.ops.render.render(write_still=True)
    print(f"pop {seed}: rendered {args.frames} frames in {time.time() - started:.0f}s", flush=True)


def main():
    args = parse_args()
    for seed in (int(s) for s in args.seeds.split(",")):
        bake(seed, args)


main()
