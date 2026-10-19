using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Agency.Huddle.Tests.Circuits;

/// <summary>Supplies the <see cref="Circuit"/> argument of a <see cref="CircuitHandler"/> callback.</summary>
internal static class TestCircuit
{
    /// <summary>
    /// A <see cref="Circuit"/> with no state. The framework gives the type no public constructor and a handler that
    /// only records connection state never reads it, so an uninitialised instance is all a callback needs.
    /// </summary>
    /// <returns>An empty circuit.</returns>
    public static Circuit Any() => (Circuit)RuntimeHelpers.GetUninitializedObject(typeof(Circuit));
}
