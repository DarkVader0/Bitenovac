#!/usr/bin/env bash
#
# Compacts the telemetry share and opens it for querying, with the DuckDB CLI baked into the runner
# image.
#
# Usage:
#   bash runner/telemetry.sh compact          Turn every finished day's JSON lines into Parquet and
#                                             rebuild telemetry.duckdb
#   bash runner/telemetry.sh shell            Query telemetry.duckdb interactively
#   bash runner/telemetry.sh query "<SQL>"    Run one query and print its result
#
# Layout of the share, mounted at the same path in the container as on this machine, so the views
# in telemetry.duckdb resolve from either:
#
#   <table>/date=<yyyy-MM-dd>/*.jsonl             what stages write; today's stays raw
#   parquet/<table>/date=<yyyy-MM-dd>/*.parquet   what compaction made of earlier days
#   telemetry.duckdb                               a view per table over both, and the queries of
#                                                  runner/telemetry/queries.sql
#
# A day is compacted once it is over in UTC; a stage records the day it started, so run this a
# few hours after midnight. Each table also keeps an empty file in date=0001-01-01, raw and Parquet,
# because DuckDB fails on a pattern that matches no file.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly TELEMETRY_VOLUME="${REMOTEBUILDTOOL_TELEMETRY_VOLUME:-bitenovac-telemetry}"
readonly SHARE="/srv/bitenovac/telemetry"

fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

command="${1:-}"
case "${command}" in
    compact | shell) ;;
    query) [[ -n "${2:-}" ]] || fail "query needs the SQL to run." ;;
    *) fail "Unknown command '${command}'. Use: compact | shell | query \"<SQL>\"" ;;
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
    --volume "${REPO_ROOT}/runner/telemetry:/sql:ro"
    --env "SHARE=${SHARE}"
    --env HOME=/tmp
    --entrypoint bash
)

if [[ "${command}" == compact ]]; then
    run_args+=(--volume "${TELEMETRY_VOLUME}:${SHARE}")
else
    run_args+=(--volume "${TELEMETRY_VOLUME}:${SHARE}:ro")
fi

[[ "${command}" == shell ]] && run_args+=(--interactive --tty)

exec docker run "${run_args[@]}" "${IMAGE}" /sql/in-container.sh "$@"
