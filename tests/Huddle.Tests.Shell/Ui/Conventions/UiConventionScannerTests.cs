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
            .p { border-radius: var(--hu-radius-card); }
            .q { border-radius: 0 var(--hu-radius-inner) var(--hu-radius-inner) 0; }
            .r { border-radius: var(--hu-radius-full); }
            .s { border-radius: var(--hu-feed-card-radius); }
            """;

        RuleCount result = UiConventionScanner.Cv11(CssPath, source);

        Assert.Equal("CV11", result.Rule);
        Assert.Equal(10, result.Count);
    }

    /// <summary>
    /// CV11: a radius is a step (<c>var(--hu-radius-*)</c>), 0 or 50%; a multiplier of the base, a base plus a length, the raw
    /// <c>--mud-default-borderradius</c>, a literal <c>999px</c> and a skin radius token are each counted once.
    /// </summary>
    [Fact]
    public void Cv11_RadiusMustBeAStep()
    {
        const string source = """
            .a { border-radius: var(--hu-radius-control); }
            .b { border-radius: var(--hu-radius-panel) var(--hu-radius-panel) 0 0; }
            .c { border-radius: var(--mud-default-borderradius); }
            .d { border-radius: calc(var(--mud-default-borderradius) * 0.75); }
            .e { border-radius: calc(var(--mud-default-borderradius) + 2px); }
            .f { border-radius: 999px; }
            .g { border-radius: var(--hu-gate-radius); }
            .h { border-radius: var(--hu-radius-huge); }
            """;

        Assert.Equal(6, UiConventionScanner.Cv11(CssPath, source).Count);
    }

    /// <summary>
    /// CV11: a width or height from 17px to 47px must be on the size ladder (18, 20, 24, 28, 32, 40, 44); a smaller mark, a larger
    /// layout size, a size in rem and a Fluent switch track are not checked.
    /// </summary>
    [Fact]
    public void Cv11_ControlSizeMustBeOnTheLadder()
    {
        const string source = """
            .a { height: 32px; }
            .b { min-height: 30px; }
            .c { width: 36px; height: 36px; }
            .d { height: 19px; }
            .e { width: 10px; height: 3px; }
            .f { width: 72px; min-height: 61px; }
            .g { height: 2rem; max-width: 33px; }
            .h { width: 26px; }
            .i .mud-switch-track { width: 36px; height: 22px; }
            .j { min-width: 24px; }
            """;

        Assert.Equal(5, UiConventionScanner.Cv11(CssPath, source).Count);
    }

    /// <summary>CV11: a glyph (a <c>.mud-icon-root</c> or the Feed check) is 14, 16, 18 or 32px; 12px and 20px are counted, a button box is not a glyph.</summary>
    [Fact]
    public void Cv11_GlyphSizeMustBeAnIconSize()
    {
        const string source = """
            .a .mud-icon-root { width: 14px; height: 14px; }
            .b .mud-icon-root { width: 12px; height: 12px; }
            .c .mud-icon-root { width: 20px; }
            .d .feed-check { width: 12px; }
            .e .mud-icon-button { width: 28px; height: 28px; }
            .f .mud-icon-root { width: 32px; }
            """;

        Assert.Equal(4, UiConventionScanner.Cv11(CssPath, source).Count);
    }

    /// <summary>CV11: a window <c>@media</c> width is MudBlazor's 600 or 960, or the shell's 1099; any other width is counted, and a container query is not a window query.</summary>
    [Fact]
    public void Cv11_WindowBreakpointMustBeOnTheGrid()
    {
        const string source = """
            @media (max-width: 600px) { .a { display: none; } }
            @media (min-width: 960px) { .b { display: none; } }
            @media (max-width: 1099px) { .c { display: none; } }
            @media (max-width: 1500px) { .d { display: none; } }
            @media (hover: none) { .e { display: none; } }
            @container task-list (max-width: 1170px) { .f { display: none; } }
            """;

        Assert.Equal(1, UiConventionScanner.Cv11(CssPath, source).Count);
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

    /// <summary>CV14: a raw <c>MudToggleGroup</c> is counted, but not its items, a <c>SegmentedControl</c> or the generic type name in code.</summary>
    [Fact]
    public void Cv14_RawMudToggleGroup_IsCounted()
    {
        const string source = """
            <MudToggleGroup T="string" Size="Size.Small"><MudToggleItem Value="@("a")" /></MudToggleGroup>
            <SegmentedControl T="string" Options="this.options" />
            <MudToggleGroup T="int" Size="Size.Small" Value="1" />
            @code { private MudToggleGroup<string>? group; }
            """;

        RuleCount result = UiConventionScanner.Cv14(RazorPath, source);

        Assert.Equal("CV14", result.Rule);
        Assert.Equal(2, result.Count);
    }

    /// <summary>CV14: the one <c>SegmentedControl</c> component is the only file allowed to draw a <c>MudToggleGroup</c>.</summary>
    [Fact]
    public void Cv14_SegmentedControlRazor_IsExempt()
    {
        const string source = """<MudToggleGroup T="T" Size="Size.Small" Value="@this.Value" />""";

        Assert.Equal(0, UiConventionScanner.Cv14("src/Huddle.App/Components/Shared/SegmentedControl.razor", source).Count);
        Assert.Equal(1, UiConventionScanner.Cv14("src/Huddle.App/Components/Shared/RoomList.razor", source).Count);
    }

    /// <summary>CV15: a raw <c>MudButton</c> is counted, but not an <c>ActionButton</c>, an icon button, a button group or the type name in code.</summary>
    [Fact]
    public void Cv15_RawMudButton_IsCounted()
    {
        const string source = """
            <MudButton Size="Size.Small" Variant="Variant.Text">a</MudButton>
            <ActionButton Role="ButtonRole.Quiet">b</ActionButton>
            <MudIconButton Size="Size.Small" Icon="@Icons.Material.Filled.Close" aria-label="Close" />
            <MudButtonGroup Size="Size.Small"></MudButtonGroup>
            <MudButton Size="Size.Small"
                       Variant="Variant.Filled" />
            @code { private MudButton? button; }
            """;

        RuleCount result = UiConventionScanner.Cv15(RazorPath, source);

        Assert.Equal("CV15", result.Rule);
        Assert.Equal(2, result.Count);
    }

    /// <summary>CV15: the one <c>ActionButton</c> component is the only file allowed to draw a <c>MudButton</c>.</summary>
    [Fact]
    public void Cv15_ActionButtonRazor_IsExempt()
    {
        const string source = """<MudButton Variant="@this.Variant" Size="Size.Small">x</MudButton>""";

        Assert.Equal(0, UiConventionScanner.Cv15("src/Huddle.App/Components/Shared/ActionButton.razor", source).Count);
        Assert.Equal(1, UiConventionScanner.Cv15("src/Huddle.App/Components/Shared/DialogFooter.razor", source).Count);
    }

    /// <summary>CV16: a raw <c>MudAlert</c> is counted, across line breaks, but not a <c>Notice</c> or the type name in code.</summary>
    [Fact]
    public void Cv16_RawMudAlert_IsCounted()
    {
        const string source = """
            <MudAlert Severity="Severity.Error" Dense="true" role="alert">a</MudAlert>
            <Notice Severity="NoticeLevel.Warning">b</Notice>
            <MudAlert Severity="Severity.Info"
                      role="status">c</MudAlert>
            @code { private MudAlert? alert; }
            """;

        RuleCount result = UiConventionScanner.Cv16(RazorPath, source);

        Assert.Equal("CV16", result.Rule);
        Assert.Equal(2, result.Count);
    }

    /// <summary>CV16: the one <c>Notice</c> component is the only file allowed to draw a <c>MudAlert</c>.</summary>
    [Fact]
    public void Cv16_NoticeRazor_IsExempt()
    {
        const string source = """<MudAlert Severity="@this.MudSeverity" Dense="true" role="@this.Role">x</MudAlert>""";

        Assert.Equal(0, UiConventionScanner.Cv16("src/Huddle.App/Components/Shared/Notice.razor", source).Count);
        Assert.Equal(1, UiConventionScanner.Cv16("src/Huddle.App/Components/Shared/Composer.razor", source).Count);
    }

    /// <summary>CV17: a raw <c>MudTextField</c> is counted, across line breaks, but not a <c>TextField</c>, a <c>SearchField</c> or the type name in code.</summary>
    [Fact]
    public void Cv17_RawMudTextField_IsCounted()
    {
        const string source = """
            <MudTextField T="string" Label="a" Variant="Variant.Outlined" Margin="Margin.Dense" />
            <TextField Label="b" Shape="TextFieldShape.LongText" />
            <SearchField Placeholder="c" AriaLabel="c" />
            <MudTextField T="string"
                          aria-label="d" Variant="Variant.Outlined" Margin="Margin.Dense" />
            @code { private MudTextField<string>? field; }
            """;

        RuleCount result = UiConventionScanner.Cv17(RazorPath, source);

        Assert.Equal("CV17", result.Rule);
        Assert.Equal(2, result.Count);
    }

    /// <summary>CV17: the one <c>TextField</c> component is the only file allowed to draw a <c>MudTextField</c>.</summary>
    [Fact]
    public void Cv17_TextFieldRazor_IsExempt()
    {
        const string source = """<MudTextField T="string" Value="@this.Value" Variant="Variant.Outlined" Margin="Margin.Dense" />""";

        Assert.Equal(0, UiConventionScanner.Cv17("src/Huddle.App/Components/Shared/TextField.razor", source).Count);
        Assert.Equal(1, UiConventionScanner.Cv17("src/Huddle.App/Components/Shared/SearchField.razor", source).Count);
    }

    /// <summary>CV18: a raw <c>MudSelect</c> is counted, but not its items, a <c>SelectField</c> or the type name in code.</summary>
    [Fact]
    public void Cv18_RawMudSelect_IsCounted()
    {
        const string source = """
            <MudSelect T="string" Label="a" Variant="Variant.Outlined" Margin="Margin.Dense">
                <MudSelectItem T="string" Value="@("x")">x</MudSelectItem>
            </MudSelect>
            <SelectField T="string" Label="b"><MudSelectItem T="string" Value="@("y")">y</MudSelectItem></SelectField>
            @code { private MudSelect<string>? select; }
            """;

        RuleCount result = UiConventionScanner.Cv18(RazorPath, source);

        Assert.Equal("CV18", result.Rule);
        Assert.Equal(1, result.Count);
    }

    /// <summary>CV18: the one <c>SelectField</c> component is the only file allowed to draw a <c>MudSelect</c>.</summary>
    [Fact]
    public void Cv18_SelectFieldRazor_IsExempt()
    {
        const string source = """<MudSelect T="T" Value="@this.Value" Variant="Variant.Outlined" Margin="Margin.Dense">x</MudSelect>""";

        Assert.Equal(0, UiConventionScanner.Cv18("src/Huddle.App/Components/Shared/SelectField.razor", source).Count);
        Assert.Equal(1, UiConventionScanner.Cv18("src/Huddle.App/Components/Shared/AdapterModelEffortPicker.razor", source).Count);
    }
}