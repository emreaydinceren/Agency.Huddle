using System.Reflection;
using System.Runtime.ExceptionServices;
using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// A pass-through <see cref="ITeamDirectory"/> that, while <see cref="Gate"/> is set, holds every
/// task-returning call until the gate opens. Lets a card test keep a service call in flight long enough
/// to see the card's busy view. Public and unsealed because <see cref="DispatchProxy"/> requires it.
/// </summary>
public class DirectoryGate : DispatchProxy
{
    private ITeamDirectory? inner;

    /// <summary>When set, task-returning calls wait for it before reaching the real directory. Null means pass straight through.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Wraps <paramref name="inner"/> in a gate that starts open.</summary>
    /// <param name="inner">The real directory every call is forwarded to.</param>
    /// <param name="gate">The handle a test sets <see cref="Gate"/> on.</param>
    public static ITeamDirectory Wrap(ITeamDirectory inner, out DirectoryGate gate)
    {
        ITeamDirectory proxy = Create<ITeamDirectory, DirectoryGate>();
        gate = (DirectoryGate)(object)proxy;
        gate.inner = inner;
        return proxy;
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        ITeamDirectory target = this.inner ?? throw new InvalidOperationException("The gate wraps no directory.");

        object? Call()
        {
            try
            {
                return targetMethod.Invoke(target, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is { } real)
            {
                ExceptionDispatchInfo.Capture(real).Throw();
                throw;
            }
        }

        if (this.Gate is not { } gate)
        {
            return Call();
        }

        Type returnType = targetMethod.ReturnType;
        if (returnType == typeof(Task))
        {
            return HoldAsync(gate.Task, Call);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            MethodInfo hold = typeof(DirectoryGate).GetMethod(nameof(HoldAsyncOf), BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("HoldAsyncOf is missing.");
            return hold.MakeGenericMethod(returnType.GetGenericArguments()[0]).Invoke(null, [gate.Task, (Func<object?>)Call]);
        }

        return Call();
    }

    /// <summary>Waits for the gate, then makes the call.</summary>
    /// <param name="gate">The task that opens the gate.</param>
    /// <param name="call">The forwarded call.</param>
    private static async Task HoldAsync(Task gate, Func<object?> call)
    {
        await gate;
        await (call() as Task ?? throw new InvalidOperationException("The call returned no task."));
    }

    /// <summary>Waits for the gate, then makes the call.</summary>
    /// <typeparam name="T">The call's result type.</typeparam>
    /// <param name="gate">The task that opens the gate.</param>
    /// <param name="call">The forwarded call.</param>
    private static async Task<T> HoldAsyncOf<T>(Task gate, Func<object?> call)
    {
        await gate;
        return await (call() as Task<T> ?? throw new InvalidOperationException("The call returned no task."));
    }
}
