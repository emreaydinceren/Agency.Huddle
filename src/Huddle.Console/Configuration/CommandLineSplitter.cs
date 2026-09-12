namespace Agency.Huddle.Console.Configuration;

using System.Collections.Generic;
using System.Text;

internal static class CommandLineSplitter
{
    internal static string[] Split(string? commandLine)
    {
        if (string.IsNullOrEmpty(commandLine))
        {
            return [];
        }

        List<string> results = new List<string>();
        StringBuilder current = new StringBuilder();
        bool inQuotes = false;
        bool hasCurrent = false;

        for (int i = 0; i < commandLine.Length; i++)
        {
            char c = commandLine[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                hasCurrent = true;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (hasCurrent)
                {
                    results.Add(current.ToString());
                    current.Clear();
                    hasCurrent = false;
                }

                continue;
            }

            current.Append(c);
            hasCurrent = true;
        }

        if (hasCurrent)
        {
            results.Add(current.ToString());
        }

        return results.ToArray();
    }
}
