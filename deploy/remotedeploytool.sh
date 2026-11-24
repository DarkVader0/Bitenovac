#!/usr/bin/env bash
#
# Runs a RemoteDeployTool command on the runner host, against the Official drops on it.
#
# Usage, on the runner host:
#   bash deploy/remotedeploytool.sh <command> [arguments...]
#
# For example:
#   bash deploy/remotedeploytool.sh drops
#   NUGET_API_KEY=... bash deploy/remotedeploytool.sh nuget 'Bitenovac.DecompressionCalculator.*'
#   bash deploy/remotedeploytool.sh pages Bitenovac.DecompressionCalculator.Web /tmp/site
#
# The command runs in the runner image, with the drops mounted read-only: a deployment cannot alter
# what Official produced. RemoteDeployTool itself is installed from the drop it deploys; see
# deploy/lib/run-remotedeploytool.sh.
#
# - pages writes its site to a directory on this machine: the directory is mounted into the
#   container, so give it as an ordinary path.
# - Deployments are recorded in the ledger, kept in the telemetry volume beside the build telemetry,
#   when this machine has one.
# - Secrets reach the container by name, never on a command line: NUGET_API_KEY,
#   CLOUDFLARE_API_TOKEN and CLOUDFLARE_ACCOUNT_ID.
# - Under GitHub Actions the drop deployed is written to the step's outputs as drop=<name>.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

source "${REPO_ROOT}/runner/lib/common.sh"

readonly OFFICIAL_VOLUME="${REMOTEBUILDTOOL_OFFICIAL_VOLUME:-${OFFICIAL_VOLUME}}"
readonly TELEMETRY_VOLUME="${REMOTEBUILDTOOL_TELEMETRY_VOLUME:-${TELEMETRY_VOLUME}}"

fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

[[ $# -gt 0 ]] || fail "Name a RemoteDeployTool command, for example: drops"

sdk_version="$(sdk_version_in "${REPO_ROOT}")"
[[ -n "${sdk_version}" ]] || fail "Could not read the SDK version from global.json."

readonly IMAGE="bitenovac-remotebuildtool-runner:${sdk_version}"

docker image inspect "${IMAGE}" > /dev/null 2>&1 \
    || fail "The image ${IMAGE} is missing. Run runner/build-image.sh on this machine."
docker volume inspect "${OFFICIAL_VOLUME}" > /dev/null 2>&1 \
    || fail "The volume ${OFFICIAL_VOLUME} does not exist. Has Official run on this machine?"

args=("$@")
run_args=(
    --rm
    --init
    --user "$(id -u):$(id -g)"
    --volume "${OFFICIAL_VOLUME}:/mnt/official:ro"
    --volume "${REPO_ROOT}:/repo:ro"
    --env REMOTEDEPLOYTOOL_DROP_ROOT=/mnt/official
    --env "REMOTEDEPLOYTOOL_MACHINE=${RUNNER_NAME:-$(hostname)}"
    --env HOME=/tmp
    --env DOTNET_CLI_HOME=/tmp
    --env DOTNET_NOLOGO=true
    --env DOTNET_CLI_TELEMETRY_OPTOUT=true
)

# The site pages writes is mounted in its place, so it lands on this machine.
if [[ "${args[0]}" == pages && ${#args[@]} -ge 3 ]]; then
    mkdir -p "${args[2]}"
    run_args+=(--volume "$(cd "${args[2]}" && pwd):/mnt/site")
    args[2]=/mnt/site
fi

if docker volume inspect "${TELEMETRY_VOLUME}" > /dev/null 2>&1; then
    run_args+=(--volume "${TELEMETRY_VOLUME}:/mnt/telemetry" --env REMOTEDEPLOYTOOL_LEDGER_DIR=/mnt/telemetry)
fi

if [[ -n "${GITHUB_OUTPUT:-}" && -f "${GITHUB_OUTPUT}" ]]; then
    run_args+=(--volume "${GITHUB_OUTPUT}:/mnt/github-output" --env GITHUB_OUTPUT=/mnt/github-output)
fi

for variable in NUGET_API_KEY REMOTEDEPLOYTOOL_NUGET_SOURCE CLOUDFLARE_API_TOKEN CLOUDFLARE_ACCOUNT_ID GITHUB_ACTIONS GITHUB_RUN_ID GITHUB_RUN_ATTEMPT; do
    [[ -n "${!variable:-}" ]] && run_args+=(--env "${variable}")
done

exec docker run "${run_args[@]}" --entrypoint bash "${IMAGE}" /repo/deploy/lib/run-remotedeploytool.sh "${args[@]}"
