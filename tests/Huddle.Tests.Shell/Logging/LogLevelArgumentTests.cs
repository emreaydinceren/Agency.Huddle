using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Agency.Huddle.App.Logging;

namespace Agency.Huddle.Tests.Logging;

/// <summary>
/// Tests for <see cref="LogLevelArgument"/> and for the console levels the shipped
/// <c>appsettings*.json</c> give, with and without the option.
/// </summary>
public sealed class LogLevelArgumentTests
{
    private const string HuddleCategory = "Agency.Huddle.App.Services.ChatService";
    private const string FrameworkCategory = "Microsoft.AspNetCore.Hosting.Diagnostics";
    private const string LifetimeCategory = "Microsoft.Hosting.Lifetime";

    /// <summary>Both spellings become the console key for Huddle's categories, keeping the level's own case and the other arguments' order.</summary>
    [Theory]
    [InlineData("--log-level", "debug")]
    [InlineData("--LOG-LEVEL", "Debug")]
    [InlineData("--log-level=DEBUG", null)]
    public void TryTranslate_EitherSpelling_SetsTheConsoleKey(string option, string? value)
    {
        string[] args = value is null
            ? ["--urls", "http://localhost:5100", option, "--Team:DataDir=x"]
            : ["--urls", "http://localhost:5100", option, value, "--Team:DataDir=x"];

        Assert.True(LogLevelArgument.TryTranslate(args, out string[]? hostArgs, out string? error));
        Assert.Null(error);
        Assert.Equal(
            ["--urls", "http://localhost:5100", "--Logging:Console:LogLevel:Agency.Huddle=Debug", "--Team:DataDir=x"],
            hostArgs);
    }

    /// <summary>Arguments without the option pass through unchanged.</summary>
    [Fact]
    public void TryTranslate_NoOption_PassesArgumentsThrough()
    {
        string[] args = ["--urls", "http://localhost:5100"];

        Assert.True(LogLevelArgument.TryTranslate(args, out string[]? hostArgs, out _));
        Assert.Equal(args, hostArgs);
    }

    /// <summary>A value that is not a level name is refused, numbers included, and the message names the value and the levels.</summary>
    [Theory]
    [InlineData("Dbug")]
    [InlineData("3")]
    [InlineData("")]
    public void TryTranslate_NotALevel_IsRefused(string value)
    {
        bool ok = LogLevelArgument.TryTranslate(["--log-level", value], out string[]? hostArgs, out string? error);

        Assert.False(ok);
        Assert.Null(hostArgs);
        Assert.Equal(
            $"'{value}' is not a log level. --log-level needs one of: Trace, Debug, Information, Warning, Error, Critical, None.",
            error);
    }

    /// <summary>The option as the last argument, with no value after it, is refused with the usage line.</summary>
    [Fact]
    public void TryTranslate_MissingValue_IsRefused()
    {
        bool ok = LogLevelArgument.TryTranslate(["--urls", "http://localhost:5100", "--log-level"], out _, out string? error);

        Assert.False(ok);
        Assert.Equal("--log-level needs one of: Trace, Debug, Information, Warning, Error, Critical, None.", error);
    }

    /// <summary>
    /// With no option, the shipped settings let only errors and the start-up lines (the
    /// "Now listening on" line that test-health.ps1 waits for) reach the console, in both environments.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Console_Default_ShowsOnlyErrorsAndTheStartupLines(bool development)
    {
        using ServiceProvider services = ConsoleOnlyLogging(development, []);
        ILoggerFactory factory = services.GetRequiredService<ILoggerFactory>();

        Assert.False(factory.CreateLogger(HuddleCategory).IsEnabled(LogLevel.Warning));
        Assert.True(factory.CreateLogger(HuddleCategory).IsEnabled(LogLevel.Error));
        Assert.False(factory.CreateLogger(FrameworkCategory).IsEnabled(LogLevel.Warning));
        Assert.True(factory.CreateLogger(LifetimeCategory).IsEnabled(LogLevel.Information));
    }

    /// <summary><c>--log-level Debug</c> raises Huddle's own categories on the console and leaves the framework's at errors.</summary>
    [Fact]
    public void Console_LogLevelDebug_RaisesHuddleOnly()
    {
        Assert.True(LogLevelArgument.TryTranslate(["--log-level", "Debug"], out string[]? hostArgs, out _));
        using ServiceProvider services = ConsoleOnlyLogging(development: false, hostArgs);
        ILoggerFactory factory = services.GetRequiredService<ILoggerFactory>();

        Assert.True(factory.CreateLogger(HuddleCategory).IsEnabled(LogLevel.Debug));
        Assert.False(factory.CreateLogger(HuddleCategory).IsEnabled(LogLevel.Trace));
        Assert.False(factory.CreateLogger(FrameworkCategory).IsEnabled(LogLevel.Warning));
    }

    /// <summary>The file log is on by default, so a quiet console still leaves a record of the run.</summary>
    [Fact]
    public void FileLog_IsEnabledByDefault()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile(RepoPaths.App("appsettings.json"), optional: false)
            .Build();

        FileLogOptions? options = configuration.GetSection(FileLogOptions.SectionName).Get<FileLogOptions>();

        Assert.NotNull(options);
        Assert.True(options.Enabled);
    }

    /// <summary>
    /// A logger factory whose only provider is the console, configured from the shipped settings
    /// files plus <paramref name="args"/> in the host's order, so <see cref="ILogger.IsEnabled"/>
    /// answers for the console alone.
    /// </summary>
    /// <param name="development">Whether to layer <c>appsettings.Development.json</c> over the base file.</param>
    /// <param name="args">Command-line arguments, added last as the host adds them.</param>
    /// <returns>The service provider owning the factory.</returns>
    private static ServiceProvider ConsoleOnlyLogging(bool development, string[] args)
    {
        ConfigurationBuilder builder = new();
        builder.AddJsonFile(RepoPaths.App("appsettings.json"), optional: false);
        if (development)
        {
            builder.AddJsonFile(RepoPaths.App("appsettings.Development.json"), optional: false);
        }

        IConfiguration configuration = builder.AddCommandLine(args).Build();

        ServiceCollection services = new();
        services.AddLogging(logging => logging
            .AddConfiguration(configuration.GetSection("Logging"))
            .AddConsole());
        return services.BuildServiceProvider();
    }
}
