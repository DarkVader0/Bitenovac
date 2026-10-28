#!/usr/bin/env bash
#
# Puts the runner bundle into an Official drop, as <drop>/runner/, so the Publish runner bundle
# workflow can release it under the drop's version, the way the Web app is deployed from a drop.
#
# Usage, on the runner host, from the checkout Official built:
#   bash runner/bundle-into-drop.sh <drop>
#
#   drop   The drop Official just made, for example 2026.10.11.001.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

source "${REPO_ROOT}/runner/lib/common.sh"

fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

drop="${1:-}"
[[ "${drop}" =~ ^[0-9]{4}\.[0-9]{2}\.[0-9]{2}\.[0-9]{3}$ ]] || fail "'${drop}' is not a drop name. Drops are named yyyy.MM.dd.NNN."

sdk_version="$(sdk_version_in "${REPO_ROOT}")"
[[ -n "${sdk_version}" ]] || fail "Could not read the SDK version from global.json."

readonly IMAGE="bitenovac-remotebuildtool-runner:${sdk_version}"
docker image inspect "${IMAGE}" > /dev/null 2>&1 \
    || fail "The image ${IMAGE} is missing. Run runner/build-image.sh on this machine."

docker volume inspect "${OFFICIAL_VOLUME}" > /dev/null 2>&1 \
    || fail "The volume ${OFFICIAL_VOLUME} does not exist. Has Official run on this machine?"

docker run \
    --rm \
    --init \
    --user "$(id -u):$(id -g)" \
    --volume "${OFFICIAL_VOLUME}:/mnt/official" \
    --volume "${REPO_ROOT}:/repo:ro" \
    --env "DROP=${drop}" \
    --entrypoint bash \
    "${IMAGE}" -c '
        set -euo pipefail
        [[ -d "/mnt/official/${DROP}" ]] || { echo "error: no drop ${DROP}." >&2; exit 1; }
        mkdir -p "/mnt/official/${DROP}/runner"
        cd /repo
        bash runner/make-bundle.sh "/mnt/official/${DROP}/runner"
    '
