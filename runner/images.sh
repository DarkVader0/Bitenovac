#!/usr/bin/env bash
#
# Moves the runner image that RemoteBuildTool runs from. The image every run and the service use is the
# one tagged bitenovac-remotebuildtool-runner:<sdk>; this script keeps that tag, and a state file that
# names the current image and the one before it, so the previous one can be restored.
#
# Usage, on the runner host:
#   bash runner/images.sh promote <image>   Make <image> the current one; the current one becomes previous.
#   bash runner/images.sh revert            Make the previous image current again.
#   bash runner/images.sh check             Run remotebuildtool --version in the current image.
#
# The state file is REMOTEBUILDTOOL_IMAGE_STATE, or /var/lib/remotebuildtool/images.state.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

source "${REPO_ROOT}/runner/lib/common.sh"

fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

sdk_version="$(sdk_version_in "${REPO_ROOT}")"
[[ -n "${sdk_version}" ]] || fail "Could not read the SDK version from global.json."

readonly ALIAS="bitenovac-remotebuildtool-runner:${sdk_version}"
readonly STATE="${REMOTEBUILDTOOL_IMAGE_STATE:-/var/lib/remotebuildtool/images.state}"

current=""
previous=""
if [[ -f "${STATE}" ]]; then
    while IFS='=' read -r key value; do
        case "${key}" in
            current) current="${value}" ;;
            previous) previous="${value}" ;;
        esac
    done < "${STATE}"
fi

write_state() {
    mkdir -p "$(dirname "${STATE}")"
    printf 'current=%s\nprevious=%s\n' "$1" "$2" > "${STATE}.tmp"
    mv "${STATE}.tmp" "${STATE}"
}

image_id() {
    docker image inspect -f '{{.Id}}' "$1" 2> /dev/null || fail "There is no image $1. Build it with runner/build-image.sh first."
}

case "${1:-}" in
    promote)
        [[ $# -eq 2 ]] || fail "Name the image to promote, for example bitenovac-remotebuildtool-runner:10.0.100-1.4.0."
        candidate="$(image_id "$2")"
        if [[ "${candidate}" == "${current}" ]]; then
            echo "$2 is already the current image."
            exit 0
        fi

        docker tag "${candidate}" "${ALIAS}"
        write_state "${candidate}" "${current}"
        echo "Promoted $2; the image before it is kept as previous."
        ;;
    revert)
        [[ -n "${previous}" ]] || fail "There is no previous image to revert to."
        docker tag "${previous}" "${ALIAS}"
        write_state "${previous}" "${current}"
        echo "Reverted to the previous image."
        ;;
    check)
        docker run --rm --entrypoint remotebuildtool "${ALIAS}" --version > /dev/null \
            || fail "The current image does not run remotebuildtool."
        echo "The current image runs remotebuildtool."
        ;;
    *)
        fail "Usage: images.sh promote <image> | revert | check"
        ;;
esac
