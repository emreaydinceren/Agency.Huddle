// The /auth connect page (Remote access Spec 6.6). A plain module, no Blazor: it signs the device handshake with Ed25519
// in JavaScript (so it works on plain HTTP), shows the pairing wait, and returns to the page the Human came from.
import { buildPayloadV3, deviceIdFromPublicKey, fromBase64Url, getPublicKey, signPayload, toBase64Url } from '/auth-core.js';

const IDENTITY_KEY = 'huddle-device-identity-v1';
const DEVICE_TOKEN_KEY = 'huddle-device-token-v1';
const GATEWAY_TOKEN_KEY = 'huddle-gateway-token';
const LIMITED = ['operator.read', 'operator.write'];
const ADMIN = ['operator.admin'];
const POLL_MS = 2000;

const el = (id) => document.getElementById(id);

// The gateway password lives in memory only; the identity falls back to memory when storage is blocked.
let memoryIdentity = null;
let typedPassword = null;

function storage(kind) {
    try {
        return window[kind];
    } catch {
        return null;
    }
}

function readJson(store, key) {
    try {
        const raw = store?.getItem(key);
        return raw ? JSON.parse(raw) : null;
    } catch {
        return null;
    }
}

function writeJson(store, key, value) {
    try {
        store?.setItem(key, JSON.stringify(value));
    } catch {
        // Storage blocked or full: the page still works for this visit.
    }
}

function removeKey(store, key) {
    try {
        store?.removeItem(key);
    } catch {
        // Nothing to remove if storage is blocked.
    }
}

async function loadIdentity() {
    const saved = memoryIdentity ?? readJson(storage('localStorage'), IDENTITY_KEY);
    if (saved?.version === 1 && saved.publicKey && saved.privateKey) {
        const keyBytes = fromBase64Url(saved.publicKey);
        if (keyBytes.length === 32 && (await deviceIdFromPublicKey(keyBytes)) === saved.deviceId) {
            return saved;
        }
    }
    const seed = crypto.getRandomValues(new Uint8Array(32));
    const publicKey = await getPublicKey(seed);
    const identity = {
        version: 1,
        deviceId: await deviceIdFromPublicKey(publicKey),
        publicKey: toBase64Url(publicKey),
        privateKey: toBase64Url(seed),
        createdAtMs: Date.now(),
    };
    memoryIdentity = identity;
    writeJson(storage('localStorage'), IDENTITY_KEY, identity);
    return identity;
}

function savedDeviceToken(identity) {
    const record = readJson(storage('localStorage'), DEVICE_TOKEN_KEY);
    return record?.deviceId === identity.deviceId && record.token ? record.token : null;
}

function saveDeviceToken(identity, token, scopes) {
    writeJson(storage('localStorage'), DEVICE_TOKEN_KEY, {
        version: 1,
        deviceId: identity.deviceId,
        role: 'operator',
        scopes,
        token,
        updatedAtMs: Date.now(),
    });
}

// A return value is only ever a local path (C-14); the server checks it again and answers with the one it accepted.
function safeReturn(value) {
    const ok = typeof value === 'string' && value.startsWith('/') && !/^\/[/\\]/.test(value) && !/[\\\u0000-\u001f]/.test(value);
    return ok ? value : '/';
}

function readFragment() {
    const params = new URLSearchParams(location.hash.slice(1));
    const found = { bootstrapToken: params.get('bootstrapToken'), token: params.get('token') };
    if (location.hash) {
        history.replaceState(null, '', location.pathname + location.search);
    }
    return found;
}

function platformText() {
    const raw = navigator.userAgentData?.platform || navigator.platform || '';
    return raw.replace(/[^\x20-\x7e]/g, '').trim().toLowerCase();
}

function familyText() {
    return /Mobi|Android|iPhone|iPad/i.test(navigator.userAgent) ? 'mobile' : 'desktop';
}

function buildPayload(identity, scopes, signedAt, credential, nonce) {
    return buildPayloadV3(identity.deviceId, scopes, signedAt, credential, nonce, platformText(), familyText());
}

async function handshake(identity, auth, scopes, returnTo) {
    const started = performance.now();
    const challenge = await (await fetch('/auth/challenge', { cache: 'no-store' })).json();
    const signedAt = Math.round(challenge.ts + (performance.now() - started));
    const credential = auth.deviceToken ?? auth.token ?? auth.password ?? auth.bootstrapToken ?? '';
    const payload = buildPayload(identity, scopes, signedAt, credential, challenge.nonce);
    const signature = await signPayload(fromBase64Url(identity.privateKey), payload);
    const response = await fetch('/auth/connect?return=' + encodeURIComponent(returnTo), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            deviceId: identity.deviceId,
            publicKey: identity.publicKey,
            signature: toBase64Url(signature),
            signedAt,
            nonce: challenge.nonce,
            platform: platformText(),
            deviceFamily: familyText(),
            role: 'operator',
            scopes,
            auth,
        }),
    });
    const body = await response.json().catch(() => ({}));
    return { status: response.status, body };
}

function show(...ids) {
    for (const id of ['busy', 'form', 'waiting']) {
        el(id).hidden = !ids.includes(id);
    }
}

function showMessage(text) {
    el('message').textContent = text;
    el('message').hidden = !text;
}

function describeError(body) {
    switch (body.code) {
        case 'AUTH_TOKEN_MISMATCH': return 'That token is not right.';
        case 'AUTH_PASSWORD_MISMATCH': return 'That password is not right.';
        case 'AUTH_TOKEN_MISSING': return 'Enter the gateway token.';
        case 'AUTH_PASSWORD_MISSING': return 'Enter the password.';
        case 'AUTH_BOOTSTRAP_TOKEN_INVALID': return 'This link was already used or has expired. Ask for a new one.';
        case 'AUTH_REQUIRED': return 'This copy of Huddle only accepts connections from the computer it runs on.';
        case 'AUTH_RATE_LIMITED': return `Too many attempts. Try again in ${Math.ceil((body.retryAfterMs ?? 60000) / 60000)} min.`;
        case 'PAIRING_QUEUE_FULL': return 'Too many devices are waiting for approval. Try again later.';
        default: return `Could not connect${body.code ? ` (${body.code})` : ''}.`;
    }
}

async function main() {
    const query = new URLSearchParams(location.search);
    const returnTo = safeReturn(query.get('return'));
    const scopes = query.get('request') === 'admin' ? ADMIN : LIMITED;
    const fragment = readFragment();
    const identity = await loadIdentity();
    const status = await fetch('/auth/status', { cache: 'no-store' }).then((r) => r.json()).catch(() => ({}));
    const sessionStore = storage('sessionStorage');

    if (status.host) {
        el('where').textContent = `On ${status.host}${status.port ? ` (port ${status.port})` : ''}`;
    }
    if (status.tailscaleLogin) {
        el('tailscale').textContent = `Signed in to Tailscale as ${status.tailscaleLogin}`;
        el('tailscale').hidden = false;
    }
    if (status.authenticated && scopes === LIMITED) {
        location.replace(returnTo);
        return;
    }

    let pollTimer = null;
    let lastAuth = {};

    const succeed = async (body, usedBootstrap) => {
        if (body.deviceToken) {
            saveDeviceToken(identity, body.deviceToken, body.scopes);
        }
        // C-9: a bootstrap link that reaches a paired device is spent, so it cannot be used again from the history.
        if (fragment.bootstrapToken && savedDeviceToken(identity) && (!usedBootstrap || body.deviceToken)) {
            await fetch('/auth/bootstrap/burn', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ bootstrapToken: fragment.bootstrapToken }),
            }).catch(() => undefined);
        }
        location.replace(safeReturn(body.return));
    };

    const waitForApproval = (requestId) => {
        show('waiting');
        el('requestId').textContent = requestId;
        let checking = false;
        const check = async () => {
            if (checking) {
                return;
            }
            checking = true;
            const poll = await fetch(`/auth/pairing/${encodeURIComponent(requestId)}`, { cache: 'no-store' })
                .then((r) => r.json()).catch(() => ({}));
            if (poll.status === 'approved') {
                clearInterval(pollTimer);
                await attempt(lastAuth);
            } else if (poll.status === 'rejected' || poll.status === 'expired') {
                clearInterval(pollTimer);
                show();
                showMessage(poll.status === 'rejected'
                    ? 'This device was not approved.'
                    : 'The request expired. Reload this page to ask again.');
            }
            checking = false;
        };
        el('checkNow').onclick = check;
        clearInterval(pollTimer);
        pollTimer = setInterval(check, POLL_MS);
    };

    // Runs one handshake with one credential. Returns true when it ended the page flow (success, waiting or a final error).
    const attempt = async (auth) => {
        lastAuth = auth;
        show('busy');
        showMessage('');
        const { status: code, body } = await handshake(identity, auth, scopes, returnTo).catch(() => ({ status: 0, body: {} }));
        if (code === 200 && body.code === 'PAIRING_REQUIRED') {
            waitForApproval(body.requestId);
        } else if (code === 200) {
            await succeed(body, auth.bootstrapToken !== undefined);
        } else if (body.code === 'AUTH_DEVICE_TOKEN_MISMATCH') {
            removeKey(storage('localStorage'), DEVICE_TOKEN_KEY);
            return false;
        } else {
            removeKey(sessionStore, GATEWAY_TOKEN_KEY);
            showFailure(body);
        }
        return true;
    };

    const showFailure = (body) => {
        const secretNeeded = ['AUTH_TOKEN_MISMATCH', 'AUTH_PASSWORD_MISMATCH', 'AUTH_TOKEN_MISSING', 'AUTH_PASSWORD_MISSING'];
        show(secretNeeded.includes(body.code) ? 'form' : undefined);
        showMessage(describeError(body));
    };

    const askForSecret = () => {
        const passwordMode = status.mode === 'password';
        el('label').textContent = passwordMode ? 'Password' : 'Gateway token';
        el('form').onsubmit = async (event) => {
            event.preventDefault();
            const value = el('secret').value.trim();
            if (!value) {
                return;
            }
            if (passwordMode) {
                typedPassword = value;
                await attempt({ password: typedPassword });
            } else {
                writeJson(sessionStore, GATEWAY_TOKEN_KEY, value);
                await attempt({ token: value });
            }
        };
        show('form');
        el('secret').focus();
    };

    // The credential order of Spec 6.5: this device's token, then a gateway token, then a bootstrap link.
    const candidates = [];
    const deviceToken = savedDeviceToken(identity);
    if (deviceToken) {
        candidates.push({ deviceToken });
    }
    const gatewayToken = fragment.token ?? readJson(sessionStore, GATEWAY_TOKEN_KEY);
    if (gatewayToken) {
        candidates.push({ token: gatewayToken });
    }
    if (fragment.bootstrapToken) {
        candidates.push({ bootstrapToken: fragment.bootstrapToken });
    }
    if (status.tailscaleLogin || status.mode === 'none' || status.mode === 'trusted-proxy') {
        candidates.push({});
    }

    for (const auth of candidates) {
        if (await attempt(auth)) {
            return;
        }
    }
    askForSecret();
}

main().catch(() => {
    show();
    showMessage('Could not start the connect page. Reload to try again.');
});
