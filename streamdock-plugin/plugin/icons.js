const SIZE = 112;

function encodeSvg(svg) {
  return 'data:image/svg+xml;charset=utf8,' + encodeURIComponent(svg);
}

function drawChannelKey(name, volume, muted) {
  const vol = Math.round(volume);
  const bg = muted ? '#D93128' : '#282828';
  const barFill = muted ? 0 : (vol / 100) * (SIZE - 24);

  return encodeSvg(`<svg xmlns="http://www.w3.org/2000/svg" width="${SIZE}" height="${SIZE}">
  <rect width="${SIZE}" height="${SIZE}" fill="${bg}" rx="8"/>
  <text x="${SIZE/2}" y="22" text-anchor="middle" font-family="Segoe UI,Arial,sans-serif" font-size="13" font-weight="bold" fill="${muted ? '#FFF' : '#999'}">${esc(name)}</text>
  <text x="${SIZE/2}" y="${muted ? SIZE/2 + 10 : SIZE/2 + 5}" text-anchor="middle" font-family="Segoe UI,Arial,sans-serif" font-size="32" font-weight="bold" fill="#FFF">${muted ? 'MUTED' : vol + '%'}</text>
  <rect x="12" y="${SIZE - 16}" width="${SIZE - 24}" height="6" rx="3" fill="#3A3A3A"/>
  <rect x="12" y="${SIZE - 16}" width="${barFill}" height="6" rx="3" fill="#60CDFF"/>
</svg>`);
}

function drawMixKey(name, volume, muted) {
  const vol = Math.round(volume);
  const bg = muted ? '#D93128' : '#282828';
  const barFill = muted ? 0 : (vol / 100) * (SIZE - 24);

  return encodeSvg(`<svg xmlns="http://www.w3.org/2000/svg" width="${SIZE}" height="${SIZE}">
  <rect width="${SIZE}" height="${SIZE}" fill="${bg}" rx="8"/>
  <text x="${SIZE/2}" y="18" text-anchor="middle" font-family="Segoe UI,Arial,sans-serif" font-size="10" font-weight="bold" fill="${muted ? '#FFF' : '#60CDFF'}">MIX</text>
  <text x="${SIZE/2}" y="34" text-anchor="middle" font-family="Segoe UI,Arial,sans-serif" font-size="13" font-weight="bold" fill="${muted ? '#FFF' : '#999'}">${esc(name)}</text>
  <text x="${SIZE/2}" y="${muted ? SIZE/2 + 8 : SIZE/2 + 5}" text-anchor="middle" font-family="Segoe UI,Arial,sans-serif" font-size="32" font-weight="bold" fill="#FFF">${muted ? 'MUTED' : vol + '%'}</text>
  <rect x="12" y="${SIZE - 16}" width="${SIZE - 24}" height="6" rx="3" fill="#3A3A3A"/>
  <rect x="12" y="${SIZE - 16}" width="${barFill}" height="6" rx="3" fill="#60CDFF"/>
</svg>`);
}

function drawUnrouted(name) {
  return encodeSvg(`<svg xmlns="http://www.w3.org/2000/svg" width="${SIZE}" height="${SIZE}">
  <rect width="${SIZE}" height="${SIZE}" fill="#1C1C1C" rx="8"/>
  <text x="${SIZE/2}" y="24" text-anchor="middle" font-family="Segoe UI,Arial,sans-serif" font-size="12" font-weight="bold" fill="#555">${esc(name)}</text>
  <text x="${SIZE/2}" y="${SIZE/2}" text-anchor="middle" font-family="Segoe UI,Arial,sans-serif" font-size="28" font-weight="bold" fill="#444">+</text>
  <text x="${SIZE/2}" y="${SIZE/2 + 24}" text-anchor="middle" font-family="Segoe UI,Arial,sans-serif" font-size="10" fill="#555">Add to mix</text>
</svg>`);
}

function esc(text) {
  if (!text) return '';
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

module.exports = { drawChannelKey, drawMixKey, drawUnrouted };
