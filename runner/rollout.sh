#!/usr/bin/env bash
#
# Rolls out the RemoteBuildTool release in <VersionPrefix> to the runner image. The image is built under
# its own tag, then proven on master before the current tag moves to it: plan and a Release build run in
# it, and the promoted image must run remotebuildtool. A failing check moves the current tag back.
#
# Usage, on the runner host, from a checkout of the commit to roll out:
#   bash runner/rollout.sh [docker build options...]

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

source "${REPO_ROOT}/runner/lib/common.sh"

fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

sdk_version="$(sdk_version_in "${REPO_ROOT}")"
[[ -n "${sdk_version}" ]] || fail "Could not read the SDK version from global.json."

tool_version="$(tool_version_in "${REPO_ROOT}")"
[[ -n "${tool_version}" ]] || fail "Could not read <VersionPrefix> from src/RemoteBuildTool/Directory.Build.props."

readonly CANDIDATE="bitenovac-remotebuildtool-runner:${sdk_version}-${tool_version}"

bash "${REPO_ROOT}/runner/build-image.sh" "$@"

echo "==> Canary: plan and a Release build in ${CANDIDATE}"
RUNNER_IMAGE="${CANDIDATE}" bash "${REPO_ROOT}/runner/in-container.sh" plan
RUNNER_IMAGE="${CANDIDATE}" bash "${REPO_ROOT}/runner/in-container.sh" build Release

echo "==> Promoting ${CANDIDATE}"
bash "${REPO_ROOT}/runner/images.sh" promote "${CANDIDATE}"

if ! bash "${REPO_ROOT}/runner/images.sh" check; then
    echo "==> The promoted image failed its check; reverting" >&2
    bash "${REPO_ROOT}/runner/images.sh" revert
    if [[ -r /etc/remotebuildtool/.env ]]; then
        bash "${REPO_ROOT}/runner/service.sh" start
    fi
    fail "${CANDIDATE} was rolled back; the previous image is current again."
fi

if [[ -r /etc/remotebuildtool/.env ]]; then
    bash "${REPO_ROOT}/runner/service.sh" start
fi

echo "Rolled out ${CANDIDATE}."
