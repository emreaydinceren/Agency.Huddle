@{
    # Used by .github/workflows/installer-ci.yaml:
    #   Invoke-ScriptAnalyzer installer -Recurse -Settings installer/PSScriptAnalyzerSettings.psd1
    Severity     = @('Warning', 'Error')

    # install.ps1 prints its [ok]/[!!] step lines with Write-Host on purpose: the output is for
    # the person at the console and must survive `iex` in a session where streams are redirected.
    ExcludeRules = @('PSAvoidUsingWriteHost')
}
