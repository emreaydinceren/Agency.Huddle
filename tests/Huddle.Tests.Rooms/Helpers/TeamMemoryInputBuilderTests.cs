using Agency.Huddle.App.Helpers.TeamMemory;
using Agency.Huddle.App.Memory;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>Pins what the Team Memory Helper is shown for one proposal (Team Memory Helper spec §6.3): folders, a capped index, and the best-matching files in full.</summary>
public sealed class TeamMemoryInputBuilderTests
{
    /// <summary>The folders are the Team's and its Projects', and the index lists every memory file in them with its description.</summary>
    [Fact]
    public void Build_ListsTheTeamsFoldersAndEveryFile()
    {
        using TeamMemoryHarness harness = new();
        harness.Put("pricing.md", "Pricing decisions and discounts; read before quoting a customer", "Ten percent at most.");
        harness.Put("launch.md", "Atlas launch plan and dates; read before scheduling Atlas work", "Launch in March.", project: "Atlas");

        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal());

        Assert.Equal(["Research", "Research/Atlas"], input.Folders);
        Assert.Equal(
            [
                "Research/pricing.md — Pricing decisions and discounts; read before quoting a customer",
                "Research/Atlas/launch.md — Atlas launch plan and dates; read before scheduling Atlas work",
            ],
            input.Index);
        Assert.Equal(1, input.Attempt);
    }

    /// <summary>The reserved <c>MEMORY.md</c> is never in the index.</summary>
    [Fact]
    public void Build_SkipsTheReservedIndexFile()
    {
        using TeamMemoryHarness harness = new();
        Directory.CreateDirectory(harness.Folder());
        File.WriteAllText(Path.Combine(harness.Folder(), "MEMORY.md"), "- a hand-kept index\n");

        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal());

        Assert.Empty(input.Index);
    }

    /// <summary>Past the line cap the index stops, and its last line counts the files not shown.</summary>
    [Fact]
    public void Build_OverTheLineCap_EndsWithTheCountNotShown()
    {
        using TeamMemoryHarness harness = new();
        for (int i = 0; i < TeamMemoryInputBuilder.MaxIndexLines + 5; i++)
        {
            harness.Put($"t{i:D3}.md", $"Topic {i:D3} notes here", "Body.");
        }

        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal());

        Assert.Equal(TeamMemoryInputBuilder.MaxIndexLines + 1, input.Index.Count);
        Assert.Equal("[…5 more files not shown]", input.Index[^1]);
    }

    /// <summary>Past the character cap the index stops before the line that would cross it, and counts the rest.</summary>
    [Fact]
    public void Build_OverTheCharacterCap_StopsBeforeItAndCountsTheRest()
    {
        using TeamMemoryHarness harness = new();
        string padding = new('p', 150);
        for (int i = 0; i < 100; i++)
        {
            harness.Put($"topic-{i:D3}.md", $"Topic {i:D3} {padding}", "Body.");
        }

        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal());

        List<string> listed = [.. input.Index.Take(input.Index.Count - 1)];
        Assert.True(listed.Sum(static line => line.Length + 1) <= TeamMemoryInputBuilder.MaxIndexCharacters);
        Assert.Equal($"[…{100 - listed.Count} more files not shown]", input.Index[^1]);
        Assert.InRange(listed.Count, 1, 99);
    }

    /// <summary>The candidates are the files sharing the most words with the proposal, at most two, each in full with its version.</summary>
    [Fact]
    public void Build_PicksTheBestMatchingFilesInFull()
    {
        using TeamMemoryHarness harness = new();
        string consent = harness.Put("interview-consent.md", "Interview recording consent rules; read before recording an interview", "Written consent only.");
        harness.Put("interview-schedule.md", "Interview schedule and slots; read before booking", "Tuesdays.");
        harness.Put("pricing.md", "Pricing decisions and discounts; read before quoting", "Ten percent.");

        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal("Interview recording needs written consent from the customer."));

        Assert.Equal(["interview-consent.md", "interview-schedule.md"], input.Candidates.Select(static candidate => candidate.File));
        TeamMemoryCandidate best = input.Candidates[0];
        Assert.Equal("Research", best.Folder);
        Assert.Equal(File.ReadAllText(consent), best.Text);
        Assert.Equal(MemoryVersion.Of(File.ReadAllBytes(consent)), best.Version);
    }

    /// <summary>A candidate saved with a byte-order mark reaches the Helper without it, so its text starts at the frontmatter marker.</summary>
    [Fact]
    public void Build_CandidateWithAByteOrderMark_IsShownWithoutIt()
    {
        using TeamMemoryHarness harness = new();
        string path = harness.Put("interview-consent.md", "Interview recording consent rules; read before recording an interview", "Written consent only.");
        File.WriteAllText(path, File.ReadAllText(path), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal("Interview recording consent."));

        Assert.StartsWith("---\ndescription:", input.Candidates[0].Text, StringComparison.Ordinal); // contains-ok: file text; its first bytes are the assertion
        Assert.Equal(MemoryVersion.Of(File.ReadAllBytes(path)), input.Candidates[0].Version);
    }

    /// <summary>A file sharing no word with the proposal is never a candidate.</summary>
    [Fact]
    public void Build_NoSharedWords_NoCandidates()
    {
        using TeamMemoryHarness harness = new();
        harness.Put("pricing.md", "Pricing decisions and discounts; read before quoting", "Ten percent.");

        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal("Interviews are recorded only with written consent.", why: null));

        Assert.Empty(input.Candidates);
    }

    /// <summary>On the second judgement the file the stale Verdict named comes first, whatever its score.</summary>
    [Fact]
    public void Build_ForcedFile_ComesFirst()
    {
        using TeamMemoryHarness harness = new();
        harness.Put("interview-consent.md", "Interview recording consent rules; read before recording an interview", "Written consent only.");
        harness.Put("pricing.md", "Pricing decisions and discounts; read before quoting", "Ten percent.");

        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal("Interview recording needs consent."), attempt: 2, "research", "PRICING.md");

        Assert.Equal(2, input.Attempt);
        Assert.Equal(["pricing.md", "interview-consent.md"], input.Candidates.Select(static candidate => candidate.File));
    }

    /// <summary>The words counted are letters and digits, lower-cased, three or more long; shorter words and punctuation are dropped.</summary>
    [Fact]
    public void Words_KeepsLettersAndDigitsOfThreeOrMore()
    {
        HashSet<string> words = TeamMemoryInputBuilder.Words("An Interview, in Q4 of 2027: CONSENT!");

        Assert.Equal(["2027", "consent", "interview"], words.Order(StringComparer.Ordinal));
    }
}
