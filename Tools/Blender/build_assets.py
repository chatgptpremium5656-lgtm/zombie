# Generates every stylized model of the test scene as FBX into Assets/Art/Models.
# Run headless:  Blender -b --factory-startup -P Tools/Blender/build_assets.py -- <out_dir> [preview_dir]
#
# Style rules (CLAUDE.md section 1): smooth rounded shapes built from metaballs and
# subdivided primitives, smooth normals, colour painted into vertex colours (sRGB).
# Vertex alpha = wind weight (0 = rigid, 1 = sways fully) read by the Zombie/Toon shader.

import bpy, bmesh, math, os, sys, random
from mathutils import Vector, Matrix, Quaternion, noise

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else os.path.join(os.getcwd(), "Assets/Art/Models")
PREVIEW = argv[1] if len(argv) > 1 else None
os.makedirs(OUT, exist_ok=True)

SURFACE = 0.575  # surface radius of a lone metaball / influence radius (stiffness 2, threshold 0.6)


def surface_factor(stiffness, threshold=0.6):
    """Field is stiffness * (1 - d^2/r^2)^3; returns surface radius / influence radius."""
    return math.sqrt(1.0 - (threshold / stiffness) ** (1.0 / 3.0))


def clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.metaballs, bpy.data.armatures, bpy.data.actions, bpy.data.materials):
        for d in list(coll):
            coll.remove(d)


def U(x, y, z):
    """Unity-space (x right, y up, z forward) -> Blender space for the FBX axis conversion used here."""
    return Vector((-x, -z, y))


def hexcol(h, a=0.0):
    h = h.lstrip("#")
    return (int(h[0:2], 16) / 255, int(h[2:4], 16) / 255, int(h[4:6], 16) / 255, a)


def lerp(a, b, t):
    return a + (b - a) * t


def lerpc(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(lerp(a[i], b[i], t) for i in range(4))


def smooth01(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------- metaball helpers

class Blob:
    """One metaball family -> one smooth mesh."""

    def __init__(self, res=0.04):
        self.mb = bpy.data.metaballs.new("blob")
        self.mb.resolution = res
        self.mb.render_resolution = res
        self.obj = bpy.data.objects.new("blob", self.mb)
        bpy.context.scene.collection.objects.link(self.obj)

    def ball(self, p, s, stiff=2.0):
        e = self.mb.elements.new(type='BALL')
        e.co = p
        e.radius = s / surface_factor(stiff)
        e.stiffness = stiff
        return e

    def ellipsoid(self, p, s, size, rot=None):
        e = self.mb.elements.new(type='ELLIPSOID')
        e.co = p
        e.radius = s / SURFACE
        e.size_x, e.size_y, e.size_z = size
        if rot is not None:
            e.rotation = rot
        return e

    def capsule(self, a, b, s):
        a, b = Vector(a), Vector(b)
        d = b - a
        e = self.mb.elements.new(type='CAPSULE')
        e.co = (a + b) / 2
        e.radius = s / SURFACE
        e.size_x = d.length / 2
        e.rotation = Vector((1, 0, 0)).rotation_difference(d.normalized())
        return e

    def chain(self, pts, s0, s1=None):
        s1 = s0 if s1 is None else s1
        n = len(pts) - 1
        for i in range(n):
            self.capsule(pts[i], pts[i + 1], lerp(s0, s1, (i + 0.5) / n))

    def to_mesh(self):
        dg = bpy.context.evaluated_depsgraph_get()
        me = bpy.data.meshes.new_from_object(self.obj.evaluated_get(dg))
        bpy.data.objects.remove(self.obj, do_unlink=True)
        return me


# ---------------------------------------------------------------- mesh helpers

def mesh_from_bm(bm, name="m"):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    return me


def icosphere(radius=1.0, subdiv=4):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    return bm


def transform_mesh(me, mat):
    me.transform(mat)
    return me


def decimate(me, target_tris):
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    if tris <= target_tris:
        return me
    ob = bpy.data.objects.new("dec", me)
    bpy.context.scene.collection.objects.link(ob)
    mod = ob.modifiers.new("d", 'DECIMATE')
    mod.ratio = target_tris / tris
    mod.use_collapse_triangulate = True
    dg = bpy.context.evaluated_depsgraph_get()
    out = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bpy.data.objects.remove(ob, do_unlink=True)
    activate_colors(out)
    return out


def smooth_mesh(me, iterations=2, factor=0.5):
    ob = bpy.data.objects.new("sm", me)
    bpy.context.scene.collection.objects.link(ob)
    mod = ob.modifiers.new("s", 'SMOOTH')
    mod.factor = factor
    mod.iterations = iterations
    dg = bpy.context.evaluated_depsgraph_get()
    out = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bpy.data.objects.remove(ob, do_unlink=True)
    return out


def paint(me, fn):
    """fn(co: Vector, normal: Vector) -> (r, g, b, a) in sRGB; alpha = wind weight."""
    if "Col" in me.color_attributes:
        me.color_attributes.remove(me.color_attributes["Col"])
    ca = me.color_attributes.new("Col", 'BYTE_COLOR', 'CORNER')
    cache = {}
    for loop in me.loops:
        vi = loop.vertex_index
        if vi not in cache:
            v = me.vertices[vi]
            cache[vi] = fn(v.co.copy(), v.normal.copy())
        ca.data[loop.index].color = cache[vi]
    me.color_attributes.active_color = ca
    return me


def join(meshes, name):
    bm = bmesh.new()
    for me in meshes:
        bm.from_mesh(me)
    out = mesh_from_bm(bm, name)
    activate_colors(out)
    return out


def activate_colors(me):
    if "Col" in me.color_attributes:
        me.color_attributes.active_color = me.color_attributes["Col"]
        me.color_attributes.render_color_index = me.color_attributes.active_color_index


def set_smooth(me):
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    me.update()


def tri_count(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def export(me, name, report):
    set_smooth(me)
    activate_colors(me)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    path = os.path.join(OUT, name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'},
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, mesh_smooth_type='OFF', colors_type='SRGB',
                             use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False)
    report.append((name, tri_count(me)))
    if PREVIEW:
        preview(ob, name)
    bpy.data.objects.remove(ob, do_unlink=True)


# ---------------------------------------------------------------- preview renders (for checking shapes)

def preview(ob, name):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'VERTEX'
    scene.render.resolution_x = 480
    scene.render.resolution_y = 480
    scene.render.film_transparent = False
    bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    lo = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb)))
    hi = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
    c = (lo + hi) / 2
    size = (hi - lo).length
    cam_data = bpy.data.cameras.new("pc")
    cam = bpy.data.objects.new("pc", cam_data)
    scene.collection.objects.link(cam)
    d = Vector((0.9, -1.6, 0.7)).normalized()
    cam.location = c + d * size * 1.25
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam_data.lens = 50
    scene.camera = cam
    scene.render.filepath = os.path.join(PREVIEW, name + ".png")
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)


# ================================================================= palette (sRGB)

LEAF_DARK = hexcol("2f6b2a")
LEAF_MID = hexcol("4f9a33")
LEAF_LIGHT = hexcol("9fd04a")
PINE_DARK = hexcol("1d4a30")
PINE_LIGHT = hexcol("4f9a48")
BARK_DARK = hexcol("4a2f22")
BARK_LIGHT = hexcol("7e5638")
ROCK_DARK = hexcol("6b6a74")
ROCK_LIGHT = hexcol("b4b0a6")
MOSS = hexcol("6f9a3c")
ORE = hexcol("e0873a")
ORE_DARK = hexcol("8a4a2a")
SKIN = hexcol("f0b58a")
SKIN_SHADE = hexcol("d48d68")
SLEEVE = hexcol("3f6f7a")
SLEEVE_DARK = hexcol("2b4c57")
CUFF = hexcol("c9a35a")
WOOD = hexcol("a2703f")
WOOD_DARK = hexcol("6b4526")
LEATHER = hexcol("5a3424")
STONE_HEAD = hexcol("8e8c94")
Z_SKIN = hexcol("8fae7c")
Z_SKIN_DARK = hexcol("5f7d5c")
Z_SHIRT = hexcol("6a5585")
Z_SHIRT_DARK = hexcol("4a3a62")
Z_PANTS = hexcol("4b5468")
Z_SHOE = hexcol("33303a")
Z_EYE = hexcol("f2f07a")
Z_MOUTH = hexcol("3a1f2a")
CAR_PAINT = hexcol("5b9aa0")
CAR_RUST = hexcol("a0572e")
CAR_DARK = hexcol("2e3138")
GLASS = hexcol("9ab8c8")
FENCE = hexcol("9b8a72")
FENCE_DARK = hexcol("6b5c4a")
GRASS_BASE = hexcol("2f5e22")
GRASS_TIP = hexcol("8fbf4a")
CLOUD = hexcol("ffffff")
CLOUD_SHADE = hexcol("dfe6f2")


def n3(p, scale=1.0, seed=0.0):
    return noise.noise(Vector((p.x * scale + seed, p.y * scale + seed * 1.7, p.z * scale - seed)))


# ================================================================= trees

def canopy_painter(center_z, radius, dark, mid, light, trunk_height):
    def fn(co, n):
        h = (co.z - (center_z - radius)) / (2 * radius)
        t = smooth01(h * 0.9 + n.z * 0.25 + 0.05 + n3(co, 1.3) * 0.18)
        c = lerpc(dark, mid, t * 1.6) if t < 0.62 else lerpc(mid, light, (t - 0.62) / 0.38)
        sway = max(0.0, min(1.0, (co.z - trunk_height * 0.6) / (radius * 2.0) + 0.45))
        return (c[0], c[1], c[2], sway)
    return fn


def bark_painter(height, base_sway=0.0):
    def fn(co, n):
        t = smooth01(co.z / max(height, 0.01))
        c = lerpc(BARK_DARK, BARK_LIGHT, 0.25 + 0.5 * t + n3(co, 4.0) * 0.12)
        sway = base_sway + max(0.0, co.z / height) ** 2 * 0.35
        return (c[0], c[1], c[2], min(sway, 1.0))
    return fn


def trunk(b, pts, r0, r1):
    b.chain([Vector(p) for p in pts], r0, r1)
    # root flare
    base = Vector(pts[0])
    for i in range(5):
        a = i / 5 * math.tau + 0.4
        d = Vector((math.cos(a), math.sin(a), 0))
        b.capsule(base + Vector((0, 0, 0.35)), base + d * r0 * 2.0 + Vector((0, 0, -0.05)), r0 * 0.38)


def tree_round(seed, report):
    rnd = random.Random(seed)
    h = 2.6 + rnd.random() * 0.5
    lean = 0.25 if seed % 2 else -0.2
    bt = Blob(0.05)
    pts = [(0, 0, 0), (lean * 0.3, 0.05, h * 0.4), (lean * 0.8, 0.1, h * 0.8), (lean, 0.05, h + 0.3)]
    trunk(bt, pts, 0.42, 0.28)
    top = Vector((lean, 0.05, h + 0.3))
    for k in range(3):
        a = k / 3 * math.tau + seed
        bt.chain([Vector((lean * 0.8, 0.1, h * 0.8)), top + Vector((math.cos(a) * 0.9, math.sin(a) * 0.9, 0.5))], 0.17, 0.1)
    me_t = paint(bt.to_mesh(), bark_painter(h + 0.6))
    bc = Blob(0.07)
    cz = h + 1.45
    puffs = [(0, 0, 0.35, 1.25)]
    for i in range(6):
        a = i / 6 * math.tau + rnd.random() * 0.6
        puffs.append((math.cos(a) * 1.25, math.sin(a) * 1.25, -0.15 + rnd.random() * 0.5, 0.9 + rnd.random() * 0.2))
    for i in range(3):
        a = i / 3 * math.tau + 0.5
        puffs.append((math.cos(a) * 0.6, math.sin(a) * 0.6, 0.95 + rnd.random() * 0.2, 0.8 + rnd.random() * 0.15))
    for (x, y, z, r) in puffs:
        bc.ball((lean + x, y, cz + z), r, stiff=5.0)
    me_c = paint(bc.to_mesh(), canopy_painter(cz + 0.2, 1.9, LEAF_DARK, LEAF_MID, LEAF_LIGHT, h))
    me_t = decimate(me_t, 1200)
    me_c = decimate(me_c, 3600)
    export(join([me_t, me_c], "tree"), "Tree_Round_%d" % seed, report)


def pine_tier(radius, height, z, droop, seed):
    """One rounded cone tier: soft bevelled rim, slightly drooping, smooth shaded."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=28, radius1=radius, radius2=radius * 0.12, depth=height)
    rim = [e for e in bm.edges if all(abs(v.co.z + height / 2) < 1e-4 for v in e.verts)]
    bmesh.ops.bevel(bm, geom=rim, offset=radius * 0.22, segments=4, affect='EDGES', profile=0.5, clamp_overlap=True)
    bmesh.ops.subdivide_edges(bm, edges=[e for e in bm.edges if abs(e.verts[0].co.z - e.verts[1].co.z) > height * 0.3], cuts=3, use_grid_fill=False)
    for v in bm.verts:
        r = math.hypot(v.co.x, v.co.y)
        a = math.atan2(v.co.y, v.co.x)
        wobble = 1.0 + 0.06 * math.sin(a * 5 + seed) + 0.03 * math.sin(a * 9 + seed * 2)
        v.co.x *= wobble
        v.co.y *= wobble
        v.co.z += z - droop * (r / radius) ** 2
    me = mesh_from_bm(bm, "tier")
    return smooth_mesh(me, 2, 0.35)


def tree_pine(seed, report):
    rnd = random.Random(seed)
    h = 6.2 + rnd.random() * 0.6
    bt = Blob(0.05)
    trunk(bt, [(0, 0, 0), (0.05, 0, h * 0.5), (0, 0.04, h - 0.6)], 0.34, 0.14)
    me_t = paint(bt.to_mesh(), bark_painter(h))
    tiers = []
    count = 4
    for i in range(count):
        t = i / (count - 1)
        rad = lerp(2.0, 0.8, t)
        th = lerp(2.0, 1.5, t)
        z = lerp(1.9, h - 0.6, t)
        tier = pine_tier(rad, th, z, 0.25, seed + i)
        base_z = z - th / 2

        def fn(co, n, base_z=base_z, th=th):
            local = (co.z - base_z) / th
            c = lerpc(PINE_DARK, PINE_LIGHT, smooth01(local * 0.9 + n.z * 0.25 + 0.1 + n3(co, 1.5) * 0.1))
            return (c[0], c[1], c[2], min(1.0, 0.2 + co.z / h * 0.8))
        tiers.append(decimate(paint(tier, fn), 1000))
    export(join([decimate(me_t, 700)] + tiers, "tree"), "Tree_Pine_%d" % seed, report)


def tree_dead(seed, report):
    # Bare, twisted post-apocalyptic tree: a readable silhouette without leaves.
    rnd = random.Random(seed)
    h = 3.6
    b = Blob(0.05)
    trunk(b, [(0, 0, 0), (0.15, 0, 1.2), (-0.1, 0.1, 2.4), (0.1, 0.05, h)], 0.3, 0.12)
    b.chain([Vector((-0.1, 0.1, 2.2)), Vector((-0.8, 0.2, 2.9)), Vector((-1.1, 0.5, 3.6))], 0.12, 0.06)
    b.chain([Vector((0.1, 0.0, 2.8)), Vector((0.7, -0.2, 3.4)), Vector((0.9, -0.1, 4.1))], 0.1, 0.05)
    b.chain([Vector((0.1, 0.05, h)), Vector((0.0, 0.3, h + 0.6))], 0.09, 0.05)
    me = paint(b.to_mesh(), bark_painter(h + 0.5, 0.0))
    export(decimate(me, 2200), "Tree_Dead_%d" % seed, report)


# ================================================================= rocks, ore, bush

def rock_mesh(seed, size, squash=0.62, subdiv=4):
    bm = icosphere(1.0, subdiv)
    for v in bm.verts:
        p = v.co.copy()
        d = 1.0 + n3(p, 0.9, seed) * 0.28 + n3(p, 2.1, seed + 3) * 0.08
        v.co = p * d
        v.co.z *= squash
        if v.co.z < -0.15:
            v.co.z = -0.15 + (v.co.z + 0.15) * 0.25
    for v in bm.verts:
        v.co *= size
    me = mesh_from_bm(bm, "rock")
    return smooth_mesh(me, 2, 0.4)


def rock_painter(top_moss=True):
    def fn(co, n):
        t = 0.35 + n.z * 0.35 + n3(co, 1.6) * 0.18
        c = lerpc(ROCK_DARK, ROCK_LIGHT, t)
        if top_moss and n.z > 0.72 + n3(co, 2.0, 5) * 0.2:
            c = lerpc(c, MOSS, 0.85)
        return (c[0], c[1], c[2], 0.0)
    return fn


def rocks(report):
    for i, (size, sq) in enumerate([(1.0, 0.62), (1.6, 0.5), (0.6, 0.8)]):
        me = paint(rock_mesh(11 + i * 7, size, sq), rock_painter(i != 2))
        export(decimate(me, 1400), "Rock_%d" % i, report)


def ore_rock(report):
    me_r = paint(rock_mesh(41, 1.25, 0.7), rock_painter(False))
    bm = bmesh.new()
    rnd = random.Random(5)
    for k in range(7):
        a = rnd.random() * math.tau
        el = 0.15 + rnd.random() * 0.55
        dirv = Vector((math.cos(a) * math.cos(el), math.sin(a) * math.cos(el), math.sin(el)))
        p = dirv * 1.25 * 0.95
        p.z *= 0.7
        r = 0.22 + rnd.random() * 0.12
        tmp = icosphere(r, 2)
        bmesh.ops.translate(tmp, verts=tmp.verts, vec=p)
        tmp_me = mesh_from_bm(tmp, "n")
        bm.from_mesh(tmp_me)
    me_o = mesh_from_bm(bm, "ore")
    me_o = smooth_mesh(me_o, 1, 0.3)

    def ofn(co, n):
        c = lerpc(ORE_DARK, ORE, 0.45 + n.z * 0.5)
        return (c[0], c[1], c[2], 0.0)
    paint(me_o, ofn)
    export(join([decimate(me_r, 1100), me_o], "ore"), "Rock_Ore", report)


def bush(seed, report):
    rnd = random.Random(seed)
    b = Blob(0.07)
    for i in range(6):
        a = i / 6 * math.tau + rnd.random()
        b.ball((math.cos(a) * 0.45, math.sin(a) * 0.45, 0.45 + rnd.random() * 0.2), 0.42 + rnd.random() * 0.1)
    b.ball((0, 0, 0.75), 0.5)

    def fn(co, n):
        c = canopy_painter(0.6, 0.6, LEAF_DARK, LEAF_MID, LEAF_LIGHT, 0.0)(co, n)
        return (c[0], c[1], c[2], min(1.0, max(0.0, co.z / 1.1)))
    me = paint(b.to_mesh(), fn)
    export(decimate(me, 1400), "Bush_%d" % seed, report)


# ================================================================= campfire, log

def log_mesh(a, b, r, charred_end=None, dark=False):
    bl = Blob(0.025)
    bl.capsule(a, b, r)
    me = bl.to_mesh()
    a, b = Vector(a), Vector(b)

    def fn(co, n):
        t = (co - a).dot((b - a).normalized()) / (b - a).length
        endness = max(0.0, 1.0 - abs(n.dot((b - a).normalized())) * 1.2)
        c = lerpc(WOOD_DARK, WOOD, (0.15 if dark else 0.4) + n.z * 0.35 + n3(co, 6.0) * 0.1)
        # light end grain on caps
        cap = abs(n.dot((b - a).normalized()))
        if cap > 0.7:
            c = lerpc(c, hexcol("d7b07a"), 0.8)
        if charred_end is not None:
            ch = smooth01((t - 0.55) / 0.35) if charred_end == 1 else smooth01((0.45 - t) / 0.35)
            c = lerpc(c, hexcol("2a2220"), ch)
        return (c[0], c[1], c[2], 0.0)
    return paint(me, fn)


def campfire(report):
    parts = []
    rnd = random.Random(3)
    for i in range(9):
        a = i / 9 * math.tau
        p = Vector((math.cos(a) * 0.72, math.sin(a) * 0.72, 0.08))
        rm = rock_mesh(60 + i, 0.2 + rnd.random() * 0.05, 0.7, 3)
        rm.transform(Matrix.Translation(p) @ Matrix.Rotation(rnd.random() * 6, 4, 'Z'))
        paint(rm, lambda co, n: lerpc(hexcol("34323b"), hexcol("6a6664"), 0.35 + n.z * 0.4 + n3(co, 3.0) * 0.15))
        parts.append(decimate(rm, 220))
    for i in range(4):
        a = i / 4 * math.tau + 0.4
        base = Vector((math.cos(a) * 0.5, math.sin(a) * 0.5, 0.06))
        top = Vector((math.cos(a) * 0.08, math.sin(a) * 0.08, 0.62))
        parts.append(decimate(log_mesh(base, top, 0.1, charred_end=1, dark=True), 500))
    # ash bed
    ash = Blob(0.04)
    ash.ellipsoid((0, 0, 0.0), 0.5, (1.0, 1.0, 0.18))
    me_a = ash.to_mesh()
    paint(me_a, lambda co, n: lerpc(hexcol("3a3533"), hexcol("6a5f58"), n3(co, 5.0) * 0.5 + 0.4))
    parts.append(decimate(me_a, 400))
    export(join(parts, "campfire"), "Campfire", report)


def wood_log(report):
    me = log_mesh((-0.35, 0, 0.12), (0.35, 0, 0.12), 0.12)
    export(decimate(me, 900), "Item_Log", report)


# ================================================================= ruins: car and fence

def rounded_box(size, center, bevel=0.12, segments=3):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0] * 2, v.co.y * size[1] * 2, v.co.z * size[2] * 2))
    bmesh.ops.bevel(bm, geom=list(bm.verts) + list(bm.edges), offset=bevel, segments=segments,
                    affect='EDGES', profile=0.5, clamp_overlap=True)
    bmesh.ops.translate(bm, verts=bm.verts, vec=Vector(center))
    return mesh_from_bm(bm, "rb")


def cylinder(r, depth, center, axis='X', segs=24, bevel=0.05):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segs, radius1=r, radius2=r, depth=depth)
    edges = [e for e in bm.edges if all(abs(abs(v.co.z) - depth / 2) < 1e-4 for v in e.verts)]
    bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=3, affect='EDGES', profile=0.5, clamp_overlap=True)
    rot = {'X': Matrix.Rotation(math.pi / 2, 4, 'Y'), 'Y': Matrix.Rotation(math.pi / 2, 4, 'X'), 'Z': Matrix.Identity(4)}[axis]
    bmesh.ops.transform(bm, matrix=rot, verts=bm.verts)
    bmesh.ops.translate(bm, verts=bm.verts, vec=Vector(center))
    return mesh_from_bm(bm, "cyl")


def car(report):
    parts = []

    def rust_paint(base, rust_amount=0.35):
        def fn(co, n):
            c = lerpc(base, lerpc(base, (1, 1, 1, 0), 0.25), n.z * 0.4 + 0.2)
            r = n3(co, 1.4, 9) + n3(co, 3.5, 2) * 0.4
            if r > 0.5 - rust_amount or co.z < 0.45 + n3(co, 2.0) * 0.15:
                c = lerpc(c, CAR_RUST, 0.85)
            return (c[0], c[1], c[2], 0.0)
        return fn

    body = rounded_box((2.1, 0.95, 0.36), (0, 0, 0.78), 0.22, 4)
    parts.append(paint(body, rust_paint(CAR_PAINT)))
    cabin = rounded_box((1.15, 0.85, 0.32), (-0.25, 0, 1.3), 0.24, 4)
    for v in cabin.vertices:  # taper the roof
        if v.co.z > 1.45:
            v.co.x *= 0.85
            v.co.y *= 0.92

    def cabin_paint(co, n):
        side = abs(n.x) > 0.55 or abs(n.y) > 0.55
        if side and 1.12 < co.z < 1.5:
            g = lerpc(GLASS, hexcol("e8f2f7"), 0.3 + n3(co, 3.0) * 0.4)
            if n3(co, 2.5, 4) > 0.25:  # broken windows: dark holes
                g = CAR_DARK
            return g
        return rust_paint(CAR_PAINT, 0.25)(co, n)
    parts.append(paint(cabin, cabin_paint))
    # bumpers + lights
    for x in (-2.12, 2.12):
        bmp = rounded_box((0.12, 0.98, 0.14), (x, 0, 0.42), 0.06, 3)
        parts.append(paint(bmp, lambda co, n: lerpc(hexcol("8f8a84"), CAR_RUST, 0.4 + n3(co, 3.0) * 0.6)))
    for y in (-0.62, 0.62):
        lamp = rounded_box((0.04, 0.16, 0.1), (2.1, y, 0.72), 0.04, 2)
        parts.append(paint(lamp, lambda co, n: hexcol("f0e3a8")))
    # three wheels (one missing) - the car sits tilted on that corner in Unity
    for (x, y) in ((1.35, -0.92), (1.35, 0.92), (-1.35, 0.92)):
        tire = cylinder(0.4, 0.3, (x, y, 0.4), 'Y', 28, 0.09)
        parts.append(paint(tire, lambda co, n: lerpc(CAR_DARK, hexcol("4a4d55"), 0.3 + n.z * 0.3)))
        hub = cylinder(0.18, 0.32, (x, y, 0.4), 'Y', 18, 0.03)
        parts.append(paint(hub, lambda co, n: lerpc(hexcol("8f8a84"), CAR_RUST, 0.5)))
    me = join(parts, "car")
    export(decimate(me, 4800), "Car_Wreck", report)


def fence(report):
    parts = []
    rnd = random.Random(8)

    def plank_paint(seed):
        def fn(co, n):
            c = lerpc(FENCE_DARK, FENCE, 0.4 + n.z * 0.3 + n3(co, 3.0, seed) * 0.25)
            return (c[0], c[1], c[2], 0.0)
        return fn
    for i, x in enumerate((-1.5, 0.0, 1.5)):
        tilt = (rnd.random() - 0.5) * 0.15
        post = rounded_box((0.11, 0.11, 0.62), (x, 0, 0.6), 0.04, 2)
        post.transform(Matrix.Translation((x, 0, 0)) @ Matrix.Rotation(tilt, 4, 'Y') @ Matrix.Translation((-x, 0, 0)))
        parts.append(paint(post, plank_paint(i)))
    rails = [((-1.5, 0.75), (1.5, 0.8), 0.0), ((-1.5, 0.38), (0.0, 0.4), 0.0), ((0.0, 0.42), (1.2, 0.05), 0.0)]
    for i, ((x0, z0), (x1, z1), _) in enumerate(rails):
        length = math.hypot(x1 - x0, z1 - z0)
        r = rounded_box((length / 2, 0.035, 0.08), (0, 0.09, 0), 0.03, 2)
        ang = math.atan2(z1 - z0, x1 - x0)
        r.transform(Matrix.Translation(((x0 + x1) / 2, 0.0, (z0 + z1) / 2)) @ Matrix.Rotation(-ang, 4, 'Y'))
        parts.append(paint(r, plank_paint(10 + i)))
    me = join(parts, "fence")
    export(me, "Fence_Broken", report)


# ================================================================= first-person arms + stone axe
# Built directly in view space (Unity axes via U()): the camera sits at the origin looking +Z.

AXE_GRIP = (0.24, -0.3, 0.5)          # where the right fist holds the handle (Unity coords)
AXE_DIR = Vector((-0.28, 1.0, 0.32)).normalized()  # handle direction, bottom -> top (Unity coords)


def u(v):
    return U(v[0], v[1], v[2])


def stone_axe_parts():
    g = Vector(AXE_GRIP)
    bottom = g - AXE_DIR * 0.16
    top = g + AXE_DIR * 0.42
    parts = []
    hb = Blob(0.005)
    pts = [bottom, g, g + AXE_DIR * 0.22 + Vector((0.01, 0, 0.005)), top]
    hb.chain([u(p) for p in pts], 0.02, 0.018)
    hb.ball(u(bottom), 0.026)

    def handle_paint(co, n):
        c = lerpc(WOOD_DARK, WOOD, 0.45 + n3(co, 25.0) * 0.25 + n.z * 0.2)
        return (c[0], c[1], c[2], 0.0)
    parts.append(paint(hb.to_mesh(), handle_paint))
    # stone head: chunky rounded wedge pointing left (-X) and a counter-lump on the right
    side = Vector((-1.0, 0.0, 0.0))
    side = (side - AXE_DIR * side.dot(AXE_DIR)).normalized()
    sb = Blob(0.01)
    head_c = top - AXE_DIR * 0.06
    sb.ellipsoid(u(head_c + side * 0.045), 0.046, (1.6, 0.75, 1.0), rot=Vector((1, 0, 0)).rotation_difference(u(side) - U(0, 0, 0)))
    sb.ellipsoid(u(head_c + side * 0.115), 0.042, (0.7, 0.45, 1.25), rot=Vector((1, 0, 0)).rotation_difference(u(side) - U(0, 0, 0)))
    sb.ball(u(head_c - side * 0.03), 0.035)

    def stone_paint(co, n):
        c = lerpc(hexcol("4e4c58"), hexcol("9d9aa8"), 0.4 + n.z * 0.35 + n3(co, 18.0) * 0.15)
        return (c[0], c[1], c[2], 0.0)
    parts.append(paint(sb.to_mesh(), stone_paint))
    # leather wrap around the head joint
    wb = Blob(0.008)
    for k in range(3):
        c = head_c - AXE_DIR * (0.035 + k * 0.022)
        wb.ellipsoid(u(c), 0.028, (1.0, 1.0, 0.45), rot=Vector((0, 0, 1)).rotation_difference(u(AXE_DIR) - U(0, 0, 0)))
    parts.append(paint(wb.to_mesh(), lambda co, n: lerpc(LEATHER, hexcol("8a5a3a"), 0.3 + n.z * 0.4)))
    return parts


def fist(b, center, axis, back, scale=1.0):
    """Cartoon fist wrapped around `axis` at `center`; `back` points to the back of the hand."""
    axis = axis.normalized()
    back = (back - axis * back.dot(axis)).normalized()
    side = axis.cross(back)
    R = 0.034 * scale
    b.ellipsoid(u(center + back * 0.03 * scale), 0.04 * scale, (1.0, 1.15, 0.75))  # palm + back of hand
    for i in range(4):
        off = axis * ((i - 1.5) * 0.021 * scale)
        ring = []
        for ang in (20, 95, 170, 235):
            a = math.radians(ang)
            ring.append(u(center + off + (back * math.cos(a) + side * math.sin(a)) * R))
        b.chain(ring, 0.0125 * scale, 0.0115 * scale)
    # thumb over the fingers
    t0 = center + back * 0.02 * scale + axis * 0.04 * scale + side * 0.02 * scale
    t1 = center - back * 0.01 * scale + axis * 0.045 * scale - side * 0.03 * scale
    b.chain([u(t0), u(t1)], 0.014 * scale, 0.012 * scale)


def arm_parts(grip, axis, shoulder, back_hint, name):
    grip, shoulder = Vector(grip), Vector(shoulder)
    k = 1.3
    hand = Blob(0.007)
    fist(hand, grip, axis, back_hint, k)
    back = (back_hint - axis * back_hint.dot(axis)).normalized()
    wrist = grip + back * 0.045 * k - axis * 0.055 * k
    hand.capsule(u(wrist), u(grip + back * 0.025 * k - axis * 0.03 * k), 0.034 * k)

    def skin(co, n):
        c = lerpc(SKIN_SHADE, SKIN, 0.55 + n.z * 0.3)
        return (c[0], c[1], c[2], 0.0)
    me_h = paint(hand.to_mesh(), skin)
    sl = Blob(0.012)
    fore = (shoulder - wrist).normalized()
    cuff_c = wrist + fore * 0.05
    sl.capsule(u(cuff_c), u(cuff_c + fore * 0.03), 0.058)
    elbow = wrist + (shoulder - wrist) * 0.5 + Vector((0.0, -0.04, 0.02))
    sl.chain([u(cuff_c + fore * 0.04), u(elbow), u(shoulder)], 0.056, 0.075)

    def sleeve(co, n):
        # cuff band is a different colour
        p = Vector((-co.x, co.z, -co.y))  # back to Unity coords
        d = (p - cuff_c).dot(fore)
        if d < 0.03:
            c = lerpc(hexcol("8f7240"), CUFF, 0.4 + n.z * 0.4)
        else:
            c = lerpc(SLEEVE_DARK, SLEEVE, 0.45 + n.z * 0.35 + n3(co, 12.0) * 0.08)
        return (c[0], c[1], c[2], 0.0)
    me_s = paint(sl.to_mesh(), sleeve)
    return [decimate(me_h, 5000), decimate(me_s, 3000)]


def viewmodel(report):
    right = arm_parts(AXE_GRIP, AXE_DIR, (0.98, -0.44, 0.3), Vector((1.0, -0.15, -0.6)), "R")
    axe = stone_axe_parts()
    me_r = join(right, "vm_r")
    axe = [decimate(m, 1000) for m in axe]
    me_axe = join(axe, "axe")
    export(me_r, "FP_Arm_Right", report)
    export(me_axe, "FP_StoneAxe", report)
    left_grip = Vector((-0.34, -0.31, 0.47))
    left = arm_parts(left_grip, Vector((0.25, 0.9, 0.35)).normalized(), (-1.0, -0.46, 0.3), Vector((-1.0, -0.1, -0.5)), "L")
    export(join(left, "vm_l"), "FP_Arm_Left", report)


# ================================================================= grass clump

def grass_clump(report, seed=1, blades=9):
    rnd = random.Random(seed)
    bm = bmesh.new()
    col = bm.loops.layers.color.new("Col")
    for i in range(blades):
        a = rnd.random() * math.tau
        r = rnd.random() * 0.12
        base = Vector((math.cos(a) * r, math.sin(a) * r, 0))
        h = 0.32 + rnd.random() * 0.3
        w = 0.05 + rnd.random() * 0.02
        face_dir = rnd.random() * math.tau
        side = Vector((math.cos(face_dir), math.sin(face_dir), 0))
        lean = Vector((math.cos(face_dir + 1.57), math.sin(face_dir + 1.57), 0)) * (0.08 + rnd.random() * 0.12)
        segs = 3
        rows = []
        for s in range(segs + 1):
            t = s / segs
            p = base + Vector((0, 0, h * t)) + lean * (t * t)
            ww = w * (1 - t) ** 0.8 if s < segs else 0.0
            if s < segs:
                rows.append((bm.verts.new(p - side * ww), bm.verts.new(p + side * ww), t))
            else:
                v = bm.verts.new(p)
                rows.append((v, v, t))
        for s in range(segs):
            a0, b0, t0 = rows[s]
            a1, b1, t1 = rows[s + 1]
            vs = [a0, b0, b1] if a1 is b1 else [a0, b0, b1, a1]
            f = bm.faces.new(vs)
            for lp in f.loops:
                vt = lp.vert.co.z / h
                c = lerpc(GRASS_BASE, GRASS_TIP, vt ** 1.2)
                lp[col] = (c[0], c[1], c[2], min(1.0, vt))
    me = mesh_from_bm(bm, "grass")
    me.color_attributes.active_color = me.color_attributes["Col"]
    export(me, "Grass_Clump", report)


# ================================================================= clouds

def clouds(report):
    for k in range(3):
        rnd = random.Random(100 + k)
        b = Blob(0.6)
        n = 6 + k
        for i in range(n):
            x = (i - n / 2) * 4.2 + rnd.random() * 2
            b.ball((x, rnd.random() * 3 - 1.5, rnd.random() * 1.5), 4.0 + rnd.random() * 2.5)
        b.ball((0, 0, 3.2), 5.2)

        def fn(co, nn):
            c = lerpc(CLOUD_SHADE, CLOUD, 0.5 + nn.z * 0.5)
            return (c[0], c[1], c[2], 0.0)
        me = paint(b.to_mesh(), fn)
        for v in me.vertices:
            if v.co.z < -0.5:
                v.co.z = -0.5 + (v.co.z + 0.5) * 0.3
        export(decimate(me, 1500), "Cloud_%d" % k, report)


# ================================================================= zombie (rigged + shamble loop)

def zombie(report):
    clear_scene()
    # Parts: (label, kind, data). Colour comes from the nearest part, so regions stay clean.
    parts = []

    def cap(label, a, b, r):
        parts.append((label, "cap", (Vector(a), Vector(b), r)))

    def ell(label, c, r, size):
        parts.append((label, "ell", (Vector(c), r, Vector(size))))

    for s in (-1, 1):
        ell("shoe", (0.13 * s, -0.06, 0.07), 0.1, (0.95, 1.6, 0.6))
        cap("pants_low", (0.13 * s, 0.0, 0.12), (0.13 * s, -0.02, 0.48), 0.088)
        cap("pants", (0.12 * s, 0.0, 0.5), (0.11 * s, 0.02, 0.88), 0.11)
        ell("shirt", (0.29 * s, 0.03, 1.42), 0.12, (1, 1, 1))
        cap("shirt", (0.3 * s, 0.02, 1.42), (0.34 * s, -0.24, 1.33), 0.088)
        cap("skin", (0.34 * s, -0.24, 1.33), (0.3 * s, -0.54, 1.35), 0.074)
        ell("skin", (0.3 * s, -0.62, 1.34), 0.088, (1.0, 1.15, 0.62))
        for f in range(3):
            cap("skin", (0.3 * s + (f - 1) * 0.04, -0.66, 1.33), (0.3 * s + (f - 1) * 0.045, -0.76, 1.28), 0.026)
    ell("pants", (0, 0.02, 0.93), 0.2, (1.3, 0.9, 0.8))
    ell("shirt", (0, -0.03, 1.12), 0.25, (1.05, 0.95, 1.0))
    ell("shirt", (0, 0.03, 1.36), 0.28, (1.3, 0.85, 0.85))
    cap("skin", (0, 0.02, 1.5), (0, -0.02, 1.63), 0.08)

    head = []
    head.append(("skin", "ell", (Vector((0, -0.05, 1.86)), 0.27, Vector((1.05, 0.95, 1.0)))))
    head.append(("skin", "ell", (Vector((0, -0.16, 1.71)), 0.17, Vector((1.1, 0.9, 0.72)))))
    head.append(("brow", "ell", (Vector((0, -0.25, 1.94)), 0.075, Vector((2.3, 0.75, 0.6)))))
    for s in (-1, 1):
        head.append(("skin", "ell", (Vector((0.27 * s, -0.02, 1.86)), 0.06, Vector((0.6, 1.0, 1.2)))))

    def build(part_list, res):
        b = Blob(res)
        for label, kind, d in part_list:
            if kind == "cap":
                b.capsule(d[0], d[1], d[2])
            else:
                b.ellipsoid(d[0], d[1], tuple(d[2]))
        return b.to_mesh()

    def dist(p, kind, d):
        if kind == "cap":
            a, b2, r = d
            ab = b2 - a
            t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
            return (a + ab * t - p).length - r
        c, r, size = d
        q = p - c
        q = Vector((q.x / size.x, q.y / size.y, q.z / size.z))
        return (q.length - r) * min(size)

    def labeller(part_list):
        def label(p):
            best, lab = 1e9, "skin"
            for l, kind, d in part_list:
                dd = dist(p, kind, d)
                if dd < best:
                    best, lab = dd, l
            return lab
        return label

    body_label = labeller(parts)
    head_label = labeller(head)

    def shade(dark, light, n, co, amount=0.3):
        return lerpc(dark, light, 0.5 + n.z * amount + n3(co, 5.0, 3) * 0.06)

    def body_paint(co, n):
        lab = body_label(co)
        if lab == "shoe":
            c = shade(Z_SHOE, hexcol("4a4552"), n, co)
        elif lab.startswith("pants"):
            c = shade(hexcol("323a4e"), Z_PANTS, n, co)
            if lab == "pants_low" and co.z < 0.3 + n3(co, 7.0, 4) * 0.1:
                c = shade(Z_SKIN_DARK, Z_SKIN, n, co)  # torn trouser legs
        elif lab == "shirt":
            c = shade(Z_SHIRT_DARK, Z_SHIRT, n, co)
            if n3(co, 4.0, 11) > 0.5:
                c = shade(Z_SKIN_DARK, Z_SKIN, n, co)  # a few rips
        else:
            c = shade(Z_SKIN_DARK, Z_SKIN, n, co)
        return (c[0], c[1], c[2], 0.0)

    def head_paint(co, n):
        lab = head_label(co)
        c = shade(Z_SKIN_DARK, Z_SKIN, n, co, 0.35)
        if lab == "brow":
            c = shade(hexcol("4f6a4e"), hexcol("6f8f66"), n, co)
        if co.z > 2.0:
            c = lerpc(c, hexcol("7d7396"), 0.35)  # bruised crown
        return (c[0], c[1], c[2], 0.0)

    body = decimate(paint(build(parts, 0.022), body_paint), 4550)
    me_head = decimate(paint(build(head, 0.018), head_paint), 1900)

    feat = Blob(0.006)
    feat.ball((-0.1, -0.255, 1.87), 0.078)
    feat.ball((0.115, -0.262, 1.85), 0.056)
    eyes = paint(feat.to_mesh(), lambda co, n: lerpc(hexcol("d9d46a"), Z_EYE, 0.55 + n.z * 0.3))
    pupil = Blob(0.005)
    pupil.ball((-0.095, -0.325, 1.865), 0.03)
    pupil.ball((0.118, -0.312, 1.845), 0.022)
    pupils = paint(pupil.to_mesh(), lambda co, n: hexcol("221a28"))
    mouth = Blob(0.006)
    mouth.ellipsoid((0.0, -0.3, 1.685), 0.07, (1.6, 0.45, 0.55))
    me_m = paint(mouth.to_mesh(), lambda co, n: Z_MOUTH)
    teeth = Blob(0.005)
    teeth.ellipsoid((-0.04, -0.325, 1.705), 0.022, (0.8, 0.5, 1.1))
    teeth.ellipsoid((0.035, -0.322, 1.71), 0.019, (0.8, 0.5, 1.0))
    me_t = paint(teeth.to_mesh(), lambda co, n: hexcol("efe6c8"))
    hair = Blob(0.008)
    for (x, y, tip) in ((-0.06, 0.0, (-0.1, 0.06, 2.22)), (0.04, -0.03, (0.07, -0.02, 2.24)), (0.0, 0.08, (0.02, 0.16, 2.2))):
        hair.capsule((x, y, 2.08), tip, 0.022)
    me_h = paint(hair.to_mesh(), lambda co, n: hexcol("3a3440"))
    me = join([body, me_head, decimate(eyes, 500), decimate(pupils, 200), decimate(me_m, 300), decimate(me_t, 200), decimate(me_h, 300)], "zombie")
    set_smooth(me)
    ob = bpy.data.objects.new("Zombie", me)
    bpy.context.scene.collection.objects.link(ob)

    # ---- armature
    arm_data = bpy.data.armatures.new("ZombieRig")
    rig = bpy.data.objects.new("ZombieRig", arm_data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    bones = {}

    def bone(name, head, tail, parent=None):
        eb = arm_data.edit_bones.new(name)
        eb.head, eb.tail = Vector(head), Vector(tail)
        eb.roll = 0
        if parent:
            eb.parent = arm_data.edit_bones[parent]
        bones[name] = (Vector(head), Vector(tail))
        return eb
    bone("Hips", (0, 0, 0.9), (0, 0, 1.08))
    bone("Spine", (0, 0, 1.08), (0, 0.0, 1.3), "Hips")
    bone("Chest", (0, 0.0, 1.3), (0, 0.02, 1.5), "Spine")
    bone("Neck", (0, 0.02, 1.5), (0, -0.02, 1.64), "Chest")
    bone("Head", (0, -0.02, 1.64), (0, -0.05, 2.1), "Neck")
    for s, side in ((1, "L"), (-1, "R")):
        bone("UpperArm." + side, (0.26 * s, 0.03, 1.43), (0.34 * s, -0.24, 1.33), "Chest")
        bone("Forearm." + side, (0.34 * s, -0.24, 1.33), (0.3 * s, -0.54, 1.35), "UpperArm." + side)
        bone("Hand." + side, (0.3 * s, -0.54, 1.35), (0.3 * s, -0.76, 1.28), "Forearm." + side)
        bone("Thigh." + side, (0.12 * s, 0.0, 0.9), (0.13 * s, -0.01, 0.49), "Hips")
        bone("Shin." + side, (0.13 * s, -0.01, 0.49), (0.13 * s, 0.0, 0.1), "Thigh." + side)
        bone("Foot." + side, (0.13 * s, 0.0, 0.1), (0.13 * s, -0.18, 0.04), "Shin." + side)
    bpy.ops.object.mode_set(mode='OBJECT')

    # ---- weights: smooth falloff by distance to each bone segment (top 3 influences)
    groups = {name: ob.vertex_groups.new(name=name) for name in bones}

    def seg_dist(p, a, b2):
        ab = b2 - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
        return (a + ab * t - p).length
    for v in me.vertices:
        p = v.co
        ds = []
        for name, (h, t) in bones.items():
            d = seg_dist(p, h, t)
            # keep arms and legs from grabbing torso vertices
            if name.startswith(("UpperArm", "Forearm", "Hand")) and abs(p.x) < 0.2:
                d += 0.3
            if name.startswith(("Thigh", "Shin", "Foot")) and p.z > 1.0:
                d += 0.3
            if (name.startswith(("Thigh", "Shin", "Foot", "UpperArm", "Forearm", "Hand")) and
                    ((name.endswith(".L") and p.x < -0.02) or (name.endswith(".R") and p.x > 0.02))):
                d += 0.5
            if name == "Head" and p.z > 1.64:
                d = 0.0
            ds.append((d, name))
        ds.sort()
        top = ds[:3]
        ws = [1.0 / (d + 0.02) ** 4 for d, _ in top]
        tot = sum(ws)
        for (d, name), w in zip(top, ws):
            if w / tot > 0.02:
                groups[name].add([v.index], w / tot, 'REPLACE')
    ob.parent = rig
    mod = ob.modifiers.new("Armature", 'ARMATURE')
    mod.object = rig

    # ---- shamble loop: 48 frames @ 24 fps, everything periodic so frame 1 == frame 49
    scene = bpy.context.scene
    scene.render.fps = 24
    scene.frame_start, scene.frame_end = 1, 49
    action = bpy.data.actions.new("Shamble")
    rig.animation_data_create()
    rig.animation_data.action = action
    pb = rig.pose.bones
    for p in pb:
        p.rotation_mode = 'XYZ'
    D = math.radians
    for f in range(1, 50):
        t = (f - 1) / 48.0
        w = t * math.tau
        pb["Hips"].location = (0.0, 0.03 * abs(math.sin(w)) - 0.015, 0.0)  # bone-space Y = up
        pb["Hips"].rotation_euler = (0.0, D(6) * math.sin(w), D(5) * math.sin(w))
        pb["Spine"].rotation_euler = (D(14) + D(4) * math.sin(2 * w), D(8) * math.sin(w + 0.6), 0)
        pb["Chest"].rotation_euler = (D(8) + D(3) * math.sin(2 * w + 0.5), D(-6) * math.sin(w), D(3) * math.sin(w))
        pb["Neck"].rotation_euler = (D(-10), 0, D(10) * math.sin(w + 1.2))
        pb["Head"].rotation_euler = (D(-6) + D(7) * math.sin(2 * w + 1.0), D(10) * math.sin(w + 0.4), D(16) + D(8) * math.sin(w + 1.5))
        for s, side in ((1, "L"), (-1, "R")):
            ph = 0 if s > 0 else math.pi
            pb["UpperArm." + side].rotation_euler = (D(9) * math.sin(w + ph), 0, D(5) * math.sin(w + ph + 0.8) * s)
            pb["Forearm." + side].rotation_euler = (D(-8) + D(10) * math.sin(w + ph + 0.7), 0, 0)
            pb["Hand." + side].rotation_euler = (D(-20) + D(14) * math.sin(w + ph + 1.4), 0, 0)
            pb["Thigh." + side].rotation_euler = (D(14) * math.sin(w + ph), 0, 0)
            pb["Shin." + side].rotation_euler = (D(-12) * max(0.0, math.sin(w + ph + 0.8)), 0, 0)
            pb["Foot." + side].rotation_euler = (D(8) * math.sin(w + ph + 1.2), 0, 0)
        for p in pb:
            p.keyframe_insert("rotation_euler", frame=f)
        pb["Hips"].keyframe_insert("location", frame=f)
    for p in pb:
        p.rotation_euler = (0, 0, 0)
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    path = os.path.join(OUT, "Zombie_Shambler.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'ARMATURE'},
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             mesh_smooth_type='OFF', colors_type='SRGB', add_leaf_bones=False,
                             bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
                             primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL')
    report.append(("Zombie_Shambler", tri_count(me)))
    if PREVIEW:
        scene.frame_set(12)
        preview(ob, "Zombie_Shambler")


# ================================================================= main

def main():
    report = []
    clear_scene()
    for s in (1, 2):
        tree_round(s, report)
    tree_pine(1, report)
    tree_dead(1, report)
    rocks(report)
    ore_rock(report)
    bush(1, report)
    bush(2, report)
    campfire(report)
    wood_log(report)
    car(report)
    fence(report)
    viewmodel(report)
    grass_clump(report)
    clouds(report)
    zombie(report)
    for name, tris in report:
        print("ASSET %-18s %6d tris" % (name, tris))


main()
