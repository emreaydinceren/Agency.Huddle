using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tools.LoadDriver;

/// <summary>
/// Joins the app's pipe as the Agent <c>loadbot</c>, picks the first Room it shares with another Agent, and
/// every <c>intervalMs</c> for <c>seconds</c> posts the next of a fixed set of requests that make that Agent
/// work the Tasks and the Library through its App Tools. With a base URL it also loads the given pages
/// once a second, so their server render runs during the measurement.
/// Usage: <c>LoadDriver &lt;pipeName&gt; &lt;seconds&gt; &lt;intervalMs&gt; [baseUrl path...]</c>.
/// </summary>
internal static class Program
{
    private const string AgentName = "loadbot";

    /// <summary>Requests in posting order; <c>{0}</c> is the Agent mention and <c>{1}</c> the post number.</summary>
    private static readonly string[] Requests =
    [
        "{0} please create a Task in the Platform team titled \"Perf check {1}: rotate the staging API keys\", assigned to Marcus, Normal priority. Reply in one sentence.",
        "{0} list the open Platform Tasks and tell me how many are In Progress. One sentence, no table.",
        "{0} read the Platform library note Architecture.md and summarise it in two sentences.",
        "{0} move PLAT-0013 to In Progress and log a change on it saying perf check {1} touched it. One sentence.",
        "{0} search the library for \"release calendar\" and tell me the next release date. One sentence.",
        "{0} append the line \"- perf check {1} follow-up\" to the Platform note Meeting-Notes-2026-10-14.md. One sentence.",
    ];

    /// <summary>Runs the workload and prints how many Messages and page loads went each way.</summary>
    private static async Task<int> Main(string[] args)
    {
        if (args.Length < 3)
        {
            await Console.Error.WriteLineAsync("Usage: LoadDriver <pipeName> <seconds> <intervalMs> [baseUrl path...]").ConfigureAwait(false);
            return 1;
        }

        string pipeName = args[0];
        TimeSpan duration = TimeSpan.FromSeconds(int.Parse(args[1], CultureInfo.InvariantCulture));
        TimeSpan interval = TimeSpan.FromMilliseconds(int.Parse(args[2], CultureInfo.InvariantCulture));

        NamedPipeClientStream pipe = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await using var pipeScope = pipe.ConfigureAwait(false);
        await pipe.ConnectAsync(10_000).ConfigureAwait(false);
        JsonLineStream lines = new(pipe, leaveOpen: true);
        await using var linesScope = lines.ConfigureAwait(false);
        await lines.WriteAsync(new Hello(AgentName, "perf-audit load driver")).ConfigureAwait(false);

        RoomInfo? room = await ReadWelcomeRoomAsync(lines).ConfigureAwait(false);
        if (room is null)
        {
            await Console.Error.WriteLineAsync($"'{AgentName}' is not in a Room with other Agents; add it to one in the UI first.").ConfigureAwait(false);
            return 2;
        }

        string mention = "@" + room.Members.First(IsOtherAgent).Name;
        Console.WriteLine($"Room '{room.Name}' ({room.Id}); asking {mention}");

        using CancellationTokenSource stop = new();
        Counts counts = new();
        Task reader = DrainAsync(lines, counts, stop.Token);
        Task<int> pages = args.Length > 4 ? LoadPagesAsync(new Uri(args[3]), args[4..], stop.Token) : Task.FromResult(0);
        int posted = await PostAsync(lines, room.Id, mention, duration, interval).ConfigureAwait(false);

        await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        await stop.CancelAsync().ConfigureAwait(false);
        await reader.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        int loaded = await pages.ConfigureAwait(false);
        Console.WriteLine($"posted={posted} received messagePosted={counts.Messages} messageDelta={counts.Deltas} other={counts.Other} pages={loaded}");
        return 0;
    }

    /// <summary>Reads envelopes until the Welcome and returns the first Room shared with another Agent.</summary>
    private static async Task<RoomInfo?> ReadWelcomeRoomAsync(JsonLineStream lines)
    {
        while (await lines.ReadAsync().ConfigureAwait(false) is { } message)
        {
            if (message is Welcome welcome)
            {
                return welcome.Rooms.FirstOrDefault(room => room.Members.Any(IsOtherAgent));
            }

            if (message is ProtocolError error)
            {
                await Console.Error.WriteLineAsync($"Server refused hello: {error}").ConfigureAwait(false);
                return null;
            }
        }

        return null;
    }

    /// <summary>Posts the next request on a fixed cadence until <paramref name="duration"/> has passed; the first goes out at once.</summary>
    private static async Task<int> PostAsync(JsonLineStream lines, string roomId, string mention, TimeSpan duration, TimeSpan interval)
    {
        using PeriodicTimer timer = new(interval);
        Stopwatch elapsed = Stopwatch.StartNew();
        int posted = 0;
        do
        {
            string text = string.Format(CultureInfo.InvariantCulture, Requests[posted % Requests.Length], mention, posted + 1);
            posted++;
            await lines.WriteAsync(new PostMessage(roomId, Guid.NewGuid().ToString("N"), text)).ConfigureAwait(false);
        }
        while (await timer.WaitForNextTickAsync().ConfigureAwait(false) && elapsed.Elapsed + interval <= duration);

        // Hold the connection for the rest of the window so replies to the last request still arrive.
        TimeSpan rest = duration - elapsed.Elapsed;
        if (rest > TimeSpan.Zero)
        {
            await Task.Delay(rest).ConfigureAwait(false);
        }

        return posted;
    }

    /// <summary>Loads each page in turn, once a second, until cancelled; returns how many loads succeeded.</summary>
    private static async Task<int> LoadPagesAsync(Uri baseUrl, string[] paths, CancellationToken ct)
    {
        using HttpClient http = new() { BaseAddress = baseUrl };
        int loaded = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                foreach (string path in paths)
                {
                    using HttpResponseMessage response = await http.GetAsync(new Uri(path, UriKind.Relative), ct).ConfigureAwait(false);
                    loaded += response.IsSuccessStatusCode ? 1 : 0;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is how the run ends; the count so far is the result.
        }

        return loaded;
    }

    /// <summary>Keeps the pipe drained so the server never blocks on a full buffer.</summary>
    private static async Task DrainAsync(JsonLineStream lines, Counts counts, CancellationToken ct)
    {
        while (await lines.ReadAsync(ct).ConfigureAwait(false) is { } message)
        {
            counts.Record(message);
        }
    }

    /// <summary>True for an Agent Member other than this driver.</summary>
    private static bool IsOtherAgent(MemberInfo member) =>
        member.Kind == UserKind.Agent && !string.Equals(member.Name, AgentName, StringComparison.Ordinal);
}
