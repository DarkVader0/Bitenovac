#!/usr/bin/env bash
#
# Queries the telemetry share with the DuckDB CLI baked into the runner image.
#
# Usage:
#   bash runner/telemetry.sh shell            Query telemetry.duckdb interactively
#   bash runner/telemetry.sh query "<SQL>"    Run one query and print its result
#
# The RemoteBuildTool service rebuilds telemetry.duckdb every day when it compacts the share: a
# view per table over the Parquet of earlier days and today's raw records, and the ready-made
# queries. Its views name the share by its path, /srv/bitenovac/telemetry, so it is mounted there.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly TELEMETRY_VOLUME="${REMOTEBUILDTOOL_TELEMETRY_VOLUME:-bitenovac-telemetry}"
readonly SHARE="/srv/bitenovac/telemetry"

fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

command="${1:-}"
case "${command}" in
    shell) ;;
    query) [[ -n "${2:-}" ]] || fail "query needs the SQL to run." ;;
    *) fail "Unknown command '${command}'. Use: shell | query \"<SQL>\"" ;;
esac

sdk_version="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "${REPO_ROOT}/global.json" | head -n 1)"
[[ -n "${sdk_version}" ]] || fail "Could not read the SDK version from global.json."

readonly IMAGE="bitenovac-remotebuildtool-runner:${sdk_version}"

docker image inspect "${IMAGE}" > /dev/null 2>&1 \
    || fail "The image ${IMAGE} is missing. Run runner/build-image.sh on this machine."

docker volume inspect "${TELEMETRY_VOLUME}" > /dev/null 2>&1 \
    || fail "The volume ${TELEMETRY_VOLUME} does not exist. Run runner/setup-runner.sh on this machine."

run_args=(
    --rm
    --init
    --user "$(id -u):$(id -g)"
    --volume "${TELEMETRY_VOLUME}:${SHARE}:ro"
    --env HOME=/tmp
    --entrypoint bash
)

[[ "${command}" == shell ]] && run_args+=(--interactive --tty)

exec docker run "${run_args[@]}" "${IMAGE}" -c '
    set -euo pipefail
    database="$1/telemetry.duckdb"
    if [[ ! -f "${database}" ]]; then
        echo "No ${database} yet: the service builds it at its first daily housekeeping." >&2
        exit 1
    fi
    if [[ "$2" == shell ]]; then
        exec duckdb -readonly "${database}"
    fi
    exec duckdb -readonly "${database}" -c "$3"
' telemetry "${SHARE}" "$@"
