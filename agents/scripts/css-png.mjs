// Minimal PNG decode, encode and pixel diff for css-snapshot.mjs (Node built-ins only: zlib, no npm packages).
// Decodes 8-bit grey, grey+alpha, RGB and RGBA non-interlaced PNGs, which is what headless Chrome writes.

import { crc32, deflateSync, inflateSync } from 'node:zlib';

const SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
const CHANNELS = { 0: 1, 2: 3, 4: 2, 6: 4 };

/** Decodes a PNG buffer to { width, height, data } where data is RGBA, 4 bytes per pixel. */
export function decodePng(buffer) {
  if (buffer.length < 8 || !buffer.subarray(0, 8).equals(SIGNATURE)) { throw new Error('not a PNG file'); }
  let width = 0;
  let height = 0;
  let depth = 0;
  let colorType = 0;
  let interlace = 0;
  const idat = [];
  for (let at = 8; at + 8 <= buffer.length;) {
    const length = buffer.readUInt32BE(at);
    const type = buffer.toString('latin1', at + 4, at + 8);
    const body = buffer.subarray(at + 8, at + 8 + length);
    if (type === 'IHDR') {
      width = body.readUInt32BE(0); height = body.readUInt32BE(4); depth = body[8]; colorType = body[9]; interlace = body[12];
    } else if (type === 'IDAT') {
      idat.push(body);
    } else if (type === 'IEND') {
      break;
    }
    at += 12 + length;
  }
  const channels = CHANNELS[colorType];
  if (!channels || depth !== 8 || interlace !== 0) { throw new Error(`unsupported PNG (colour type ${colorType}, depth ${depth}, interlace ${interlace})`); }
  const raw = inflateSync(Buffer.concat(idat));
  const stride = width * channels;
  if (raw.length < (stride + 1) * height) { throw new Error('truncated PNG data'); }
  const pixels = Buffer.alloc(stride * height);
  for (let y = 0; y < height; y++) {
    const filter = raw[y * (stride + 1)];
    const src = y * (stride + 1) + 1;
    const dst = y * stride;
    for (let x = 0; x < stride; x++) {
      const left = x >= channels ? pixels[dst + x - channels] : 0;
      const up = y > 0 ? pixels[dst - stride + x] : 0;
      const upLeft = y > 0 && x >= channels ? pixels[dst - stride + x - channels] : 0;
      let predictor = 0;
      if (filter === 1) { predictor = left; }
      else if (filter === 2) { predictor = up; }
      else if (filter === 3) { predictor = (left + up) >> 1; }
      else if (filter === 4) {
        const estimate = left + up - upLeft;
        const dl = Math.abs(estimate - left);
        const du = Math.abs(estimate - up);
        const dul = Math.abs(estimate - upLeft);
        predictor = dl <= du && dl <= dul ? left : (du <= dul ? up : upLeft);
      }
      pixels[dst + x] = (raw[src + x] + predictor) & 255;
    }
  }
  const data = Buffer.alloc(width * height * 4);
  for (let i = 0; i < width * height; i++) {
    const s = i * channels;
    if (colorType === 6) { data[i * 4] = pixels[s]; data[i * 4 + 1] = pixels[s + 1]; data[i * 4 + 2] = pixels[s + 2]; data[i * 4 + 3] = pixels[s + 3]; }
    else if (colorType === 2) { data[i * 4] = pixels[s]; data[i * 4 + 1] = pixels[s + 1]; data[i * 4 + 2] = pixels[s + 2]; data[i * 4 + 3] = 255; }
    else { data[i * 4] = data[i * 4 + 1] = data[i * 4 + 2] = pixels[s]; data[i * 4 + 3] = channels === 2 ? pixels[s + 1] : 255; }
  }
  return { width, height, data };
}

function chunk(type, body) {
  const head = Buffer.alloc(8);
  head.writeUInt32BE(body.length, 0);
  head.write(type, 4, 'latin1');
  const tail = Buffer.alloc(4);
  tail.writeUInt32BE(crc32(Buffer.concat([head.subarray(4), body])) >>> 0, 0);
  return Buffer.concat([head, body, tail]);
}

/** Encodes { width, height, data (RGBA) } as an 8-bit RGBA PNG. */
export function encodePng({ width, height, data }) {
  const header = Buffer.alloc(13);
  header.writeUInt32BE(width, 0); header.writeUInt32BE(height, 4); header[8] = 8; header[9] = 6;
  const stride = width * 4;
  const raw = Buffer.alloc((stride + 1) * height);
  for (let y = 0; y < height; y++) { data.copy(raw, y * (stride + 1) + 1, y * stride, (y + 1) * stride); }
  return Buffer.concat([SIGNATURE, chunk('IHDR', header), chunk('IDAT', deflateSync(raw, { level: 6 })), chunk('IEND', Buffer.alloc(0))]);
}

/**
 * Compares two decoded images (the canvas is the larger of the two; pixels outside the smaller one count as changed).
 * Returns { changed, total, percent, box, image } where image is the "after" picture faded, with every changed pixel
 * solid magenta and a pale halo around it so a one-pixel change can be seen.
 */
export function diffImages(before, after) {
  const width = Math.max(before.width, after.width);
  const height = Math.max(before.height, after.height);
  const out = Buffer.alloc(width * height * 4);
  const mask = new Uint8Array(width * height);
  let changed = 0;
  let minX = width;
  let minY = height;
  let maxX = -1;
  let maxY = -1;
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const i = y * width + x;
      const inB = x < before.width && y < before.height;
      const inA = x < after.width && y < after.height;
      const bi = inB ? (y * before.width + x) * 4 : -1;
      const ai = inA ? (y * after.width + x) * 4 : -1;
      let differs = inA !== inB;
      if (!differs && inA) {
        differs = before.data[bi] !== after.data[ai] || before.data[bi + 1] !== after.data[ai + 1]
          || before.data[bi + 2] !== after.data[ai + 2] || before.data[bi + 3] !== after.data[ai + 3];
      }
      const o = i * 4;
      if (inA) {
        out[o] = 255 - ((255 - after.data[ai]) >> 2); out[o + 1] = 255 - ((255 - after.data[ai + 1]) >> 2); out[o + 2] = 255 - ((255 - after.data[ai + 2]) >> 2);
      } else { out[o] = out[o + 1] = out[o + 2] = 235; }
      out[o + 3] = 255;
      if (differs) {
        mask[i] = 1; changed++;
        if (x < minX) { minX = x; }
        if (x > maxX) { maxX = x; }
        if (y < minY) { minY = y; }
        if (y > maxY) { maxY = y; }
      }
    }
  }
  if (changed > 0 && changed < width * height / 4) {
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        if (!mask[y * width + x]) { continue; }
        for (let dy = -2; dy <= 2; dy++) {
          for (let dx = -2; dx <= 2; dx++) {
            const nx = x + dx;
            const ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= width || ny >= height || mask[ny * width + nx]) { continue; }
            const o = (ny * width + nx) * 4;
            out[o] = 255; out[o + 1] = 205; out[o + 2] = 225;
          }
        }
      }
    }
  }
  for (let i = 0; i < mask.length; i++) { if (mask[i]) { out[i * 4] = 230; out[i * 4 + 1] = 0; out[i * 4 + 2] = 140; } }
  return {
    changed, total: width * height, percent: (changed * 100) / (width * height),
    box: changed ? { x: minX, y: minY, width: maxX - minX + 1, height: maxY - minY + 1 } : null,
    image: { width, height, data: out },
  };
}
