@{
    # Used by .github/workflows/installer-ci.yaml:
    #   Invoke-ScriptAnalyzer installer -Recurse -Settings installer/PSScriptAnalyzerSettings.psd1
    Severity     = @('Warning', 'Error')

    # install.ps1 prints its [ok]/[!!] step lines with Write-Host on purpose: the output is for
    # the person at the console and must survive `iex` in a session where streams are redirected.
    #
    # install.ps1 is a script, not a module: its helpers (Set-*, Install-*, Remove-* and the like)
    # are internal and are never called with -WhatIf or -Confirm, and the tests call them by their
    # current names, so PSUseShouldProcessForStateChangingFunctions is excluded. For the same
    # reason PSUseSingularNouns is excluded: a plural noun names a helper that works on several
    # things, and renaming the functions would only churn every caller.
    ExcludeRules = @(
        'PSAvoidUsingWriteHost'
        'PSUseShouldProcessForStateChangingFunctions'
        'PSUseSingularNouns'
    )
}
