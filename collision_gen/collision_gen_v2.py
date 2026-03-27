"""
The Last Arrow - Automatic Arena Collision Generator v2
Uses Sobel gradient edge detection + seed-guided platform tracing.
Seeds come from _Recovery/0.unity world coordinates (ground truth).
"""

import numpy as np
from PIL import Image, ImageFilter, ImageDraw, ImageFont
import json, sys

IMG_PATH  = "/sessions/happy-ecstatic-hawking/mnt/The Last Arrow/Assets/backg.png"
OUT_JSON  = "/sessions/happy-ecstatic-hawking/collision_out_v2.json"
OUT_DBG   = "/sessions/happy-ecstatic-hawking/collision_debug_v2.png"

IMG_W, IMG_H = 2752, 1536

# ── Coordinate conversion ─────────────────────────────────────────────────────
def world_to_px(wx, wy):
    return (int(wx + IMG_W/2), int(IMG_H/2 - wy))

def px_to_world(px, py):
    return (px - IMG_W/2.0, IMG_H/2.0 - py)

def px_to_norm(px, py):
    return round(px / IMG_W, 4), round(1.0 - py / IMG_H, 4)

# ── Known platform seeds from _Recovery/0.unity (world coords) ───────────────
# Each seed: (name, x_start_world, x_end_world, y_world_approx, search_half_window)
PLATFORM_SEEDS = [
    # name,                    x_lo,   x_hi,   y_approx, window
    ("Left Ground",           -1264,   -570,    -435,     80),
    ("Root Bridge",            -210,    400,    -240,     100),
    ("Right Slope",             400,    560,    -310,     120),
    ("Right Lower Ledge",       560,   1135,    -415,     80),
    ("Left Mid Platform",      -990,   -407,     108,     80),
    ("Left Upper Platform",   -1068,   -500,     215,     80),
    ("Upper Center Island",     10,     390,     185,     80),
    ("Upper Right Ledge",      880,    1160,     215,     80),
    ("Right Mid Platform",     555,     855,      15,     80),
]

# ── Douglas-Peucker simplification ───────────────────────────────────────────
def dp(pts, eps):
    if len(pts) < 3:
        return pts
    s, e = np.array(pts[0], float), np.array(pts[-1], float)
    d = e - s
    ln = np.linalg.norm(d)
    dists = []
    for p in pts:
        pv = np.array(p, float) - s
        if ln > 0:
            proj = np.dot(pv, d/ln)
            closest = s + proj*(d/ln)
        else:
            closest = s
        dists.append(np.linalg.norm(np.array(p, float) - closest))
    mi = int(np.argmax(dists))
    if dists[mi] > eps:
        return dp(pts[:mi+1], eps)[:-1] + dp(pts[mi:], eps)
    return [pts[0], pts[-1]]

# ── Compute gradient image ────────────────────────────────────────────────────
def compute_gradient(gray_arr):
    """Sobel Y gradient: positive = dark above bright below, negative = bright above dark below."""
    h, w = gray_arr.shape
    gy = np.zeros_like(gray_arr, dtype=float)
    # Simple 3-pixel vertical Sobel
    gy[1:-1, :] = (gray_arr[2:, :].astype(float) - gray_arr[:-2, :].astype(float)) / 2.0
    return gy

# ── Find top edge for a given seed ───────────────────────────────────────────
def find_platform_edge(gray, gy, x_lo_px, x_hi_px, y_center_px, window):
    """
    For each x column in [x_lo_px..x_hi_px], find the topmost strong POSITIVE
    gradient transition within y_center_px ± window.
    Positive Sobel Y = dark-to-bright going downward = TOP surface of a platform.
    """
    h, w = gray.shape
    x_lo_px = max(0, x_lo_px)
    x_hi_px = min(w-1, x_hi_px)
    y_lo = max(1, y_center_px - window)
    y_hi = min(h-2, y_center_px + window)

    top_y = {}

    for x in range(x_lo_px, x_hi_px+1):
        col_grad = gy[y_lo:y_hi, x]
        # Find local max in gradient (brightest transition)
        local_max_idx = int(np.argmax(col_grad))
        local_max_val = col_grad[local_max_idx]

        if local_max_val > 8:   # threshold: must be a real edge
            top_y[x] = y_lo + local_max_idx
        else:
            # Fallback: use brightest pixel approach — find where brightness
            # jumps significantly relative to neighbors
            col = gray[y_lo:y_hi, x].astype(float)
            diff = np.diff(col)
            if len(diff) > 0:
                best = int(np.argmax(diff))
                if diff[best] > 12:
                    top_y[x] = y_lo + best

    return top_y

# ── Smooth and fill gaps in top_y ────────────────────────────────────────────
def smooth_top_y(top_y_dict, x_lo, x_hi, max_gap=20, kernel=11):
    """Fill small gaps and smooth the detected edge."""
    xs = list(range(x_lo, x_hi+1))
    arr = np.array([top_y_dict.get(x, -1) for x in xs], dtype=float)

    # Fill small gaps by linear interpolation
    valid_mask = arr >= 0
    if valid_mask.sum() < 5:
        return {}

    # Interpolate gaps
    valid_idxs = np.where(valid_mask)[0]
    for i in range(len(valid_idxs)-1):
        i0, i1 = valid_idxs[i], valid_idxs[i+1]
        gap = i1 - i0
        if 1 < gap <= max_gap:
            y0, y1 = arr[i0], arr[i1]
            for j in range(1, gap):
                arr[i0+j] = y0 + (y1-y0)*j/gap

    # Median smooth
    smoothed = arr.copy()
    for i in range(len(arr)):
        lo = max(0, i-kernel)
        hi = min(len(arr), i+kernel)
        region = arr[lo:hi]
        valid = region[region >= 0]
        if len(valid) > 2:
            smoothed[i] = np.median(valid)

    return {xs[i]: int(smoothed[i]) for i in range(len(xs)) if smoothed[i] >= 0}

# ── Main ──────────────────────────────────────────────────────────────────────
def main():
    print("Loading image...")
    img = Image.open(IMG_PATH).convert("RGBA")
    arr = np.array(img)
    r, g, b = arr[:,:,0].astype(float), arr[:,:,1].astype(float), arr[:,:,2].astype(float)

    # Luminance (weighted)
    gray = (0.299*r + 0.587*g + 0.114*b).astype(np.uint8)
    gy = compute_gradient(gray.astype(float))

    print(f"Gradient stats: min={gy.min():.1f} max={gy.max():.1f} mean={gy.mean():.2f}")

    results = []
    dbg = img.copy().convert("RGB")
    draw = ImageDraw.Draw(dbg)

    colors = [(0,255,80), (255,160,0), (0,200,255), (255,60,255),
              (255,255,0), (200,80,255), (80,255,200), (255,80,80), (120,200,255)]

    print("\nProcessing platform seeds...")
    for idx, (name, wx0, wx1, wy, win) in enumerate(PLATFORM_SEEDS):
        px0, py_c = world_to_px(wx0, wy)
        px1, _    = world_to_px(wx1, wy)
        col = colors[idx % len(colors)]

        print(f"\n[{idx}] {name}: x=[{px0}..{px1}] y_center={py_c} window=±{win}px")

        # Find edge
        top_y = find_platform_edge(gray, gy, px0, px1, py_c, win)
        top_y = smooth_top_y(top_y, px0, px1)

        if len(top_y) < 10:
            print(f"  WARNING: only {len(top_y)} points found, skipping")
            continue

        pts = sorted(top_y.items())
        pts_list = [(x, y) for x, y in pts]
        simplified = dp(pts_list, 4.0)

        world_pts = [px_to_world(x, y) for x, y in simplified]
        norm_pts  = [px_to_norm(x, y)  for x, y in simplified]

        print(f"  {len(pts_list)} raw → {len(simplified)} simplified points")
        for nx, ny in norm_pts:
            print(f"  new Vector2({nx:.4f}f, {ny:.4f}f),")

        results.append({
            "label":      name,
            "norm_pts":   norm_pts,
            "world_pts":  world_pts,
            "pixel_pts":  simplified,
        })

        # Draw on debug image
        for j in range(len(simplified)-1):
            draw.line([simplified[j], simplified[j+1]], fill=col, width=5)
        for (x,y) in simplified:
            draw.ellipse([x-5,y-5,x+5,y+5], fill=col, outline=(255,255,255))

        # Label
        mid = simplified[len(simplified)//2]
        draw.text((mid[0]-40, mid[1]-22), name, fill=(255,255,255))

        # Draw seed line (dashed)
        draw.line([(px0, py_c), (px1, py_c)], fill=(100,100,100), width=1)

    # Save JSON
    with open(OUT_JSON, "w") as f:
        json.dump({"segments": results}, f, indent=2)
    print(f"\nJSON → {OUT_JSON}")

    # Save debug
    dbg.save(OUT_DBG)
    print(f"Debug image → {OUT_DBG}")

    # Print Unity C# EdgeStamp code
    print("\n" + "="*70)
    print("// ── Generated EdgeStamps for ProjectPvpArenaCollisionTools.cs ──")
    for seg in results:
        lbl = seg["label"]
        pts = seg["norm_pts"]
        print(f'\nnew EdgeStamp("{lbl}",')
        for i, (nx, ny) in enumerate(pts):
            comma = "," if i < len(pts)-1 else ""
            print(f"    new Vector2({nx:.4f}f, {ny:.4f}f){comma}")
        print("),")

if __name__ == "__main__":
    main()
# This file already has main(), the patch is applied in v3
