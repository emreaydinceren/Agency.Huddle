namespace Agency.Huddle.App.Library;

/// <summary>Reads a YAML flow list, <c>[a, "b"]</c>, of scalars (D-2 step 3).</summary>
internal static class NoteYamlFlowList
{
    /// <summary>Reads the list that starts <paramref name="text"/>. An item that is itself a list or a map, an unclosed bracket or quote, or text after the closing bracket makes it <see langword="false"/>.</summary>
    /// <param name="text">The list, starting at its opening bracket.</param>
    /// <param name="items">The items without quotes; a YAML null item and an empty item are dropped.</param>
    internal static bool TryRead(string text, out List<string> items)
    {
        items = [];
        int index = 1;
        while (true)
        {
            index = SkipSpaces(text, index);
            if (index >= text.Length)
            {
                return false;
            }

            char c = text[index];
            if (c == ']')
            {
                string after = text[(index + 1)..].Trim();
                return after.Length == 0 || after[0] == '#';
            }

            if (c == ',')
            {
                index++;
                continue;
            }

            if (!TryReadItem(text, ref index, items))
            {
                return false;
            }
        }
    }

    /// <summary>Reads one item and leaves <paramref name="index"/> on the comma or bracket that follows it.</summary>
    private static bool TryReadItem(string text, ref int index, List<string> items)
    {
        char c = text[index];
        if (c is '[' or '{')
        {
            return false;
        }

        if (c is '"' or '\'')
        {
            if (!NoteYamlScalars.TryReadQuoted(text, index, out string quoted, out int end))
            {
                return false;
            }

            items.Add(quoted);
            index = SkipSpaces(text, end);
            return index < text.Length && text[index] is ',' or ']';
        }

        int stop = index;
        while (stop < text.Length && text[stop] is not (',' or ']'))
        {
            stop++;
        }

        string plain = text[index..stop].Trim();
        if (stop >= text.Length || plain.Length > 0 && plain[0] is '&' or '*' or '!' or '{')
        {
            return false;
        }

        if (!NoteYamlScalars.IsNull(plain))
        {
            items.Add(plain);
        }

        index = stop;
        return true;
    }

    private static int SkipSpaces(string text, int index)
    {
        while (index < text.Length && text[index] is ' ' or '\t')
        {
            index++;
        }

        return index;
    }
}
