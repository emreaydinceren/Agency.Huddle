# Allocation report: alloc-after.nettrace
Sampled total: 1577.3 MB (estimates from AllocationTick sampling)
System.String + System.Char[] share: 16.6%

## Allocations by type

```text
        MB      %  samples  item
     340.0   21.6     3338  Microsoft.AspNetCore.Components.Endpoints.Rendering.TextChunk[]
     244.4   15.5     2400  System.String
     100.3    6.4      986  Entry[System.String,System.Int32][]
      66.1    4.2      653  System.Int32[]
      46.0    2.9      452  Microsoft.AspNetCore.Components.RenderFragment
      41.2    2.6      389  Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrame[]
      33.7    2.1      331  System.Collections.Generic.Dictionary`2[System.String,System.Object]
      32.9    2.1      324  Microsoft.AspNetCore.Components.RenderTree.RenderTreeFrameArrayBuilder
      31.0    2.0      284  Entry[System.Int32,Microsoft.AspNetCore.Components.Rendering.ComponentState][]
      29.7    1.9      265  Entry[System.UInt64,System.ValueTuple`3[System.Int32,Microsoft.AspNetCore.Components.EventCallback,System.String]][]
      28.2    1.8      277  Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder
      27.4    1.7      270  Microsoft.AspNetCore.Components.Endpoints.EndpointComponentState
      24.5    1.6      242  System.Text.StringBuilder
      24.3    1.5      240  System.Collections.Generic.Stack`1[System.Int32]
      24.1    1.5      237  Entry[System.String,System.Object][]
      19.4    1.2      191  System.Collections.Generic.Dictionary`2[System.String,System.Int32]
      17.9    1.1      176  System.Object[]
      17.6    1.1      159  Entry[Microsoft.AspNetCore.Components.IComponent,Microsoft.AspNetCore.Components.Rendering.ComponentState][]
      17.4    1.1      172  System.Char[]
      17.0    1.1      168  System.Func`1[System.Threading.Tasks.Task]
      12.6    0.8      125  System.Action
      10.5    0.7      104  Enumerator[System.String,System.Object]
       9.9    0.6       97  MudBlazor.MudMenu
       9.4    0.6       93  MudBlazor.State.Builder.RegisterParameterBuilder`1[System.Boolean]
       9.3    0.6       92  System.Func`2[Microsoft.AspNetCore.Components.Web.MouseEventArgs,System.Threading.Tasks.Task]
```

## String-related allocations by first app method

```text
        MB      %  samples  item
     188.3   11.9     1848  Agency.Huddle.App.Gateway.GatewaySessionRequirement+<InvokeAsync>d__2.MoveNext()  [System.String]
      23.3    1.5      230  Agency.Huddle.App.Gateway.GatewaySessionRequirement+<InvokeAsync>d__2.MoveNext()  [System.Text.StringBuilder]
      13.9    0.9      136  Agency.Huddle.App.Data.SqliteTeamDirectory.ReadRoom(class Microsoft.Data.Sqlite.SqliteDataReader)  [System.String]
      12.4    0.8      122  Agency.Huddle.App.Gateway.GatewaySessionRequirement+<InvokeAsync>d__2.MoveNext()  [System.Char[]]
       5.9    0.4       58  Agency.Huddle.App.Data.SqliteTeamDirectory.ReadUser(class Microsoft.Data.Sqlite.SqliteDataReader)  [System.String]
       4.2    0.3       41  Agency.Huddle.App.Components.Layout.SkillArrivalWatcher.Usable()  [System.String]
       3.4    0.2       33  Agency.Huddle.App.Components.Shared.Dialogs.MenuDialogs.ActivatorLabel(class System.String)  [System.String]
       3.2    0.2       31  (no app frame found)  [System.String]
       2.4    0.2       24  Agency.Huddle.App.Tasks.TaskId.ToString()  [System.String]
       2.2    0.1       22  Agency.Huddle.App.Components.Tasks.TaskListView+<>c__DisplayClass0_23.<BuildRenderTree>b__68(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)  [System.String]
       1.9    0.1       19  Agency.Huddle.App.Acp.PersonaFrontmatter.Parse(class System.String)  [System.String]
       1.9    0.1       19  Agency.Huddle.App.Data.SqliteTeamDirectory+<QueryAsync>d__41`1[System.__Canon].MoveNext()  [System.Byte[]]
       1.8    0.1       18  Agency.Huddle.App.Components.Tasks.TaskActionsMenu.get_MenuClass()  [System.String]
       1.8    0.1       18  Agency.Huddle.App.Library.TeamFolderProvisioner.TryReadTeamFile(class System.String,class Agency.Huddle.App.Teams.TeamFile&)  [System.Char[]]
       1.7    0.1       17  Agency.Huddle.App.Tabs.TabKeys.Describe(class System.String)  [System.String[]]
       1.7    0.1       17  Agency.Huddle.App.Services.MarkdownRenderer.Render(class System.String,class Agency.Huddle.App.Tasks.ITaskReferenceResolver,class Agency.Huddle.App.Library.ILibraryReferenceResolver,class Agency.Huddle.App.Library.ILibraryNoteResolver,class Agency.Huddle.App.Library.LibraryPath,class Agency.Huddle.App.Services.MentionMarks)  [System.Char[]]
       1.3    0.1       13  Agency.Huddle.App.Services.MarkdownRenderer.Render(class System.String,class Agency.Huddle.App.Tasks.ITaskReferenceResolver,class Agency.Huddle.App.Library.ILibraryReferenceResolver,class Agency.Huddle.App.Library.ILibraryNoteResolver,class Agency.Huddle.App.Library.LibraryPath,class Agency.Huddle.App.Services.MentionMarks)  [System.String]
       1.3    0.1       13  Agency.Huddle.App.Components.Shared.AgentAvatar.get_RootClass()  [System.String]
       1.2    0.1       12  Agency.Huddle.App.Components.Pages.Chat+<>c__DisplayClass59_0.<OnDraftChanged>b__0()  [System.String]
       1.1    0.1       11  Agency.Huddle.App.Gateway.GatewaySessionRequirement+<InvokeAsync>d__2.MoveNext()  [System.String[]]
       1.0    0.1       10  Agency.Huddle.App.Components.Shared.RoomNavRow.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)  [System.String]
       0.8    0.1        8  Agency.Huddle.App.Components.Shared.AgentAvatar.get_ImageSource()  [System.String]
       0.8    0.1        8  Agency.Huddle.App.Components.Tasks.TaskActionsMenu.get_MenuClass()  [System.String[]]
       0.8    0.1        8  Agency.Huddle.App.Gateway.GatewaySessionRequirement+<InvokeAsync>d__2.MoveNext()  [System.Byte[]]
       0.7    0.0        7  Agency.Huddle.App.Components.Shared.RendererDispatch+<>c__DisplayClass1_0.<Repaint>b__0()  [System.String]
```

## String-related allocations by call stack (leaf first)

```text
        MB      %  samples  item
      18.7    1.2      185  [System.Text.StringBuilder]
        System.Text.StringBuilder.AppendWithExpansion(wchar&,int32)
        System.Text.StringBuilder.Append(class System.String)
        MudBlazor.MudThemeProvider.GenerateTheme(class System.Text.StringBuilder)
        MudBlazor.MudThemeProvider.BuildTheme()
        MudBlazor.MudThemeProvider.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
      12.3    0.8      122  [System.String]
        System.String.Ctor(value class System.ReadOnlySpan`1<wchar>)
        MudBlazor.MudPopoverProvider+<>c__DisplayClass17_0.<BuildRenderTree>b__0(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        MudBlazor.MudRender.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
      10.7    0.7      105  [System.String]
        MudBlazor.MudIconButton.get_Classname()
        MudBlazor.MudIconButton.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.SupplyCombinedParameters(value class Microsoft.AspNetCore.Components.ParameterView)
      10.3    0.7       96  [System.String]
        System.Text.StringBuilder.ToString()
        MudBlazor.MudThemeProvider.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.SupplyCombinedParameters(value class Microsoft.AspNetCore.Components.ParameterView)
       8.6    0.5       85  [System.String]
        MudBlazor.MudIcon.get_Classname()
        MudBlazor.MudIcon.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.SupplyCombinedParameters(value class Microsoft.AspNetCore.Components.ParameterView)
       8.5    0.5       84  [System.String]
        MudBlazor.MudPopover.get_PopoverClass()
        MudBlazor.PopoverService+<CreatePopoverAsync>d__28.MoveNext()
        MudBlazor.MudPopoverBase+<OnInitializedAsync>d__24.MoveNext()
        MudBlazor.MudPopoverBase.OnInitializedAsync()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.RunInitAndSetParametersAsync()
        MudBlazor.ComponentBaseWithState.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.SupplyCombinedParameters(value class Microsoft.AspNetCore.Components.ParameterView)
       7.9    0.5       78  [System.String]
        MudBlazor.MudDataGrid`1+<>c__DisplayClass673_0[System.__Canon].<Cell>b__0(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        MudBlazor.DataGridVirtualizeRow`1+<>c__DisplayClass16_0[System.__Canon].<BuildRenderTree>b__6(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.CascadingValue`1[System.__Canon].Render(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       6.7    0.4       66  [System.String]
        System.Text.StringBuilder.ToString()
        MudBlazor.MudIcon.get_Classname()
        MudBlazor.MudIcon.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       6.6    0.4       65  [System.String]
        System.String.TrimWhiteSpaceHelper(value class System.Text.TrimType)
        MudBlazor.MudPopover.get_PopoverClass()
        MudBlazor.PopoverService+<CreatePopoverAsync>d__28.MoveNext()
        MudBlazor.MudPopoverBase+<OnInitializedAsync>d__24.MoveNext()
        MudBlazor.MudPopoverBase.OnInitializedAsync()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.RunInitAndSetParametersAsync()
        MudBlazor.ComponentBaseWithState.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       6.3    0.4       63  [System.String]
        System.Text.StringBuilder.ToString()
        MudBlazor.MudIconButton.get_Classname()
        MudBlazor.MudIconButton.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       5.9    0.4       58  [System.String]
        Agency.Huddle.App.Data.SqliteTeamDirectory.ReadRoom(class Microsoft.Data.Sqlite.SqliteDataReader)
        Agency.Huddle.App.Data.SqliteTeamDirectory+<QueryAsync>d__41`1[System.__Canon].MoveNext()
        Agency.Huddle.App.Data.SqliteTeamDirectory+<GetRoomsAsync>d__22.MoveNext()
        Agency.Huddle.App.Components.Teams.TeamsNav+<LoadRoomsAsync>d__50.MoveNext()
        Agency.Huddle.App.Components.Teams.TeamsNav+<OnInitializedAsync>d__25.MoveNext()
        Agency.Huddle.App.Components.Teams.TeamsNav.OnInitializedAsync()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       5.7    0.4       56  [System.Char[]]
        System.GC.<AllocateUninitializedArray>g__AllocateNewArrayWorker|77_0(int32,bool)
        System.Text.StringBuilder.AppendWithExpansion(wchar&,int32)
        System.Text.StringBuilder.Append(class System.String)
        MudBlazor.MudThemeProvider.GenerateTheme(class System.Text.StringBuilder)
        MudBlazor.MudThemeProvider.BuildTheme()
        MudBlazor.MudThemeProvider.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
       5.6    0.4       55  [System.String]
        System.String.Ctor(value class System.ReadOnlySpan`1<wchar>)
        MudBlazor.Utilities.MudColor.ToString(value class MudBlazor.Utilities.MudColorOutputFormats)
        System.Text.StringBuilder+AppendInterpolatedStringHandler.AppendFormatted(!!0)
        MudBlazor.MudThemeProvider.GenerateTheme(class System.Text.StringBuilder)
        MudBlazor.MudThemeProvider.BuildTheme()
        MudBlazor.MudThemeProvider.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
       5.5    0.3       54  [System.String]
        Agency.Huddle.App.Data.SqliteTeamDirectory.ReadUser(class Microsoft.Data.Sqlite.SqliteDataReader)
        Agency.Huddle.App.Data.SqliteTeamDirectory+<QueryAsync>d__41`1[System.__Canon].MoveNext()
        Agency.Huddle.App.Data.SqliteTeamDirectory+<GetRoomMembersAsync>d__25.MoveNext()
        Agency.Huddle.App.Components.Shared.RoomNavRow+<OnParametersSetAsync>d__22.MoveNext()
        Agency.Huddle.App.Components.Shared.RoomNavRow.OnParametersSetAsync()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.SupplyCombinedParameters(value class Microsoft.AspNetCore.Components.ParameterView)
       5.2    0.3       51  [System.String]
        Agency.Huddle.App.Data.SqliteTeamDirectory.ReadRoom(class Microsoft.Data.Sqlite.SqliteDataReader)
        Agency.Huddle.App.Data.SqliteTeamDirectory+<QueryAsync>d__41`1[System.__Canon].MoveNext()
        Agency.Huddle.App.Data.SqliteTeamDirectory+<GetRoomsAsync>d__22.MoveNext()
        Agency.Huddle.App.Components.Layout.TabSync+<ReconcileAsync>d__8.MoveNext()
        Agency.Huddle.App.Components.Layout.TabSync.ReconcileAsync()
        Agency.Huddle.App.Components.Shared.RendererDispatch+<RunAsync>d__0.MoveNext()
        Agency.Huddle.App.Components.Layout.TabSync.OnChanged()
        Agency.Huddle.App.Components.Layout.TabHost.OnInitialized()
       5.2    0.3       51  [System.String]
        System.Text.StringBuilder.ToString()
        MudBlazor.MudPopover.get_PopoverClass()
        MudBlazor.PopoverService+<CreatePopoverAsync>d__28.MoveNext()
        MudBlazor.MudPopoverBase+<OnInitializedAsync>d__24.MoveNext()
        MudBlazor.MudPopoverBase.OnInitializedAsync()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.RunInitAndSetParametersAsync()
        MudBlazor.ComponentBaseWithState.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       5.1    0.3       50  [System.String]
        System.Text.StringBuilder.ToString()
        MudBlazor.MudPopover.get_PopoverStyles()
        MudBlazor.PopoverService+<CreatePopoverAsync>d__28.MoveNext()
        MudBlazor.MudPopoverBase+<OnInitializedAsync>d__24.MoveNext()
        MudBlazor.MudPopoverBase.OnInitializedAsync()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.RunInitAndSetParametersAsync()
        MudBlazor.ComponentBaseWithState.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       5.0    0.3       49  [System.String]
        System.String.TrimWhiteSpaceHelper(value class System.Text.TrimType)
        MudBlazor.MudIcon.get_Classname()
        MudBlazor.MudIcon.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       4.8    0.3       47  [System.String]
        System.Text.StringBuilder.ToString()
        MudBlazor.MudTooltip.get_ContainerClass()
        MudBlazor.MudTooltip.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       4.4    0.3       43  [System.Text.StringBuilder]
        System.Text.StringBuilder.AppendWithExpansion(wchar&,int32)
        System.Text.StringBuilder+AppendInterpolatedStringHandler.AppendFormatted(class System.String)
        MudBlazor.MudThemeProvider.GenerateTheme(class System.Text.StringBuilder)
        MudBlazor.MudThemeProvider.BuildTheme()
        MudBlazor.MudThemeProvider.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
       4.2    0.3       41  [System.String]
        System.Linq.Enumerable+ListWhereSelectIterator`2[System.__Canon,System.__Canon].MoveNext()
        Agency.Huddle.App.Components.Layout.SkillArrivalWatcher.Usable()
        Agency.Huddle.App.Components.Layout.SkillArrivalWatcher.OnInitialized()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.SupplyCombinedParameters(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.RenderTree.RenderTreeDiffBuilder.InitializeNewSubtree(value class DiffContext&,int32)
        Microsoft.AspNetCore.Components.RenderTree.RenderTreeDiffBuilder.InsertNewFrame(value class DiffContext&,int32)
       3.3    0.2       32  [System.String]
        System.Text.StringBuilder.ToString()
        MudBlazor.MudAppBar.get_Classname()
        MudBlazor.MudAppBar.<get_Header>b__49_0(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        MudBlazor.MudAppBar.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
       3.3    0.2       32  [System.String]
        MudBlazor.MudMenu.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.SupplyCombinedParameters(value class Microsoft.AspNetCore.Components.ParameterView)
        Microsoft.AspNetCore.Components.RenderTree.Renderer+<RenderRootComponentAsync>d__69.MoveNext()
       3.2    0.2       31  [System.String]
        System.String.Ctor(value class System.ReadOnlySpan`1<wchar>)
        MudBlazor.MudPopover.get_PopoverStyles()
        MudBlazor.PopoverService+<CreatePopoverAsync>d__28.MoveNext()
        MudBlazor.MudPopoverBase+<OnInitializedAsync>d__24.MoveNext()
        MudBlazor.MudPopoverBase.OnInitializedAsync()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
        Microsoft.AspNetCore.Components.ComponentBase.RunInitAndSetParametersAsync()
        MudBlazor.ComponentBaseWithState.SetParametersAsync(value class Microsoft.AspNetCore.Components.ParameterView)
       3.1    0.2       31  [System.String]
        System.Text.StringBuilder.ToString()
        MudBlazor.MudNavLink.get_IconClassname()
        MudBlazor.MudNavLink.<BuildRenderTree>b__72_0(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Routing.NavLink.BuildRenderTree(class Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder)
        Microsoft.AspNetCore.Components.Rendering.ComponentState.RenderIntoBatch(class Microsoft.AspNetCore.Components.Rendering.RenderBatchBuilder,class Microsoft.AspNetCore.Components.RenderFragment,class System.Exception&)
        Microsoft.AspNetCore.Components.RenderTree.Renderer.ProcessRenderQueue()
        Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged()
        Microsoft.AspNetCore.Components.ComponentBase+<RunInitAndSetParametersAsync>d__28.MoveNext()
```
