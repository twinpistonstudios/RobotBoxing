import math
import os
import random
import struct
import wave
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
MODELS_PARTS_DIR = os.path.join(ROOT, "Assets", "Art", "Models", "Parts")
MODELS_ARENA_DIR = os.path.join(ROOT, "Assets", "Art", "Models", "Arena")
TEXTURES_DIR = os.path.join(ROOT, "Assets", "Art", "Textures")
UI_DIR = os.path.join(ROOT, "Assets", "Art", "UI")
SFX_DIR = os.path.join(ROOT, "Assets", "Resources", "Audio", "SFX")
BGM_DIR = os.path.join(ROOT, "Assets", "Resources", "Audio", "BGM")

for d in [MODELS_PARTS_DIR, MODELS_ARENA_DIR, TEXTURES_DIR, UI_DIR, SFX_DIR, BGM_DIR]:
    os.makedirs(d, exist_ok=True)


# ==============================================================================
# 1. MULTI-MATERIAL PROCEDURAL 3D MESH BUILDER (.OBJ with 3 Submeshes)
#    mat 0 = Armor (Primary Paint), mat 1 = Gunmetal (Joints/Pistons), mat 2 = Emissive (Glow)
# ==============================================================================
MAT_NAMES = ["Armor", "Gunmetal", "Emissive"]


class MeshBuilder:
    def __init__(self):
        self.verts = []
        self.faces = []  # (i0, i1, i2, mat_idx)

    def add_vert(self, x, y, z):
        self.verts.append((float(x), float(y), float(z)))
        return len(self.verts) - 1

    def add_tri(self, a, b, c, mat=0):
        if a != b and b != c and a != c:
            self.faces.append((a, b, c, mat))

    def add_quad(self, a, b, c, d, mat=0):
        self.add_tri(a, b, c, mat)
        self.add_tri(a, c, d, mat)

    def add_box(self, cx, cy, cz, sx, sy, sz, bevel=0.0, taper_top=1.0, taper_front=1.0, mat=0):
        hx, hy, hz = sx * 0.5, sy * 0.5, sz * 0.5
        if bevel <= 0.0:
            pts = [
                (-hx, -hy, -hz), (hx, -hy, -hz), (hx, hy, -hz), (-hx, hy, -hz),
                (-hx, -hy, hz), (hx, -hy, hz), (hx, hy, hz), (-hx, hy, hz),
            ]
            idx = []
            for px, py, pz in pts:
                t = taper_top if py > 0 else 1.0
                tf = taper_front if pz > 0 else 1.0
                idx.append(self.add_vert(cx + px * t * tf, cy + py, cz + pz * t))
            self.add_quad(idx[0], idx[3], idx[2], idx[1], mat)
            self.add_quad(idx[4], idx[5], idx[6], idx[7], mat)
            self.add_quad(idx[0], idx[4], idx[7], idx[3], mat)
            self.add_quad(idx[1], idx[2], idx[6], idx[5], mat)
            self.add_quad(idx[3], idx[7], idx[6], idx[2], mat)
            self.add_quad(idx[0], idx[1], idx[5], idx[4], mat)
            return

        b = min(bevel, hx * 0.45, hy * 0.45, hz * 0.45)
        self.add_rounded_box(cx, cy, cz, sx, sy, sz, b, segs=2, taper_top=taper_top, taper_front=taper_front, mat=mat)

    def add_rounded_box(self, cx, cy, cz, sx, sy, sz, radius, segs=2, taper_top=1.0, taper_front=1.0, mat=0):
        hx, hy, hz = sx * 0.5, sy * 0.5, sz * 0.5
        r = min(radius, hx * 0.48, hy * 0.48, hz * 0.48)
        ix, iy, iz = hx - r, hy - r, hz - r
        n = segs * 2 + 1

        def project(px, py, pz):
            qx = max(-ix, min(ix, px))
            qy = max(-iy, min(iy, py))
            qz = max(-iz, min(iz, pz))
            dx, dy, dz = px - qx, py - qy, pz - qz
            length = math.sqrt(dx * dx + dy * dy + dz * dz)
            if length > 1e-6:
                dx /= length
                dy /= length
                dz /= length
            vx, vy, vz = qx + dx * r, qy + dy * r, qz + dz * r
            t = taper_top if vy > 0 else 1.0
            tf = taper_front if vz > 0 else 1.0
            return (cx + vx * t * tf, cy + vy, cz + vz * t)

        vert_cache = {}
        def get_v(gx, gy, gz):
            key = (gx, gy, gz)
            if key not in vert_cache:
                px = (gx / n) * 2.0 - 1.0
                py = (gy / n) * 2.0 - 1.0
                pz = (gz / n) * 2.0 - 1.0
                x, y, z = project(px * hx, py * hy, pz * hz)
                vert_cache[key] = self.add_vert(x, y, z)
            return vert_cache[key]

        for i in range(n):
            for j in range(n):
                self.add_quad(get_v(i, j, n), get_v(i + 1, j, n), get_v(i + 1, j + 1, n), get_v(i, j + 1, n), mat)
                self.add_quad(get_v(i + 1, j, 0), get_v(i, j, 0), get_v(i, j + 1, 0), get_v(i + 1, j + 1, 0), mat)
                self.add_quad(get_v(n, j, n - i), get_v(n, j, n - i - 1), get_v(n, j + 1, n - i - 1), get_v(n, j + 1, n - i), mat)
                self.add_quad(get_v(0, j, i), get_v(0, j, i + 1), get_v(0, j + 1, i + 1), get_v(0, j + 1, i), mat)
                self.add_quad(get_v(i, n, n - j), get_v(i + 1, n, n - j), get_v(i + 1, n, n - j - 1), get_v(i, n, n - j - 1), mat)
                self.add_quad(get_v(i, 0, j), get_v(i + 1, 0, j), get_v(i + 1, 0, j + 1), get_v(i, 0, j + 1), mat)

    def add_cylinder(self, cx, cy, cz, r_bottom, r_top, height, segs=12, axis='y', bevel=0.0, mat=0):
        hy = height * 0.5
        if bevel > 0.0 and bevel < hy * 0.4:
            profile = [
                (-hy, r_bottom * 0.85),
                (-hy + bevel, r_bottom),
                (hy - bevel, r_top),
                (hy, r_top * 0.85),
            ]
        else:
            profile = [(-hy, r_bottom), (hy, r_top)]

        def make_pt(angle, rad, h):
            a = rad * math.cos(angle)
            b = rad * math.sin(angle)
            if axis == 'y':
                return (cx + a, cy + h, cz + b)
            elif axis == 'z':
                return (cx + a, cy + b, cz + h)
            else:
                return (cx + h, cy + b, cz + a)

        rings = []
        for h, rad in profile:
            ring = []
            for s in range(segs):
                ang = (2.0 * math.pi * s) / segs
                ring.append(self.add_vert(*make_pt(ang, rad, h)))
            rings.append(ring)

        for r_idx in range(len(rings) - 1):
            r0, r1 = rings[r_idx], rings[r_idx + 1]
            for s in range(segs):
                sn = (s + 1) % segs
                self.add_quad(r0[s], r0[sn], r1[sn], r1[s], mat)

        bot_center = self.add_vert(*make_pt(0, 0, profile[0][0]))
        top_center = self.add_vert(*make_pt(0, 0, profile[-1][0]))
        for s in range(segs):
            sn = (s + 1) % segs
            self.add_tri(bot_center, rings[0][sn], rings[0][s], mat)
            self.add_tri(top_center, rings[-1][s], rings[-1][sn], mat)

    def add_sphere(self, cx, cy, cz, rx, ry, rz, u_segs=14, v_segs=10, mat=0):
        top = self.add_vert(cx, cy + ry, cz)
        bot = self.add_vert(cx, cy - ry, cz)
        rings = []
        for v in range(1, v_segs):
            phi = math.pi * v / v_segs
            y = math.cos(phi) * ry
            ring_r = math.sin(phi)
            ring = []
            for u in range(u_segs):
                theta = 2.0 * math.pi * u / u_segs
                ring.append(self.add_vert(
                    cx + math.cos(theta) * ring_r * rx,
                    cy + y,
                    cz + math.sin(theta) * ring_r * rz,
                ))
            rings.append(ring)

        for u in range(u_segs):
            un = (u + 1) % u_segs
            self.add_tri(top, rings[0][un], rings[0][u], mat)
            self.add_tri(bot, rings[-1][u], rings[-1][un], mat)

        for r in range(len(rings) - 1):
            r0, r1 = rings[r], rings[r + 1]
            for u in range(u_segs):
                un = (u + 1) % u_segs
                self.add_quad(r0[u], r0[un], r1[un], r1[u], mat)

    def add_plane_xy(self, cx, cy, cz, w, h, mat=0):
        hw, hh = w * 0.5, h * 0.5
        v0 = self.add_vert(cx - hw, cy - hh, cz)
        v1 = self.add_vert(cx + hw, cy - hh, cz)
        v2 = self.add_vert(cx + hw, cy + hh, cz)
        v3 = self.add_vert(cx - hw, cy + hh, cz)
        self.add_quad(v0, v1, v2, v3, mat)

    def write_obj(self, path, explicit_uvs=False, multi_material=True):
        if multi_material:
            # Ensure all 3 material groups always have at least 1 triangle (tiny degenerate-free inner filler if absent)
            present = {f[3] for f in self.faces}
            for m_idx in (0, 1, 2):
                if m_idx not in present:
                    # Tiny micro-tetrahedron face hidden at origin inside mesh
                    a = self.add_vert(-0.002, 0.0, -0.002)
                    b = self.add_vert(0.002, 0.0, -0.002)
                    c = self.add_vert(0.0, 0.002, 0.002)
                    self.add_tri(a, b, c, m_idx)

        lines = ["# Iron Knuckle Multi-Material Stylized Mesh\n"]
        if multi_material:
            lines.append("mtllib iron_knuckle_parts.mtl\n")
        for vx, vy, vz in self.verts:
            lines.append(f"v {vx:.5f} {vy:.5f} {vz:.5f}\n")

        if explicit_uvs and len(self.verts) == 4:
            for u, v in [(0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0)]:
                lines.append(f"vt {u:.4f} {v:.4f}\n")
        else:
            for vx, vy, vz in self.verts:
                u = (vx + vz) * 0.5 + 0.5
                v = vy * 0.5 + 0.5
                lines.append(f"vt {u:.4f} {v:.4f}\n")

        norms = [[0.0, 0.0, 0.0] for _ in self.verts]
        for a, b, c, _ in self.faces:
            va, vb, vc = self.verts[a], self.verts[b], self.verts[c]
            ux, uy, uz = vb[0] - va[0], vb[1] - va[1], vb[2] - va[2]
            wx, wy, wz = vc[0] - va[0], vc[1] - va[1], vc[2] - va[2]
            nx = uy * wz - uz * wy
            ny = uz * wx - ux * wz
            nz = ux * wy - uy * wx
            for idx in (a, b, c):
                norms[idx][0] += nx
                norms[idx][1] += ny
                norms[idx][2] += nz

        for nx, ny, nz in norms:
            ln = math.sqrt(nx * nx + ny * ny + nz * nz)
            if ln > 1e-6:
                nx, ny, nz = nx / ln, ny / ln, nz / ln
            else:
                nx, ny, nz = 0.0, 1.0, 0.0
            lines.append(f"vn {nx:.5f} {ny:.5f} {nz:.5f}\n")

        if multi_material:
            for m_idx, m_name in enumerate(MAT_NAMES):
                lines.append(f"usemtl {m_name}\n")
                for a, b, c, fm in self.faces:
                    if fm == m_idx:
                        ia, ib, ic = a + 1, b + 1, c + 1
                        lines.append(f"f {ia}/{ia}/{ia} {ib}/{ib}/{ib} {ic}/{ic}/{ic}\n")
        else:
            for a, b, c, _ in self.faces:
                ia, ib, ic = a + 1, b + 1, c + 1
                lines.append(f"f {ia}/{ia}/{ia} {ib}/{ib}/{ib} {ic}/{ic}/{ic}\n")

        with open(path, "w", encoding="utf-8") as f:
            f.writelines(lines)


def build_all_models():
    mtl_path = os.path.join(MODELS_PARTS_DIR, "iron_knuckle_parts.mtl")
    with open(mtl_path, "w", encoding="utf-8") as mf:
        mf.write(
            "# Iron Knuckle Shared Parts Materials\n"
            "newmtl Armor\nKd 0.22 0.56 0.96\n"
            "newmtl Gunmetal\nKd 0.20 0.23 0.30\n"
            "newmtl Emissive\nKd 1.00 0.65 0.15\n"
        )

    # Visor screen (single-submesh plane)
    m = MeshBuilder()
    m.add_plane_xy(0, 0, 0, 0.34, 0.16, mat=0)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "head_visor_screen.obj"), explicit_uvs=True, multi_material=False)

    # ==========================================================================
    # 12 HEADS (mat 0=Armor, 1=Gunmetal, 2=Emissive)
    # ==========================================================================
    # 1. Classic Boxing Helmet
    m = MeshBuilder()
    m.add_box(0, 0.22, 0, 0.42, 0.40, 0.40, bevel=0.07, mat=0)
    m.add_box(0, 0.44, 0.02, 0.14, 0.08, 0.36, bevel=0.02, mat=2)
    m.add_cylinder(-0.22, 0.22, 0, 0.08, 0.08, 0.06, segs=10, axis='x', bevel=0.01, mat=1)
    m.add_cylinder(0.22, 0.22, 0, 0.08, 0.08, 0.06, segs=10, axis='x', bevel=0.01, mat=1)
    m.add_box(0, 0.08, 0.17, 0.34, 0.11, 0.10, bevel=0.02, mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "head_classic.obj"))

    # 2. Cyclops Heavy Dome
    m = MeshBuilder()
    m.add_cylinder(0, 0.22, 0, 0.24, 0.21, 0.42, segs=14, axis='y', bevel=0.06, mat=0)
    m.add_box(0, 0.24, 0.18, 0.36, 0.18, 0.10, bevel=0.03, mat=1)
    m.add_cylinder(0, 0.45, -0.02, 0.06, 0.04, 0.10, segs=10, axis='y', mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "head_cyclops.obj"))

    # 3. Twin-Horn Brawler
    m = MeshBuilder()
    m.add_box(0, 0.21, 0, 0.46, 0.38, 0.42, bevel=0.06, taper_top=0.92, mat=0)
    m.add_cylinder(-0.20, 0.44, 0.04, 0.055, 0.02, 0.24, segs=8, axis='y', mat=1)
    m.add_cylinder(0.20, 0.44, 0.04, 0.055, 0.02, 0.24, segs=8, axis='y', mat=1)
    m.add_box(0, 0.08, 0.18, 0.38, 0.12, 0.12, bevel=0.02, mat=1)
    m.add_box(0, 0.41, 0.12, 0.18, 0.05, 0.06, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "head_horned.obj"))

    # 4. Aero Samurai
    m = MeshBuilder()
    m.add_box(0, 0.22, 0, 0.38, 0.38, 0.42, bevel=0.06, taper_front=0.82, mat=0)
    m.add_box(0, 0.46, 0.04, 0.38, 0.06, 0.16, bevel=0.015, mat=2)
    m.add_box(0, 0.48, -0.04, 0.06, 0.15, 0.34, bevel=0.015, mat=1)
    m.add_box(0, 0.09, 0.16, 0.30, 0.10, 0.10, bevel=0.02, mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "head_samurai.obj"))

    # 5. Tesla Coil Dome
    m = MeshBuilder()
    m.add_sphere(0, 0.24, 0, 0.23, 0.23, 0.23, u_segs=14, v_segs=10, mat=0)
    m.add_cylinder(0, 0.48, 0, 0.13, 0.13, 0.05, segs=12, axis='y', bevel=0.01, mat=1)
    m.add_cylinder(0, 0.56, 0, 0.09, 0.09, 0.04, segs=12, axis='y', bevel=0.01, mat=2)
    m.add_box(0, 0.10, 0.15, 0.32, 0.12, 0.12, bevel=0.02, mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "head_tesla.obj"))

    # 6. Apex Crown
    m = MeshBuilder()
    m.add_box(0, 0.22, 0, 0.44, 0.40, 0.42, bevel=0.07, mat=0)
    for xoff in (-0.14, 0.0, 0.14):
        hs = 0.22 if xoff == 0.0 else 0.15
        m.add_cylinder(xoff, 0.42 + hs * 0.4, 0.02, 0.045, 0.015, hs, segs=8, axis='y', mat=2)
    m.add_box(0, 0.09, 0.19, 0.36, 0.12, 0.10, bevel=0.025, mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "head_apex.obj"))

    # 7. Retro CRT Monitor
    m = MeshBuilder()
    m.add_box(0, 0.24, -0.02, 0.48, 0.42, 0.42, bevel=0.04, mat=0)
    m.add_box(0, 0.24, 0.16, 0.44, 0.34, 0.10, bevel=0.02, mat=1)  # monitor bezel
    m.add_cylinder(0.18, 0.52, -0.05, 0.02, 0.01, 0.22, segs=6, axis='y', mat=2)  # antenna
    m.add_cylinder(-0.18, 0.50, -0.05, 0.02, 0.01, 0.16, segs=6, axis='y', mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "head_crt_retro.obj"))

    # 8. Valkyrie Winged Helm
    m = MeshBuilder()
    m.add_box(0, 0.22, 0, 0.38, 0.38, 0.40, bevel=0.06, taper_front=0.85, mat=0)
    m.add_box(-0.26, 0.34, -0.06, 0.10, 0.26, 0.30, bevel=0.02, mat=2)  # wing ear L
    m.add_box(0.26, 0.34, -0.06, 0.10, 0.26, 0.30, bevel=0.02, mat=2)   # wing ear R
    m.add_box(0, 0.08, 0.16, 0.30, 0.10, 0.10, bevel=0.02, mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "head_valkyrie.obj"))

    # 9. Iron Cage Brawler
    m = MeshBuilder()
    m.add_box(0, 0.22, 0, 0.44, 0.40, 0.42, bevel=0.05, mat=0)
    for bx in (-0.12, 0.0, 0.12):
        m.add_cylinder(bx, 0.12, 0.22, 0.018, 0.018, 0.20, segs=6, axis='y', mat=1)  # cage bars
    m.add_box(0, 0.43, 0, 0.28, 0.06, 0.28, bevel=0.02, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "head_cage.obj"))

    # 10. Punk Mohawk Crest
    m = MeshBuilder()
    m.add_box(0, 0.21, 0, 0.40, 0.38, 0.40, bevel=0.06, mat=0)
    for idx_s, zoff in enumerate((-0.14, -0.04, 0.06, 0.16)):
        m.add_box(0, 0.48, zoff, 0.06, 0.18 - idx_s * 0.015, 0.07, bevel=0.01, mat=2)
    m.add_cylinder(-0.22, 0.22, 0, 0.09, 0.09, 0.05, segs=10, axis='x', mat=1)
    m.add_cylinder(0.22, 0.22, 0, 0.09, 0.09, 0.05, segs=10, axis='x', mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "head_mohawk.obj"))

    # 11. Arachnid Quad-Optic
    m = MeshBuilder()
    m.add_sphere(0, 0.22, 0, 0.24, 0.20, 0.23, u_segs=14, v_segs=10, mat=0)
    for ox, oy in ((-0.15, 0.33), (0.15, 0.33), (-0.19, 0.18), (0.19, 0.18)):
        m.add_cylinder(ox, oy, 0.18, 0.05, 0.05, 0.08, segs=8, axis='z', mat=2)
    m.add_box(0, 0.08, 0.15, 0.32, 0.10, 0.12, bevel=0.02, mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "head_arachnid.obj"))

    # 12. Overlord Dreadnought
    m = MeshBuilder()
    m.add_box(0, 0.24, 0, 0.50, 0.44, 0.44, bevel=0.06, taper_top=1.08, mat=0)
    m.add_box(0, 0.48, 0.08, 0.52, 0.09, 0.24, bevel=0.02, mat=1)  # heavy brow
    m.add_box(0, 0.08, 0.20, 0.42, 0.14, 0.12, bevel=0.02, mat=1)
    m.add_cylinder(-0.25, 0.34, 0.02, 0.04, 0.04, 0.14, segs=8, axis='y', mat=2)
    m.add_cylinder(0.25, 0.34, 0.02, 0.04, 0.04, 0.14, segs=8, axis='y', mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "head_overlord.obj"))

    # ==========================================================================
    # 12 TORSOS
    # ==========================================================================
    # 1. Scrap Barrel
    m = MeshBuilder()
    m.add_box(0, 0.42, 0, 0.78, 0.78, 0.52, bevel=0.10, taper_top=1.08, mat=0)
    m.add_cylinder(0, 0.45, 0.25, 0.15, 0.15, 0.06, segs=12, axis='z', bevel=0.015, mat=1)
    m.add_cylinder(0, 0.45, 0.27, 0.08, 0.08, 0.04, segs=12, axis='z', mat=2)
    m.add_box(0, 0.12, 0, 0.58, 0.24, 0.40, bevel=0.05, mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "torso_scrap.obj"))

    # 2. Aero V-Taper
    m = MeshBuilder()
    m.add_box(0, 0.46, 0, 0.66, 0.76, 0.44, bevel=0.08, taper_top=1.22, taper_front=0.92, mat=0)
    m.add_box(-0.22, 0.58, 0.19, 0.20, 0.22, 0.10, bevel=0.02, mat=1)
    m.add_box(0.22, 0.58, 0.19, 0.20, 0.22, 0.10, bevel=0.02, mat=1)
    m.add_box(0, 0.46, 0.23, 0.12, 0.30, 0.06, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "torso_aero.obj"))

    # 3. Anvil Heavy Plate
    m = MeshBuilder()
    m.add_box(0, 0.44, 0, 0.94, 0.80, 0.60, bevel=0.09, mat=0)
    m.add_box(0, 0.68, 0.08, 1.02, 0.28, 0.52, bevel=0.06, mat=1)
    m.add_box(0, 0.30, 0.29, 0.56, 0.24, 0.10, bevel=0.02, mat=0)
    m.add_box(0, 0.50, 0.31, 0.38, 0.07, 0.05, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "torso_anvil.obj"))

    # 4. Mantis Duelist
    m = MeshBuilder()
    m.add_box(0, 0.45, 0, 0.62, 0.78, 0.42, bevel=0.07, taper_top=1.18, mat=0)
    m.add_box(0, 0.58, 0.18, 0.44, 0.24, 0.14, bevel=0.03, taper_front=0.75, mat=1)
    m.add_box(-0.20, 0.44, 0.20, 0.06, 0.26, 0.05, bevel=0.01, mat=2)
    m.add_box(0.20, 0.44, 0.20, 0.06, 0.26, 0.05, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "torso_mantis.obj"))

    # 5. Furnace Boiler
    m = MeshBuilder()
    m.add_cylinder(0, 0.44, 0, 0.40, 0.44, 0.78, segs=12, axis='y', bevel=0.08, mat=0)
    m.add_box(0, 0.46, 0.34, 0.36, 0.32, 0.12, bevel=0.02, mat=2)
    m.add_cylinder(-0.28, 0.76, -0.18, 0.06, 0.06, 0.28, segs=8, axis='y', mat=1)
    m.add_cylinder(0.28, 0.76, -0.18, 0.06, 0.06, 0.28, segs=8, axis='y', mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "torso_furnace.obj"))

    # 6. Titan Aegis
    m = MeshBuilder()
    m.add_box(0, 0.46, 0, 0.88, 0.82, 0.56, bevel=0.09, taper_top=1.14, mat=0)
    m.add_box(-0.24, 0.56, 0.26, 0.26, 0.28, 0.12, bevel=0.03, mat=1)
    m.add_box(0.24, 0.56, 0.26, 0.26, 0.28, 0.12, bevel=0.03, mat=1)
    m.add_box(0, 0.76, -0.12, 0.72, 0.22, 0.26, bevel=0.04, mat=1)
    m.add_cylinder(0, 0.54, 0.28, 0.10, 0.10, 0.06, segs=10, axis='z', mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "torso_titan.obj"))

    # 7. Ribcage Exposed Pistons
    m = MeshBuilder()
    m.add_box(0, 0.44, 0, 0.66, 0.76, 0.46, bevel=0.07, mat=1)
    for ry in (0.30, 0.48, 0.66):
        m.add_box(0, ry, 0.06, 0.80, 0.09, 0.46, bevel=0.02, mat=0)
    m.add_cylinder(0, 0.48, 0.18, 0.11, 0.11, 0.12, segs=10, axis='z', mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "torso_ribcage.obj"))

    # 8. Bulldozer Scoop Chest
    m = MeshBuilder()
    m.add_box(0, 0.44, 0, 0.90, 0.78, 0.58, bevel=0.08, mat=0)
    m.add_box(0, 0.24, 0.24, 0.88, 0.26, 0.24, bevel=0.04, taper_front=0.85, mat=1)
    m.add_box(-0.34, 0.74, 0.22, 0.16, 0.10, 0.10, bevel=0.02, mat=2)
    m.add_box(0.34, 0.74, 0.22, 0.16, 0.10, 0.10, bevel=0.02, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "torso_dozer.obj"))

    # 9. Jet Turbine Chest
    m = MeshBuilder()
    m.add_box(0, 0.46, 0, 0.76, 0.76, 0.50, bevel=0.08, taper_top=1.18, mat=0)
    m.add_cylinder(-0.20, 0.54, 0.24, 0.14, 0.11, 0.14, segs=12, axis='z', mat=1)
    m.add_cylinder(0.20, 0.54, 0.24, 0.14, 0.11, 0.14, segs=12, axis='z', mat=1)
    m.add_cylinder(-0.20, 0.54, 0.27, 0.07, 0.07, 0.08, segs=10, axis='z', mat=2)
    m.add_cylinder(0.20, 0.54, 0.27, 0.07, 0.07, 0.08, segs=10, axis='z', mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "torso_turbine.obj"))

    # 10. Shogun Layered Armor
    m = MeshBuilder()
    m.add_box(0, 0.46, 0, 0.80, 0.78, 0.52, bevel=0.08, mat=0)
    m.add_box(0, 0.76, 0.02, 0.96, 0.16, 0.44, bevel=0.04, mat=1)
    m.add_box(0, 0.26, 0.02, 0.82, 0.18, 0.54, bevel=0.03, mat=0)
    m.add_box(0, 0.54, 0.27, 0.46, 0.14, 0.08, bevel=0.02, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "torso_shogun.obj"))

    # 11. Bank Vault Door Chest
    m = MeshBuilder()
    m.add_box(0, 0.45, 0, 0.96, 0.82, 0.62, bevel=0.06, mat=0)
    m.add_cylinder(0, 0.46, 0.31, 0.22, 0.22, 0.08, segs=14, axis='z', mat=1)
    m.add_box(0, 0.46, 0.35, 0.34, 0.06, 0.05, bevel=0.01, mat=2)
    m.add_box(0, 0.46, 0.35, 0.06, 0.34, 0.05, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "torso_vault.obj"))

    # 12. Quantum Exo-Frame
    m = MeshBuilder()
    m.add_box(0, 0.46, 0, 0.74, 0.80, 0.48, bevel=0.08, taper_top=1.24, mat=0)
    m.add_sphere(0, 0.52, 0.22, 0.14, 0.14, 0.08, u_segs=12, v_segs=8, mat=2)
    m.add_box(0, 0.78, -0.08, 0.86, 0.14, 0.30, bevel=0.03, mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "torso_exo.obj"))

    # ==========================================================================
    # SHARED ARTICULATED SEGMENTS
    # ==========================================================================
    m = MeshBuilder()
    m.add_box(0, 0.0, 0, 0.54, 0.26, 0.42, bevel=0.05, mat=1)
    m.add_box(0, 0.02, 0.18, 0.24, 0.12, 0.10, bevel=0.02, mat=0)
    m.add_box(0, 0.04, 0.22, 0.10, 0.06, 0.04, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "pelvis_hip.obj"))

    m = MeshBuilder()
    m.add_box(0, 0.0, 0, 0.28, 0.24, 0.28, bevel=0.05, mat=0)
    m.add_box(0, 0.10, 0, 0.22, 0.06, 0.22, bevel=0.02, mat=1)
    m.add_box(0, 0.0, 0.13, 0.14, 0.05, 0.04, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "shoulder_pad.obj"))

    m = MeshBuilder()
    m.add_cylinder(0, -0.22, 0, 0.10, 0.12, 0.44, segs=10, axis='y', bevel=0.03, mat=1)
    m.add_box(0, -0.22, 0, 0.24, 0.24, 0.24, bevel=0.04, mat=0)
    m.add_box(0, -0.22, 0.12, 0.08, 0.14, 0.04, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "upper_arm.obj"))

    # ==========================================================================
    # 12 BOXING GLOVES / FOREARMS
    # ==========================================================================
    # 1. Riveted Scrap
    m = MeshBuilder()
    m.add_cylinder(0, 0, 0.14, 0.11, 0.13, 0.28, segs=10, axis='z', bevel=0.03, mat=1)
    m.add_box(0, 0, 0.36, 0.30, 0.28, 0.28, bevel=0.07, mat=0)
    m.add_box(0, 0.10, 0.42, 0.22, 0.05, 0.08, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "glove_riveted.obj"))

    # 2. Shock Volt
    m = MeshBuilder()
    m.add_cylinder(0, 0, 0.14, 0.10, 0.12, 0.28, segs=10, axis='z', bevel=0.03, mat=1)
    m.add_box(0, 0, 0.36, 0.28, 0.26, 0.30, bevel=0.06, mat=0)
    m.add_box(0, 0.12, 0.38, 0.24, 0.08, 0.18, bevel=0.02, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "glove_shock.obj"))

    # 3. Piledriver Heavy Fist
    m = MeshBuilder()
    m.add_cylinder(0, 0, 0.14, 0.13, 0.16, 0.28, segs=12, axis='z', bevel=0.03, mat=1)
    m.add_box(0, 0, 0.40, 0.36, 0.34, 0.34, bevel=0.08, mat=0)
    m.add_box(0, 0, 0.56, 0.28, 0.26, 0.08, bevel=0.02, mat=1)
    m.add_box(0, 0.14, 0.40, 0.24, 0.05, 0.12, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "glove_piledriver.obj"))

    # 4. Mantis Speed Glove
    m = MeshBuilder()
    m.add_box(0, 0, 0.16, 0.18, 0.18, 0.32, bevel=0.04, mat=1)
    m.add_box(0, 0, 0.38, 0.24, 0.24, 0.30, bevel=0.05, taper_front=0.78, mat=0)
    m.add_box(0, 0.10, 0.40, 0.08, 0.06, 0.22, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "glove_mantis.obj"))

    # 5. Magma Piston
    m = MeshBuilder()
    m.add_cylinder(0, 0, 0.15, 0.12, 0.15, 0.30, segs=12, axis='z', bevel=0.03, mat=1)
    m.add_sphere(0, 0, 0.38, 0.18, 0.17, 0.19, u_segs=12, v_segs=8, mat=0)
    m.add_cylinder(0, 0.15, 0.22, 0.045, 0.045, 0.14, segs=8, axis='y', mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "glove_magma.obj"))

    # 6. Titan Mega Knuckle
    m = MeshBuilder()
    m.add_box(0, 0, 0.15, 0.24, 0.24, 0.30, bevel=0.05, mat=1)
    m.add_box(0, 0, 0.40, 0.35, 0.32, 0.32, bevel=0.08, mat=0)
    m.add_box(0, 0.14, 0.42, 0.30, 0.09, 0.18, bevel=0.02, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "glove_titan.obj"))

    # 7. Wrecking Ball Mace Fist
    m = MeshBuilder()
    m.add_cylinder(0, 0, 0.14, 0.09, 0.10, 0.28, segs=10, axis='z', mat=1)
    m.add_sphere(0, 0, 0.40, 0.21, 0.21, 0.21, u_segs=12, v_segs=8, mat=0)
    m.add_cylinder(0, 0, 0.40, 0.23, 0.23, 0.06, segs=12, axis='z', mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "glove_wreckball.obj"))

    # 8. Buzzguard Circular Cuff
    m = MeshBuilder()
    m.add_cylinder(0, 0, 0.18, 0.16, 0.16, 0.30, segs=12, axis='z', bevel=0.03, mat=1)
    m.add_box(0, 0, 0.38, 0.30, 0.30, 0.26, bevel=0.06, mat=0)
    m.add_cylinder(0, 0, 0.22, 0.18, 0.18, 0.05, segs=14, axis='z', mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "glove_buzzguard.obj"))

    # 9. Jackhammer Pneumatic Fist
    m = MeshBuilder()
    m.add_box(0, 0, 0.16, 0.22, 0.22, 0.32, bevel=0.04, mat=1)
    m.add_box(0, 0, 0.40, 0.32, 0.30, 0.28, bevel=0.05, mat=0)
    m.add_cylinder(0, 0, 0.54, 0.09, 0.09, 0.14, segs=10, axis='z', mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "glove_jackhammer.obj"))

    # 10. Plasma Claw Gauntlet
    m = MeshBuilder()
    m.add_box(0, 0, 0.16, 0.20, 0.20, 0.32, bevel=0.04, mat=0)
    m.add_box(0, 0, 0.38, 0.28, 0.24, 0.26, bevel=0.05, mat=1)
    for kx in (-0.09, 0.0, 0.09):
        m.add_box(kx, 0.04, 0.54, 0.04, 0.14, 0.14, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "glove_plasmaclaw.obj"))

    # 11. Riot Shield Buckler Glove
    m = MeshBuilder()
    m.add_cylinder(0, 0, 0.14, 0.12, 0.14, 0.28, segs=10, axis='z', mat=1)
    m.add_box(0, 0, 0.36, 0.32, 0.30, 0.28, bevel=0.06, mat=0)
    m.add_box(-0.14, 0, 0.24, 0.08, 0.44, 0.42, bevel=0.02, mat=0)  # forearm shield plate
    m.add_box(-0.18, 0, 0.24, 0.03, 0.30, 0.26, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "glove_shield.obj"))

    # 12. Gravity Singularity Fist
    m = MeshBuilder()
    m.add_box(0, 0, 0.16, 0.22, 0.22, 0.30, bevel=0.05, mat=1)
    m.add_sphere(0, 0, 0.40, 0.19, 0.19, 0.19, u_segs=14, v_segs=10, mat=2)
    m.add_box(0, 0, 0.40, 0.38, 0.12, 0.26, bevel=0.03, mat=0)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "glove_gravity.obj"))

    # ==========================================================================
    # 12 LEGS
    # ==========================================================================
    # 1. Spring Coil
    m = MeshBuilder()
    m.add_cylinder(0, -0.22, 0, 0.12, 0.10, 0.42, segs=10, axis='y', bevel=0.02, mat=1)
    m.add_cylinder(0, -0.56, 0, 0.14, 0.11, 0.36, segs=10, axis='y', bevel=0.03, mat=0)
    m.add_box(0, -0.80, 0.06, 0.26, 0.14, 0.38, bevel=0.03, mat=1)
    m.add_box(0, -0.56, 0.13, 0.10, 0.14, 0.04, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "leg_spring.obj"))

    # 2. Hover Thruster
    m = MeshBuilder()
    m.add_box(0, -0.24, 0, 0.22, 0.42, 0.24, bevel=0.05, taper_top=1.1, mat=0)
    m.add_cylinder(0, -0.62, -0.02, 0.15, 0.10, 0.36, segs=12, axis='y', bevel=0.03, mat=1)
    m.add_cylinder(0, -0.78, -0.02, 0.12, 0.14, 0.08, segs=12, axis='y', mat=2)
    m.add_box(0, -0.81, 0.07, 0.24, 0.12, 0.36, bevel=0.03, mat=0)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "leg_hover.obj"))

    # 3. Heavy Tread Boot
    m = MeshBuilder()
    m.add_box(0, -0.22, 0, 0.28, 0.42, 0.28, bevel=0.05, mat=0)
    m.add_box(0, -0.58, 0, 0.30, 0.36, 0.32, bevel=0.05, mat=0)
    m.add_box(0, -0.80, 0.08, 0.34, 0.16, 0.46, bevel=0.04, mat=1)
    m.add_box(0, -0.44, 0.16, 0.16, 0.12, 0.06, bevel=0.02, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "leg_tread.obj"))

    # 4. Agile Strider
    m = MeshBuilder()
    m.add_box(0, -0.24, 0, 0.20, 0.44, 0.22, bevel=0.04, mat=0)
    m.add_box(0, -0.58, -0.03, 0.18, 0.38, 0.20, bevel=0.04, mat=1)
    m.add_box(0, -0.81, 0.08, 0.22, 0.12, 0.38, bevel=0.03, taper_front=0.75, mat=0)
    m.add_box(0, -0.40, 0.11, 0.08, 0.26, 0.04, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "leg_strider.obj"))

    # 5. Hydraulic Stomper
    m = MeshBuilder()
    m.add_cylinder(0, -0.22, 0, 0.14, 0.14, 0.42, segs=12, axis='y', bevel=0.03, mat=1)
    m.add_box(0, -0.56, 0, 0.28, 0.38, 0.28, bevel=0.05, mat=0)
    m.add_box(0, -0.80, 0.07, 0.32, 0.16, 0.42, bevel=0.04, mat=1)
    m.add_cylinder(0, -0.56, 0.14, 0.05, 0.05, 0.20, segs=8, axis='y', mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "leg_hydraulic.obj"))

    # 6. Apex Greave
    m = MeshBuilder()
    m.add_box(0, -0.22, 0, 0.26, 0.44, 0.26, bevel=0.05, mat=0)
    m.add_box(0, -0.56, 0.02, 0.28, 0.38, 0.30, bevel=0.05, mat=0)
    m.add_box(0, -0.44, 0.15, 0.18, 0.18, 0.10, bevel=0.02, mat=2)
    m.add_box(0, -0.80, 0.08, 0.30, 0.15, 0.42, bevel=0.04, mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "leg_apex.obj"))

    # 7. Wheel Skate Boots
    m = MeshBuilder()
    m.add_box(0, -0.24, 0, 0.22, 0.42, 0.24, bevel=0.04, mat=0)
    m.add_box(0, -0.56, 0, 0.24, 0.34, 0.26, bevel=0.04, mat=0)
    m.add_cylinder(0, -0.78, -0.06, 0.14, 0.14, 0.18, segs=12, axis='x', mat=1)
    m.add_cylinder(0, -0.78, 0.16, 0.12, 0.12, 0.18, segs=12, axis='x', mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "leg_wheels.obj"))

    # 8. Reverse-Joint Mech Legs
    m = MeshBuilder()
    m.add_box(0, -0.20, 0.06, 0.22, 0.38, 0.24, bevel=0.04, mat=0)
    m.add_box(0, -0.52, -0.08, 0.18, 0.40, 0.22, bevel=0.04, mat=1)
    m.add_box(0, -0.80, 0.08, 0.26, 0.14, 0.42, bevel=0.03, mat=0)
    m.add_box(0, -0.44, -0.18, 0.08, 0.16, 0.06, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "leg_reverse.obj"))

    # 9. Rocket Jet Boots
    m = MeshBuilder()
    m.add_box(0, -0.24, 0, 0.24, 0.42, 0.24, bevel=0.04, mat=0)
    m.add_box(0, -0.58, 0, 0.28, 0.36, 0.30, bevel=0.05, mat=0)
    m.add_cylinder(0, -0.62, -0.18, 0.09, 0.06, 0.26, segs=10, axis='y', mat=1)
    m.add_cylinder(0, -0.76, -0.18, 0.08, 0.08, 0.06, segs=10, axis='y', mat=2)
    m.add_box(0, -0.80, 0.08, 0.28, 0.14, 0.40, bevel=0.03, mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "leg_jetboot.obj"))

    # 10. Sumo Heavy Pillars
    m = MeshBuilder()
    m.add_cylinder(0, -0.24, 0, 0.18, 0.17, 0.44, segs=12, axis='y', bevel=0.03, mat=0)
    m.add_cylinder(0, -0.58, 0, 0.20, 0.18, 0.36, segs=12, axis='y', bevel=0.03, mat=0)
    m.add_box(0, -0.80, 0.06, 0.38, 0.18, 0.46, bevel=0.04, mat=1)
    m.add_box(0, -0.42, 0.17, 0.20, 0.12, 0.06, bevel=0.02, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "leg_sumo.obj"))

    # 11. Shogun Hakama Greaves
    m = MeshBuilder()
    m.add_box(0, -0.24, 0, 0.24, 0.44, 0.24, bevel=0.04, mat=1)
    m.add_box(0, -0.56, 0, 0.34, 0.40, 0.34, bevel=0.05, taper_top=0.80, mat=0)
    m.add_box(0, -0.81, 0.08, 0.28, 0.14, 0.40, bevel=0.03, mat=1)
    m.add_box(0, -0.56, 0.18, 0.12, 0.22, 0.04, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "leg_hakama.obj"))

    # 12. Quantum Strut Greaves
    m = MeshBuilder()
    m.add_box(0, -0.24, 0, 0.24, 0.42, 0.24, bevel=0.05, mat=0)
    m.add_box(0, -0.56, 0, 0.26, 0.38, 0.28, bevel=0.05, mat=0)
    m.add_box(-0.15, -0.52, -0.06, 0.05, 0.30, 0.18, bevel=0.01, mat=2)
    m.add_box(0.15, -0.52, -0.06, 0.05, 0.30, 0.18, bevel=0.01, mat=2)
    m.add_box(0, -0.80, 0.08, 0.28, 0.14, 0.42, bevel=0.03, mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "leg_quantum.obj"))

    # ==========================================================================
    # 12 BACK POWER CORES
    # ==========================================================================
    # 1. Lead Battery
    m = MeshBuilder()
    m.add_box(0, 0, -0.10, 0.34, 0.36, 0.20, bevel=0.04, mat=0)
    m.add_box(0, 0.12, -0.20, 0.24, 0.08, 0.05, bevel=0.01, mat=2)
    m.add_box(0, -0.10, -0.18, 0.28, 0.12, 0.08, bevel=0.02, mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "core_battery.obj"))

    # 2. Twin Volt Coils
    m = MeshBuilder()
    m.add_cylinder(-0.12, 0, -0.12, 0.08, 0.08, 0.38, segs=10, axis='y', bevel=0.02, mat=1)
    m.add_cylinder(0.12, 0, -0.12, 0.08, 0.08, 0.38, segs=10, axis='y', bevel=0.02, mat=1)
    m.add_box(0, 0, -0.06, 0.28, 0.24, 0.12, bevel=0.02, mat=0)
    m.add_cylinder(-0.12, 0.18, -0.12, 0.09, 0.09, 0.06, segs=10, axis='y', mat=2)
    m.add_cylinder(0.12, 0.18, -0.12, 0.09, 0.09, 0.06, segs=10, axis='y', mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "core_volt.obj"))

    # 3. Kinetic Flywheel
    m = MeshBuilder()
    m.add_cylinder(0, 0, -0.12, 0.22, 0.22, 0.18, segs=14, axis='z', bevel=0.04, mat=0)
    m.add_cylinder(0, 0, -0.18, 0.14, 0.14, 0.10, segs=12, axis='z', mat=1)
    m.add_cylinder(0, 0, -0.22, 0.07, 0.07, 0.06, segs=10, axis='z', mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "core_flywheel.obj"))

    # 4. Plasma Heart
    m = MeshBuilder()
    m.add_sphere(0, 0, -0.14, 0.18, 0.18, 0.16, u_segs=12, v_segs=8, mat=2)
    m.add_box(0, 0, -0.06, 0.38, 0.30, 0.12, bevel=0.03, mat=0)
    m.add_box(0, 0, -0.14, 0.42, 0.08, 0.12, bevel=0.02, mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "core_plasma.obj"))

    # 5. Fusion Stack
    m = MeshBuilder()
    m.add_box(0, 0, -0.12, 0.42, 0.40, 0.24, bevel=0.05, mat=0)
    m.add_cylinder(-0.15, 0.24, -0.14, 0.06, 0.05, 0.24, segs=8, axis='y', mat=1)
    m.add_cylinder(0.15, 0.24, -0.14, 0.06, 0.05, 0.24, segs=8, axis='y', mat=1)
    m.add_box(0, 0, -0.24, 0.24, 0.20, 0.05, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "core_fusion.obj"))

    # 6. Quantum Wings
    m = MeshBuilder()
    m.add_box(0, 0, -0.12, 0.38, 0.42, 0.22, bevel=0.05, mat=0)
    m.add_box(-0.30, 0.10, -0.14, 0.22, 0.48, 0.08, bevel=0.02, mat=2)
    m.add_box(0.30, 0.10, -0.14, 0.22, 0.48, 0.08, bevel=0.02, mat=2)
    m.add_box(0, 0, -0.22, 0.20, 0.26, 0.06, bevel=0.02, mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "core_quantum.obj"))

    # 7. V8 Twin Exhaust Pipes
    m = MeshBuilder()
    m.add_box(0, 0, -0.10, 0.40, 0.34, 0.20, bevel=0.04, mat=1)
    for sx in (-0.18, 0.18):
        m.add_cylinder(sx, 0.22, -0.16, 0.05, 0.04, 0.36, segs=8, axis='y', mat=1)
        m.add_cylinder(sx, 0.38, -0.16, 0.06, 0.06, 0.06, segs=8, axis='y', mat=2)
    m.add_box(0, 0.04, -0.20, 0.28, 0.24, 0.08, bevel=0.02, mat=0)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "core_v8pipes.obj"))

    # 8. Twin Jet Thruster Pack
    m = MeshBuilder()
    m.add_box(0, 0.04, -0.12, 0.48, 0.36, 0.22, bevel=0.05, mat=0)
    for sx in (-0.16, 0.16):
        m.add_cylinder(sx, -0.14, -0.18, 0.11, 0.08, 0.24, segs=10, axis='y', mat=1)
        m.add_cylinder(sx, -0.26, -0.18, 0.09, 0.09, 0.06, segs=10, axis='y', mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "core_jetpack.obj"))

    # 9. Tesla Arc Tower
    m = MeshBuilder()
    m.add_box(0, -0.02, -0.10, 0.34, 0.34, 0.20, bevel=0.04, mat=0)
    m.add_cylinder(0, 0.26, -0.14, 0.05, 0.04, 0.42, segs=10, axis='y', mat=1)
    m.add_sphere(0, 0.48, -0.14, 0.12, 0.12, 0.12, u_segs=10, v_segs=8, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "core_teslatower.obj"))

    # 10. Halo Ring Generator
    m = MeshBuilder()
    m.add_box(0, 0, -0.08, 0.34, 0.32, 0.16, bevel=0.04, mat=0)
    for i in range(8):
        ang = i * math.pi / 4.0
        px = math.cos(ang) * 0.34
        py = math.sin(ang) * 0.34 + 0.10
        m.add_box(px, py, -0.18, 0.10, 0.10, 0.08, bevel=0.02, mat=2 if i % 2 == 0 else 1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "core_halo.obj"))

    # 11. Twin Shoulder Missile Pods
    m = MeshBuilder()
    m.add_box(0, 0, -0.08, 0.42, 0.28, 0.16, bevel=0.03, mat=1)
    m.add_box(-0.34, 0.26, -0.06, 0.24, 0.24, 0.26, bevel=0.03, mat=0)
    m.add_box(0.34, 0.26, -0.06, 0.24, 0.24, 0.26, bevel=0.03, mat=0)
    m.add_box(-0.34, 0.26, 0.08, 0.16, 0.16, 0.04, bevel=0.01, mat=2)
    m.add_box(0.34, 0.26, 0.08, 0.16, 0.16, 0.04, bevel=0.01, mat=2)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "core_pods.obj"))

    # 12. Apex Angel Wing Pack
    m = MeshBuilder()
    m.add_box(0, 0.02, -0.10, 0.36, 0.38, 0.20, bevel=0.04, mat=0)
    m.add_box(-0.38, 0.26, -0.16, 0.40, 0.56, 0.06, bevel=0.02, taper_top=1.18, mat=0)
    m.add_box(0.38, 0.26, -0.16, 0.40, 0.56, 0.06, bevel=0.02, taper_top=1.18, mat=0)
    m.add_box(-0.36, 0.26, -0.20, 0.26, 0.40, 0.04, bevel=0.01, mat=2)
    m.add_box(0.36, 0.26, -0.20, 0.26, 0.40, 0.04, bevel=0.01, mat=2)
    m.add_box(0, 0.02, -0.21, 0.18, 0.24, 0.06, bevel=0.01, mat=1)
    m.write_obj(os.path.join(MODELS_PARTS_DIR, "core_angelwings.obj"))

    # ==========================================================================
    # ARENA & HANGAR STUDIO ARCHITECTURE
    # ==========================================================================
    m = MeshBuilder()
    m.add_box(0, -0.20, 0, 10.0, 0.40, 10.0, bevel=0.08)
    m.add_box(0, -0.55, 0, 9.2, 0.30, 9.2, bevel=0.04)
    m.write_obj(os.path.join(MODELS_ARENA_DIR, "ring_platform.obj"), multi_material=False)

    m = MeshBuilder()
    m.add_cylinder(0, 0.85, 0, 0.18, 0.16, 1.70, segs=12, axis='y', bevel=0.03)
    m.add_box(0, 0.90, 0, 0.34, 1.30, 0.34, bevel=0.06)
    m.write_obj(os.path.join(MODELS_ARENA_DIR, "ring_post.obj"), multi_material=False)

    m = MeshBuilder()
    m.add_cylinder(0, 0, 0, 0.045, 0.045, 9.0, segs=8, axis='x')
    m.write_obj(os.path.join(MODELS_ARENA_DIR, "ring_rope.obj"), multi_material=False)

    m = MeshBuilder()
    m.add_box(0, 0, 0, 11.0, 0.35, 0.35, bevel=0.04)
    m.write_obj(os.path.join(MODELS_ARENA_DIR, "arena_truss.obj"), multi_material=False)

    m = MeshBuilder()
    m.add_cylinder(0, -0.10, 0, 1.65, 1.55, 0.20, segs=24, axis='y', bevel=0.04)
    m.add_cylinder(0, -0.28, 0, 1.90, 1.75, 0.16, segs=24, axis='y', bevel=0.03)
    m.write_obj(os.path.join(MODELS_ARENA_DIR, "garage_turntable.obj"), multi_material=False)

    # Stadium Pillar Tower
    m = MeshBuilder()
    m.add_box(0, 2.5, 0, 0.65, 6.0, 0.65, bevel=0.05)
    m.add_box(0, 5.4, 0, 1.25, 0.55, 0.75, bevel=0.04)
    m.write_obj(os.path.join(MODELS_ARENA_DIR, "arena_pillar.obj"), multi_material=False)

    # Perimeter LED Billboard
    m = MeshBuilder()
    m.add_box(0, 0, 0, 6.5, 0.9, 0.22, bevel=0.03)
    m.write_obj(os.path.join(MODELS_ARENA_DIR, "arena_billboard.obj"), multi_material=False)

    # Industrial Hangar Arch Rib
    m = MeshBuilder()
    m.add_box(-3.6, 2.2, 0, 0.55, 4.8, 0.65, bevel=0.04)
    m.add_box(3.6, 2.2, 0, 0.55, 4.8, 0.65, bevel=0.04)
    m.add_box(0, 4.6, 0, 7.8, 0.55, 0.65, bevel=0.04)
    m.write_obj(os.path.join(MODELS_ARENA_DIR, "garage_arch.obj"), multi_material=False)

    # Volumetric Spotlight Cone
    m = MeshBuilder()
    m.add_cylinder(0, 0, 0, 2.2, 0.28, 5.2, segs=16, axis='y')
    m.write_obj(os.path.join(MODELS_ARENA_DIR, "light_cone.obj"), multi_material=False)


# ==============================================================================
# 2. TEXTURES, NORMAL MAPS & SURFACE FINISHES (PIL)
# ==============================================================================
def heightmap_to_normal(height_img, strength=2.6):
    w, h = height_img.size
    pix = height_img.load()
    norm = Image.new("RGB", (w, h), (128, 128, 255))
    npix = norm.load()
    for y in range(h):
        ym = (y - 1) % h
        yp = (y + 1) % h
        for x in range(w):
            xm = (x - 1) % w
            xp = (x + 1) % w
            dx = (pix[xp, y] - pix[xm, y]) / 255.0 * strength
            dy = (pix[x, yp] - pix[x, ym]) / 255.0 * strength
            nx, ny, nz = -dx, -dy, 1.0
            ln = math.sqrt(nx * nx + ny * ny + nz * nz)
            npix[x, y] = (
                int((nx / ln * 0.5 + 0.5) * 255),
                int((ny / ln * 0.5 + 0.5) * 255),
                int((nz / ln * 0.5 + 0.5) * 255),
            )
    return norm


def make_textures():
    w, h = 512, 512
    # 1. Armor Albedo + Height -> Normal Map
    albedo = Image.new("RGBA", (w, h), (235, 239, 246, 255))
    height = Image.new("L", (w, h), 140)
    ad = ImageDraw.Draw(albedo)
    hd = ImageDraw.Draw(height)

    for y in range(0, h, 128):
        ad.line([(0, y), (w, y)], fill=(170, 180, 196, 255), width=4)
        ad.line([(0, y + 4), (w, y + 4)], fill=(252, 253, 255, 255), width=2)
        hd.line([(0, y), (w, y)], fill=40, width=5)
    for x in range(0, w, 128):
        ad.line([(x, 0), (x, h)], fill=(170, 180, 196, 255), width=4)
        ad.line([(x + 4, 0), (x + 4, h)], fill=(252, 253, 255, 255), width=2)
        hd.line([(x, 0), (x, h)], fill=40, width=5)

    for y in range(0, h, 128):
        for x in range(0, w, 128):
            # Subtle vent louvers on alternate panels
            if ((x // 128) + (y // 128)) % 2 == 0:
                for vy in range(42, 86, 12):
                    ad.rectangle([x + 36, y + vy, x + 92, y + vy + 4], fill=(185, 194, 210, 255))
                    hd.rectangle([x + 36, y + vy, x + 92, y + vy + 4], fill=70)
            for rx, ry in [(14, 14), (114, 14), (14, 114), (114, 114)]:
                ad.ellipse([x + rx - 5, y + ry - 5, x + rx + 5, y + ry + 5], fill=(165, 174, 190, 255))
                ad.ellipse([x + rx - 3, y + ry - 3, x + rx + 2, y + ry + 2], fill=(250, 252, 255, 255))
                hd.ellipse([x + rx - 4, y + ry - 4, x + rx + 4, y + ry + 4], fill=230)

    albedo.save(os.path.join(TEXTURES_DIR, "armor_stylized_albedo.png"))
    norm = heightmap_to_normal(height, strength=2.4)
    norm.save(os.path.join(TEXTURES_DIR, "armor_stylized_normal.png"))

    # 2. Brushed Chrome Finish
    chrome = Image.new("RGBA", (w, h), (242, 247, 255, 255))
    cd = ImageDraw.Draw(chrome)
    random.seed(7)
    for y in range(0, h, 2):
        v = random.randint(218, 255)
        cd.line([(0, y), (w, y)], fill=(v, min(255, v + 3), 255, 255), width=2)
    for x in range(0, w, 128):
        cd.line([(x, 0), (x, h)], fill=(180, 195, 215, 255), width=3)
    chrome.save(os.path.join(TEXTURES_DIR, "finish_chrome.png"))

    # 3. Carbon Fiber Finish
    carbon = Image.new("RGBA", (w, h), (62, 68, 78, 255))
    cbd = ImageDraw.Draw(carbon)
    step = 16
    for y in range(0, h, step):
        for x in range(0, w, step):
            if ((x // step) + (y // step)) % 2 == 0:
                cbd.rectangle([x, y, x + step - 2, y + step - 2], fill=(95, 102, 116, 255))
            else:
                cbd.rectangle([x, y, x + step - 2, y + step - 2], fill=(48, 52, 62, 255))
    carbon.save(os.path.join(TEXTURES_DIR, "finish_carbon.png"))

    # 4. Industrial Hazard Finish
    haz = Image.new("RGBA", (w, h), (228, 228, 232, 255))
    hzd = ImageDraw.Draw(haz)
    for i in range(-h, w + h, 64):
        pts = [(i, 0), (i + 32, 0), (i + 32 - h, h), (i - h, h)]
        hzd.polygon(pts, fill=(92, 96, 106, 255))
    haz.save(os.path.join(TEXTURES_DIR, "finish_hazard.png"))

    # 5. Cyber Neon Circuit Finish
    circ = Image.new("RGBA", (w, h), (155, 165, 185, 255))
    cid = ImageDraw.Draw(circ)
    for y in range(32, h, 64):
        cid.line([(0, y), (w, y)], fill=(250, 252, 255, 255), width=4)
        for x in range(32, w, 64):
            cid.line([(x, y), (x + 24, y - 24), (x + 48, y - 24)], fill=(255, 255, 255, 255), width=3)
            cid.ellipse([x - 6, y - 6, x + 6, y + 6], fill=(255, 255, 255, 255))
    circ.save(os.path.join(TEXTURES_DIR, "finish_circuit.png"))

    # 6. Hazard Stripes (arena posts)
    img = Image.new("RGBA", (256, 256), (35, 38, 45, 255))
    d = ImageDraw.Draw(img)
    for i in range(-256, 512, 64):
        d.polygon([(i, 0), (i + 32, 0), (i + 32 - 256, 256), (i - 256, 256)], fill=(245, 185, 35, 255))
    img.save(os.path.join(TEXTURES_DIR, "hazard_stripes.png"))

    # 7. Garage Floor Grid
    gf = Image.new("RGBA", (1024, 1024), (24, 30, 44, 255))
    gfd = ImageDraw.Draw(gf)
    for g in range(0, 1024, 64):
        gfd.line([(g, 0), (g, 1024)], fill=(38, 52, 78, 255), width=2)
        gfd.line([(0, g), (1024, g)], fill=(38, 52, 78, 255), width=2)
    gfd.ellipse([262, 262, 762, 762], outline=(52, 185, 240, 255), width=10)
    gfd.ellipse([320, 320, 704, 704], outline=(245, 175, 40, 255), width=6)
    gf.save(os.path.join(TEXTURES_DIR, "garage_floor_grid.png"))

    # 8. Upgraded Ring Canvas
    rc = Image.new("RGBA", (1024, 1024), (30, 48, 78, 255))
    rcd = ImageDraw.Draw(rc)
    rcd.rectangle([28, 28, 996, 996], outline=(238, 244, 255, 255), width=14)
    rcd.rectangle([64, 64, 960, 960], outline=(82, 185, 245, 255), width=6)
    rcd.polygon([(28, 28), (200, 28), (28, 200)], fill=(220, 52, 52, 255))
    rcd.polygon([(996, 996), (824, 996), (996, 824)], fill=(42, 132, 235, 255))
    cx, cy = 512, 512
    rcd.ellipse([cx - 220, cy - 220, cx + 220, cy + 220], fill=(22, 36, 62, 255), outline=(245, 185, 45, 255), width=14)
    rcd.ellipse([cx - 178, cy - 178, cx + 178, cy + 178], outline=(75, 205, 255, 255), width=6)
    rcd.rounded_rectangle([cx - 65, cy - 55, cx + 65, cy + 55], radius=24, fill=(245, 185, 45, 255), outline=(20, 24, 34, 255), width=6)
    for kx in (-42, -14, 14, 42):
        rcd.rounded_rectangle([cx + kx - 11, cy - 75, cx + kx + 11, cy - 35], radius=6, fill=(255, 215, 90, 255), outline=(20, 24, 34, 255), width=4)
    rc.save(os.path.join(TEXTURES_DIR, "ring_canvas.png"))

    # 9. Visor Atlas
    va = Image.new("RGBA", (512, 768), (10, 14, 24, 255))
    vad = ImageDraw.Draw(va)
    cw, ch = 256, 256
    # 0 Neutral
    ox, oy = 0, 0
    vad.rounded_rectangle([ox + 42, oy + 82, ox + 106, oy + 162], radius=16, fill=(255, 255, 255, 255))
    vad.rounded_rectangle([ox + 150, oy + 82, ox + 214, oy + 162], radius=16, fill=(255, 255, 255, 255))
    # 1 Angry
    ox, oy = cw, 0
    vad.polygon([(ox + 36, oy + 72), (ox + 112, oy + 106), (ox + 102, oy + 158), (ox + 36, oy + 136)], fill=(255, 255, 255, 255))
    vad.polygon([(ox + 220, oy + 72), (ox + 144, oy + 106), (ox + 154, oy + 158), (ox + 220, oy + 136)], fill=(255, 255, 255, 255))
    # 2 Hit
    ox, oy = 0, ch
    vad.line([(ox + 42, oy + 85), (ox + 105, oy + 128), (ox + 42, oy + 170)], fill=(255, 255, 255, 255), width=20)
    vad.line([(ox + 214, oy + 85), (ox + 151, oy + 128), (ox + 214, oy + 170)], fill=(255, 255, 255, 255), width=20)
    # 3 Stunned
    ox, oy = cw, ch
    vad.ellipse([ox + 38, oy + 86, ox + 114, oy + 162], outline=(255, 255, 255, 255), width=16)
    vad.ellipse([ox + 142, oy + 74, ox + 226, oy + 158], outline=(255, 255, 255, 255), width=16)
    vad.ellipse([ox + 174, oy + 106, ox + 194, oy + 126], fill=(255, 255, 255, 255))
    # 4 KO
    ox, oy = 0, ch * 2
    vad.line([(ox + 42, oy + 86), (ox + 106, oy + 162)], fill=(255, 255, 255, 255), width=18)
    vad.line([(ox + 106, oy + 86), (ox + 42, oy + 162)], fill=(255, 255, 255, 255), width=18)
    vad.line([(ox + 150, oy + 86), (ox + 214, oy + 162)], fill=(255, 255, 255, 255), width=18)
    vad.line([(ox + 214, oy + 86), (ox + 150, oy + 162)], fill=(255, 255, 255, 255), width=18)
    # 5 Victory
    ox, oy = cw, ch * 2
    vad.line([(ox + 38, oy + 146), (ox + 74, oy + 92), (ox + 110, oy + 146)], fill=(255, 255, 255, 255), width=20)
    vad.line([(ox + 146, oy + 146), (ox + 182, oy + 92), (ox + 218, oy + 146)], fill=(255, 255, 255, 255), width=20)
    va.save(os.path.join(TEXTURES_DIR, "visor_faces_atlas.png"))


if __name__ == "__main__":
    print("Generating 64+ multi-material 3D models & studio props...")
    build_all_models()
    print("Generating stylized normal maps, surface finishes & arena textures...")
    make_textures()
    print("Done! Upgraded assets generated cleanly.")
