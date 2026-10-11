#!/usr/bin/env bash
#
# Pushes the Bitenovac.RemoteBuildTool package from an Official drop to nuget.org.
#
# Usage, on the runner host:
#   NUGET_API_KEY=... bash deploy/remotebuildtool.sh [drop]
#
#   drop   The drop to deploy from, for example 2026.10.11.001. Defaults to the newest one.
#
# Environment:
#   NUGET_API_KEY   Required. In CI this is the short-lived key NuGet/login exchanges for the
#                   workflow's OIDC token (see .github/workflows/deploy-remotebuildtool.yml).
#
# The drops live in a Docker volume on this host only, so this must run here. They are mounted
# read-only: a deploy cannot alter what Official produced. The key reaches the container by name,
# never on a command line.
#
# An already published version is rejected by nuget.org rather than skipped, so a forgotten
# <VersionPrefix> bump fails loudly.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly OFFICIAL_VOLUME="${REMOTEBUILDTOOL_OFFICIAL_VOLUME:-bitenovac-official}"

fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

[[ -n "${NUGET_API_KEY:-}" ]] || fail "NUGET_API_KEY is not set."

drop="${1:-}"
[[ -z "${drop}" || "${drop}" =~ ^[0-9]{4}\.[0-9]{2}\.[0-9]{2}\.[0-9]{3}$ ]] \
    || fail "'${drop}' is not a drop name. Drops are named yyyy.MM.dd.NNN."

sdk_version="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "${REPO_ROOT}/global.json" | head -n 1)"
[[ -n "${sdk_version}" ]] || fail "Could not read the SDK version from global.json."

readonly IMAGE="bitenovac-remotebuildtool-runner:${sdk_version}"

docker image inspect "${IMAGE}" > /dev/null 2>&1 \
    || fail "The image ${IMAGE} is missing. Run runner/build-image.sh on this machine."

# Mounting a volume that does not exist would create it empty and hide the real problem.
docker volume inspect "${OFFICIAL_VOLUME}" > /dev/null 2>&1 \
    || fail "The volume ${OFFICIAL_VOLUME} does not exist. Has Official run on this machine?"

exec docker run \
    --rm \
    --init \
    --user "$(id -u):$(id -g)" \
    --volume "${OFFICIAL_VOLUME}:/mnt/official:ro" \
    --env NUGET_API_KEY \
    --env "DROP=${drop}" \
    --env HOME=/tmp \
    --env DOTNET_CLI_HOME=/tmp \
    --env DOTNET_NOLOGO=true \
    --env DOTNET_CLI_TELEMETRY_OPTOUT=true \
    --entrypoint bash \
    "${IMAGE}" -c '
        set -euo pipefail
        fail() { printf "\nerror: %s\n" "$*" >&2; exit 1; }

        # The name filter also skips a .staging-* directory left by a release in progress.
        if [[ -z "${DROP}" ]]; then
            DROP="$(ls /mnt/official | grep -E "^[0-9]{4}\.[0-9]{2}\.[0-9]{2}\.[0-9]{3}$" | sort | tail -n 1 || true)"
            [[ -n "${DROP}" ]] || fail "There are no drops in /mnt/official."
        fi

        packages="/mnt/official/${DROP}/packages"
        [[ -d "${packages}" ]] || fail "Drop ${DROP} has no packages directory."

        # The digit after the id keeps Bitenovac.RemoteBuildTool.Core.* out; the tool bundles it.
        shopt -s nullglob
        found=("${packages}"/Bitenovac.RemoteBuildTool.[0-9]*.nupkg)
        [[ ${#found[@]} -eq 1 ]] || fail "Expected one Bitenovac.RemoteBuildTool package in drop ${DROP}, found ${#found[@]}."

        printf "==> Pushing %s from drop %s\n" "$(basename "${found[0]}")" "${DROP}"
        exec dotnet nuget push "${found[0]}" \
            --source https://api.nuget.org/v3/index.json \
            --api-key "${NUGET_API_KEY}"
    '
