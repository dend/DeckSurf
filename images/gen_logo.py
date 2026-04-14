#!/usr/bin/env python3
"""Generate low-poly isometric wave SVG for DeckSurf logo.

Three wave fins modeled as 3D extruded curves, rendered with
isometric projection and deterministic face draw order.
All faces rendered (no backface culling) — painter's algorithm
ensures back faces only peek out where they extend beyond front.
"""

import math

CANVAS = 128

def project(x, y, z):
    a = math.radians(30)
    return ((x - z) * math.cos(a), (x + z) * math.sin(a) - y)

def area2d(pts):
    a = 0
    for i in range(len(pts)):
        j = (i+1) % len(pts)
        a += pts[i][0]*pts[j][1] - pts[j][0]*pts[i][1]
    return a/2

def shade(rgb, intensity):
    return "#{:02x}{:02x}{:02x}".format(
        min(255, int(rgb[0]*intensity)),
        min(255, int(rgb[1]*intensity)),
        min(255, int(rgb[2]*intensity))
    )

# Face type brightness — consistent directional light from upper-right
FACE_BRIGHT = {
    'front':    1.0,    # Main visible face, fully lit
    'outer':    0.72,   # Right side, medium
    'top_cap':  0.88,   # Top cap, well lit
    'inner':    0.52,   # Left side, shadow (peeks out on left)
    'back':     0.42,   # Back face, deep shadow (peeks out behind)
    'bot_cap':  0.35,   # Bottom cap, darkest
}

# Draw order: lower = drawn first = behind. Higher = drawn last = in front.
FACE_ORDER = {
    'back':     0,
    'bot_cap':  1,
    'inner':    2,
    'outer':    3,
    'top_cap':  4,
    'front':    5,
}

def wave_profile(n_seg=6, scale=1.0):
    outer, inner = [], []
    for i in range(n_seg + 1):
        t = i / n_seg
        y = t * 52 * scale
        lean = math.sin(t * math.pi * 0.82) * (1 - 0.1 * t)
        ox = 18 * lean * scale
        w = 12 * (1 - 0.55 * t) * scale
        ix = ox - w
        if t > 0.82:
            b = (t - 0.82) / 0.18
            m = (ox + ix) / 2
            min_hw = 2.5
            ox += (m + min_hw - ox) * b * 0.6
            ix += (m - min_hw - ix) * b * 0.6
        outer.append((ox, y))
        inner.append((ix, y))
    return outer, inner

def extrude(outer, inner, z, depth, color):
    """Extrude profile. Each face: (verts_3d, color, face_type, wave_z, seg_idx)"""
    faces = []
    n = len(outer)
    z0, z1 = z, z + depth

    for i in range(n - 1):
        ox0, oy0 = outer[i]; ox1, oy1 = outer[i+1]
        ix0, iy0 = inner[i]; ix1, iy1 = inner[i+1]

        # Front face (at z0)
        faces.append(([(ix0,iy0,z0),(ix1,iy1,z0),(ox1,oy1,z0),(ox0,oy0,z0)], color, 'front', z, i))
        # Back face (at z1) — reversed winding so it renders visible
        faces.append(([(ox0,oy0,z1),(ox1,oy1,z1),(ix1,iy1,z1),(ix0,iy0,z1)], color, 'back', z, i))
        # Outer side
        faces.append(([(ox0,oy0,z0),(ox1,oy1,z0),(ox1,oy1,z1),(ox0,oy0,z1)], color, 'outer', z, i))
        # Inner side — reversed winding so it renders visible
        faces.append(([(ix0,iy0,z0),(ix0,iy0,z1),(ix1,iy1,z1),(ix1,iy1,z0)], color, 'inner', z, i))

    ot = outer[-1]; it = inner[-1]
    faces.append(([(ot[0],ot[1],z0),(ot[0],ot[1],z1),(it[0],it[1],z1),(it[0],it[1],z0)], color, 'top_cap', z, n))
    ob = outer[0]; ib = inner[0]
    faces.append(([(ob[0],ob[1],z1),(ob[0],ob[1],z0),(ib[0],ib[1],z0),(ib[0],ib[1],z1)], color, 'bot_cap', z, -1))

    return faces

# ---- SCENE ----

wave_defs = [
    (24,  8, 0.65, (30, 80, 180)),     # Back wave — dark blue (leftmost)
    (12,  8, 0.82, (50, 150, 230)),    # Middle wave — medium blue
    (0,   10, 1.0, (0, 220, 250)),     # Front wave — bright cyan (rightmost)
]

all_faces = []
for z, depth, scale, color in wave_defs:
    o, i = wave_profile(6, scale)
    all_faces.extend(extrude(o, i, z, depth, color))

# ---- RENDER ----

rendered = []
for verts, color, face_type, wave_z, seg_idx in all_faces:
    proj = [project(*v) for v in verts]
    a = area2d(proj)
    if abs(a) < 0.01:  # skip degenerate
        continue
    # Fix winding for SVG if needed (ensure consistent fill)
    if a > 0:
        proj = list(reversed(proj))

    c = shade(color, FACE_BRIGHT[face_type])
    # Sort by actual isometric depth: faces further from camera drawn first
    # Camera looks from roughly (1, 1, -1) direction, so depth = x + z
    avg_depth = sum(v[0] + v[2] for v in verts) / len(verts)
    # Within same depth, draw back/inner before front/outer
    sort_key = (round(avg_depth, 1), FACE_ORDER[face_type])
    rendered.append((proj, c, sort_key))

rendered.sort(key=lambda f: f[2])

# ---- VIEWPORT ----

all_pts = [p for f in rendered for p in f[0]]
min_x = min(p[0] for p in all_pts)
max_x = max(p[0] for p in all_pts)
min_y = min(p[1] for p in all_pts)
max_y = max(p[1] for p in all_pts)

w, h = max_x - min_x, max_y - min_y
s = min(CANVAS / w, CANVAS / h) * 1.35

mid_x = (min_x + max_x) / 2
mid_y = (min_y + max_y) / 2

def tx(p):
    x = CANVAS/2 + (p[0] - mid_x) * s - 4
    y = CANVAS/2 + (p[1] - mid_y) * s + 36
    return (x, y)

# ---- SVG OUTPUT ----

lines = [
    f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {CANVAS} {CANVAS}" width="{CANVAS}" height="{CANVAS}">',
    f'  <defs><clipPath id="b"><rect x="4" y="4" width="120" height="120" rx="14" ry="14"/></clipPath></defs>',
    f'  <rect x="4" y="4" width="120" height="120" rx="14" ry="14" fill="#FFFFFF"/>',
    f'  <g clip-path="url(#b)">',
]
for proj, color, _ in rendered:
    pts = " ".join(f"{tx(p)[0]:.1f},{tx(p)[1]:.1f}" for p in proj)
    lines.append(f'    <polygon points="{pts}" fill="{color}" stroke="#192a42" stroke-width="0.6" stroke-linejoin="round"/>')
lines.append('  </g>')
lines.append(f'  <rect x="4" y="4" width="120" height="120" rx="14" ry="14" fill="none" stroke="#C0C4CC" stroke-width="1.5"/>')
lines.append('</svg>')

with open('/home/ibbi/source/decksurf/images/logo.svg', 'w') as f:
    f.write("\n".join(lines))

print(f"Generated {len(rendered)} faces")
