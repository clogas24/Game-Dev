"""Generates Lantern Keeper's original particle and UI textures.

Everything here is drawn from scratch with Pillow and numpy, so it carries no third-party
license. docs/CREDITS.md lists the files as "Original, generated for this project".

Run from the Unity project folder ("My project"):
    python Tools/generate_textures.py
It overwrites the .png files under Assets/_Project/Art/Generated/Textures. Unity keeps each
file's .meta, so import settings and references survive. Needs numpy and Pillow.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "_Project", "Art", "Generated", "Textures")
SS = 4  # supersampling factor for the drawn shapes
rng = np.random.default_rng(1031)


def save(name, image):
    os.makedirs(OUT, exist_ok=True)
    image.save(os.path.join(OUT, name + ".png"))
    print(f"  {name}.png  {image.size[0]}x{image.size[1]}")


def from_alpha(alpha, rgb=(255, 255, 255)):
    """White (or tinted) image whose alpha is the given 0..1 array."""
    h, w = alpha.shape
    data = np.zeros((h, w, 4), dtype=np.uint8)
    data[..., 0], data[..., 1], data[..., 2] = rgb
    data[..., 3] = np.clip(alpha * 255, 0, 255).astype(np.uint8)
    return Image.fromarray(data, "RGBA")


def grid(size):
    axis = (np.arange(size) + 0.5) / size * 2 - 1
    return np.meshgrid(axis, axis)


def soft_dot(size=64):
    x, y = grid(size)
    r = np.sqrt(x * x + y * y)
    return from_alpha(np.clip(1 - r, 0, 1) ** 2.2)


def spark(size=64):
    """A four-point star with a soft core."""
    x, y = grid(size)
    r = np.sqrt(x * x + y * y)
    rays = np.exp(-(np.abs(x) * 14)) * np.clip(1 - np.abs(y), 0, 1) ** 2 \
        + np.exp(-(np.abs(y) * 14)) * np.clip(1 - np.abs(x), 0, 1) ** 2
    core = np.clip(1 - r * 2.2, 0, 1) ** 2
    return from_alpha(np.clip(rays * 0.9 + core, 0, 1))


def mist(size=128):
    """A lumpy puff: a few overlapping soft blobs, faded out before the border."""
    x, y = grid(size)
    alpha = np.zeros_like(x)
    for _ in range(9):
        cx, cy = rng.uniform(-0.35, 0.35, 2)
        radius = rng.uniform(0.3, 0.55)
        d = np.sqrt((x - cx) ** 2 + (y - cy) ** 2) / radius
        alpha += np.clip(1 - d, 0, 1) ** 2 * rng.uniform(0.3, 0.6)
    alpha *= np.clip(1.05 - np.sqrt(x * x + y * y), 0, 1) ** 1.5
    return from_alpha(np.clip(alpha, 0, 1) * 0.9)


def leaf(size=64):
    big = size * SS
    image = Image.new("L", (big, big), 0)
    draw = ImageDraw.Draw(image)
    # A pointed oval with a notch cut by the midrib, drawn along the diagonal.
    points = []
    for i in range(41):
        t = i / 40
        half_width = math.sin(t * math.pi) ** 0.8 * 0.26 * (1 - 0.25 * t)
        points.append((t, half_width))
    outline = [(0.12 + 0.76 * t, 0.5 - w) for t, w in points] + [(0.12 + 0.76 * t, 0.5 + w) for t, w in reversed(points)]
    draw.polygon([(x * big, y * big) for x, y in outline], fill=255)
    draw.line([(0.04 * big, 0.5 * big), (0.14 * big, 0.5 * big)], fill=255, width=SS * 2)  # stalk
    draw.line([(0.14 * big, 0.5 * big), (0.86 * big, 0.5 * big)], fill=150, width=SS)       # midrib
    image = image.rotate(35, resample=Image.BICUBIC).resize((size, size), Image.LANCZOS)
    return from_alpha(np.asarray(image, dtype=float) / 255)


def ui_flame(width=128, height=192):
    """Candle flame for the HUD: a teardrop, yellow-white at the heart and orange at the rim."""
    xs = (np.arange(width) + 0.5) / width * 2 - 1
    ys = 1 - (np.arange(height) + 0.5) / height  # 0 at the bottom, 1 at the tip
    x, y = np.meshgrid(xs, ys)
    # Half-width of the teardrop at each height: round at the bottom, pointed at the top.
    half = 0.78 * np.sin(np.clip(y, 0, 1) ** 0.62 * math.pi) ** 0.9 * (1 - 0.35 * y)
    edge = np.clip((half - np.abs(x)) / 0.12, 0, 1)
    inner_half = half * 0.5
    heart = np.clip((inner_half - np.abs(x)) / 0.2, 0, 1) * np.clip(1 - y * 1.25, 0, 1)
    rgb = np.zeros((height, width, 3))
    rgb[..., 0] = 1.0
    rgb[..., 1] = 0.5 + 0.45 * heart
    rgb[..., 2] = 0.1 + 0.6 * heart ** 2
    data = np.dstack([rgb, edge[..., None] if edge.ndim == 2 else edge])
    return Image.fromarray(np.clip(data * 255, 0, 255).astype(np.uint8), "RGBA")


def ui_candy(size=128):
    """A wrapped sweet: a striped round body with a twisted paper fan on each side."""
    big = size * SS
    image = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    pink, light, dark = (255, 96, 170, 255), (255, 214, 232, 255), (150, 40, 100, 255)
    cx = cy = big / 2
    body = big * 0.24
    for side in (-1, 1):
        tip_x = cx + side * big * 0.46
        base_x = cx + side * body * 0.8
        draw.polygon([(base_x, cy), (tip_x, cy - big * 0.2), (tip_x, cy + big * 0.2)], fill=light, outline=dark, width=SS * 2)
        draw.line([(base_x, cy), (tip_x, cy)], fill=dark, width=SS * 2)
    draw.ellipse([cx - body, cy - body, cx + body, cy + body], fill=pink)
    # Stripes are drawn on their own layer and pasted through the body's shape, so none stick out.
    stripes = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    stripe_draw = ImageDraw.Draw(stripes)
    for offset in (-0.45, 0.0, 0.45):
        x0 = cx + offset * body * 1.5
        stripe_draw.arc([x0 - body * 0.7, cy - body * 1.1, x0 + body * 0.7, cy + body * 1.1], 290, 70, fill=light, width=SS * 4)
    body_mask = Image.new("L", (big, big), 0)
    ImageDraw.Draw(body_mask).ellipse([cx - body, cy - body, cx + body, cy + body], fill=255)
    image.paste(stripes, (0, 0), Image.composite(stripes.getchannel("A"), Image.new("L", (big, big), 0), body_mask))
    draw.ellipse([cx - body, cy - body, cx + body, cy + body], outline=dark, width=SS * 3)
    return image.resize((size, size), Image.LANCZOS)


def ui_rounded(size=64, radius=18):
    """White rounded rectangle for 9-sliced buttons, bars and panels."""
    big = size * SS
    image = Image.new("L", (big, big), 0)
    ImageDraw.Draw(image).rounded_rectangle([0, 0, big - 1, big - 1], radius=radius * SS, fill=255)
    return from_alpha(np.asarray(image.resize((size, size), Image.LANCZOS), dtype=float) / 255)


def ui_parchment(size=128, radius=22):
    """A 9-sliced sheet of old paper: warm, a little blotchy, darker toward the edge."""
    big = size * SS
    mask = Image.new("L", (big, big), 0)
    ImageDraw.Draw(mask).rounded_rectangle([SS, SS, big - 1 - SS, big - 1 - SS], radius=radius * SS, fill=255)
    mask = mask.resize((size, size), Image.LANCZOS)
    x, y = grid(size)
    edge = np.clip(np.maximum(np.abs(x), np.abs(y)), 0, 1) ** 6
    blotch = np.asarray(Image.fromarray((rng.random((size // 8, size // 8)) * 255).astype(np.uint8), "L")
                        .resize((size, size), Image.BICUBIC).filter(ImageFilter.GaussianBlur(2)), dtype=float) / 255
    tone = 1.0 - 0.28 * edge - 0.08 * (blotch - 0.5)
    data = np.zeros((size, size, 4))
    data[..., 0] = 0.93 * tone
    data[..., 1] = 0.84 * tone
    data[..., 2] = 0.64 * tone
    data[..., 3] = np.asarray(mask, dtype=float) / 255
    return Image.fromarray(np.clip(data * 255, 0, 255).astype(np.uint8), "RGBA")


def main():
    print("Textures")
    save("T_SoftDot", soft_dot())
    save("T_Spark", spark())
    save("T_Mist", mist())
    save("T_Leaf", leaf())
    save("UI_Flame", ui_flame())
    save("UI_Candy", ui_candy())
    save("UI_Rounded", ui_rounded())
    save("UI_Parchment", ui_parchment())
    return 0


if __name__ == "__main__":
    sys.exit(main())
