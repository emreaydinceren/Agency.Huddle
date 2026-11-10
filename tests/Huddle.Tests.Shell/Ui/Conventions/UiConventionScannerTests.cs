namespace Agency.Huddle.Tests.Ui.Conventions;

/// <summary>
/// Pins the scanner behind the UI convention test (plan section 3.2, rules CV1 to CV11) over small
/// inline Razor and CSS samples: each rule counts what it names, ignores what it does not, sees a tag
/// whose attribute list spans lines, and skips commented-out markup.
/// </summary>
public sealed class UiConventionScannerTests
{
    private const string RazorPath = "src/Huddle.App/Components/Shared/Sample.razor";

    private const string CssPath = "src/Huddle.App/wwwroot/css/base.css";

    /// <summary>CV1: a button, icon button, chip, toggle group or fab with no Size is counted.</summary>
    [Fact]
    public void Cv1_ControlsWithoutSize_AreCounted()
    {
        const string source = """
            <MudButton OnClick="Go">Go</MudButton>
            <MudIconButton Icon="x" />
            <MudChip T="string">a</MudChip>
            <MudToggleGroup T="string"></MudToggleGroup>
            <MudFab />
            <MudButton Size="Size.Small">Ok</MudButton>
            """;

        RuleCount result = UiConventionScanner.Cv1(RazorPath, source);

        Assert.Equal("CV1", result.Rule);
        Assert.Equal(RazorPath, result.Path);
        Assert.Equal(5, result.Count);
    }

    /// <summary>CV1: an attribute list that spans several lines is read as one tag.</summary>
    [Fact]
    public void Cv1_MultiLineAttributeList_SizeOnLaterLineCountsAsPresent()
    {
        const string source = """
            <MudButton Variant="Variant.Text"
                       OnClick="Go"
                       Size="Size.Small">Go</MudButton>
            <MudButton Variant="Variant.Text"
                       OnClick="Go">Bare</MudButton>
            """;

        Assert.Equal(1, UiConventionScanner.Cv1(RazorPath, source).Count);
    }

    /// <summary>CV1: a tag inside a Razor comment or an HTML comment is not counted.</summary>
    [Fact]
    public void Cv1_CommentedOutTag_IsSkipped()
    {
        const string source = """
            @* <MudButton OnClick="Go">Old</MudButton> *@
            <!-- <MudIconButton Icon="x" /> -->
            <MudButton Size="Size.Small">Live</MudButton>
            """;

        Assert.Equal(0, UiConventionScanner.Cv1(RazorPath, source).Count);
    }

    /// <summary>CV2: an input missing Outlined, missing Dense, or both is counted once per tag.</summary>
    [Fact]
    public void Cv2_InputsWithoutOutlinedDense_AreCounted()
    {
        const string source = """
            <MudTextField @bind-Value="a" />
            <MudSelect T="string" Variant="Variant.Outlined" />
            <MudAutocomplete T="string" Margin="Margin.Dense" />
            <MudNumericField T="int" Variant="Variant.Outlined" Margin="Margin.Dense" />
            <MudDatePicker Variant="Variant.Filled" Margin="Margin.Dense" />
            """;

        Assert.Equal(4, UiConventionScanner.Cv2(RazorPath, source).Count);
    }

    /// <summary>CV2: both attributes placed on separate lines of one tag satisfy the rule.</summary>
    [Fact]
    public void Cv2_MultiLineTagWithBothAttributes_IsClean()
    {
        const string source = """
            <MudTextField @bind-Value="a"
                          Variant="Variant.Outlined"
                          Margin="Margin.Dense" />
            """;

        Assert.Equal(0, UiConventionScanner.Cv2(RazorPath, source).Count);
    }

    /// <summary>CV3: an alert with no role is counted, one with a role is not.</summary>
    [Fact]
    public void Cv3_AlertWithoutRole_IsCounted()
    {
        const string source = """
            <MudAlert Severity="Severity.Info">x</MudAlert>
            <MudAlert Severity="Severity.Error" role="alert">y</MudAlert>
            """;

        Assert.Equal(1, UiConventionScanner.Cv3(RazorPath, source).Count);
    }

    /// <summary>CV4: a Snackbar.Add call without a key argument before its closing paren is counted.</summary>
    [Fact]
    public void Cv4_SnackbarAddWithoutKey_IsCounted()
    {
        const string source = """
            this.Snackbar.Add("Saved", Severity.Success);
            this.Snackbar.Add(
                "Failed",
                Severity.Error);
            this.Snackbar.Add("Copied", Severity.Info, key: "copied");
            """;

        Assert.Equal(2, UiConventionScanner.Cv4("src/Huddle.App/Sample.cs", source).Count);
    }

    /// <summary>CV5: ReadOnly on the named inputs is counted, on other components it is not.</summary>
    [Fact]
    public void Cv5_ReadOnlyOnInputs_IsCounted()
    {
        const string source = """
            <MudTextField @bind-Value="a" ReadOnly="true" />
            <MudSelect T="string" ReadOnly="@locked" />
            <MudCheckBox T="bool" ReadOnly="true" />
            <MudRadioGroup T="string" ReadOnly="true" />
            <MudTextField @bind-Value="b" />
            <MudSomethingElse ReadOnly="true" />
            """;

        Assert.Equal(4, UiConventionScanner.Cv5(RazorPath, source).Count);
    }

    /// <summary>CV6: Typo.h1 to h4 and raw h1 to h6 elements are counted, Typo.h5 and h6 are not.</summary>
    [Fact]
    public void Cv6_HeadingTyposAndRawHeadings_AreCounted()
    {
        const string source = """
            <MudText Typo="Typo.h1">a</MudText>
            <MudText Typo="Typo.h4">b</MudText>
            <MudText Typo="Typo.h5">c</MudText>
            <h2>d</h2>
            <h6 class="x">e</h6>
            """;

        Assert.Equal(4, UiConventionScanner.Cv6(RazorPath, source).Count);
    }

    /// <summary>CV7: null-forgiving default and null initialisers are counted.</summary>
    [Fact]
    public void Cv7_ForgivingInitialisers_AreCounted()
    {
        const string bang = "!";
        const string source = $$"""
            [Parameter] public string Name { get; set; } = default{{bang}};
            [Parameter] public Foo Item { get; set; } = null{{bang}};
            [Parameter] public string? Ok { get; set; }
            """;

        Assert.Equal(2, UiConventionScanner.Cv7(RazorPath, source).Count);
    }

    /// <summary>CV8: the banned icons and a button or menu label starting with a plus are counted.</summary>
    [Fact]
    public void Cv8_BannedIconsAndPlusLabels_AreCounted()
    {
        const string source = """
            <MudIcon Icon="@Icons.Material.Outlined.Add" />
            <MudIcon Icon="@Icons.Material.Filled.ContentPaste" />
            <MudIcon Icon="@Icons.Material.Outlined.ContentPaste" />
            <MudButton Size="Size.Small">+ New task</MudButton>
            <MudMenuItem>+ Add skill</MudMenuItem>
            <MudIcon Icon="@Icons.Material.Filled.Add" />
            <MudButton Size="Size.Small">New task</MudButton>
            """;

        Assert.Equal(5, UiConventionScanner.Cv8(RazorPath, source).Count);
    }

    /// <summary>CV9: a style whose value does not start with an at sign is counted, a bound one is not.</summary>
    [Fact]
    public void Cv9_LiteralStyleAttributes_AreCounted()
    {
        const string source = """
            <div style="width: 10px"></div>
            <MudPaper Style="margin: 0"></MudPaper>
            <div style="@this.WidthStyle"></div>
            <MudPaper Style="@Computed"></MudPaper>
            """;

        Assert.Equal(2, UiConventionScanner.Cv9(RazorPath, source).Count);
    }

    /// <summary>CV10: a DispatchAsync method declared in a component is counted, a call to one is not.</summary>
    [Fact]
    public void Cv10_DeclaredDispatchAsync_IsCounted()
    {
        const string source = """
            @code {
                private Task DispatchAsync(Func<Task> work) => this.InvokeAsync(work);

                private async Task Run() => await this.DispatchAsync(this.Load);
            }
            """;

        Assert.Equal(1, UiConventionScanner.Cv10(RazorPath, source).Count);
    }

    /// <summary>
    /// CV11: off-scale radius, literal font size, off-scale px spacing, rem spacing, high z-index and important are
    /// counted; px spacing on the scale (including a clearance such as 48px), negative or not, and 0rem are not.
    /// </summary>
    [Fact]
    public void Cv11_CssDeclarations_AreCounted()
    {
        const string source = """
            .a { border-radius: 6px; }
            .b { border-radius: 50%; }
            .c { border-radius: calc(var(--mud-default-borderradius) * 2); }
            .d { font-size: 13px; }
            .e { padding: 10px; }
            .f { margin: 0 1px; }
            .g { gap: 4px; }
            .h { z-index: 5; }
            .i { z-index: 1; }
            .j { color: red !important; }
            .k { padding: 24px 16px 8px; margin: -16px; }
            .l { padding: 6px 8px; }
            .m { padding-right: 48px; margin: 0rem; }
            .n { gap: 0.5rem; }
            .o { margin-inline: -1rem; }
            .p { border-radius: var(--hu-feed-card-radius); }
            """;

        RuleCount result = UiConventionScanner.Cv11(CssPath, source);

        Assert.Equal("CV11", result.Rule);
        Assert.Equal(8, result.Count);
    }

    /// <summary>CV11: a violation inside a CSS comment is not counted.</summary>
    [Fact]
    public void Cv11_CommentedOutDeclaration_IsSkipped()
    {
        const string source = """
            /* .a { border-radius: 6px; font-size: 13px; } */
            .b { border-radius: 0; }
            """;

        Assert.Equal(0, UiConventionScanner.Cv11(CssPath, source).Count);
    }

    /// <summary>CV13: a raw <c>MudChip</c> is counted wherever it is written, but not a <c>MudChipSet</c>, a <c>Chip</c> or the generic type name in code.</summary>
    [Fact]
    public void Cv13_RawMudChip_IsCounted()
    {
        const string source = """
            <MudChipSet T="string"><MudChip T="string" Size="Size.Small">a</MudChip></MudChipSet>
            <Chip T="string" Look="ChipLook.Tag">b</Chip>
            <MudChip T="TaskId" Size="Size.Small" Value="x" />
            @code { private void Close(MudChip<string> chip) { } }
            """;

        RuleCount result = UiConventionScanner.Cv13(RazorPath, source);

        Assert.Equal("CV13", result.Rule);
        Assert.Equal(2, result.Count);
    }

    /// <summary>CV13: the one <c>Chip</c> component is the only file allowed to draw a <c>MudChip</c>.</summary>
    [Fact]
    public void Cv13_ChipRazor_IsExempt()
    {
        const string source = """<MudChip T="T" Size="Size.Small">@this.ChildContent</MudChip>""";

        Assert.Equal(0, UiConventionScanner.Cv13("src/Huddle.App/Components/Shared/Chip.razor", source).Count);
        Assert.Equal(1, UiConventionScanner.Cv13("src/Huddle.App/Components/Shared/StatusChip.razor", source).Count);
    }
}
