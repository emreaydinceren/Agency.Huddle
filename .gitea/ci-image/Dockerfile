# CI image for the Gitea Actions `validate`, `secret-scan`, `docs` and `installer` jobs: the stock
# .NET SDK image plus the tools those jobs used to download on every run (Node.js for the
# Huddle.Acp.Tests process tests, gitleaks for the secret scan, and for the installer checks
# ShellCheck, dash, PSScriptAnalyzer and Pester).
#
# Build it on the runner host, from the repo root:
#
#   docker build -t huddle-ci:11.0.100 .gitea/ci-image
#
# The tag must match the two `image:` lines in .gitea/workflows/ci-shared.yaml (the one place the
# PR and main workflows get their jobs from),
# and the runner's config.yaml needs `force_pull: false` so it uses this local image instead of
# trying to pull `huddle-ci` from a registry. No registry holds this image: build it on every
# machine that runs these workflows.
#
# REBUILD WHEN global.json's SDK changes. Bumping the SDK is one edit here (SDK_VERSION) plus the
# tag in the build command and the two workflow `image:` lines. Build with
# `--build-arg SDK_VERSION=<new>` or change the default below. To bump gitleaks, change
# GITLEAKS_VERSION and GITLEAKS_SHA256 (the linux_x64 line of the release's
# gitleaks_<version>_checksums.txt). To bump ShellCheck, change SHELLCHECK_VERSION and
# SHELLCHECK_SHA256 (sha256sum of the downloaded shellcheck-v<version>.linux.x86_64.tar.xz; the
# release publishes no checksum file). To bump PSScriptAnalyzer or Pester, change
# PSSCRIPTANALYZER_VERSION or PESTER_VERSION; keep them in step with .github/workflows/installer-ci.yaml.
# After any change, rebuild and re-tag huddle-ci:11.0.100 on the runner host (the command above).
#
# x64 only: the runner host is x86_64.

ARG SDK_VERSION=11.0.100
FROM mcr.microsoft.com/dotnet/sdk:${SDK_VERSION}

ARG GITLEAKS_VERSION=8.30.1
ARG GITLEAKS_SHA256=551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb
ARG SHELLCHECK_VERSION=0.11.0
ARG SHELLCHECK_SHA256=8c3be12b05d5c177a04c29e3c78ce89ac86f1595681cab149b65b97c4e227198
ARG PSSCRIPTANALYZER_VERSION=1.25.0
ARG PESTER_VERSION=5.7.1

# Huddle.Acp.Tests' AgentProcessLauncherTests ([Trait("Category", "Process")]) launch a real
# `node -e` subprocess; without Node they fail with `Failed to start 'node'`. Ubuntu 26.04 (this
# image's base) ships Node 22, plenty for that. The ACP adapter itself wants something newer, but
# it only runs under TEAM_E2E=1, which CI pins off.
RUN set -eux; \
    apt-get update -qq; \
    apt-get install -y -qq --no-install-recommends nodejs; \
    rm -rf /var/lib/apt/lists/*

RUN set -eux; \
    curl -sSLf "https://github.com/gitleaks/gitleaks/releases/download/v${GITLEAKS_VERSION}/gitleaks_${GITLEAKS_VERSION}_linux_x64.tar.gz" -o /tmp/gitleaks.tgz; \
    echo "${GITLEAKS_SHA256}  /tmp/gitleaks.tgz" | sha256sum -c -; \
    tar -xzf /tmp/gitleaks.tgz -C /usr/local/bin gitleaks; \
    rm /tmp/gitleaks.tgz; \
    gitleaks version

# The `installer` job's tools: ShellCheck (xz-utils only to unpack its release tarball, which is
# xz; dash is Ubuntu's /bin/sh, installed explicitly so the suites never depend on that; zip and
# unzip are what build_release_test.sh packs and lists archives with) and the two
# PowerShell modules, installed for all users so every job user finds them. The checksum is of the
# release asset itself (GitHub publishes none for it); compute it with sha256sum when bumping.
RUN set -eux; \
    apt-get update -qq; \
    apt-get install -y -qq --no-install-recommends dash xz-utils zip unzip; \
    rm -rf /var/lib/apt/lists/*; \
    curl -sSLf "https://github.com/koalaman/shellcheck/releases/download/v${SHELLCHECK_VERSION}/shellcheck-v${SHELLCHECK_VERSION}.linux.x86_64.tar.xz" -o /tmp/shellcheck.txz; \
    echo "${SHELLCHECK_SHA256}  /tmp/shellcheck.txz" | sha256sum -c -; \
    tar -xJf /tmp/shellcheck.txz -C /tmp; \
    install -m 0755 "/tmp/shellcheck-v${SHELLCHECK_VERSION}/shellcheck" /usr/local/bin/shellcheck; \
    rm -rf /tmp/shellcheck.txz "/tmp/shellcheck-v${SHELLCHECK_VERSION}"; \
    shellcheck --version

RUN set -eux; \
    pwsh -NoProfile -NonInteractive -Command " \
      \$ErrorActionPreference = 'Stop'; \
      Set-PSRepository PSGallery -InstallationPolicy Trusted; \
      Install-Module PSScriptAnalyzer -RequiredVersion ${PSSCRIPTANALYZER_VERSION} -Scope AllUsers -Force -SkipPublisherCheck; \
      Install-Module Pester -RequiredVersion ${PESTER_VERSION} -Scope AllUsers -Force -SkipPublisherCheck; \
      Import-Module PSScriptAnalyzer, Pester; \
      Get-Module PSScriptAnalyzer, Pester | Format-Table Name, Version"
