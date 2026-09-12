namespace Agency.Huddle.Console;

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Console.Terminal;

/// <summary>Answers agent permission requests by prompting on the console, serialising concurrent requests.</summary>
internal sealed class ConsolePermissionHandler(IConsoleInput input, IConsoleOutput output, bool discardPendingInput = true) : IPermissionHandler, IDisposable
{
    private const int MaxRawInputLength = 500;

    private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

    public async Task<PermissionDecision> DecideAsync(PermissionRequestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return PermissionDecision.Cancelled;
        }

        try
        {
            if (discardPendingInput)
            {
                input.DiscardPending();
            }

            ConsolePermissionHandler.Render(context, output);

            while (true)
            {
                string? line = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    return PermissionDecision.Cancelled;
                }

                if (int.TryParse(line, NumberStyles.Integer, CultureInfo.InvariantCulture, out int choice)
                    && choice >= 1
                    && choice <= context.Options.Count)
                {
                    return new SelectedDecision(context.Options[choice - 1].OptionId);
                }

                output.WriteLine("Invalid choice.", ConsoleStyle.Error);
                ConsolePermissionHandler.WriteChoosePrompt(context.Options.Count, output);
            }
        }
        catch (OperationCanceledException)
        {
            return PermissionDecision.Cancelled;
        }
        finally
        {
            this.gate.Release();
        }
    }

    private static void Render(PermissionRequestContext context, IConsoleOutput output)
    {
        string kind = context.ToolCall.Kind.ToString().ToLowerInvariant();
        string title = context.ToolCall.Title ?? context.ToolCall.ToolCallId;
        output.WriteLine($"Permission requested: [{kind}] {title}", ConsoleStyle.Tool);

        if (context.ToolCall.RawInputJson is not null)
        {
            string rawInput = context.ToolCall.RawInputJson;
            if (rawInput.Length > ConsolePermissionHandler.MaxRawInputLength)
            {
                rawInput = rawInput[..ConsolePermissionHandler.MaxRawInputLength] + "…";
            }

            output.WriteLine($"  input: {rawInput}", ConsoleStyle.Tool);
        }

        for (int i = 0; i < context.Options.Count; i++)
        {
            output.WriteLine($"  {i + 1}) {context.Options[i].Name}", ConsoleStyle.Tool);
        }

        ConsolePermissionHandler.WriteChoosePrompt(context.Options.Count, output);
    }

    private static void WriteChoosePrompt(int optionCount, IConsoleOutput output)
    {
        output.Write($"Choose [1-{optionCount}]: ", ConsoleStyle.Prompt);
    }

    public void Dispose()
    {
        this.gate.Dispose();
    }
}
