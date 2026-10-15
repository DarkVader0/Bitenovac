#!/usr/bin/env bash
#
# Builds the runner image, with RemoteBuildTool and its service installed at the release named by
# <VersionPrefix> in their csproj files. Pull requests run the RemoteBuildTool this image holds, so
# re-run this once a bumped version is on the feed, or after changing global.json or
# docker/ci-runner.Dockerfile.
#
# Usage:
#   bash runner/build-image.sh [docker build options...]
#
# Environment:
#   REMOTEBUILDTOOL_VERSION   Install this release instead of the one in the csproj.
#   REMOTEBUILDTOOL_SOURCE    NuGet feed to install it from. Defaults to nuget.org.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

sdk_version="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "${REPO_ROOT}/global.json" | head -n 1)"
[[ -n "${sdk_version}" ]] || { printf 'error: could not read the SDK version from %s/global.json.\n' "${REPO_ROOT}" >&2; exit 1; }

readonly TOOL_PROJECT="${REPO_ROOT}/src/tools/Bitenovac.RemoteBuildTool/Bitenovac.RemoteBuildTool.csproj"
tool_version="${REMOTEBUILDTOOL_VERSION:-$(sed -n 's/.*<VersionPrefix>[[:space:]]*\([^<[:space:]]*\)[[:space:]]*<\/VersionPrefix>.*/\1/p' "${TOOL_PROJECT}" | head -n 1)}"
[[ -n "${tool_version}" ]] || { printf 'error: could not read <VersionPrefix> from %s.\n' "${TOOL_PROJECT}" >&2; exit 1; }

build_args=(
    --build-arg "DOTNET_SDK_VERSION=${sdk_version}"
    --build-arg "REMOTEBUILDTOOL_VERSION=${tool_version}"
)
[[ -n "${REMOTEBUILDTOOL_SOURCE:-}" ]] && build_args+=(--build-arg "REMOTEBUILDTOOL_SOURCE=${REMOTEBUILDTOOL_SOURCE}")

docker build \
    --file "${REPO_ROOT}/docker/ci-runner.Dockerfile" \
    "${build_args[@]}" \
    --tag "bitenovac-remotebuildtool-runner:${sdk_version}" \
    "$@" \
    "${REPO_ROOT}"

# The service runs the image too; restarting it puts the new release to work at once.
if [[ -r /etc/remotebuildtool/.env ]]; then
    bash "${REPO_ROOT}/runner/service.sh" start
fi
