using Agency.Huddle.App.Acp;

namespace Agency.Huddle.App.Modules;

/// <summary>What a module knows when it offers tools. Tool names are not settled yet, so they are not here.</summary>
/// <param name="Persona">The Persona being started.</param>
/// <param name="Profile">The resolved Adapter Profile.</param>
/// <param name="Options">The app's options.</param>
/// <param name="Services">The app's own container, which builds the tools; never the tool server's.</param>
internal sealed record ModuleToolOffer(Persona Persona, AdapterProfile Profile, TeamOptions Options, IServiceProvider Services);
