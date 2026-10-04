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

    private static readonly TimeSpan MaxRecordWait = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Stops the instance that owns <paramref name="dataDir"/>, if any.
    /// </summary>
    /// <param name="dataDir">The data directory that holds <c>instance.json</c> and <c>instance.lock</c>.</param>
    /// <param name="handler">The HTTP handler to use, or <see langword="null"/> for a real network handler.</param>
    /// <param name="output">Where the outcome is printed. The stop token is never printed.</param>
    /// <param name="stopTimeout">How long to wait for the instance to release its lock after it accepts the request.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>
    /// 0 when nothing is running (the lock is free, so any record is stale and is deleted) or the instance stopped,
    /// 1 when the instance refused, is still starting, did not answer or did not stop in time.
    /// </returns>
    internal static async Task<int> RunAsync(
        string dataDir,
        HttpMessageHandler? handler,
        TextWriter output,
        TimeSpan stopTimeout,
        CancellationToken ct)
    {
        // The lock, not the record, says whether an instance is alive: a record outlives a crashed owner and its
        // port may by now belong to another service. The probe is released before anything else happens.
        using (InstanceLock? probe = InstanceLock.TryAcquire(dataDir))
        {
            if (probe is not null)
            {
                InstanceFile.Delete(dataDir);
                output.WriteLine("Agency Huddle is not running.");
                return 0;
            }
        }

        TimeSpan recordWait = stopTimeout < MaxRecordWait ? stopTimeout : MaxRecordWait;
        InstanceInfo? info = await WaitForRecordAsync(dataDir, recordWait, ct);
        if (info is null)
        {
            output.WriteLine("Agency Huddle is starting or not answering.");
            return 1;
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
            // The lock is held, so a live instance is starting or stuck: keep its record.
            output.WriteLine("Agency Huddle is starting or not answering.");
            return 1;
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
    /// Reads the record, polling every <see cref="PollInterval"/> for up to <paramref name="wait"/> when the
    /// lock is held but the instance has not published it yet.
    /// </summary>
    /// <param name="dataDir">The data directory.</param>
    /// <param name="wait">How long to wait for the record.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>The record, or <see langword="null"/> when none appeared in time.</returns>
    private static async Task<InstanceInfo?> WaitForRecordAsync(string dataDir, TimeSpan wait, CancellationToken ct)
    {
        Stopwatch clock = Stopwatch.StartNew();
        while (true)
        {
            InstanceInfo? info = InstanceFile.TryRead(dataDir);
            if (info is not null)
            {
                return info;
            }

            TimeSpan remaining = wait - clock.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                return null;
            }

            await Task.Delay(remaining < PollInterval ? remaining : PollInterval, ct);
        }
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
