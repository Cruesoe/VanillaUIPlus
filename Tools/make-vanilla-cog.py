# Regenerates Textures/UI/Icons/MainButtons/cog.{png,dds} as a vanilla-style
# play-settings icon: flat gray fill, 1px black outline, 24x24, no anti-aliasing.
from PIL import Image
import math
import os
import struct

SIZE = 24
FILL = (156, 158, 156, 255)
LINE = (0, 0, 0, 255)
EMPTY = (0, 0, 0, 0)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(ROOT, "Textures", "UI", "Icons", "MainButtons")


def occupancy():
    occ = [[False] * SIZE for _ in range(SIZE)]
    cx = cy = (SIZE - 1) / 2.0

    def add_disk(r_min, r_max):
        for y in range(SIZE):
            for x in range(SIZE):
                if r_min <= math.hypot(x - cx, y - cy) <= r_max:
                    occ[y][x] = True

    def add_rect(x0, y0, x1, y1):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                if 0 <= x < SIZE and 0 <= y < SIZE:
                    occ[y][x] = True

    add_disk(3.2, 7.4)
    add_rect(10, 1, 13, 22)
    add_rect(1, 10, 22, 13)
    for y in range(SIZE):
        for x in range(SIZE):
            dx = x - cx
            dy = y - cy
            r = math.hypot(dx, dy)
            if 3.2 <= r <= 11.1 and (abs(dx - dy) <= 2.2 or abs(dx + dy) <= 2.2):
                occ[y][x] = True
    for y in range(SIZE):
        for x in range(SIZE):
            if math.hypot(x - cx, y - cy) < 3.2:
                occ[y][x] = False
    return occ


def render():
    occ = occupancy()
    img = Image.new("RGBA", (SIZE, SIZE), EMPTY)
    px = img.load()
    for y in range(SIZE):
        for x in range(SIZE):
            if not occ[y][x]:
                continue
            edge = any(
                not (0 <= nx < SIZE and 0 <= ny < SIZE) or not occ[ny][nx]
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1))
            )
            px[x, y] = LINE if edge else FILL
    return img


def write_dds_bgra(image, path):
    image = image.convert("RGBA")
    w, h = image.size
    pixels = image.load()
    payload = bytearray()
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            payload.extend((b, g, r, a))
    header = bytearray()
    header += b"DDS "
    header += struct.pack("<I", 124)
    header += struct.pack("<I", 0x1 | 0x2 | 0x4 | 0x8 | 0x1000)
    header += struct.pack("<I", h)
    header += struct.pack("<I", w)
    header += struct.pack("<I", w * 4)
    header += struct.pack("<I", 0)
    header += struct.pack("<I", 1)
    header += b"\x00" * 44
    header += struct.pack("<I", 32)
    header += struct.pack("<I", 0x41)
    header += struct.pack("<I", 0)
    header += struct.pack("<I", 32)
    header += struct.pack("<I", 0x00FF0000)
    header += struct.pack("<I", 0x0000FF00)
    header += struct.pack("<I", 0x000000FF)
    header += struct.pack("<I", 0xFF000000)
    header += struct.pack("<I", 0x1000)
    header += struct.pack("<I", 0)
    header += struct.pack("<I", 0)
    header += struct.pack("<I", 0)
    header += struct.pack("<I", 0)
    with open(path, "wb") as f:
        f.write(header)
        f.write(payload)


def main():
    img = render()
    os.makedirs(OUT_DIR, exist_ok=True)
    png = os.path.join(OUT_DIR, "cog.png")
    dds = os.path.join(OUT_DIR, "cog.dds")
    img.save(png, "PNG")
    write_dds_bgra(img, dds)
    print(png)
    print(dds)


if __name__ == "__main__":
    main()
