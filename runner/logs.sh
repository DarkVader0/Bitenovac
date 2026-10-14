#!/usr/bin/env bash
#
# Maintains the logs volume, where every logged run keeps its console output and MSBuild binary logs.
#
# Usage, from a workflow step with the repository checked out:
#   bash runner/logs.sh prune
#   bash runner/logs.sh seal-official <run number>
#
# Layout, one folder per pull request or build, holding one folder per run attempt:
#
#   PR_<pr number>/<run id>-<attempt>/            kept 30 days
#   CacheRefresh_<run number>/<run id>-<attempt>/ kept 30 days
#   Official_<drop version>/<run id>-<attempt>/   kept 365 days
#   Official_failed_<run number>/...              kept 365 days, a release that dropped nothing
#
# Each attempt folder holds <command>.log per stage (plan.log, build-Debug.log, ...), a
# <command>.resources.csv beside each with the container's memory and CPU every 5 seconds, and a .binlog
# per MSBuild invocation. Age counts from the last file written into an attempt folder.
#
# An Official run cannot know its drop version until 'release' takes one, so it logs into
# Official_pending_<run number> and 'seal-official' renames that folder afterwards, leaving a link
# under the old name for the telemetry that recorded it.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly LOGS_VOLUME="${REMOTEBUILDTOOL_LOGS_VOLUME:-bitenovac-logs}"
readonly SHORT_LIVED_DAYS=30
readonly OFFICIAL_DAYS=365

fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

command="${1:-}"
case "${command}" in
    prune) ;;
    seal-official)
        [[ "${2:-}" =~ ^[0-9]+$ ]] || fail "seal-official needs the workflow run number."
        ;;
    *) fail "Unknown command '${command}'. Use: prune | seal-official <run number>" ;;
esac

sdk_version="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "${REPO_ROOT}/global.json" | head -n 1)"
[[ -n "${sdk_version}" ]] || fail "Could not read the SDK version from global.json."

readonly IMAGE="bitenovac-remotebuildtool-runner:${sdk_version}"

docker image inspect "${IMAGE}" > /dev/null 2>&1 \
    || fail "The image ${IMAGE} is missing. Run runner/build-image.sh on this machine."

# Mounting a volume that does not exist would create it, owned by root, and hide that setup was skipped.
docker volume inspect "${LOGS_VOLUME}" > /dev/null 2>&1 \
    || fail "The volume ${LOGS_VOLUME} does not exist. Run runner/setup-runner.sh on this machine."

exec docker run \
    --rm \
    --init \
    --user "$(id -u):$(id -g)" \
    --volume "${LOGS_VOLUME}:/mnt/logs" \
    --env "SHORT_LIVED_DAYS=${SHORT_LIVED_DAYS}" \
    --env "OFFICIAL_DAYS=${OFFICIAL_DAYS}" \
    --entrypoint bash \
    "${IMAGE}" -c '
        set -euo pipefail
        cd /mnt/logs

        case "$1" in
            prune)
                # Concurrent runs prune too, so a folder can vanish mid-walk; that is not a failure.
                expire() {
                    find . -mindepth 2 -maxdepth 2 -type d -path "./$1/*" -mtime "+$2" -print -exec rm -rf {} + 2> /dev/null \
                        | sed "s|^\./|pruned |" || true
                }
                expire "PR_*" "${SHORT_LIVED_DAYS}"
                expire "CacheRefresh_*" "${SHORT_LIVED_DAYS}"
                expire "Official_*" "${OFFICIAL_DAYS}"
                find . -mindepth 1 -maxdepth 1 -type d -empty -delete 2> /dev/null || true
                find . -mindepth 1 -maxdepth 1 -xtype l -delete 2> /dev/null || true
                ;;

            seal-official)
                pending="Official_pending_$2"
                # An attempt after the first logs through the link an earlier seal left, straight into its folder.
                if [[ -L "${pending}" ]]; then
                    echo "Logs already sealed as $(readlink "${pending}")"
                    exit 0
                fi
                if [[ ! -d "${pending}" ]]; then
                    echo "No logs under ${pending}; nothing to seal."
                    exit 0
                fi

                version="$(cat "${pending}"/*/release.log 2> /dev/null \
                    | sed -n "s/^Release \([0-9]\{4\}\.[0-9]\{2\}\.[0-9]\{2\}\.[0-9]\{3\}\) dropped to .*/\1/p" | tail -n 1)"
                target="Official_${version:-failed_$2}"

                # A re-run attempt seals into the folder an earlier attempt already made.
                mkdir -p "${target}"
                mv "${pending}"/* "${target}"/
                rmdir "${pending}"
                # Telemetry recorded the pending folder before the drop had a version.
                ln -s "${target}" "${pending}"
                echo "Logs sealed as ${target}"
                ;;
        esac
    ' logs "$@"
