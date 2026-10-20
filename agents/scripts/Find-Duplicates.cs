#:package Microsoft.CodeAnalysis.CSharp@4.12.0
#:property ManagePackageVersionsCentrally=false
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property GenerateDocumentationFile=false
// Finds copy-pasted C# blocks the way SonarQube's CPD does: a duplicate is a run of at least
// --min-tokens identical tokens (default 100) spanning at least --min-lines lines (default 10).
// String, character and numeric literals are normalised, so two blocks that differ only in a
// message or a constant still match; identifiers are kept, so a renamed copy does not. Using
// directives and namespace names are skipped. Comments and whitespace are trivia and never count.
//
// Reads repository-relative .cs paths from stdin, one per line, and prints one tab-separated row
// per duplicated pair, longest first:
//
//     <tokens>\t<fileA>:<startLine>-<endLine>\t<fileB>:<startLine>-<endLine>
//
// followed by a "#" summary line with the duplicated-line density. Report-Duplication.ps1 is the
// usual caller; run it directly from the repository root with:
//
//     git ls-files 'src/*.cs' | dotnet run agents/scripts/Find-Duplicates.cs -- --min-tokens 100
//
// The #:property lines opt it out of the repository's central package management and strict build
// settings, which would otherwise reject a standalone script.
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

int minTokens = 100;
int minLines = 10;
for (int a = 0; a < args.Length - 1; a++)
{
    if (args[a] == "--min-tokens")
    {
        minTokens = int.Parse(args[a + 1], CultureInfo.InvariantCulture);
    }
    else if (args[a] == "--min-lines")
    {
        minLines = int.Parse(args[a + 1], CultureInfo.InvariantCulture);
    }
}

List<string> paths = [];
while (Console.ReadLine() is { } input)
{
    string path = input.Trim().Replace('\\', '/');
    if (path.EndsWith(".cs", StringComparison.Ordinal)
        && !path.EndsWith(".g.cs", StringComparison.Ordinal)
        && !path.EndsWith(".Designer.cs", StringComparison.Ordinal)
        && File.Exists(path))
    {
        paths.Add(path);
    }
}

// Token texts are interned to ints so windows compare and hash cheaply.
Dictionary<string, int> vocabulary = new(StringComparer.Ordinal);
List<int[]> fileTokens = [];
List<int[]> fileLines = [];
long totalLines = 0;
foreach (string path in paths)
{
    string text = File.ReadAllText(path);
    SyntaxTree tree = CSharpSyntaxTree.ParseText(text, path: path);
    totalLines += tree.GetText().Lines.Count;
    List<int> ids = [];
    List<int> lines = [];
    foreach (SyntaxToken token in tree.GetRoot().DescendantTokens())
    {
        if (token.IsKind(SyntaxKind.EndOfFileToken) || IsSkipped(token))
        {
            continue;
        }

        string key = Normalise(token);
        if (!vocabulary.TryGetValue(key, out int id))
        {
            id = vocabulary.Count;
            vocabulary[key] = id;
        }

        ids.Add(id);
        lines.Add(token.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
    }

    fileTokens.Add([.. ids]);
    fileLines.Add([.. lines]);
}

// Bucket every window of minTokens tokens by a rolling hash; only buckets with two or more
// occurrences can hold a duplicate.
const ulong Base = 1_000_003UL;
ulong basePower = 1;
for (int k = 1; k < minTokens; k++)
{
    basePower *= Base;
}

Dictionary<ulong, List<(int File, int Start)>> buckets = [];
for (int f = 0; f < fileTokens.Count; f++)
{
    int[] tokens = fileTokens[f];
    if (tokens.Length < minTokens)
    {
        continue;
    }

    ulong hash = 0;
    for (int k = 0; k < minTokens; k++)
    {
        hash = (hash * Base) + (ulong)tokens[k] + 1;
    }

    for (int start = 0; ; start++)
    {
        if (!buckets.TryGetValue(hash, out List<(int File, int Start)>? bucket))
        {
            bucket = [];
            buckets[hash] = bucket;
        }

        bucket.Add((f, start));
        int next = start + minTokens;
        if (next >= tokens.Length)
        {
            break;
        }

        hash = ((hash - (((ulong)tokens[start] + 1) * basePower)) * Base) + (ulong)tokens[next] + 1;
    }
}

// A pair of matching windows starts a block only when the tokens just before them differ; the
// block then extends while the tokens keep matching, so each maximal duplicate is reported once.
List<(int Length, int FileA, int StartA, int FileB, int StartB)> blocks = [];
foreach (List<(int File, int Start)> bucket in buckets.Values)
{
    if (bucket.Count < 2)
    {
        continue;
    }

    for (int x = 0; x < bucket.Count; x++)
    {
        for (int y = x + 1; y < bucket.Count; y++)
        {
            (int fa, int sa) = bucket[x];
            (int fb, int sb) = bucket[y];
            int[] ta = fileTokens[fa];
            int[] tb = fileTokens[fb];
            if (sa > 0 && sb > 0 && ta[sa - 1] == tb[sb - 1])
            {
                continue;
            }

            int limit = fa == fb ? sb - sa : int.MaxValue;
            int length = 0;
            while (sa + length < ta.Length && sb + length < tb.Length && length < limit && ta[sa + length] == tb[sb + length])
            {
                length++;
            }

            if (length < minTokens)
            {
                continue;
            }

            int linesA = fileLines[fa][sa + length - 1] - fileLines[fa][sa] + 1;
            int linesB = fileLines[fb][sb + length - 1] - fileLines[fb][sb] + 1;
            if (Math.Min(linesA, linesB) >= minLines)
            {
                blocks.Add((length, fa, sa, fb, sb));
            }
        }
    }
}

// Longest first, a pair is reported only when one of its sides adds tokens no reported pair has
// covered yet. That folds the shifted self-matches of a repetitive region (a table of similar
// lines matches itself at every offset) and the extra pairs of a block copied three times into
// one row each.
HashSet<(int File, int Token)> coveredTokens = [];
HashSet<(int File, int Line)> duplicatedLines = [];
HashSet<string> printedSpans = new(StringComparer.Ordinal);
foreach ((int length, int fa, int sa, int fb, int sb) in blocks.OrderByDescending(b => b.Length))
{
    if (IsCovered(fa, sa, length) && IsCovered(fb, sb, length))
    {
        continue;
    }

    Mark(fa, sa, length);
    Mark(fb, sb, length);
    string spans = $"{Span(fa, sa, length)}\t{Span(fb, sb, length)}";
    if (printedSpans.Add(spans))
    {
        Console.WriteLine($"{length}\t{spans}");
    }
}

int reported = printedSpans.Count;

double density = totalLines == 0 ? 0 : 100.0 * duplicatedLines.Count / totalLines;
Console.WriteLine(string.Create(
    CultureInfo.InvariantCulture,
    $"# files={paths.Count} lines={totalLines} blocks={reported} duplicatedLines={duplicatedLines.Count} density={density:F2}%"));
return 0;

/// <summary>True when every token of the block already belongs to a reported pair.</summary>
bool IsCovered(int file, int start, int length)
{
    for (int k = start; k < start + length; k++)
    {
        if (!coveredTokens.Contains((file, k)))
        {
            return false;
        }
    }

    return true;
}

/// <summary>Formats a block as <c>path:firstLine-lastLine</c>.</summary>
string Span(int file, int start, int length) =>
    $"{paths[file]}:{fileLines[file][start]}-{fileLines[file][start + length - 1]}";

/// <summary>Records every token and line a block covers, for de-duplication and the density figure.</summary>
void Mark(int file, int start, int length)
{
    for (int k = start; k < start + length; k++)
    {
        coveredTokens.Add((file, k));
    }

    for (int line = fileLines[file][start]; line <= fileLines[file][start + length - 1]; line++)
    {
        duplicatedLines.Add((file, line));
    }
}

/// <summary>True for tokens of a using directive and of a namespace's header (keyword, name and file-scoped semicolon).</summary>
static bool IsSkipped(SyntaxToken token)
{
    for (SyntaxNode? node = token.Parent; node is not null; node = node.Parent)
    {
        switch (node)
        {
            case UsingDirectiveSyntax:
                return true;
            case FileScopedNamespaceDeclarationSyntax fileScoped:
                return token == fileScoped.NamespaceKeyword || token == fileScoped.SemicolonToken || fileScoped.Name.Span.Contains(token.SpanStart);
            case NamespaceDeclarationSyntax block:
                return token == block.NamespaceKeyword || block.Name.Span.Contains(token.SpanStart);
        }
    }

    return false;
}

/// <summary>Collapses literals to a placeholder so blocks differing only in a value still match.</summary>
static string Normalise(SyntaxToken token) => token.Kind() switch
{
    SyntaxKind.StringLiteralToken or SyntaxKind.InterpolatedStringTextToken
        or SyntaxKind.SingleLineRawStringLiteralToken or SyntaxKind.MultiLineRawStringLiteralToken
        or SyntaxKind.Utf8StringLiteralToken or SyntaxKind.Utf8SingleLineRawStringLiteralToken
        or SyntaxKind.Utf8MultiLineRawStringLiteralToken => "$str",
    SyntaxKind.CharacterLiteralToken => "$char",
    SyntaxKind.NumericLiteralToken => "$num",
    _ => token.Text,
};
