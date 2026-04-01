# Arena Collision Generator

Automated computer vision pipeline that reads `backg.png` and generates
`EdgeCollider2D` coordinates for `ProjectPvpArenaCollisionTools.cs`.

## How it works

1. Loads `Assets/backg.png` (2752×1536 RGBA, PPU=1)
2. Computes Sobel-Y gradient (detects dark→bright transitions = platform top surfaces)
3. For each platform seed, scans pixel columns within a ±window around known Y position
4. Applies outlier rejection + median smoothing to clean noisy detections
5. Simplifies with Douglas-Peucker algorithm
6. Outputs normalized `[0,1]` coordinates ready for `EdgeStamp` entries

## Usage

```bash
cd "C:\Users\user\Documents\Claude\The Last Arrow"
python collision_gen\collision_gen_v2.py
```

Output:
- `collision_out_v2.json` — full coordinate data
- `collision_debug_v2.png` — visualization overlay on backg.png
- Console prints ready-to-paste C# `EdgeStamp` code

## Applying results in Unity

1. Copy the printed `EdgeStamp` entries into `ProjectPvpArenaCollisionTools.cs`
2. In Unity: `ProjectPVP > Environment > Stamp Default Arena Collisions`
3. Check Scene view — collision lines should follow platform surfaces
4. Ctrl+S to save scene

## Platform seeds

Seeds come from `_Recovery/0.unity` world coordinates:

| Platform             | X range (world) | Y approx | Window |
|----------------------|-----------------|----------|--------|
| Left Ground          | -1264 → -570    | -435     | ±60px  |
| Root Bridge          | -210 → 400      | -240     | ±80px  |
| Right Slope          | 400 → 560       | -310     | ±100px |
| Right Lower Ledge    | 560 → 1135      | -415     | ±60px  |
| Left Mid Platform    | -990 → -407     | 108      | ±55px  |
| Left Upper Platform  | -1068 → -500    | 215      | ±55px  |
| Upper Center Island  | 10 → 390        | 185      | ±60px  |
| Upper Right Ledge    | 880 → 1160      | 215      | ±55px  |
| Right Mid Platform   | 555 → 855       | 15       | ±60px  |

## To regenerate after art changes

Update the `PLATFORM_SEEDS` list in `collision_gen_v2.py` if platform positions
shift, then re-run the script and paste results into `ProjectPvpArenaCollisionTools.cs`.
