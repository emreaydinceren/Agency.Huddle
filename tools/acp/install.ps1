Push-Location $PSScriptRoot
try
{
    npm install --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw "npm install failed with exit code $LASTEXITCODE" }
}
finally
{
    Pop-Location
}
