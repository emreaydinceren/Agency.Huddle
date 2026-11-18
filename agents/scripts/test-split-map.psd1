@{
    # The folder-to-feature table of agents/Testing.md ("Where a test goes"), as data for Split-TestProject.ps1.
    # Paths are relative to the old project root, with forward slashes. Resolution order for a file:
    # Files, then RootFiles, then the longest matching Folders prefix, then Fallback (files directly in
    # that folder only), else unmapped.
    Folders   = @{
        Domain     = @{
            'Tasks'           = 'Tasks'
            'Ui/Tasks'        = 'Tasks'

            'Library'         = 'Library'
            'DocHistory'      = 'Library'
            'References'      = 'Library'
            'Search'          = 'Library'
            'FileChanges'     = 'Library'
            'Uploads'         = 'Library'
            'Ui/Library'      = 'Library'
            'Ui/References'   = 'Library'
            'Ui/Search'       = 'Library'

            'Teams'           = 'Teams'
            'TeamTypes'       = 'Teams'
            'Avatars'         = 'Teams'
            'Profile'         = 'Teams'
            'Skills'          = 'Teams'
            'Seeder'          = 'Teams'
            'Ui/Teams'        = 'Teams'

            'Acp'             = 'Personas'
            'Agents'          = 'Personas'
            'Prompts'         = 'Personas'
            'Memory'          = 'Personas'
            'MockAdapter'     = 'Personas'
            'Contracts'       = 'Personas'

            'Services'        = 'Rooms'
            'Acp/Tools'       = 'Rooms'
            'Elicitation'     = 'Rooms'
            'Questions'       = 'Rooms'
            'Waiting'         = 'Rooms'
            'Watching'        = 'Rooms'
            'NeedsYou'        = 'Rooms'
            'Jobs'            = 'Rooms'
            'Helpers'         = 'Rooms'
            'Feed'            = 'Rooms'
            'Ui/Helpers'      = 'Rooms'
            'Goldfish'        = 'Rooms'

            'Gateway'         = 'Gateway'
            'Desktop'         = 'Gateway'

            'Themes'          = 'Shell'
            'Appearance'      = 'Shell'
            'Css'             = 'Shell'
            'Tabs'            = 'Shell'
            'Sidebar'         = 'Shell'
            'Circuits'        = 'Shell'
            'Logging'         = 'Shell'
            'Factories'       = 'Shell'
            'Fakes'           = 'Shell'
            'Ui/Conventions'  = 'Shell'
            'Ui/Skins'        = 'Shell'
            'Ui/Traits'       = 'Shell'
            'Ui/Layout'       = 'Shell'
            'Ui/Sidebar'      = 'Shell'
            'Ui/Dialogs'      = 'Shell'

            'Ui/SkillPacks'   = 'SkillPacks'
        }
        Components = @{
            'Ui/Tasks'            = 'Tasks'

            'Ui/Library'          = 'Library'
            'Ui/DocHistory'       = 'Library'
            'Ui/References'       = 'Library'
            'Ui/Search'           = 'Library'

            'Ui/Teams'            = 'Teams'
            'Avatars'             = 'Teams'

            'Ui/Helpers'          = 'Rooms'
            'Ui/Feed'             = 'Rooms'

            'Settings/RemoteAccess' = 'Gateway'

            'Ui/Layout'           = 'Shell'
            'Ui/Sidebar'          = 'Shell'
            'Ui/Dialogs'          = 'Shell'
            'Circuits'            = 'Shell'

            'SkillPacks'          = 'SkillPacks'
            'Ui/SkillPacks'       = 'SkillPacks'
        }
    }
    Files     = @{
        Domain     = @{
            'Ui/AgentDialogParametersTests.cs'         = 'Teams'
            'Ui/AgentGroupingTests.cs'                 = 'Teams'
            'Ui/AgentProfilesTests.cs'                 = 'Teams'
            'Ui/AvatarChipCloudTests.cs'               = 'Teams'
            'Ui/AvatarSizesAndPresenceTests.cs'        = 'Teams'
            'Data/SqliteTeamDirectoryArchivedAtTests.cs' = 'Teams'
            'Data/SqliteTeamDirectoryMemberSetTests.cs'  = 'Teams'
            'Data/SqliteTeamDirectoryTeamScopeTests.cs'  = 'Teams'
            'Data/SqliteTeamDirectoryTests.cs'         = 'Teams'

            'Ui/FolderBrowserTests.cs'                 = 'Library'

            'Ui/PromptFieldFactoryTests.cs'            = 'Personas'
            'Data/PersonaEffortStoreTests.cs'          = 'Personas'
            'Data/PersonaModelStoreTests.cs'           = 'Personas'
            'Data/PersonaSpendStoreTests.cs'           = 'Personas'
            'Data/PersonaWorkModeStoreTests.cs'        = 'Personas'

            'Ui/RoomDeleteCopyTests.cs'                = 'Rooms'
            'Ui/RendererDispatchTests.cs'              = 'Rooms'
            'Data/FileChatStoreTests.cs'               = 'Rooms'
            'Data/FileChatStoreReferencesTests.cs'     = 'Rooms'
        }
        Components = @{
            'Ui/AgentAvatarTests.cs'                   = 'Teams'
            'Ui/AgentCardBaseTests.cs'                 = 'Teams'
            'Ui/AgentCardTests.cs'                     = 'Teams'
            'Ui/AgentLabelTests.cs'                    = 'Teams'
            'Ui/AvatarLibraryChipCloudTests.cs'        = 'Teams'
            'Ui/AvatarLibraryDialogTests.cs'           = 'Teams'
            'Ui/InviteAgentTests.cs'                   = 'Teams'
            'Ui/ProfileAvatarTests.cs'                 = 'Teams'
            'Ui/ProfileCardTests.cs'                   = 'Teams'
            'Ui/PresenceBadgeTests.cs'                 = 'Teams'
            'Ui/AdapterModelEffortPickerTests.cs'      = 'Teams'
            'Ui/RetroProposalCardTests.cs'             = 'Teams'

            'Ui/ComposerTests.cs'                      = 'Rooms'
            'Ui/ComposerUploadTests.cs'                = 'Rooms'
            'Ui/ElicitationCardTests.cs'               = 'Rooms'
            'Ui/HelperCardTests.cs'                    = 'Rooms'
            'Ui/NeedsYouComponentTests.cs'             = 'Rooms'
            'Ui/NeedsYouPageTests.cs'                  = 'Rooms'
            'Ui/NeedsYouTaskRowsTests.cs'              = 'Rooms'
            'Ui/NeedsYouTaskTestHost.cs'               = 'Rooms'
            'Ui/NewChatTests.cs'                       = 'Rooms'
            'Ui/ArchivedChatsDialogTests.cs'           = 'Rooms'
            'Ui/ProposalCardTests.cs'                  = 'Rooms'
            'Ui/QuestionCardTests.cs'                  = 'Rooms'
            'Ui/RoomListTests.cs'                      = 'Rooms'
            'Ui/PromptsPanelTests.cs'                  = 'Rooms'
            'Ui/PromptsPanelHighlightTests.cs'         = 'Rooms'

            'Ui/SprintProposalCardTests.cs'            = 'Tasks'

            'Ui/SkillProvenanceLineTests.cs'           = 'SkillPacks'
        }
    }
    RootFiles = @{
        Domain     = @{
            'RepoPathsTests.cs'         = 'Shell'
            'TeamOptionsDocTests.cs'    = 'Shell'
            'TempDataDirTests.cs'       = 'Shell'
            'TestTempRedirectTests.cs'  = 'Shell'
            'TestProjectLayoutTests.cs' = 'Shell'
        }
        Components = @{ }
    }
    Keep      = @('GlobalUsings.cs', 'TestAssemblyInitializer.cs', 'DomainAssemblySetupTests.cs', 'ComponentsAssemblySetupTests.cs', 'xunit.runner.json')
    Fallback  = @{
        Domain     = @{ 'Ui' = 'Shell' }
        Components = @{ 'Ui' = 'Shell' }
    }
}
