using System.Text.RegularExpressions;

namespace Agency.Huddle.Contracts;

public static partial class NameRules
{
    /// <summary>
    /// A Teammate Name is a display name — "Emily Lee" is as legitimate as "echo" — so single
    /// interior spaces are allowed. Everything else stays narrow, because a Name is not only shown:
    /// it is also the filename of a Persona ({name}.md), which makes this method the
    /// path-traversal guard, and it is matched against message text to find Mentions.
    /// <para>
    /// Leading, trailing and doubled spaces are rejected on purpose. Windows silently strips a
    /// trailing space from a filename, so "coo " and "coo" would resolve to one file while
    /// presenting as two Teammates; and "Emily  Lee" beside "Emily Lee" is a distinction no reader
    /// can see. Rejecting them keeps a Name's rendered form and its stored form the same string.
    /// </para>
    /// </summary>
    public static bool IsValidAgentName(string? name)
    {
        return name is not null && AgentNameRegex().IsMatch(name);
    }

    public static bool IsValidId(string? id)
    {
        return id is not null && IdRegex().IsMatch(id);
    }

    // Reads as: at most 64 characters; opens on a letter or digit; thereafter each character is a
    // letter, digit, underscore or hyphen, optionally preceded by ONE space. Requiring a real
    // character after every space is what rules out a trailing space and a doubled one in the same
    // clause. '.' stays excluded so a Name can never reach a file extension, and RegexOptions
    // default (no Singleline) keeps a newline out of ".". The anchors are \A and \z rather
    // than ^ and $ because .NET's $ also matches BEFORE a trailing newline, which would let
    // "coo\n" through a guard whose whole job is to be exact.
    [GeneratedRegex(@"\A(?=.{1,64}\z)[A-Za-z0-9](?:[ ]?[A-Za-z0-9_-])*\z", RegexOptions.CultureInvariant)]
    private static partial Regex AgentNameRegex();

    [GeneratedRegex(@"\A[A-Za-z0-9_-]{1,64}\z")]
    private static partial Regex IdRegex();
}