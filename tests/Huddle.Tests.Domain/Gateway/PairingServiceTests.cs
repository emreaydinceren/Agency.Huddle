using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.7 and 8.4: pairing requests open, supersede, expire, cap at ten, and resolve within the approver's scopes.</summary>
public sealed class PairingServiceTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private static readonly string[] AdminApprover = [GatewayScopes.Admin];

    private static readonly string[] PairingApprover = [GatewayScopes.Pairing, GatewayScopes.Read, GatewayScopes.Write];

    private readonly TempDataDir dir = new();
    private readonly FakeTimeProvider clock = new(Start);
    private readonly DeviceRegistry devices;
    private readonly PairingService service;

    /// <summary>Creates a service over a fresh registry.</summary>
    public PairingServiceTests()
    {
        this.devices = new DeviceRegistry(this.dir.Path, this.clock);
        this.service = new PairingService(this.devices, this.clock);
    }

    private static PairingService.Candidate Candidate(TestDevice device, params string[] scopes)
        => new(device.DeviceId, device.PublicKey, scopes.Length == 0 ? [GatewayScopes.Read, GatewayScopes.Write] : scopes, "Windows Desktop", "203.0.113.9", "ua/1", "Direct", null);

    /// <summary>Opens a request that the test expects to be accepted, failing the test when the service returns none.</summary>
    private PairingRequest OpenRequired(TestDevice device, params string[] scopes)
    {
        PairingRequest? request = this.service.Open(Candidate(device, scopes));
        Assert.NotNull(request);
        return request;
    }

    /// <summary>Returns the scopes of a paired device, failing the test when the device is not paired.</summary>
    private IReadOnlyCollection<string> ScopesOf(string deviceId)
    {
        DeviceRecord? record = this.devices.Find(deviceId);
        Assert.NotNull(record);
        return record.Scopes;
    }

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();

    /// <summary>Opening a request records the device, scopes and name, a random id, and a five minute expiry.</summary>
    [Fact]
    public void Open_RecordsTheRequest()
    {
        TestDevice device = new();

        PairingRequest? request = this.service.Open(Candidate(device));

        Assert.NotNull(request);
        Assert.Equal(device.DeviceId, request.DeviceId);
        Assert.Equal([GatewayScopes.Read, GatewayScopes.Write], request.Scopes);
        Assert.Equal("Windows Desktop", request.Name);
        Assert.Equal(Start.AddMinutes(5), request.ExpiresAt);
        Assert.False(string.IsNullOrEmpty(request.RequestId));
        Assert.Equal(PairingService.State.Pending, this.service.StateOf(request.RequestId));
        Assert.Single(this.service.Pending());
    }

    /// <summary>The same device asking for the same scopes again gets the same pending request, not a second one.</summary>
    [Fact]
    public void Open_SameDeviceSameScopes_ReturnsTheSamePendingRequest()
    {
        TestDevice device = new();

        PairingRequest? first = this.service.Open(Candidate(device));
        PairingRequest? second = this.service.Open(Candidate(device));

        Assert.Equal(first?.RequestId, second?.RequestId);
        Assert.Single(this.service.Pending());
    }

    /// <summary>A new handshake with different scopes supersedes the pending request with a new id; the old id reads as expired.</summary>
    [Fact]
    public void Open_DifferentScopes_SupersedesThePendingRequest()
    {
        TestDevice device = new();
        PairingRequest first = this.OpenRequired(device);

        PairingRequest? second = this.service.Open(Candidate(device, GatewayScopes.Admin));

        Assert.NotNull(second);
        Assert.NotEqual(first.RequestId, second.RequestId);
        Assert.Equal(PairingService.State.Expired, this.service.StateOf(first.RequestId));
        Assert.Equal(PairingService.State.Pending, this.service.StateOf(second.RequestId));
        Assert.Single(this.service.Pending());
    }

    /// <summary>A request lapses five minutes after it opened: pending at 4:59, expired at 5:01, and gone from the pending list.</summary>
    [Fact]
    public void Request_Expires_AfterFiveMinutes()
    {
        PairingRequest request = this.OpenRequired(new TestDevice());

        this.clock.Advance(TimeSpan.FromSeconds(299));
        Assert.Equal(PairingService.State.Pending, this.service.StateOf(request.RequestId));
        this.clock.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(PairingService.State.Expired, this.service.StateOf(request.RequestId));
        Assert.Empty(this.service.Pending());
        Assert.Equal(PairingService.ApprovalOutcome.NotFound, this.service.Approve(request.RequestId, AdminApprover).Outcome);
    }

    /// <summary>An unknown request id reads as expired.</summary>
    [Fact]
    public void StateOf_UnknownId_IsExpired()
    {
        Assert.Equal(PairingService.State.Expired, this.service.StateOf("nope"));
    }

    /// <summary>At most ten requests are pending; the eleventh is refused, and a lapsed one frees a place.</summary>
    [Fact]
    public void Open_Beyond10Pending_ReturnsNull()
    {
        for (int index = 0; index < 10; index++)
        {
            Assert.NotNull(this.service.Open(Candidate(new TestDevice())));
        }

        Assert.Null(this.service.Open(Candidate(new TestDevice())));

        this.clock.Advance(TimeSpan.FromMinutes(6));
        Assert.NotNull(this.service.Open(Candidate(new TestDevice())));
    }

    /// <summary>Superseding does not count twice: ten devices asking again with new scopes still leave ten, and the same device may supersede at the cap.</summary>
    [Fact]
    public void Open_SupersedeAtTheCap_IsAllowed()
    {
        TestDevice first = new();
        _ = this.service.Open(Candidate(first));
        for (int index = 0; index < 9; index++)
        {
            _ = this.service.Open(Candidate(new TestDevice()));
        }

        PairingRequest? superseding = this.service.Open(Candidate(first, GatewayScopes.Admin));

        Assert.NotNull(superseding);
        Assert.Equal(10, this.service.Pending().Count);
    }

    /// <summary>An approver holding the scopes approves: the device is paired via approval with the requested scopes, and the request reads as approved.</summary>
    [Fact]
    public void Approve_WithinTheApproversScopes_PairsTheDevice()
    {
        TestDevice device = new();
        PairingRequest request = this.OpenRequired(device);

        PairingService.ApprovalResult result = this.service.Approve(request.RequestId, PairingApprover);

        Assert.Equal(PairingService.ApprovalOutcome.Approved, result.Outcome);
        Assert.Equal("approval", result.Device?.PairedVia);
        Assert.Equal([GatewayScopes.Read, GatewayScopes.Write], this.devices.Find(device.DeviceId)?.Scopes);
        Assert.Equal(PairingService.State.Approved, this.service.StateOf(request.RequestId));
        Assert.Empty(this.service.Pending());
    }

    /// <summary>The approver may grant fewer scopes than were asked for.</summary>
    [Fact]
    public void Approve_GrantedScopesNarrowerThanRequested_PairsWithTheGrant()
    {
        TestDevice device = new();
        PairingRequest request = this.OpenRequired(device, GatewayScopes.Admin);

        PairingService.ApprovalResult result = this.service.Approve(request.RequestId, PairingApprover, [GatewayScopes.Read, GatewayScopes.Write]);

        Assert.Equal(PairingService.ApprovalOutcome.Approved, result.Outcome);
        Assert.False(GatewayScopes.Holds(this.ScopesOf(device.DeviceId), GatewayScopes.Admin));
    }

    /// <summary>An approver cannot grant a scope it does not hold: a pairing approver without write cannot approve a write request.</summary>
    [Fact]
    public void Approve_ScopeTheApproverLacks_IsScopeNotHeld()
    {
        TestDevice device = new();
        PairingRequest request = this.OpenRequired(device);

        PairingService.ApprovalResult result = this.service.Approve(request.RequestId, [GatewayScopes.Pairing, GatewayScopes.Read]);

        Assert.Equal(PairingService.ApprovalOutcome.ScopeNotHeld, result.Outcome);
        Assert.Null(this.devices.Find(device.DeviceId));
        Assert.Equal(PairingService.State.Pending, this.service.StateOf(request.RequestId));
    }

    /// <summary>Approving needs <c>operator.pairing</c> (admin implies it); a plain write session cannot.</summary>
    [Fact]
    public void Approve_WithoutPairingScope_IsScopeNotHeld()
    {
        PairingRequest request = this.OpenRequired(new TestDevice());

        PairingService.ApprovalResult result = this.service.Approve(request.RequestId, [GatewayScopes.Read, GatewayScopes.Write]);

        Assert.Equal(PairingService.ApprovalOutcome.ScopeNotHeld, result.Outcome);
    }

    /// <summary>An admin request needs an admin approver: a pairing approver gets <c>AdminRequired</c> and the request stays pending.</summary>
    [Fact]
    public void Approve_AdminRequestByNonAdmin_IsAdminRequired()
    {
        TestDevice device = new();
        PairingRequest request = this.OpenRequired(device, GatewayScopes.Admin);

        PairingService.ApprovalResult result = this.service.Approve(request.RequestId, PairingApprover);

        Assert.Equal(PairingService.ApprovalOutcome.AdminRequired, result.Outcome);
        Assert.Null(this.devices.Find(device.DeviceId));
        Assert.Equal(PairingService.State.Pending, this.service.StateOf(request.RequestId));
    }

    /// <summary>An admin approves an admin request and the device holds admin.</summary>
    [Fact]
    public void Approve_AdminRequestByAdmin_PairsAsAdmin()
    {
        TestDevice device = new();
        PairingRequest request = this.OpenRequired(device, GatewayScopes.Admin);

        PairingService.ApprovalResult result = this.service.Approve(request.RequestId, AdminApprover);

        Assert.Equal(PairingService.ApprovalOutcome.Approved, result.Outcome);
        Assert.True(GatewayScopes.Holds(this.ScopesOf(device.DeviceId), GatewayScopes.Admin));
    }

    /// <summary>Request admin: a limited paired device asking for admin opens a request flagged as an admin request, and approval widens it, keeping its token.</summary>
    [Fact]
    public void RequestAdmin_LimitedDevice_IsAnAdminRequestApprovedByAnAdmin()
    {
        TestDevice device = new();
        string token = this.devices.Pair(new DeviceRegistry.Registration(device.DeviceId, device.PublicKey, "Phone", GatewayScopes.Limited, "tailscale", "ada@example.com", "100.64.0.7", null)) ?? string.Empty;

        PairingRequest request = this.OpenRequired(device, GatewayScopes.Admin);
        PairingService.ApprovalResult result = this.service.Approve(request.RequestId, AdminApprover);

        Assert.True(request.IsAdminRequest);
        Assert.False(this.OpenRequired(new TestDevice()).IsAdminRequest);
        Assert.Equal(PairingService.ApprovalOutcome.Approved, result.Outcome);
        Assert.Equal(device.DeviceId, this.devices.FindByToken(token)?.DeviceId);
        Assert.True(GatewayScopes.Holds(this.ScopesOf(device.DeviceId), GatewayScopes.Admin));
        Assert.Null(this.service.TakePendingToken(device.DeviceId));
    }

    /// <summary>The token of a device approved from scratch is held for its next handshake and handed over exactly once.</summary>
    [Fact]
    public void TakePendingToken_ApprovedNewDevice_IsHandedOverOnce()
    {
        TestDevice device = new();
        PairingRequest request = this.OpenRequired(device);
        _ = this.service.Approve(request.RequestId, AdminApprover);

        string? token = this.service.TakePendingToken(device.DeviceId);

        Assert.NotNull(token);
        Assert.Equal(device.DeviceId, this.devices.FindByToken(token)?.DeviceId);
        Assert.Null(this.service.TakePendingToken(device.DeviceId));
    }

    /// <summary>Reject needs the pairing scope, creates no ban, and a new handshake opens a fresh request at once.</summary>
    [Fact]
    public void Reject_ClosesTheRequestWithoutBanning()
    {
        TestDevice device = new();
        PairingRequest request = this.OpenRequired(device);

        Assert.False(this.service.Reject(request.RequestId, [GatewayScopes.Read, GatewayScopes.Write]));
        Assert.True(this.service.Reject(request.RequestId, PairingApprover));

        Assert.Equal(PairingService.State.Rejected, this.service.StateOf(request.RequestId));
        Assert.Null(this.devices.Find(device.DeviceId));
        PairingRequest? again = this.service.Open(Candidate(device));
        Assert.NotNull(again);
        Assert.NotEqual(request.RequestId, again.RequestId);
        Assert.Equal(PairingService.State.Pending, this.service.StateOf(again.RequestId));
    }

    /// <summary>A rejected or approved request cannot be approved again.</summary>
    [Fact]
    public void Approve_ResolvedRequest_IsNotFound()
    {
        PairingRequest request = this.OpenRequired(new TestDevice());
        _ = this.service.Reject(request.RequestId, AdminApprover);

        Assert.Equal(PairingService.ApprovalOutcome.NotFound, this.service.Approve(request.RequestId, AdminApprover).Outcome);
    }
}
