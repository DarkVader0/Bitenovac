#!/usr/bin/env bash
#
# Runs the RemoteBuildTool service on this machine: one container, restarted whenever it stops,
# that fills main from merged pull requests, records every build, compacts telemetry, prunes logs
# and rebuilds main every night. See "remotebuildtool serve".
#
# Usage:
#   sudo bash runner/service.sh start     (Re)create the container from the current runner image
#   sudo bash runner/service.sh stop      Stop and remove it; a running build is cancelled
#   sudo bash runner/service.sh logs      Follow what it does
#   sudo bash runner/service.sh rebuild   Empty main and restart, so the service rebuilds it now
#
# runner/setup-runner.sh writes the settings it reads, /etc/remotebuildtool/config.json, and
# .env beside it with the account and limits to run under. runner/build-image.sh restarts it, so
# a new image takes effect at once.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly CONTAINER="remotebuildtool-service"
readonly SETTINGS_DIR="/etc/remotebuildtool"

fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

command="${1:-}"
case "${command}" in
    start | stop | logs | rebuild) ;;
    *) fail "Unknown command '${command}'. Use: start | stop | logs | rebuild" ;;
esac

if [[ "${command}" == stop ]]; then
    docker rm --force "${CONTAINER}" > /dev/null 2>&1 || true
    echo "Stopped ${CONTAINER}."
    exit 0
fi

if [[ "${command}" == logs ]]; then
    exec docker logs --follow --tail 100 "${CONTAINER}"
fi

[[ -e "${SETTINGS_DIR}/config.json" && -r "${SETTINGS_DIR}/.env" ]] \
    || fail "No service settings in ${SETTINGS_DIR}. Run runner/setup-runner.sh with --api-token."

# Account, limits and volumes; written by setup-runner.sh.
# shellcheck source=/dev/null
source "${SETTINGS_DIR}/.env"

sdk_version="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "${REPO_ROOT}/global.json" | head -n 1)"
[[ -n "${sdk_version}" ]] || fail "Could not read the SDK version from global.json."

readonly IMAGE="bitenovac-remotebuildtool-runner:${sdk_version}"
docker image inspect "${IMAGE}" > /dev/null 2>&1 \
    || fail "The image ${IMAGE} is missing. Run runner/build-image.sh on this machine."

docker rm --force "${CONTAINER}" > /dev/null 2>&1 || true

if [[ "${command}" == rebuild ]]; then
    docker run --rm --user 0 --volume "${MAIN_VOLUME}:/mnt/main" --entrypoint find "${IMAGE}" /mnt/main -mindepth 1 -delete
    echo "Emptied ${MAIN_VOLUME}; the service rebuilds it as it starts."
fi

run_args=(
    --detach
    --name "${CONTAINER}"
    --restart unless-stopped
    --init
    --user "${SERVICE_UID}:${SERVICE_GID}"
    --volume "${SETTINGS_DIR}/config.json:/etc/remotebuildtool/config.json:ro"
    --volume "${NUGET_VOLUME}:/cache"
    --volume "${MAIN_VOLUME}:/mnt/main"
    --volume "${LOGS_VOLUME}:/mnt/logs"
    # At the share's own path, so the views in telemetry.duckdb resolve wherever the share is mounted alike.
    --volume "${TELEMETRY_VOLUME}:/srv/bitenovac/telemetry"
    --volume "${STAGING_VOLUME}:/mnt/staging"
    --volume "${WORK_VOLUME}:/work"
    --env NUGET_PACKAGES=/cache/nuget
    --env TMPDIR=/cache/tmp
    --env HOME=/tmp
    --env DOTNET_CLI_HOME=/tmp
    --env DOTNET_NOLOGO=true
    --env DOTNET_CLI_TELEMETRY_OPTOUT=true
    --env TESTINGPLATFORM_TELEMETRY_OPTOUT=1
    --entrypoint bash
)
[[ -n "${SERVICE_CPUS:-}" ]] && run_args+=(--cpus "${SERVICE_CPUS}")
[[ -n "${SERVICE_MEMORY:-}" ]] && run_args+=(--memory "${SERVICE_MEMORY}" --memory-swap "${SERVICE_MEMORY}")

docker run "${run_args[@]}" "${IMAGE}" -c 'mkdir -p "${TMPDIR}" && exec remotebuildtool serve' > /dev/null
echo "Started ${CONTAINER} from ${IMAGE}."
