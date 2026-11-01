# Allocation report: alloc-before.nettrace
Sampled total: 1560.7 MB (estimates from AllocationTick sampling)
System.String + System.Char[] share: 16.9%

## Allocations by type

```text
        MB      %  samples  item
     336.4   21.6     3302  Microsoft.AspNetCore.Components.Endpoints.Rendering.TextChunk[]
     245.2   15.7     2406  System.String
     102.2    6.5     1005  Entry[System.String,System.Int32][]
      62.5    4.0      617  System.Int32[]
      47.9    3.1      471  Microsoft.AspNetCore.Components.RenderFragment
      41.2    2.6      380  Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrame[]
      33.4    2.1      328  Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrameArrayBuilder
      32.5    2.1      319  System.Collections.Generic.Dictionary`2[System.String,System.Object]
      31.1    2.0      275  Entry[System.UInt64,System.ValueTuple`3[System.Int32,Microsoft.AspNetCore.Components.EventCallback,System.String]][]
      30.8    2.0      304  Microsoft.AspNetCore.Components.Endpoints.EndpointComponentState
      28.4    1.8      279  Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder
      28.0    1.8      254  Entry[System.Int32,Microsoft.AspNetCore.Components.Rendering.ComponentState][]
      23.9    1.5      236  System.Text.StringBuilder
      21.1    1.4      209  System.Collections.Generic.Stack`1[System.Int32]
      19.8    1.3      195  System.Collections.Generic.Dictionary`2[System.String,System.Int32]
      19.6    1.3      177  Entry[Microsoft.AspNetCore.Components.IComponent,Microsoft.AspNetCore.Components.Rendering.ComponentState][]
      19.1    1.2      188  System.Char[]
      18.2    1.2      179  System.Object[]
      15.7    1.0      155  Entry[System.String,System.Object][]
      13.0    0.8      128  System.Action
      12.7    0.8      125  System.Func`1[System.Threading.Tasks.Task]
      10.6    0.7      103  System.Boolean
       8.4    0.5       83  Enumerator[System.String,System.Object]
       7.6    0.5       75  MudBlazor.MudIconButton
       7.0    0.5       69  System.Func`2[Microsoft.AspNetCore.Components.ParameterView,System.Threading.Tasks.Task]
```

## String-related allocations by first app method

```text
        MB      %  samples  item
     188.1   12.1     1845  Agency.Huddle.App.Gateway.GatewaySessionRequirement+<InvokeAsync>d__2.MoveNext()  [System.String]
      21.7    1.4      214  Agency.Huddle.App.Gateway.GatewaySessionRequirement+<InvokeAsync>d__2.MoveNext()  [System.Text.StringBuilder]
      14.3    0.9      140  Agency.Huddle.App.Data.SqliteTeamDirectory.ReadRoom(class Microsoft.Data.Sqlite.SqliteDataReader)  [System.String]
      12.2    0.8      120  Agency.Huddle.App.Gateway.GatewaySessionRequirement+<InvokeAsync>d__2.MoveNext()  [System.Char[]]
       7.5    0.5       74  Agency.Huddle.App.Components.Layout.SkillArrivalWatcher.<Usable>b__7_2(class Agency.Huddle.SkillPacks.Resolving.SkillEntry)  [System.String]
       4.8    0.3       47  Agency.Huddle.App.Data.SqliteTeamDirectory.ReadUser(class Microsoft.Data.Sqlite.SqliteDataReader)  [System.String]
       3.4    0.2       33  (no app frame found)  [System.String]
       3.1    0.2       30  Agency.Huddle.App.Components.Shared.RoomNavRow.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)  [System.String]
       2.7    0.2       26  Agency.Huddle.App.Components.Shared.Dialogs.MenuDialogs.ActivatorLabel(class System.String)  [System.String]
       2.5    0.2       25  Agency.Huddle.App.Tabs.TabTrail.Build(class System.String)  [System.String]
       2.5    0.2       25  Agency.Huddle.App.Library.TeamFolderProvisioner.TryReadTeamFile(class System.String,class Agency.Huddle.App.Teams.TeamFile&)  [System.Char[]]
       2.2    0.1       21  Agency.Huddle.App.Acp.PersonaFrontmatter.Parse(class System.String)  [System.String]
       1.7    0.1       16  Agency.Huddle.App.Gateway.GatewaySessionRequirement+<InvokeAsync>d__2.MoveNext()  [System.Byte[]]
       1.5    0.1       15  Agency.Huddle.App.Services.MarkdownRenderer.Render(class System.String,class Agency.Huddle.App.Tasks.ITaskReferenceResolver,class Agency.Huddle.App.Library.ILibraryReferenceResolver,class Agency.Huddle.App.Library.ILibraryNoteResolver,class Agency.Huddle.App.Library.LibraryPath,class Agency.Huddle.App.Services.MentionMarks)  [System.Char[]]
       1.3    0.1       13  Agency.Huddle.App.Components.Shared.AgentAvatar.get_InlineStyle()  [System.String]
       1.3    0.1       13  Agency.Huddle.App.Components.Shared.AgentAvatar.get_ImageSource()  [System.String[]]
       1.2    0.1       12  Agency.Huddle.App.Acp.PersonaFrontmatter+FieldParser.StartField(class System.String)  [System.String[]]
       1.2    0.1       12  Agency.Huddle.App.Components.Pages.Chat+<>c__DisplayClass59_0.<OnDraftChanged>b__0()  [System.String]
       1.1    0.1       11  Agency.Huddle.App.Tabs.TabKeys.Describe(class System.String)  [System.String[]]
       1.1    0.1       11  Agency.Huddle.App.Components.Tasks.TaskActionsMenu.get_MenuClass()  [System.String[]]
       1.1    0.1       11  Agency.Huddle.App.Components.Shared.AgentAvatar.get_RootClass()  [System.String]
       1.0    0.1       10  Agency.Huddle.App.Avatars.Avatar.FirstTextElements(class System.String,int32)  [System.Char[]]
       1.0    0.1       10  Agency.Huddle.App.Gateway.GatewaySessionRequirement+<InvokeAsync>d__2.MoveNext()  [System.String[]]
       1.0    0.1       10  Agency.Huddle.App.Library.LibraryPathResolver.TryWalk(class Agency.Huddle.App.Library.LibraryRoot,class System.Collections.Generic.IReadOnlyList`1<class System.String>,class System.String&,class System.String&)  [System.String]
       0.9    0.1        9  Agency.Huddle.App.Data.SqliteTeamDirectory+<QueryAsync>d__41`1[System.__Canon].MoveNext()  [System.Byte[]]
```

## String-related allocations by call stack (leaf first)

```text
        MB      %  samples  item
      17.5    1.1      174  [System.Text.StringBuilder]
        System.Text.StringBuilder.AppendWithExpansion(wchar&,int32)
        System.Text.StringBuilder.Append(class System.String)
        MudBlazor.MudThemeProvider.GenerateTheme(class System.Text.StringBuilder)
        MudBlazor.MudThemeProvider.BuildTheme()
        MudBlazor.MudThemeProvider.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
      11.1    0.7      109  [System.String]
        MudBlazor.MudIconButton.get_Classname()
        MudBlazor.MudIconButton.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.SupplyCombinedParameters(value class Microsoft.AspNetCore.Components.ParameterView)
      10.5    0.7       98  [System.String]
        System.Text.StringBuilder.ToString()
        MudBlazor.MudThemeProvider.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.SupplyCombinedParameters(value class Microsoft.AspNetCore.Components.ParameterView)
      10.3    0.7      102  [System.String]
        System.String.Ctor(value class System.ReadOnlySpan`1<wchar>)
        MudBlazor.MudPopoverProvider+<>c__DisplayClass17_0.<BuildRenderTree>b__0(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        MudBlazor.MudRender.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       9.2    0.6       90  [System.String]
        MudBlazor.MudPopover.get_PopoverClass()
        MudBlazor.PopoverService+<CreatePopoverAsync>d__28.MoveNext()
        MudBlazor.MudPopoverBase+<OnInitializedAsync>d__24.MoveNext()
        MudBlazor.MudPopoverBase.OnInitializedAsync()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.RunInitAndSetParametersAsync()
        MudBlazor.ComponentBaseWithState.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.SupplyCombinedParameters(value class Microsoft.AspNetCore.Components.ParameterView)
       8.9    0.6       88  [System.String]
        MudBlazor.MudDataGrid`1+<>c__DisplayClass673_0[System.__Canon].<Cell>b__0(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        MudBlazor.DataGridVirtualizeRow`1+<>c__DisplayClass16_0[System.__Canon].<BuildRenderTree>b__6(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.CascadingValue`1[System.__Canon].Render(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       7.5    0.5       74  [System.String]
        Agency.Huddle.App.Components.Layout.SkillArrivalWatcher.<Usable>b__7_2(class Agency.Huddle.SkillPacks.Resolving.SkillEntry)
        System.Linq.Enumerable+ListWhereSelectIterator`2[System.__Canon,System.__Canon].MoveNext()
        Agency.Huddle.App.Components.Layout.SkillArrivalWatcher.Usable()
        Agency.Huddle.App.Components.Layout.SkillArrivalWatcher.OnInitialized()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.SupplyCombinedParameters(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.RenderTree.RenderTreeDiffBuilder.InitializeNewSubtree(value class DiffContext&,int32)
       7.0    0.4       68  [System.String]
        System.String.Ctor(value class System.ReadOnlySpan`1<wchar>)
        MudBlazor.Utilities.MudColor.ToString(value class MudBlazor.Utilities.MudColorOutputFormats)
        System.Text.StringBuilder+AppendInterpolatedStringHandler.AppendFormatted(!!0)
        MudBlazor.MudThemeProvider.GenerateTheme(class System.Text.StringBuilder)
        MudBlazor.MudThemeProvider.BuildTheme()
        MudBlazor.MudThemeProvider.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
       6.8    0.4       67  [System.String]
        System.Text.StringBuilder.ToString()
        MudBlazor.MudIcon.get_Classname()
        MudBlazor.MudIcon.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       6.7    0.4       66  [System.String]
        System.Text.StringBuilder.ToString()
        MudBlazor.MudIconButton.get_Classname()
        MudBlazor.MudIconButton.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       6.0    0.4       59  [System.String]
        MudBlazor.MudIcon.get_Classname()
        MudBlazor.MudIcon.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.SupplyCombinedParameters(value class Microsoft.AspNetCore.Components.ParameterView)
       5.8    0.4       57  [System.String]
        MudBlazor.MudMenu.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.SupplyCombinedParameters(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.RenderTree.Renderer+<RenderRootComponentAsync>d__69.MoveNext()
       5.6    0.4       55  [System.String]
        Agency.Huddle.App.Data.SqliteTeamDirectory.ReadRoom(class Microsoft.Data.Sqlite.SqliteDataReader)
        Agency.Huddle.App.Data.SqliteTeamDirectory+<QueryAsync>d__41`1[System.__Canon].MoveNext()
        Agency.Huddle.App.Data.SqliteTeamDirectory+<GetRoomsAsync>d__22.MoveNext()
        Agency.Huddle.App.Components.Teams.TeamsNav+<LoadRoomsAsync>d__50.MoveNext()
        Agency.Huddle.App.Components.Teams.TeamsNav+<OnInitializedAsync>d__25.MoveNext()
        Agency.Huddle.App.Components.Teams.TeamsNav.OnInitializedAsync()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       5.4    0.3       53  [System.String]
        System.String.TrimWhiteSpaceHelper(value class System.Text.TrimType)
        MudBlazor.MudIcon.get_Classname()
        MudBlazor.MudIcon.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       5.2    0.3       51  [System.Char[]]
        System.GC.<AllocateUninitializedArray>g__AllocateNewArrayWorker|77_0(int32,bool)
        System.Text.StringBuilder.AppendWithExpansion(wchar&,int32)
        System.Text.StringBuilder.Append(class System.String)
        MudBlazor.MudThemeProvider.GenerateTheme(class System.Text.StringBuilder)
        MudBlazor.MudThemeProvider.BuildTheme()
        MudBlazor.MudThemeProvider.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
       4.9    0.3       48  [System.String]
        System.Text.StringBuilder.ToString()
        MudBlazor.MudTooltip.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.SupplyCombinedParameters(value class Microsoft.AspNetCore.Components.ParameterView)
       4.9    0.3       48  [System.String]
        Agency.Huddle.App.Data.SqliteTeamDirectory.ReadRoom(class Microsoft.Data.Sqlite.SqliteDataReader)
        Agency.Huddle.App.Data.SqliteTeamDirectory+<QueryAsync>d__41`1[System.__Canon].MoveNext()
        Agency.Huddle.App.Data.SqliteTeamDirectory+<GetRoomsAsync>d__22.MoveNext()
        Agency.Huddle.App.Components.Layout.TabSync+<ReconcileAsync>d__8.MoveNext()
        Agency.Huddle.App.Components.Layout.TabSync.ReconcileAsync()
        Agency.Huddle.App.Components.Shared.RendererDispatch+<RunAsync>d__0.MoveNext()
        Agency.Huddle.App.Components.Layout.TabSync.OnChanged()
        Agency.Huddle.App.Components.Layout.TabHost.OnInitialized()
       4.7    0.3       46  [System.String]
        System.Text.StringBuilder.ToString()
        MudBlazor.MudPopover.get_PopoverClass()
        MudBlazor.PopoverService+<CreatePopoverAsync>d__28.MoveNext()
        MudBlazor.MudPopoverBase+<OnInitializedAsync>d__24.MoveNext()
        MudBlazor.MudPopoverBase.OnInitializedAsync()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.RunInitAndSetParametersAsync()
        MudBlazor.ComponentBaseWithState.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       4.3    0.3       43  [System.String]
        System.String.TrimWhiteSpaceHelper(value class System.Text.TrimType)
        MudBlazor.MudPopover.get_PopoverClass()
        MudBlazor.PopoverService+<CreatePopoverAsync>d__28.MoveNext()
        MudBlazor.MudPopoverBase+<OnInitializedAsync>d__24.MoveNext()
        MudBlazor.MudPopoverBase.OnInitializedAsync()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.RunInitAndSetParametersAsync()
        MudBlazor.ComponentBaseWithState.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       4.1    0.3       40  [System.String]
        Agency.Huddle.App.Data.SqliteTeamDirectory.ReadUser(class Microsoft.Data.Sqlite.SqliteDataReader)
        Agency.Huddle.App.Data.SqliteTeamDirectory+<QueryAsync>d__41`1[System.__Canon].MoveNext()
        Agency.Huddle.App.Data.SqliteTeamDirectory+<GetRoomMembersAsync>d__25.MoveNext()
        Agency.Huddle.App.Components.Shared.RoomNavRow+<OnParametersSetAsync>d__22.MoveNext()
        Agency.Huddle.App.Components.Shared.RoomNavRow.OnParametersSetAsync()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.SupplyCombinedParameters(value class Microsoft.AspNetCore.Components.ParameterView)
       3.9    0.2       38  [System.String]
        System.Text.StringBuilder.ToString()
        MudBlazor.MudPopover.get_PopoverStyles()
        MudBlazor.PopoverService+<CreatePopoverAsync>d__28.MoveNext()
        MudBlazor.MudPopoverBase+<OnInitializedAsync>d__24.MoveNext()
        MudBlazor.MudPopoverBase.OnInitializedAsync()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.RunInitAndSetParametersAsync()
        MudBlazor.ComponentBaseWithState.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       3.8    0.2       37  [System.Text.StringBuilder]
        System.Text.StringBuilder.AppendWithExpansion(wchar&,int32)
        System.Text.StringBuilder+AppendInterpolatedStringHandler.AppendFormatted(class System.String)
        MudBlazor.MudThemeProvider.GenerateTheme(class System.Text.StringBuilder)
        MudBlazor.MudThemeProvider.BuildTheme()
        MudBlazor.MudThemeProvider.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
       3.8    0.2       37  [System.String]
        System.String.Ctor(value class System.ReadOnlySpan`1<wchar>)
        MudBlazor.MudPopover.get_PopoverStyles()
        MudBlazor.PopoverService+<CreatePopoverAsync>d__28.MoveNext()
        MudBlazor.MudPopoverBase+<OnInitializedAsync>d__24.MoveNext()
        MudBlazor.MudPopoverBase.OnInitializedAsync()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.RunInitAndSetParametersAsync()
        MudBlazor.ComponentBaseWithState.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       3.6    0.2       35  [System.String]
        System.String.Ctor(value class System.ReadOnlySpan`1<wchar>)
        MudBlazor.MudPopover.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.SupplyCombinedParameters(value class Microsoft.AspNetCore.Components.ParameterView)
       3.2    0.2       31  [System.String]
        System.Text.StringBuilder.ToString()
        MudBlazor.MudMenu.get_ActivatorClassname()
        MudBlazor.MudMenu.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
```
