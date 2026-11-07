"""Paint the seamless optical cover of the cloud bank over the fells."""
from pathlib import Path

import numpy as np
from PIL import Image


def field(u, v, nx, ny, seed):
    grid = np.random.default_rng(seed).uniform(-1, 1, (ny, nx))
    x, y = u * nx, v * ny
    ix, iy = np.floor(x).astype(int), np.floor(y).astype(int)
    tx, ty = x - ix, y - iy
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    lower = (1 - tx) * grid[iy % ny, ix % nx] + tx * grid[iy % ny, (ix + 1) % nx]
    upper = (1 - tx) * grid[(iy + 1) % ny, ix % nx] + tx * grid[(iy + 1) % ny, (ix + 1) % nx]
    return lower * (1 - ty) + upper * ty


def paint(size=512):
    v, u = (np.indices((size, size)) + .5) / size
    # One broad bank crosses the low moon per traverse. Eddies displace the
    # leading and trailing fronts into ragged sheets and bands of finer vapour.
    curl = field(u, v, 3, 4, 35) + .42 * field(u, v, 8, 7, 36)
    x = u + .076 * curl + .018 * field(u, v, 19, 15, 37)
    y = v + .125 * field(u, v, 5, 4, 38)
    front = -.48 * np.cos(2 * np.pi * (x + .20 * np.sin(2 * np.pi * (v - .375))))
    front += .34 * field(x, y, 5, 5, 39) + .15 * field(x, y, 13, 16, 40)
    front += .09 * field(x, y, 27, 47, 41) + .045 * field(x, y, 78, 119, 42)
    dense = np.clip((front + .38) / .74, 0, 1)
    opacity = dense * dense * (3 - 2 * dense)
    # Image files begin at the top; Unity texture UVs begin at the bottom.
    return Image.fromarray(np.flipud(np.rint(255 * opacity).astype(np.uint8)))


if __name__ == "__main__":
    root = Path(__file__).resolve().parents[3]
    paint().save(root / "Assets/Transperceneige/Pass/Scenery/Drifting cloud density.png", optimize=True)
