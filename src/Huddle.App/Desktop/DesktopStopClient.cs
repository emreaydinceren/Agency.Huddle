using System.Diagnostics;
using System.Globalization;
using System.Net;

namespace Agency.Huddle.App.Desktop;

/// <summary>
/// The <c>stop</c> verb: finds a running desktop instance through <c>instance.json</c>, asks it to shut down
/// through <see cref="DesktopStopEndpoint"/>, and waits until the instance has released its lock.
/// </summary>
internal static class DesktopStopClient
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Stops the instance that owns <paramref name="dataDir"/>, if any.
    /// </summary>
    /// <param name="dataDir">The data directory that holds <c>instance.json</c> and <c>instance.lock</c>.</param>
    /// <param name="handler">The HTTP handler to use, or <see langword="null"/> for a real network handler.</param>
    /// <param name="output">Where the outcome is printed. The stop token is never printed.</param>
    /// <param name="stopTimeout">How long to wait for the instance to release its lock after it accepts the request.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>0 when nothing is running any more, 1 when the instance refused, did not answer or did not stop in time.</returns>
    internal static async Task<int> RunAsync(
        string dataDir,
        HttpMessageHandler? handler,
        TextWriter output,
        TimeSpan stopTimeout,
        CancellationToken ct)
    {
        InstanceInfo? info = InstanceFile.TryRead(dataDir);
        if (info is null)
        {
            output.WriteLine("Agency Huddle is not running.");
            return 0;
        }

        if (!Uri.TryCreate(info.Url, UriKind.Absolute, out Uri? baseUrl))
        {
            output.WriteLine("Agency Huddle is starting or not answering.");
            return 1;
        }

        Uri stopUrl = new(baseUrl, DesktopStopEndpoint.Route);
        using HttpClient client = new(handler ?? new SocketsHttpHandler(), disposeHandler: handler is null)
        {
            Timeout = RequestTimeout,
        };
        using HttpRequestMessage request = new(HttpMethod.Post, stopUrl);
        _ = request.Headers.TryAddWithoutValidation(DesktopStopEndpoint.TokenHeader, info.StopToken);

        HttpStatusCode status;
        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            status = response.StatusCode;
        }
        catch (HttpRequestException)
        {
            return HandleConnectionFailure(dataDir, output);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            output.WriteLine("Agency Huddle did not answer.");
            return 1;
        }

        int code = (int)status;
        if (code is >= 200 and < 300)
        {
            return await WaitForExitAsync(dataDir, output, stopTimeout, ct);
        }

        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture, $"Agency Huddle refused the stop request: {code} {status}."));
            return 1;
        }

        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Unexpected status {code}."));
        return 1;
    }

    /// <summary>
    /// Decides what a refused connection means: when nobody holds the instance lock the record is stale and is
    /// removed; when the lock is held a live instance is starting or stuck and the record is left alone.
    /// </summary>
    /// <param name="dataDir">The data directory.</param>
    /// <param name="output">Where the outcome is printed.</param>
    /// <returns>0 for a stale record, otherwise 1.</returns>
    private static int HandleConnectionFailure(string dataDir, TextWriter output)
    {
        using InstanceLock? probe = InstanceLock.TryAcquire(dataDir);
        if (probe is null)
        {
            output.WriteLine("Agency Huddle is starting or not answering.");
            return 1;
        }

        InstanceFile.Delete(dataDir);
        output.WriteLine("Agency Huddle is not running.");
        return 0;
    }

    /// <summary>
    /// Polls until the instance lock can be taken (the owner is gone) or <paramref name="stopTimeout"/> passes.
    /// Every lock taken while polling is released at once.
    /// </summary>
    /// <param name="dataDir">The data directory.</param>
    /// <param name="output">Where the outcome is printed.</param>
    /// <param name="stopTimeout">How long to wait.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>0 when the instance stopped, 1 on timeout.</returns>
    private static async Task<int> WaitForExitAsync(string dataDir, TextWriter output, TimeSpan stopTimeout, CancellationToken ct)
    {
        Stopwatch clock = Stopwatch.StartNew();
        while (true)
        {
            using (InstanceLock? probe = InstanceLock.TryAcquire(dataDir))
            {
                if (probe is not null)
                {
                    output.WriteLine("Stopped.");
                    return 0;
                }
            }

            TimeSpan remaining = stopTimeout - clock.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                string seconds = stopTimeout.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture);
                output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Did not stop within {seconds} s."));
                return 1;
            }

            await Task.Delay(remaining < PollInterval ? remaining : PollInterval, ct);
        }
    }
}
