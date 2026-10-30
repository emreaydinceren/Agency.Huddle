using System.Buffers.Text;
using System.Security.Cryptography;

namespace Agency.Huddle.App.Gateway;

/// <summary>Pairing requests and their resolution (Spec 6.7, 8.4): in memory, five minutes, at most ten pending.</summary>
internal sealed class PairingService
{
    /// <summary>How long a request stays open.</summary>
    public static readonly TimeSpan RequestLifetime = TimeSpan.FromMinutes(5);

    /// <summary>The most pending requests at once.</summary>
    public const int MaxPending = 10;

    private static readonly TimeSpan ResolvedKeptFor = TimeSpan.FromMinutes(10);

    private readonly DeviceRegistry devices;
    private readonly GatewayFeed? feed;
    private readonly TimeProvider timeProvider;
    private readonly List<Slot> slots = [];
    private readonly Dictionary<string, string> pendingTokens = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>Creates the service over a registry.</summary>
    /// <param name="devices">The registry an approval pairs into.</param>
    /// <param name="timeProvider">Defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="feed">Tells the Feed about requests; <see langword="null"/> for none.</param>
    public PairingService(DeviceRegistry devices, TimeProvider? timeProvider = null, GatewayFeed? feed = null)
    {
        ArgumentNullException.ThrowIfNull(devices);

        this.feed = feed;
        this.devices = devices;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>The state <c>GET /auth/pairing/{id}</c> reports.</summary>
    public enum State
    {
        /// <summary>Waiting for an approver.</summary>
        Pending,

        /// <summary>Approved; the device must handshake again.</summary>
        Approved,

        /// <summary>Rejected; no ban follows.</summary>
        Rejected,

        /// <summary>Lapsed, superseded or unknown.</summary>
        Expired,
    }

    /// <summary>Why an approval did or did not happen.</summary>
    public enum ApprovalOutcome
    {
        /// <summary>The device is paired with the granted scopes.</summary>
        Approved,

        /// <summary>No such pending request.</summary>
        NotFound,

        /// <summary>The approver lacks <c>operator.pairing</c> or a scope being granted.</summary>
        ScopeNotHeld,

        /// <summary>Admin was to be granted and the approver is not an admin.</summary>
        AdminRequired,
    }

    private enum Resolution
    {
        Open,
        Approved,
        Rejected,
        Superseded,
    }

    /// <summary>Opens a request, or returns the identical pending one; supersedes a pending one with different scopes.</summary>
    /// <param name="candidate">The device and what it asks for.</param>
    /// <returns>The request, or <see langword="null"/> when <see cref="MaxPending"/> requests are already pending.</returns>
    public PairingRequest? Open(Candidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        IReadOnlyList<string> scopes = GatewayScopes.Normalize(candidate.Scopes);
        lock (this.gate)
        {
            this.AnnounceExpired(now);
            this.Forget(now);
            Slot? same = this.slots.Find(slot => IsPending(slot, now) && string.Equals(slot.Request.DeviceId, candidate.DeviceId, StringComparison.Ordinal));
            if (same is not null)
            {
                if (same.Request.Scopes.SequenceEqual(scopes, StringComparer.Ordinal))
                {
                    return same.Request;
                }

                same.Resolution = Resolution.Superseded;
                this.feed?.RequestEnded(same.Request);
            }
            else if (this.slots.Count(slot => IsPending(slot, now)) >= MaxPending)
            {
                return null;
            }

            PairingRequest request = new()
            {
                RequestId = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32)),
                DeviceId = candidate.DeviceId,
                PublicKey = candidate.PublicKey,
                Scopes = scopes,
                Name = candidate.Name,
                Address = candidate.Address,
                UserAgent = candidate.UserAgent,
                Ingress = candidate.Ingress,
                TailscaleLogin = candidate.TailscaleLogin,
                CreatedAt = now,
                ExpiresAt = now + RequestLifetime,
            };
            this.slots.Add(new Slot(request));
            this.feed?.RequestOpened(request);
            return request;
        }
    }

    /// <summary>The state of a request id.</summary>
    /// <param name="requestId">The request id.</param>
    public State StateOf(string requestId)
    {
        ArgumentNullException.ThrowIfNull(requestId);

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            this.AnnounceExpired(now);
            Slot? slot = this.slots.Find(candidate => string.Equals(candidate.Request.RequestId, requestId, StringComparison.Ordinal));
            return slot?.Resolution switch
            {
                Resolution.Open => now < slot.Request.ExpiresAt ? State.Pending : State.Expired,
                Resolution.Approved => State.Approved,
                Resolution.Rejected => State.Rejected,
                _ => State.Expired,
            };
        }
    }

    /// <summary>The pending requests, oldest first.</summary>
    public IReadOnlyList<PairingRequest> Pending()
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            this.AnnounceExpired(now);
            return [.. this.slots.Where(slot => IsPending(slot, now)).Select(slot => slot.Request)];
        }
    }

    /// <summary>
    /// Approves a request: pairs the device with <paramref name="grantedScopes"/> (default: the requested scopes), via <c>approval</c>.
    /// The approver needs <c>operator.pairing</c> and every scope it grants; granting admin needs admin.
    /// </summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="approverScopes">The approver's scopes.</param>
    /// <param name="grantedScopes">The scopes to grant, or <see langword="null"/> for the requested ones.</param>
    public ApprovalResult Approve(string requestId, IReadOnlyCollection<string> approverScopes, IReadOnlyList<string>? grantedScopes = null)
    {
        ArgumentNullException.ThrowIfNull(requestId);
        ArgumentNullException.ThrowIfNull(approverScopes);

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            this.AnnounceExpired(now);
            Slot? slot = this.FindPending(requestId, now);
            if (slot is null)
            {
                return new ApprovalResult(ApprovalOutcome.NotFound, null);
            }

            PairingRequest request = slot.Request;
            IReadOnlyList<string> granted = grantedScopes is { Count: > 0 } ? GatewayScopes.Normalize(grantedScopes) : request.Scopes;
            ApprovalOutcome? refusal = Refusal(approverScopes, granted);
            if (refusal is { } refused)
            {
                return new ApprovalResult(refused, null);
            }

            string? token = this.devices.Pair(new DeviceRegistry.Registration(
                request.DeviceId,
                request.PublicKey,
                request.Name,
                granted,
                "approval",
                request.TailscaleLogin,
                request.Address,
                request.UserAgent));
            if (token is not null)
            {
                this.pendingTokens[request.DeviceId] = token;
            }

            slot.Resolution = Resolution.Approved;
            this.feed?.RequestEnded(request);
            return new ApprovalResult(ApprovalOutcome.Approved, this.devices.Find(request.DeviceId));
        }
    }

    /// <summary>Rejects a request. Needs <c>operator.pairing</c>. No ban: the device may open a new request.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="approverScopes">The approver's scopes.</param>
    /// <returns>Whether a pending request was rejected.</returns>
    public bool Reject(string requestId, IReadOnlyCollection<string> approverScopes)
    {
        ArgumentNullException.ThrowIfNull(requestId);
        ArgumentNullException.ThrowIfNull(approverScopes);

        if (!GatewayScopes.Holds(approverScopes, GatewayScopes.Pairing))
        {
            return false;
        }

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            this.AnnounceExpired(now);
            Slot? slot = this.FindPending(requestId, now);
            if (slot is null)
            {
                return false;
            }

            slot.Resolution = Resolution.Rejected;
            this.feed?.RequestEnded(slot.Request);
            return true;
        }
    }

    /// <summary>
    /// Hands over, once, the device token an approval created for a device that had none; the next handshake of the approved device returns it.
    /// </summary>
    /// <param name="deviceId">The device id.</param>
    /// <returns>The token, or <see langword="null"/> when none is waiting.</returns>
    public string? TakePendingToken(string deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        lock (this.gate)
        {
            return this.pendingTokens.Remove(deviceId, out string? token) ? token : null;
        }
    }

    /// <summary>Rotates a device's token and holds the new one for the device's next successful connect, as an approval does (R-2).</summary>
    /// <param name="deviceId">The device id.</param>
    /// <returns>The new token, or <see langword="null"/> when the device is unknown.</returns>
    public string? Rotate(string deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        lock (this.gate)
        {
            string? token = this.devices.Rotate(deviceId);
            if (token is not null)
            {
                this.pendingTokens[deviceId] = token;
            }

            return token;
        }
    }

    private static bool IsPending(Slot slot, DateTimeOffset now) => slot.Resolution == Resolution.Open && now < slot.Request.ExpiresAt;

    private static ApprovalOutcome? Refusal(IReadOnlyCollection<string> approverScopes, IReadOnlyList<string> granted)
    {
        if (!GatewayScopes.Holds(approverScopes, GatewayScopes.Pairing))
        {
            return ApprovalOutcome.ScopeNotHeld;
        }

        if (granted.Contains(GatewayScopes.Admin, StringComparer.Ordinal) && !approverScopes.Contains(GatewayScopes.Admin, StringComparer.Ordinal))
        {
            return ApprovalOutcome.AdminRequired;
        }

        return GatewayScopes.HoldsAll(approverScopes, granted) ? null : ApprovalOutcome.ScopeNotHeld;
    }

    /// <summary>Finds an open, unexpired request. Called under the lock.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="now">The current time.</param>
    private Slot? FindPending(string requestId, DateTimeOffset now)
        => this.slots.Find(slot => IsPending(slot, now) && string.Equals(slot.Request.RequestId, requestId, StringComparison.Ordinal));

    /// <summary>Resolves, once, the Feed item of every request that lapsed unanswered. Called under the lock, from every look at the queue.</summary>
    /// <param name="now">The current time.</param>
    private void AnnounceExpired(DateTimeOffset now)
    {
        foreach (Slot slot in this.slots.Where(slot => slot.Resolution == Resolution.Open && !slot.ExpiryAnnounced && now >= slot.Request.ExpiresAt))
        {
            slot.ExpiryAnnounced = true;
            this.feed?.RequestEnded(slot.Request);
        }
    }

    /// <summary>Forgets requests long past their end, so the list stays small. Called under the lock.</summary>
    /// <param name="now">The current time.</param>
    private void Forget(DateTimeOffset now) => _ = this.slots.RemoveAll(slot => now - slot.Request.ExpiresAt > ResolvedKeptFor);

    /// <summary>The device and what it asks for.</summary>
    /// <param name="DeviceId">The device id.</param>
    /// <param name="PublicKey">The 32 key bytes.</param>
    /// <param name="Scopes">The scopes asked for.</param>
    /// <param name="Name">The device name.</param>
    /// <param name="Address">The client address.</param>
    /// <param name="UserAgent">The user agent.</param>
    /// <param name="Ingress">The ingress kind, as text.</param>
    /// <param name="TailscaleLogin">The Tailscale login, if any.</param>
    public sealed record Candidate(
        string DeviceId,
        byte[] PublicKey,
        IReadOnlyList<string> Scopes,
        string Name,
        string Address,
        string? UserAgent,
        string Ingress,
        string? TailscaleLogin);

    /// <summary>The result of <see cref="Approve"/>.</summary>
    /// <param name="Outcome">What happened.</param>
    /// <param name="Device">The paired device when approved.</param>
    public sealed record ApprovalResult(ApprovalOutcome Outcome, DeviceRecord? Device);

    private sealed class Slot(PairingRequest request)
    {
        public PairingRequest Request { get; } = request;

        public Resolution Resolution { get; set; } = Resolution.Open;

        public bool ExpiryAnnounced { get; set; }
    }
}
