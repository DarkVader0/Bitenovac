#!/usr/bin/env bash
#
# Pushes the Bitenovac decompression library packages from an Official drop to nuget.org.
#
# Usage, on the runner host:
#   NUGET_API_KEY=... bash deploy/libraries.sh [drop]
#
#   drop   The drop to deploy from, for example 2026.10.11.001. Defaults to the newest one.
#
# Environment:
#   NUGET_API_KEY   Required. In CI this is the short-lived key NuGet/login exchanges for the
#                   workflow's OIDC token (see .github/workflows/deploy-libraries.yml).
#
# The libraries share one version, set in Directory.Build.props. A package nuget.org already has at
# that version is skipped, so a deploy that stopped part way can be run again for the rest; one in
# which every package is already published fails, so a forgotten bump still fails loudly.

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

        shopt -s nullglob
        found=("${packages}"/Bitenovac.DecompressionAlgorithms.*.nupkg "${packages}"/Bitenovac.DecompressionCalculator.*.nupkg)
        [[ ${#found[@]} -gt 0 ]] || fail "Drop ${DROP} has no library packages."

        # A drop made before the libraries had their own version stamps them with its date.
        # nuget.org never deletes a version, and that one would outrank every real one.
        for package in "${found[@]}"; do
            [[ ! "$(basename "${package}")" =~ \.[0-9]{4}\.[0-9]{2}\.[0-9]{2}\.[0-9]+\.nupkg$ ]] \
                || fail "$(basename "${package}") carries the drop date, not the library version. Deploy from a newer drop."
        done

        # nuget.org can accept an upload and still time out before saying so; the client then retries
        # and is told the version already exists. --skip-duplicate turns that into a skip, and the
        # skip shows in the output as "already exists".
        pushed=0
        for package in "${found[@]}"; do
            printf "==> Pushing %s from drop %s\n" "$(basename "${package}")" "${DROP}"
            output="$(dotnet nuget push "${package}" \
                --source https://api.nuget.org/v3/index.json \
                --api-key "${NUGET_API_KEY}" \
                --skip-duplicate 2>&1)" || { printf "%s\n" "${output}"; fail "Pushing $(basename "${package}") failed."; }
            printf "%s\n" "${output}"
            grep -qi "already exists" <<< "${output}" || pushed=$((pushed + 1))
        done

        [[ ${pushed} -gt 0 ]] \
            || fail "nuget.org already has every package of drop ${DROP}. Bump <VersionPrefix> in Directory.Build.props."
        printf "==> Pushed %s of %s package(s); the rest were already on nuget.org.\n" "${pushed}" "${#found[@]}"
    '
