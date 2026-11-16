"""Generate the flat README banner from the app icon's exact pixel geometry.

Requires Pillow, like make-icon.py. Does not write or modify app icon files.
"""
import ast
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ICON = Path(__file__).with_name("make-icon.py")


def icon_rectangles(size):
    tree = ast.parse(ICON.read_text(encoding="utf-8"))
    # Load only draw(), its imports and RED; exclude the icon script's CLI writes.
    nodes = [node for node in tree.body if isinstance(node, (ast.Import, ast.ImportFrom))
             or isinstance(node, ast.FunctionDef) and node.name == "draw"
             or isinstance(node, ast.Assign) and any(isinstance(t, ast.Name) and t.id == "RED" for t in node.targets)]
    namespace = {}
    exec(compile(ast.Module(body=nodes, type_ignores=[]), str(ICON), "exec"), namespace)
    image = namespace["draw"](size)
    pixels = image.load()
    remaining = {(x, y) for y in range(size) for x in range(size) if pixels[x, y][3]}
    rectangles = []
    while remaining:
        seed = min(remaining, key=lambda p: (p[1], p[0]))
        remaining.remove(seed)
        component, pending = {seed}, [seed]
        while pending:
            x, y = pending.pop()
            for point in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if point in remaining:
                    remaining.remove(point); component.add(point); pending.append(point)
        left = min(p[0] for p in component); top = min(p[1] for p in component)
        width = max(p[0] for p in component) - left + 1
        height = max(p[1] for p in component) - top + 1
        assert len(component) == width * height
        assert all(pixels[x, y] == (255, 42, 42, 255) for x, y in component)
        rectangles.append((left, top, width, height))
    assert len(rectangles) == 5
    return rectangles


def render():
    rects = "\n".join(f'    <rect x="{x}" y="{y}" width="{w}" height="{h}"/>'
                      for x, y, w, h in icon_rectangles(192))
    svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="280" viewBox="0 0 1200 280" role="img" aria-labelledby="title desc">
  <title id="title">LightCrosshair</title>
  <desc id="desc">Lightweight crosshair overlay for Windows. Red crosshair with a center gap and dot.</desc>
  <rect width="1200" height="280" fill="#0E0F11"/>
  <g transform="translate(44 44)" fill="#FF2A2A">
{rects}
  </g>
  <text x="280" y="132" fill="#E8EAED" font-family="Segoe UI Semibold, Segoe UI, sans-serif" font-size="56" font-weight="600">LightCrosshair</text>
  <text x="280" y="178" fill="#9AA0A6" font-family="Segoe UI, sans-serif" font-size="25">Lightweight crosshair overlay for Windows</text>
</svg>
'''
    (ROOT / "readme-banner.svg").write_text(svg, encoding="utf-8")


if __name__ == "__main__":
    render()
