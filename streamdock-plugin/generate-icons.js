const { PNG } = require('pngjs');
const fs = require('fs');
const path = require('path');

function createIcon(w, h, drawFn) {
  const png = new PNG({ width: w, height: h });
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const idx = (y * w + x) * 4;
      const [r, g, b, a] = drawFn(x, y, w, h);
      png.data[idx] = r;
      png.data[idx + 1] = g;
      png.data[idx + 2] = b;
      png.data[idx + 3] = a;
    }
  }
  return PNG.sync.write(png);
}

function inCircle(x, y, cx, cy, r) {
  return Math.sqrt((x - cx) ** 2 + (y - cy) ** 2) <= r;
}

function inRoundRect(x, y, rx, ry, rw, rh, rad) {
  if (x < rx || x >= rx + rw || y < ry || y >= ry + rh) return false;
  if (x < rx + rad && y < ry + rad && !inCircle(x, y, rx + rad, ry + rad, rad)) return false;
  if (x >= rx + rw - rad && y < ry + rad && !inCircle(x, y, rx + rw - rad, ry + rad, rad)) return false;
  if (x < rx + rad && y >= ry + rh - rad && !inCircle(x, y, rx + rad, ry + rh - rad, rad)) return false;
  if (x >= rx + rw - rad && y >= ry + rh - rad && !inCircle(x, y, rx + rw - rad, ry + rh - rad, rad)) return false;
  return true;
}

function hLine(x, y, w, h, ypos, thickness) {
  return y >= ypos && y < ypos + thickness && x >= 4 && x < w - 4;
}

const dir = __dirname;

// Plugin icon 128x128: dark bg, blue circle with "M"
function pluginIcon(x, y, w, h) {
  if (inRoundRect(x, y, 0, 0, w, h, 12)) return [28, 28, 28, 255];
  if (inCircle(x, y, w/2, h/2, 36)) return [0, 120, 212, 255];
  // Letter M (simplified)
  if ((inCircle(x, y, w/2 - 10, h/2 - 6, 12) || inCircle(x, y, w/2, h/2 - 6, 12) || inCircle(x, y, w/2 + 10, h/2 - 6, 12))) return [255, 255, 255, 255];
  if (x >= w/2 - 14 && x < w/2 + 14 && y >= h/2 + 2 && y < h/2 + 14 && Math.abs(x - w/2) > 8) return [255, 255, 255, 255];
  return [0, 120, 212, 255];
}
fs.writeFileSync(path.join(dir, 'images', 'plugin-icon.png'), createIcon(128, 128, pluginIcon));

// Category icon 48x48: blue square
function catIcon(x, y, w, h) {
  if (inRoundRect(x, y, 4, 4, w - 8, h - 8, 8)) return [0, 120, 212, 255];
  return [28, 28, 28, 255];
}
fs.writeFileSync(path.join(dir, 'images', 'plugin-category.png'), createIcon(48, 48, catIcon));

// Channel action 40x40: blue circle
function chIcon(x, y, w, h) {
  if (inCircle(x, y, w/2, h/2, 14)) return [0, 120, 212, 255];
  return [40, 40, 40, 255];
}
fs.writeFileSync(path.join(dir, 'images', 'action-channel.png'), createIcon(40, 40, chIcon));

// Mix action 40x40: blue rounded square
function mxIcon(x, y, w, h) {
  if (inRoundRect(x, y, w/2 - 12, h/2 - 12, 24, 24, 5)) return [0, 120, 212, 255];
  return [40, 40, 40, 255];
}
fs.writeFileSync(path.join(dir, 'images', 'action-mix.png'), createIcon(40, 40, mxIcon));

console.log('Icons generated.');
