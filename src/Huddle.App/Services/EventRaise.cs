namespace Agency.Huddle.App.Services;

/// <summary>Raises an event so a subscriber that throws neither stops the others nor reaches the raiser.</summary>
internal static class EventRaise
{
    /// <summary>
    /// Invokes every handler of a parameterless event in turn. A handler that throws is reported to
    /// <paramref name="onHandlerError"/> and skipped; the remaining handlers still run.
    /// </summary>
    /// <param name="handlers">The event's current delegate; <see langword="null"/> when nothing subscribes.</param>
    /// <param name="onHandlerError">Called with the exception a handler threw. The caller owns the log level and message.</param>
    internal static void Isolated(Action? handlers, Action<Exception> onHandlerError)
    {
        ArgumentNullException.ThrowIfNull(onHandlerError);

        if (handlers is null)
        {
            return;
        }

        foreach (Action handler in handlers.GetInvocationList().Cast<Action>())
        {
            try
            {
                handler();
            }
            catch (Exception ex)
            {
                // A subscriber is UI or a collaborator the raiser cannot vouch for; the callback reports it, and the others must still run.
                onHandlerError(ex);
            }
        }
    }

    /// <summary>
    /// Invokes every handler of an event carrying one argument in turn. A handler that throws is
    /// reported to <paramref name="onHandlerError"/> and skipped; the remaining handlers still run.
    /// </summary>
    /// <typeparam name="T">The event's argument type.</typeparam>
    /// <param name="handlers">The event's current delegate; <see langword="null"/> when nothing subscribes.</param>
    /// <param name="argument">The value passed to every handler.</param>
    /// <param name="onHandlerError">Called with the exception a handler threw. The caller owns the log level and message.</param>
    internal static void Isolated<T>(Action<T>? handlers, T argument, Action<Exception> onHandlerError)
    {
        ArgumentNullException.ThrowIfNull(onHandlerError);

        if (handlers is null)
        {
            return;
        }

        foreach (Action<T> handler in handlers.GetInvocationList().Cast<Action<T>>())
        {
            try
            {
                handler(argument);
            }
            catch (Exception ex)
            {
                // A subscriber is UI or a collaborator the raiser cannot vouch for; the callback reports it, and the others must still run.
                onHandlerError(ex);
            }
        }
    }
}
