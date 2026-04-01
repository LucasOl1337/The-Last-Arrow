"""
The Last Arrow - Automatic Arena Collision Generator
Reads backg.png, detects platform surfaces via computer vision,
outputs world-space EdgeCollider2D coordinates for Unity.
"""

import numpy as np
from PIL import Image, ImageFilter, ImageDraw
import json, sys, os

# ── Config ───────────────────────────────────────────────────────────────────
IMG_PATH   = "/sessions/happy-ecstatic-hawking/mnt/The Last Arrow/Assets/backg.png"
OUT_JSON   = "/sessions/happy-ecstatic-hawking/collision_out.json"
OUT_DBG    = "/sessions/happy-ecstatic-hawking/collision_debug.png"

IMG_W, IMG_H = 2752, 1536        # must match actual image
WORLD_W, WORLD_H = 2752, 1536   # PPU = 1

# Douglas-Peucker simplification tolerance (pixels)
DP_EPSILON = 6.0

# Minimum platform segment length to keep (pixels)
MIN_SEG_LEN = 60

# ── Coord conversion ─────────────────────────────────────────────────────────
def px_to_world(px, py):
    """Pixel → Unity world coords (origin at image center, Y flipped)."""
    wx = px - IMG_W / 2.0
    wy = IMG_H / 2.0 - py
    return (wx, wy)

def px_to_norm(px, py):
    """Pixel → normalized [0,1] for EdgeStamp format."""
    return (px / IMG_W, 1.0 - py / IMG_H)

# ── Douglas-Peucker ───────────────────────────────────────────────────────────
def dp_simplify(points, epsilon):
    if len(points) < 3:
        return points
    start, end = np.array(points[0]), np.array(points[-1])
    line_vec = end - start
    line_len = np.linalg.norm(line_vec)
    if line_len == 0:
        dists = [np.linalg.norm(np.array(p) - start) for p in points]
    else:
        line_unit = line_vec / line_len
        dists = []
        for p in points:
            pv = np.array(p) - start
            proj = np.dot(pv, line_unit)
            closest = start + proj * line_unit
            dists.append(np.linalg.norm(np.array(p) - closest))
    max_dist = max(dists)
    max_idx  = dists.index(max_dist)
    if max_dist > epsilon:
        left  = dp_simplify(points[:max_idx+1], epsilon)
        right = dp_simplify(points[max_idx:],   epsilon)
        return left[:-1] + right
    else:
        return [points[0], points[-1]]

# ── Main detection ────────────────────────────────────────────────────────────
def detect_platforms(img_rgba):
    arr = np.array(img_rgba).astype(np.float32)
    r, g, b, a = arr[:,:,0], arr[:,:,1], arr[:,:,2], arr[:,:,3]

    # ── 1. Color-based platform mask ──
    # Platform surfaces in this jungle arena = mossy green-brown tones
    # High green, moderate red, lower blue → typical jungle ground color
    green_dom  = (g > r * 0.85) & (g > b * 1.1)                      # greenish
    bright     = (r.astype(int) + g.astype(int) + b.astype(int)) > 200 # not too dark
    not_sky    = g < 220                                                # not bright sky/mist
    saturated  = (g.astype(int) - b.astype(int)) > 15                  # some color (not grey)

    platform_mask = (green_dom | bright) & not_sky & saturated
    platform_mask = platform_mask & (a > 200)  # ignore transparent pixels

    # ── 2. Morphological cleanup ──
    from PIL import Image as PILImage
    mask_img = PILImage.fromarray(platform_mask.astype(np.uint8) * 255, 'L')

    # Close gaps (dilate then erode)
    for _ in range(3):
        mask_img = mask_img.filter(ImageFilter.MaxFilter(3))
    for _ in range(3):
        mask_img = mask_img.filter(ImageFilter.MinFilter(3))

    mask_clean = np.array(mask_img) > 128

    # ── 3. Also try luminance-based Sobel edge detection ──
    gray = 0.299*r + 0.587*g + 0.114*b
    gray_img = PILImage.fromarray(gray.astype(np.uint8), 'L')
    edges = gray_img.filter(ImageFilter.FIND_EDGES)
    edge_arr = np.array(edges) > 30

    # ── 4. Find top surface of platform mask ──
    # For each column, find the topmost solid pixel
    h, w = mask_clean.shape
    top_surface = np.full(w, -1, dtype=int)

    for x in range(w):
        col = mask_clean[:, x]
        solid_rows = np.where(col)[0]
        if len(solid_rows) > 0:
            top_surface[x] = solid_rows[0]

    # ── 5. Clean top_surface: remove isolated spikes ──
    # Smooth with a window median to remove noise
    kernel = 15
    smoothed = top_surface.copy().astype(float)
    for x in range(w):
        lo = max(0, x - kernel)
        hi = min(w, x + kernel)
        region = top_surface[lo:hi]
        valid = region[region >= 0]
        if len(valid) > kernel // 2:
            smoothed[x] = np.median(valid)
        elif len(valid) > 0:
            smoothed[x] = np.median(valid)

    top_surface_smooth = smoothed.astype(int)

    # ── 6. Segment into continuous platform runs ──
    # A "run" is where top_surface is valid and the height doesn't jump too much
    segments = []
    in_seg   = False
    seg_start= 0
    prev_y   = -1
    MAX_JUMP = 20  # pixels

    for x in range(w):
        y = top_surface_smooth[x]
        if y < 0:
            if in_seg:
                segments.append((seg_start, x-1))
                in_seg = False
            continue
        if not in_seg:
            in_seg = True
            seg_start = x
            prev_y = y
        else:
            if abs(y - prev_y) > MAX_JUMP:
                segments.append((seg_start, x-1))
                seg_start = x
            prev_y = y

    if in_seg:
        segments.append((seg_start, w-1))

    # ── 7. Keep only long-enough segments ──
    segments = [(x0, x1) for x0, x1 in segments if (x1 - x0) >= MIN_SEG_LEN]

    # ── 8. Build point lists and simplify ──
    platform_edges = []
    for x0, x1 in segments:
        pts = [(x, int(top_surface_smooth[x])) for x in range(x0, x1+1)]
        simplified = dp_simplify(pts, DP_EPSILON)
        world_pts  = [px_to_world(px, py) for px, py in simplified]
        norm_pts   = [px_to_norm(px, py)  for px, py in simplified]
        platform_edges.append({
            "pixel_pts":  simplified,
            "world_pts":  world_pts,
            "norm_pts":   norm_pts,
            "x_range":    (x0, x1),
            "length_px":  x1 - x0,
        })

    return platform_edges, mask_clean, top_surface_smooth


def main():
    print("Loading image...")
    img = Image.open(IMG_PATH).convert("RGBA")
    print(f"  Size: {img.size}, Mode: {img.mode}")

    print("Running platform detection...")
    edges, mask, top_surf = detect_platforms(img)

    print(f"  Found {len(edges)} platform segments")
    for i, e in enumerate(edges):
        x0, x1 = e["x_range"]
        print(f"  Seg {i}: x=[{x0}..{x1}] ({e['length_px']}px), {len(e['norm_pts'])} points")
        for nx, ny in e["norm_pts"]:
            print(f"    new Vector2({nx:.4f}f, {ny:.4f}f),")

    # ── Save JSON ──
    json_data = {
        "image_size": list(img.size),
        "segments": [
            {
                "label": f"Platform_{i}",
                "norm_pts": e["norm_pts"],
                "world_pts": e["world_pts"],
                "length_px": e["length_px"],
            }
            for i, e in enumerate(edges)
        ]
    }
    with open(OUT_JSON, "w") as f:
        json.dump(json_data, f, indent=2)
    print(f"\nJSON written to: {OUT_JSON}")

    # ── Debug visualization ──
    print("Generating debug image...")
    dbg = img.copy().convert("RGB")
    draw = ImageDraw.Draw(dbg)

    # Draw platform mask outline (green tint)
    mask_rgba = np.zeros((img.height, img.width, 4), dtype=np.uint8)
    mask_rgba[mask, 1] = 80  # green tint on detected areas
    mask_rgba[mask, 3] = 80

    # Draw detected edges
    colors = [(0,255,0), (255,128,0), (0,200,255), (255,0,255),
              (255,255,0), (255,0,0), (0,128,255), (128,255,0)]
    for i, e in enumerate(edges):
        col = colors[i % len(colors)]
        pts = e["pixel_pts"]
        for j in range(len(pts)-1):
            draw.line([pts[j], pts[j+1]], fill=col, width=4)
        # Mark endpoints
        draw.ellipse([pts[0][0]-6, pts[0][1]-6, pts[0][0]+6, pts[0][1]+6],
                     fill=col, outline=(255,255,255))
        draw.ellipse([pts[-1][0]-6, pts[-1][1]-6, pts[-1][0]+6, pts[-1][1]+6],
                     fill=col, outline=(255,255,255))
        # Label
        mid = pts[len(pts)//2]
        draw.text((mid[0]-15, mid[1]-20), f"P{i}", fill=(255,255,255))

    dbg.save(OUT_DBG)
    print(f"Debug image written to: {OUT_DBG}")
    print("\nDone!")

if __name__ == "__main__":
    main()
