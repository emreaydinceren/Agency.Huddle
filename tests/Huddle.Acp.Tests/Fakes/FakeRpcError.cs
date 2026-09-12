namespace Agency.Huddle.Acp.Tests.Fakes;

using System;

/// <summary>Thrown from a <see cref="FakeAcpAgent"/> hook to produce a JSON-RPC error response.</summary>
/// <remarks>
/// S3871 wants exception types public so callers outside the assembly can catch them by type. That
/// does not apply here: this is a test-only fake, thrown by <see cref="FakeAcpAgent"/> hooks and
/// caught only within this test assembly, and it is never part of a public API surface. Making it
/// public would in turn trip CA1710 (public exception names must end in "Exception"), forcing a
/// rename that ripples through every test file that references it for no behavioural benefit.
/// Keeping it internal and suppressing S3871 here, narrowly, is the smaller and clearer change.
/// </remarks>
#pragma warning disable S3871 // Test-only fake, never crosses the assembly boundary (see remarks above).
internal sealed class FakeRpcError : Exception
#pragma warning restore S3871
{
    internal FakeRpcError(int code, string message)
        : base(message)
    {
        this.Code = code;
    }

    internal int Code { get; }
}
