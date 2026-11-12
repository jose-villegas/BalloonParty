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
    --frames 48       frames rendered per pop, out of the simulation's 48 (48 renders every one, 24 every
                      second one); a divisor of 48
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

# The simulation's length, at the scene's 30 fps: the timings below (FRONT_START, FALL_START, the release
# front's frames) count its frames, so the pop is the same however many of them are rendered.
SIM_FRAMES = 48
TIME_SCALE = 0.03
# The knot falls from the tear, speeding up by FALL_ACCELERATION units a frame each frame, and the cloth falls
# with it under gravity, its shreds trailing as the air holds them back; the camera follows the knot. The game
# moves the frames down by the same fall:
# CardRevealSettings' Rubber Falls From Frame is FALL_START - 1 (frames count from 0 there) and its Rubber
# Fall Gravity FALL_ACCELERATION / ORTHO_SCALE, in frame heights.
FALL_START = 5
FALL_ACCELERATION = 0.01395
ORTHO_SCALE = 3.1
CAMERA_HEIGHT = -0.12
# Once the flaps have folded, the rubber gathers around the knot, which sits near the frame's bottom: the camera
# eases RECENTRE units further down over RECENTRE_FRAMES from RECENTRE_START, lifting the knot toward the frame's
# middle so the gathered rubber stays inside it. The game adds the same back (Rubber Recentre From Frame
# RECENTRE_START - 1, Rubber Recentre Frames, Rubber Recentre Height RECENTRE / ORTHO_SCALE), so the knot's path
# stays the fall's.
RECENTRE_START = 19
RECENTRE_FRAMES = 12
RECENTRE = 1.612
# Measured: at TIME_SCALE and 30 fps, a free cloth falls this many units a frame squared per unit of scene
# gravity, so the gravity that drops it with the knot is FALL_ACCELERATION over it (about 13,000).
CLOTH_FALL_PER_GRAVITY = 1.0605e-6

# The camera sits 10 units in front of the balloon's centre: the front layer is the rubber nearer than
# that, the back layer the rubber beyond it.
CAMERA_DISTANCE = 10.0
LAYERS = {"front": (0.1, CAMERA_DISTANCE), "back": (CAMERA_DISTANCE, CAMERA_DISTANCE * 3.0)}
FRONT_START = 1
NECK_Z = -1.08
CRACK_STOP_Z = -0.78


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--seeds", default="0")
    parser.add_argument("--out", default=str(Path(__file__).resolve().parent / "out"))
    parser.add_argument("--frames", type=int, default=48)
    parser.add_argument("--size", type=int, default=512)
    parser.add_argument("--samples", type=int, default=32)
    parser.add_argument("--detail", type=int, default=6)
    parser.add_argument("--blend", action="store_true")
    args = parser.parse_args(argv)
    if args.frames <= 0 or SIM_FRAMES % args.frames:
        parser.error(f"--frames must divide the simulation's {SIM_FRAMES} frames")
    return args


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


def add_fall(objects, camera, sim_frames):
    """From the tear the knot falls, and the shreds trail after it: the cloth's pinned neck moves with its
    object, and the rest falls with it under gravity (add_gravity). The camera falls with the knot, keeping
    it in the frame (lifted toward the middle once the rubber gathers: RECENTRE); the game moves the frame by
    the same fall."""
    for frame in range(1, sim_frames + 1):
        t = max(0, frame - FALL_START)
        drop = -0.5 * FALL_ACCELERATION * t * t
        for obj in objects:
            obj.location.z = drop
            obj.keyframe_insert("location", index=2, frame=frame)
        recentre = RECENTRE * smoothstep(RECENTRE_START, RECENTRE_START + RECENTRE_FRAMES, frame)
        camera.location.z = CAMERA_HEIGHT + drop - recentre
        camera.keyframe_insert("location", index=2, frame=frame)


def add_gravity(cloth, sim_frames):
    """The cloth falls with the knot from the tear, so the shreds trail it only as the air holds them back;
    without it the neck alone drags them, and they stretch into a rope. Off before the tear, so the whole
    balloon doesn't sag. Keyed every frame, so the curve's interpolation never matters."""
    bpy.context.scene.gravity = (0.0, 0.0, -FALL_ACCELERATION / CLOTH_FALL_PER_GRAVITY)
    weights = cloth.settings.effector_weights
    for frame in range(1, sim_frames + 1):
        weights.gravity = 1.0 if frame > FALL_START else 0.0
        weights.keyframe_insert("gravity", frame=frame)


def surface_material():
    """Writes the surface, not a picture, so the game lights it: red and green are the normal's x and y
    as the camera sees it (0.5 is flat), blue is the ambient occlusion (the creases and the inside of
    a curl), and alpha the coverage. The rubber is thin, so a face seen from behind turns its normal to
    the camera."""
    mat = bpy.data.materials.new("Surface")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()

    geometry = nodes.new("ShaderNodeNewGeometry")
    flip = nodes.new("ShaderNodeVectorMath")
    flip.operation = "SCALE"
    sign = nodes.new("ShaderNodeMapRange")
    sign.inputs["To Min"].default_value = 1.0
    sign.inputs["To Max"].default_value = -1.0
    links.new(geometry.outputs["Backfacing"], sign.inputs["Value"])
    links.new(geometry.outputs["Normal"], flip.inputs[0])
    links.new(sign.outputs["Result"], flip.inputs["Scale"])

    to_camera = nodes.new("ShaderNodeVectorTransform")
    to_camera.vector_type = "NORMAL"
    to_camera.convert_from = "WORLD"
    to_camera.convert_to = "CAMERA"
    links.new(flip.outputs["Vector"], to_camera.inputs["Vector"])

    encode = nodes.new("ShaderNodeVectorMath")
    encode.operation = "MULTIPLY_ADD"
    encode.inputs[1].default_value = (0.5, 0.5, 0.5)
    encode.inputs[2].default_value = (0.5, 0.5, 0.5)
    links.new(to_camera.outputs["Vector"], encode.inputs[0])
    split = nodes.new("ShaderNodeSeparateXYZ")
    links.new(encode.outputs["Vector"], split.inputs["Vector"])

    occlusion = nodes.new("ShaderNodeAmbientOcclusion")
    occlusion.samples = 16
    occlusion.inputs["Distance"].default_value = 0.35
    occlusion.only_local = True

    pack = nodes.new("ShaderNodeCombineXYZ")
    links.new(split.outputs["X"], pack.inputs["X"])
    links.new(split.outputs["Y"], pack.inputs["Y"])
    links.new(occlusion.outputs["AO"], pack.inputs["Z"])

    emission = nodes.new("ShaderNodeEmission")
    links.new(pack.outputs["Vector"], emission.inputs["Color"])
    output = nodes.new("ShaderNodeOutputMaterial")
    links.new(emission.outputs["Emission"], output.inputs["Surface"])
    return mat


def setup_scene(size, samples):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    # Denoising would smear the data across the normals' edges; the AO's noise is low at these samples.
    scene.cycles.use_denoising = False
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    # The pixels are data: written as they come out of the shader, with no display transform.
    scene.view_settings.view_transform = "Raw"
    scene.view_settings.look = "None"
    scene.render.fps = 30
    scene.frame_start = 1
    scene.frame_end = SIM_FRAMES

    cam_data = bpy.data.cameras.new("Camera")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = ORTHO_SCALE
    cam = bpy.data.objects.new("Camera", cam_data)
    cam.location = (0.0, -CAMERA_DISTANCE, CAMERA_HEIGHT)
    cam.rotation_euler = (math.radians(90.0), 0.0, 0.0)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam
    return scene


def bake(seed, args):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rng = random.Random(seed)
    params = pop_params(rng)
    scene = setup_scene(args.size, args.samples)
    material = surface_material()

    obj, cut_edges = build_rubber(rng, args.detail, params)
    obj.data.materials.append(material)
    knot = add_knot(material)
    sim_frames = SIM_FRAMES
    add_release_front(obj, params, balloon_point(params["puncture"]), sim_frames)
    add_cloth(obj, params, sim_frames)
    add_gravity(obj.modifiers["Cloth"], sim_frames)
    add_fall([obj] + knot, scene.camera, sim_frames)

    out = Path(args.out) / "frames" / f"pop_{seed:02d}"
    out.mkdir(parents=True, exist_ok=True)
    print(f"pop {seed}: {len(obj.data.vertices)} verts, {cut_edges} edges cut, {params['cracks']} cracks", flush=True)

    started = time.time()
    for frame in range(1, sim_frames + 1):
        scene.frame_set(frame)
    print(f"pop {seed}: simulated {sim_frames} frames in {time.time() - started:.0f}s", flush=True)

    if args.blend:
        bpy.ops.wm.save_as_mainfile(filepath=str(out / f"pop_{seed:02d}.blend"))

    # Two layers, split at the plane through the balloon's centre, so the game can put the slip between
    # them: inside the balloon, not behind it.
    camera = scene.camera.data
    started = time.time()
    step = SIM_FRAMES // args.frames
    for i in range(args.frames):
        scene.frame_set(1 + i * step)
        for layer, (near, far) in LAYERS.items():
            camera.clip_start = near
            camera.clip_end = far
            scene.render.filepath = str(out / f"{i:02d}_{layer}.png")
            bpy.ops.render.render(write_still=True)
    print(f"pop {seed}: rendered {args.frames} frames in {time.time() - started:.0f}s", flush=True)


def main():
    args = parse_args()
    for seed in (int(s) for s in args.seeds.split(",")):
        bake(seed, args)


main()
