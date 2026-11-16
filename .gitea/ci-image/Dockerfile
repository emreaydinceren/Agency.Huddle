# CI image for the Gitea Actions `validate` and `secret-scan` jobs: the stock .NET SDK image plus
# the two tools those jobs used to download on every run (Node.js for the Huddle.Acp.Tests
# process tests, gitleaks for the secret scan).
#
# Build it on the runner host, from the repo root:
#
#   docker build -t huddle-ci:11.0.100 .gitea/ci-image
#
# The tag must match the `image:` line in .gitea/workflows/ci-pr-local.yaml and ci-main-local.yaml,
# and the runner's config.yaml needs `force_pull: false` so it uses this local image instead of
# trying to pull `huddle-ci` from a registry. No registry holds this image: build it on every
# machine that runs these workflows.
#
# REBUILD WHEN global.json's SDK changes. Bumping the SDK is one edit here (SDK_VERSION) plus the
# tag in the build command and the two workflow `image:` lines. Build with
# `--build-arg SDK_VERSION=<new>` or change the default below. To bump gitleaks, change
# GITLEAKS_VERSION and GITLEAKS_SHA256 (the linux_x64 line of the release's
# gitleaks_<version>_checksums.txt).
#
# x64 only: the runner host is x86_64.

ARG SDK_VERSION=11.0.100
FROM mcr.microsoft.com/dotnet/sdk:${SDK_VERSION}

ARG GITLEAKS_VERSION=8.30.1
ARG GITLEAKS_SHA256=551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb

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
