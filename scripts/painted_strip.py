"""Straighten a painted river strip from rhYcivtextures into a rectangle.

The painted strips (`riverwide4.png` and friends) are a straight channel with
its banks, painted in the game's isometric view on a magenta matte. This finds
the strip's axis in *ground* space -- the view unsquashed, (x, 2y) -- and
resamples it into a rectangle: u runs along the channel, v across it.
"""

from __future__ import annotations

from pathlib import Path

import numpy as np

import isotile


def bilinear(image: np.ndarray, x: np.ndarray, y: np.ndarray) -> np.ndarray:
    """Sample ``image`` (H, W[, C]) at float pixel coordinates, edge-clamped."""
    height, width = image.shape[:2]
    x = np.clip(x - 0.5, 0, width - 1.001)
    y = np.clip(y - 0.5, 0, height - 1.001)
    x0 = np.floor(x).astype(np.int64)
    y0 = np.floor(y).astype(np.int64)
    fx = x - x0
    fy = y - y0
    if image.ndim == 3:
        fx = fx[..., None]
        fy = fy[..., None]
    top = image[y0, x0] * (1 - fx) + image[y0, x0 + 1] * fx
    bottom = image[y0 + 1, x0] * (1 - fx) + image[y0 + 1, x0 + 1] * fx
    return top * (1 - fy) + bottom * fy


class Strip:
    """A painted strip, measured: centre, axis and normal in ground space."""

    def __init__(self, path: Path):
        self.rgb, self.alpha = isotile.key_matte(path)
        ys, xs = np.nonzero(self.alpha > 0.5)
        ground = np.stack([xs + 0.5, (ys + 0.5) * 2.0], axis=1)
        self.centre = ground.mean(axis=0)
        _, _, vt = np.linalg.svd(ground - self.centre, full_matrices=False)
        self.axis = vt[0]
        self.normal = vt[1]
        along = (ground - self.centre) @ self.axis
        self.u_range = (float(along.min()), float(along.max()))

    def sample(self, u: np.ndarray, v: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
        """Colour and alpha at strip coordinates (u along, v across), ground px."""
        gx = self.centre[0] + u * self.axis[0] + v * self.normal[0]
        gy = self.centre[1] + u * self.axis[1] + v * self.normal[1]
        return bilinear(self.rgb, gx, gy / 2.0), bilinear(self.alpha, gx, gy / 2.0)

    def rectified(self, u0: float, length: float, half_width: float,
                  nu: int, nv: int) -> tuple[np.ndarray, np.ndarray]:
        """The strip resampled to an (nv, nu) rectangle, v from -half to +half."""
        u = u0 + (np.arange(nu) + 0.5) / nu * length
        v = -half_width + (np.arange(nv) + 0.5) / nv * 2.0 * half_width
        uu, vv = np.meshgrid(u, v)
        return self.sample(uu, vv)
