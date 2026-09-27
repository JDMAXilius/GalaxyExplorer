"""Surface maps for the moons no spacecraft has mapped whole (owner's direction, 27 Sep 2026).

Voyager 2 saw only the southern halves of Uranus's moons, only a few frames of Proteus and only a dot for
Nereid; New Horizons saw Pluto's four small moons as a few blurry pixels each. These maps are therefore
*made*, not observed: each one is a height field built on the sphere itself (so it has no seam and no
pinched poles), shaded from the upper left the way a spacecraft mosaic is, and coloured with the moon's
measured brightness and tint. The features follow what is actually known about each moon - Miranda's
coronae, Ariel's rift valleys, Umbriel's dark face and bright Wunda ring, Titania's chasma - but their
positions are invented. Logged as generated in Assets/_sources/CREDITS.md.

    python tools/moons/make_moon_maps.py            # writes Assets/Textures/moons/<id>_texture.jpg
"""
import os
import numpy as np
from PIL import Image

W, H = 1024, 512
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Textures", "moons")

lon = (np.arange(W) + 0.5) / W * 2 * np.pi - np.pi
lat = np.pi / 2 - (np.arange(H) + 0.5) / H * np.pi
LON, LAT = np.meshgrid(lon, lat)
P = np.stack([np.cos(LAT) * np.cos(LON), np.cos(LAT) * np.sin(LON), np.sin(LAT)], axis=-1)


def unit(rng, n):
    v = rng.normal(size=(n, 3))
    return v / np.linalg.norm(v, axis=1, keepdims=True)


def noise(rng, octaves=6, base=2.0, persistence=0.5):
    """Value noise in 3D, sampled on the sphere: no seam at the date line, no smear at the poles."""
    total = np.zeros((H, W))
    amp, freq, norm = 1.0, base, 0.0
    for _ in range(octaves):
        n = int(freq) + 3
        grid = rng.random((n, n, n))
        q = (P * 0.5 + 0.5) * freq + 1.0
        i = np.floor(q).astype(int)
        f = q - i
        f = f * f * (3 - 2 * f)
        acc = 0.0
        for dx in (0, 1):
            for dy in (0, 1):
                for dz in (0, 1):
                    w = (f[..., 0] if dx else 1 - f[..., 0]) * (f[..., 1] if dy else 1 - f[..., 1]) * (f[..., 2] if dz else 1 - f[..., 2])
                    acc = acc + w * grid[i[..., 0] + dx, i[..., 1] + dy, i[..., 2] + dz]
        total += amp * (acc - 0.5)
        norm += amp
        amp *= persistence
        freq *= 2.0
    return total / norm


def craters(rng, h, count, rmin, rmax, depth=1.0, alb=None, dark_floor=0.0, rays=0.0):
    """Bowls with raised rims; radii follow a steep power law, so small craters vastly outnumber big ones."""
    centres = unit(rng, count)
    u = rng.random(count)
    radii = rmin * (rmax / rmin) ** (u ** 5)
    for c, r in zip(centres, radii):
        d = np.arccos(np.clip(P @ c, -1, 1)) / r
        near = d < 3.0
        if not near.any():
            continue
        x = d[near]
        bowl = np.where(x < 1, (x * x - 1) * depth, 0.0)
        rim = 0.35 * depth * np.exp(-((x - 1.0) / 0.18) ** 2)
        h[near] += (bowl + rim) * r * 1.6
        if alb is not None:
            if dark_floor:
                alb[near] -= dark_floor * np.clip(1 - x, 0, 1)
            if rays and r > rmax * 0.4:
                alb[near] += rays * np.exp(-x / 1.2) * (x > 1)
    return h


def rift(rng, h, count, width, length, depth):
    """Long graben: bands along random great circles, cut short to an arc."""
    for n, a in zip(unit(rng, count), unit(rng, count)):
        a = a - n * (a @ n)
        a /= np.linalg.norm(a)
        across = np.abs(P @ n)
        along = np.arccos(np.clip(P @ a, -1, 1))
        mask = (across < width) & (along < length)
        h[mask] -= depth * (1 - across[mask] / width)
    return h


def corona(rng, h, alb, count, size, ridges, contrast):
    """Miranda's coronae: large regions of parallel ridges and grooves, with light and dark banding."""
    for c, d in zip(unit(rng, count), unit(rng, count)):
        inside = np.arccos(np.clip(P @ c, -1, 1)) < size
        phase = (P @ d) * ridges
        edge = np.clip((size - np.arccos(np.clip(P @ c, -1, 1))[inside]) / (size * 0.25), 0, 1)
        h[inside] += 0.006 * np.sin(phase[inside]) * edge
        alb[inside] += contrast * np.sin(phase[inside] * 0.23) * edge
    return h, alb


def shade(h):
    gy, gx = np.gradient(h)
    gx /= np.maximum(np.cos(LAT), 0.15)
    light = np.array([-0.6, 0.6, 0.55])
    light /= np.linalg.norm(light)
    n = np.stack([-gx * 60, -gy * 60, np.ones_like(h)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return np.clip(n @ light, 0, 1) / light[2]


# id: (seed, albedo 0-1, tint RGB, builder)
def titania(rng, h, a):
    craters(rng, h, 3000, 0.002, 0.12, alb=a, rays=0.06)
    rift(rng, h, 3, 0.018, 1.1, 0.012)
    return h, a


def oberon(rng, h, a):
    craters(rng, h, 3500, 0.002, 0.14, alb=a, dark_floor=0.18, rays=0.08)
    return h, a


def miranda(rng, h, a):
    craters(rng, h, 1500, 0.002, 0.08, alb=a)
    corona(rng, h, a, 3, 0.6, 70, 0.12)
    rift(rng, h, 3, 0.012, 0.9, 0.015)
    return h, a


def ariel(rng, h, a):
    craters(rng, h, 1800, 0.002, 0.07, alb=a, rays=0.05)
    rift(rng, h, 7, 0.02, 1.4, 0.012)
    return h, a


def umbriel(rng, h, a):
    craters(rng, h, 4000, 0.002, 0.12, alb=a)
    wunda = np.arccos(np.clip(P @ np.array([1.0, 0.2, -0.1]) / np.linalg.norm([1.0, 0.2, -0.1]), -1, 1))
    a += 0.9 * np.exp(-((wunda - 0.07) / 0.02) ** 2)
    return h, a


def rubble(count, rmax, dark=0.0):
    def build(rng, h, a):
        craters(rng, h, count * 4, 0.003, rmax, alb=a, dark_floor=dark)
        return h, a
    return build


MOONS = {
    "titania": (11, 0.35, (1.00, 0.96, 0.92), titania),
    "oberon": (12, 0.31, (1.00, 0.93, 0.88), oberon),
    "miranda": (13, 0.32, (0.97, 0.98, 1.00), miranda),
    "ariel": (14, 0.39, (0.98, 0.99, 1.00), ariel),
    "umbriel": (15, 0.21, (0.97, 0.97, 1.00), umbriel),
    "proteus": (16, 0.10, (0.97, 0.96, 0.95), rubble(700, 0.30, 0.1)),
    "nereid": (17, 0.16, (0.96, 0.96, 0.96), rubble(400, 0.20)),
    "nix": (18, 0.56, (1.00, 0.95, 0.92), rubble(250, 0.25)),
    "hydra": (19, 0.83, (0.98, 0.99, 1.00), rubble(250, 0.22)),
    "kerberos": (20, 0.56, (0.97, 0.97, 0.98), rubble(200, 0.25)),
    "styx": (21, 0.65, (0.98, 0.98, 0.98), rubble(180, 0.25)),
}


def make(name, seed, albedo, tint, build):
    rng = np.random.default_rng(seed)
    h = noise(rng) * 0.02
    a = noise(rng, octaves=6, base=3) * 0.5 + noise(rng, octaves=3, base=48) * 0.12
    h, a = build(rng, h, a)
    light = shade(h)
    value = np.clip((0.18 + 0.7 * albedo) * (1 + a) * (0.7 + 0.3 * light), 0, 1)
    rgb = (value[..., None] * np.array(tint)[None, None, :] * 255).clip(0, 255).astype(np.uint8)
    Image.fromarray(rgb).save(os.path.join(OUT, f"{name}_texture.jpg"), quality=92)
    print(f"{name}: mean {value.mean():.2f}")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for moon, spec in MOONS.items():
        make(moon, *spec)
