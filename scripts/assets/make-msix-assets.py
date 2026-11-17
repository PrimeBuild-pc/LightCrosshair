"""Generate the MSIX / Microsoft Store logos in packaging/msix/Assets.

Uses draw() from make-icon.py (the generator of LightCrosshair/assets/icon.ico), so every
size is drawn pixel-exact instead of being scaled from the .ico. Requires Pillow.
Usage: python scripts/assets/make-msix-assets.py
"""
import ast
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ICON = Path(__file__).with_name("make-icon.py")
OUT = ROOT / "packaging" / "msix" / "Assets"
SCALES = [100, 125, 150, 200, 400]
# name: (width, height at scale-100, icon size as a fraction of the shorter side)
LOGOS = {
    "Square44x44Logo": (44, 44, 1.0),
    "StoreLogo": (50, 50, 1.0),
    "Square71x71Logo": (71, 71, 0.6),
    "Square150x150Logo": (150, 150, 0.5),
    "Wide310x150Logo": (310, 150, 0.5),
    "Square310x310Logo": (310, 310, 0.5),
}
TARGET_SIZES = [16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256]


def load_draw():
    tree = ast.parse(ICON.read_text(encoding="utf-8"))
    # Same approach as make-banner.py: load draw() and its imports only, not the icon CLI writes.
    nodes = [n for n in tree.body if isinstance(n, (ast.Import, ast.ImportFrom))
             or isinstance(n, ast.FunctionDef) and n.name == "draw"
             or isinstance(n, ast.Assign) and any(isinstance(t, ast.Name) and t.id == "RED" for t in n.targets)]
    namespace = {}
    exec(compile(ast.Module(body=nodes, type_ignores=[]), str(ICON), "exec"), namespace)
    return namespace["draw"]


def tile(draw, width, height, fraction):
    size = max(2, round(min(width, height) * fraction))
    size -= size % 2  # draw() is symmetric on even sizes
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    canvas.paste(draw(size), ((width - size) // 2, (height - size) // 2))
    return canvas


def main():
    draw = load_draw()
    OUT.mkdir(parents=True, exist_ok=True)
    for old in OUT.glob("*.png"):
        old.unlink()
    for name, (w, h, fraction) in LOGOS.items():
        for scale in SCALES:
            tile(draw, round(w * scale / 100), round(h * scale / 100), fraction).save(OUT / f"{name}.scale-{scale}.png")
    # Taskbar, Start and Alt+Tab icons: plain (on the tile color) and unplated (no plate) variants.
    for n in TARGET_SIZES:
        image = tile(draw, n, n, 1.0)
        for suffix in ("", "_altform-unplated", "_altform-lightunplated"):
            image.save(OUT / f"Square44x44Logo.targetsize-{n}{suffix}.png")
    print(f"Wrote {len(list(OUT.glob('*.png')))} files to {OUT}")


if __name__ == "__main__":
    main()
