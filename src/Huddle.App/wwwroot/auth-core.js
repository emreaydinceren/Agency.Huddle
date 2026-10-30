// The pure part of the /auth device handshake (Remote access Spec 6.6): the v3 payload, the device id and the Ed25519
// signature. No DOM and no fetch, so a test can run it under Node and compare it byte for byte with the server's C#.
import * as ed from './lib/noble/ed25519/index.js';
import { sha256, sha512 } from './lib/noble/hashes/sha2.js';

// @noble/ed25519 asks the caller for SHA-512; crypto.subtle is missing on plain HTTP, so use noble's.
ed.etc.sha512Sync = (...parts) => sha512(ed.etc.concatBytes(...parts));
ed.etc.sha512Async = (...parts) => Promise.resolve(ed.etc.sha512Sync(...parts));

const encoder = new TextEncoder();

export function toBase64Url(bytes) {
    let text = '';
    for (const byte of bytes) {
        text += String.fromCharCode(byte);
    }
    return btoa(text).replaceAll('+', '-').replaceAll('/', '_').replaceAll('=', '');
}

export function fromBase64Url(text) {
    const padded = text.replaceAll('-', '+').replaceAll('_', '/').padEnd(Math.ceil(text.length / 4) * 4, '=');
    return Uint8Array.from(atob(padded), (character) => character.charCodeAt(0));
}

// The device id of a public key: the lowercase hex SHA-256 of its 32 bytes.
export async function deviceIdFromPublicKey(bytes) {
    const digest = globalThis.crypto?.subtle ? new Uint8Array(await crypto.subtle.digest('SHA-256', bytes)) : sha256(bytes);
    return Array.from(digest, (byte) => byte.toString(16).padStart(2, '0')).join('');
}

export function getPublicKey(seed) {
    return ed.getPublicKeyAsync(seed);
}

// Builds the v3 payload exactly as the server's DeviceAuth.BuildPayloadV3 does.
export function buildPayloadV3(deviceId, scopes, signedAt, credential, nonce, platform, deviceFamily) {
    return ['v3', deviceId, 'huddle-web', 'webchat', 'operator', [...scopes].sort().join(','),
        String(signedAt), credential, nonce, platform.trim().toLowerCase(), deviceFamily.trim().toLowerCase()].join('|');
}

// Signs the UTF-8 payload with the 32-byte seed; returns the 64 signature bytes.
export function signPayload(seed, payload) {
    return ed.signAsync(encoder.encode(payload), seed);
}
