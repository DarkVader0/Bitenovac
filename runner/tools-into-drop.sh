#!/usr/bin/env bash
#
# Publishes the RemoteBuildTool tools into an Official drop, as standalone executables, and builds the
# runner bundle from them into <drop>/runner/. The Publish tools workflow runs it, on the commit the
# drop was built from.
#
# Usage, on the runner host, from that checkout:
#   bash runner/tools-into-drop.sh [drop]
#
#   drop   The drop to publish into, for example 2026.10.11.001. Defaults to the newest one.
#
# A drop gets <drop>/tools/ with remotebuildtool, remotebuildtool-service and remotedeploytool, each a
# single file, and whatever native libraries they carry beside them.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

source "${REPO_ROOT}/runner/lib/common.sh"

fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

readonly DROP_PATTERN='^[0-9]{4}\.[0-9]{2}\.[0-9]{2}\.[0-9]{3}$'

drop="${1:-}"
[[ -z "${drop}" || "${drop}" =~ ${DROP_PATTERN} ]] || fail "'${drop}' is not a drop name. Drops are named yyyy.MM.dd.NNN."

sdk_version="$(sdk_version_in "${REPO_ROOT}")"
[[ -n "${sdk_version}" ]] || fail "Could not read the SDK version from global.json."

readonly IMAGE="bitenovac-remotebuildtool-runner:${sdk_version}"
docker image inspect "${IMAGE}" > /dev/null 2>&1 \
    || fail "The image ${IMAGE} is missing. Run runner/build-image.sh on this machine."

docker volume inspect "${OFFICIAL_VOLUME}" > /dev/null 2>&1 \
    || fail "The volume ${OFFICIAL_VOLUME} does not exist. Has Official run on this machine?"

docker volume inspect "${NUGET_VOLUME}" > /dev/null 2>&1 || docker volume create "${NUGET_VOLUME}" > /dev/null

docker run \
    --rm \
    --init \
    --user "$(id -u):$(id -g)" \
    --volume "${OFFICIAL_VOLUME}:/mnt/official" \
    --volume "${NUGET_VOLUME}:/cache" \
    --volume "${REPO_ROOT}:/repo" \
    --env "DROP=${drop}" \
    --env "EXPECTED_SHA=${GITHUB_SHA:-}" \
    --env HOME=/tmp \
    --env DOTNET_CLI_HOME=/tmp \
    --env NUGET_PACKAGES=/cache/nuget \
    --env DOTNET_NOLOGO=true \
    --env DOTNET_CLI_TELEMETRY_OPTOUT=true \
    --entrypoint bash \
    "${IMAGE}" -c '
        set -euo pipefail
        fail() { printf "\nerror: %s\n" "$*" >&2; exit 1; }
        if [[ -z "${DROP}" ]]; then
            DROP="$(ls -1 /mnt/official | grep -E "^[0-9]{4}\.[0-9]{2}\.[0-9]{2}\.[0-9]{3}$" | sort | tail -n 1 || true)"
            [[ -n "${DROP}" ]] || fail "there are no drops on this machine."
        fi
        [[ -d "/mnt/official/${DROP}" ]] || fail "there is no drop ${DROP}."

        if [[ -n "${EXPECTED_SHA}" ]]; then
            built="$(sed -n "s/.*\"sha\": *\"\([0-9a-f]*\)\".*/\1/p" "/mnt/official/${DROP}/build-graph.json" | head -n 1)"
            [[ "${built}" == "${EXPECTED_SHA}" ]] \
                || fail "drop ${DROP} was built from ${built:-an unknown commit}; run this workflow on that commit, not ${EXPECTED_SHA}."
        fi

        tools="/mnt/official/${DROP}/tools"
        mkdir -p "${tools}"
        cd /repo

        publish() {
            local project="$1" assembly="$2" name="$3" out="/tmp/publish/$3"
            dotnet publish "${project}" -c Release -r linux-x64 --self-contained true \
                -p:PublishSingleFile=true -p:IsPublishable=true -o "${out}" > /dev/null
            cp -r "${out}/." "${tools}/"
            mv "${tools}/${assembly}" "${tools}/${name}"
        }

        publish src/RemoteBuildTool/Bitenovac.RemoteBuildTool/Bitenovac.RemoteBuildTool.csproj Bitenovac.RemoteBuildTool remotebuildtool
        publish src/RemoteBuildTool/Bitenovac.RemoteBuildTool.Service/Bitenovac.RemoteBuildTool.Service.csproj Bitenovac.RemoteBuildTool.Service remotebuildtool-service
        publish src/RemoteDeployTool/Bitenovac.RemoteDeployTool/Bitenovac.RemoteDeployTool.csproj Bitenovac.RemoteDeployTool remotedeploytool

        mkdir -p "/mnt/official/${DROP}/runner"
        bash runner/make-bundle.sh "${tools}" "/mnt/official/${DROP}/runner"
        echo "Published the tools and the runner bundle into drop ${DROP}." >&2
    '
