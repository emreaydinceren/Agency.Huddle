#!/usr/bin/env bash
# Run-LinuxRepro.sh - reproduce the CI pipeline (restore, Release build, tests) on Linux.
#
# Runs Huddle.slnx inside the CI SDK container (mcr.microsoft.com/dotnet/sdk:11.0.100) from a
# read-only mount of this checkout, copied to /work so the host's bin/obj folders stay untouched.
# Installs node (six tests need it) and the .NET 10 runtime (the image has only 11), skips the two known-flaky streaming tests, and prints the last
# 40 lines of the test run. The NuGet cache lives in the docker volume "huddle-nuget", so only the
# first run downloads packages. Takes about 4 minutes.
#
# Start it from the repo root in Git Bash (the PowerShell tool blocks docker):
#   bash agents/scripts/Run-LinuxRepro.sh
#
# "pwd -W" prints the Windows form (E:/Repos/Huddle) that Docker Desktop can mount; plain "pwd"
# is the fallback on Linux/macOS, where "-W" does not exist.
# This file must stay LF (bash rejects CRLF); Check-Eol.ps1 skips .sh files for that reason.

docker volume create huddle-nuget >/dev/null

docker run --rm \
    -v "$(pwd -W 2>/dev/null || pwd)":/src:ro \
    -v huddle-nuget:/root/.nuget/packages \
    -e TEAM_E2E=0 \
    mcr.microsoft.com/dotnet/sdk:11.0.100 \
    bash -c 'set -uo pipefail; apt-get update -qq && apt-get install -y -qq nodejs >/dev/null; curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --runtime aspnetcore --version 10.0.15 --install-dir /usr/share/dotnet >/dev/null; cp -r /src /work && cd /work && rm -rf */*/bin */*/obj; dotnet restore Huddle.slnx -v q && dotnet build Huddle.slnx --configuration Release --no-restore -v q && dotnet test Huddle.slnx --configuration Release --no-build -- 2>&1 | tail -40'
