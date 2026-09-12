namespace Agency.Huddle.Acp.Hosting;

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Agency.Huddle.Acp.Abstractions;

/// <summary>Launches the agent process using the platform process APIs.</summary>
public sealed class AgentProcessLauncher(ILogger<AgentProcessLauncher> logger) : IAgentProcessLauncher
{
    public IAgentProcess Launch(AgentProcessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ProcessStartInfo startInfo = new ProcessStartInfo(options.Command)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            WorkingDirectory = options.WorkingDirectory ?? Environment.CurrentDirectory,
        };

        foreach (string arg in options.Args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (options.EnvironmentOverrides is not null)
        {
            foreach (KeyValuePair<string, string> pair in options.EnvironmentOverrides)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        Process process;
        try
        {
            process = Process.Start(startInfo) ?? throw new AgentProcessStartException(
                $"Failed to start '{options.Command}'.",
                new InvalidOperationException("Process.Start returned null."));
        }
        catch (Win32Exception ex)
        {
            throw new AgentProcessStartException($"Failed to start '{options.Command}'.", ex);
        }

        return new AgentProcess(process, logger);
    }
}