namespace Agency.Huddle.App.Services;

/// <summary>Tells whether a Persona by this Name still exists, for a row of the Turn Queue whose Agent may have been removed.</summary>
/// <param name="name">The Persona's Name.</param>
/// <returns><see langword="true"/> when the Persona exists.</returns>
internal delegate bool PersonaExists(string name);
