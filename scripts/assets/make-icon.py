# Icon C: red crosshair with center gap and dot, drawn per size so every edge is pixel-exact.
import sys
from PIL import Image, ImageDraw
RED = (255, 42, 42, 255)
SIZES = [16, 20, 24, 32, 40, 48, 64, 256]
def draw(n):
    t = max(2, 2 * round(n / 24))          # even thickness keeps the cross symmetric on even sizes
    gap = max(1, round(n * 8 / 96))
    m = round(n * 10 / 96)
    c0 = n // 2 - t // 2                   # first pixel of the center band
    im = Image.new("RGBA", (n, n), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    r = lambda x0, y0, x1, y1: d.rectangle([x0, y0, x1 - 1, y1 - 1], fill=RED)
    r(c0, c0, c0 + t, c0 + t)                              # dot
    r(c0, m, c0 + t, c0 - gap); r(c0, c0 + t + gap, c0 + t, n - m)   # top, bottom
    r(m, c0, c0 - gap, c0 + t); r(c0 + t + gap, c0, n - m, c0 + t)   # left, right
    return im
imgs = [draw(n) for n in SIZES]
for im in imgs:  # self-check: symmetric under 180° rotation and mirroring
    assert im.tobytes() == im.rotate(180).tobytes() == im.transpose(Image.FLIP_LEFT_RIGHT).tobytes(), im.size
out = sys.argv[1]
imgs[-1].save(out, format="ICO", sizes=[(n, n) for n in SIZES], append_images=imgs[:-1])
imgs[-1].save(out.replace(".ico", "-256.png"))
