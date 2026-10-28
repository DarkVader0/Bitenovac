#!/usr/bin/env bash
#
# Builds the runner image, with RemoteBuildTool and its service installed at the release named by
# <VersionPrefix> in src/RemoteBuildTool/Directory.Build.props.
#
# Usage:
#   bash runner/build-image.sh [docker build options...]
#
# Environment:
#   REMOTEBUILDTOOL_VERSION   Install this release instead of the one in src/RemoteBuildTool/Directory.Build.props.
#   REMOTEBUILDTOOL_SOURCE    NuGet feed to install it from. Defaults to nuget.org.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

source "${REPO_ROOT}/runner/lib/common.sh"

sdk_version="$(sdk_version_in "${REPO_ROOT}")"
[[ -n "${sdk_version}" ]] || { printf 'error: could not read the SDK version from %s/global.json.\n' "${REPO_ROOT}" >&2; exit 1; }

readonly TOOL_PROPS="${REPO_ROOT}/src/RemoteBuildTool/Directory.Build.props"
tool_version="${REMOTEBUILDTOOL_VERSION:-$(tool_version_in "${REPO_ROOT}")}"
[[ -n "${tool_version}" ]] || { printf 'error: could not read <VersionPrefix> from %s.\n' "${TOOL_PROPS}" >&2; exit 1; }

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

if [[ -r /etc/remotebuildtool/.env ]]; then
    bash "${REPO_ROOT}/runner/service.sh" start
fi
