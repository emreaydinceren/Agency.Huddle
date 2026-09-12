namespace Agency.Huddle.Acp.Hosting;

using System;
using System.IO;
using System.Threading.Tasks;

/// <summary>Represents a running agent process and its standard streams.</summary>
public interface IAgentProcess : IDisposable
{
    Stream StandardInput { get; }

    Stream StandardOutput { get; }

    Task<int> Exited { get; }

    void Kill();
}