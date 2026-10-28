#!/usr/bin/env bash
#
# Copies the runner bundle of an Official drop into a folder, for the Publish runner bundle workflow.
#
# Usage, on the runner host:
#   bash deploy/runner-bundle.sh <output> [drop]
#
#   output  An empty or missing directory to copy the bundle into.
#   drop    The drop, for example 2026.10.11.001. Defaults to the newest one.
#
# Prints drop=<version> and sha=<commit the drop was built from> on stdout, in the form the
# workflow reads from GITHUB_OUTPUT. The bundle is left in <output> with the drop's build-graph.json.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# shellcheck source=../runner/lib/common.sh
source "${REPO_ROOT}/runner/lib/common.sh"

fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

output="${1:-}"
[[ -n "${output}" ]] || fail "Name the folder to copy the bundle into."
mkdir -p "${output}"
[[ -z "$(ls -A "${output}")" ]] || fail "${output} is not empty."
output="$(cd "${output}" && pwd)"

drop="${2:-}"
[[ -z "${drop}" || "${drop}" =~ ^[0-9]{4}\.[0-9]{2}\.[0-9]{2}\.[0-9]{3}$ ]] \
    || fail "'${drop}' is not a drop name. Drops are named yyyy.MM.dd.NNN."

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
    --volume "${OFFICIAL_VOLUME}:/mnt/official:ro" \
    --volume "${output}:/mnt/output" \
    --env "DROP=${drop}" \
    --entrypoint bash \
    "${IMAGE}" -c '
        set -euo pipefail
        if [[ -z "${DROP}" ]]; then
            DROP="$(ls -1 /mnt/official | grep -E "^[0-9]{4}\.[0-9]{2}\.[0-9]{2}\.[0-9]{3}$" | sort | tail -n 1 || true)"
        fi
        [[ -n "${DROP}" ]] || { echo "error: there is no drop to publish." >&2; exit 1; }
        [[ -d "/mnt/official/${DROP}/runner" ]] \
            || { echo "error: drop ${DROP} has no runner bundle; it was made before bundles were added to drops." >&2; exit 1; }
        cp /mnt/official/"${DROP}"/runner/bitenovac-runner-* /mnt/official/"${DROP}"/build-graph.json /mnt/output/
        printf "%s" "${DROP}" > /mnt/output/drop
    '

drop_name="$(cat "${output}/drop")"
rm "${output}/drop"
sha="$(sed -n 's/.*"sha": *"\([0-9a-f]*\)".*/\1/p' "${output}/build-graph.json" | head -n 1)"
[[ -n "${sha}" ]] || fail "The drop ${drop_name} does not name the commit it was built from."

printf 'drop=%s\nsha=%s\n' "${drop_name}" "${sha}"
