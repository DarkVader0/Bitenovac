#!/usr/bin/env bash
#
# Builds the runner image, with the RemoteBuildTool tools in it: remotebuildtool and
# remotebuildtool-service, standalone executables taken from runner/tools.
#
# Usage:
#   bash runner/build-image.sh [--drop [DROP]] [docker build options...]
#
# Options:
#   --drop [DROP]   Take the tools from an Official drop on this machine, for example
#                   2026.10.11.001. Without a name, from the newest drop.
#
# The release to install is <VersionPrefix> in src/RemoteBuildTool/Directory.Build.props. A runner
# bundle carries the tools at that version. A checkout does not: there, without --drop, they are
# downloaded from that version's runner bundle on the repository's GitHub Releases page.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

source "${REPO_ROOT}/runner/lib/common.sh"

readonly DOCKERFILE="${REPO_ROOT}/docker/ci-runner.Dockerfile"
readonly TOOLS_DIR="${REPO_ROOT}/runner/tools"
readonly TOOL_PROPS="${REPO_ROOT}/src/RemoteBuildTool/Directory.Build.props"
readonly DROP_PATTERN='^[0-9]{4}\.[0-9]{2}\.[0-9]{2}\.[0-9]{3}$'

fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

from_drop=false
drop=""
docker_args=()
while [[ $# -gt 0 ]]; do
    case "$1" in
        --drop)
            from_drop=true
            shift
            if [[ "${1:-}" =~ ${DROP_PATTERN} ]]; then
                drop="$1"
                shift
            fi
            ;;
        *) docker_args+=("$1"); shift ;;
    esac
done

sdk_version="$(sdk_version_in "${REPO_ROOT}")"
[[ -n "${sdk_version}" ]] || fail "Could not read the SDK version from ${REPO_ROOT}/global.json."

tool_version="$(tool_version_in "${REPO_ROOT}")"
[[ -n "${tool_version}" ]] || fail "Could not read <VersionPrefix> from ${TOOL_PROPS}."

work=""
trap '[[ -z "${work}" ]] || rm -rf "${work}"' EXIT

replace_tools() {
    rm -rf "${TOOLS_DIR}"
    mkdir -p "${TOOLS_DIR}"
    cp -r "$1/." "${TOOLS_DIR}/"
}

tools_from_drop() {
    docker volume inspect "${OFFICIAL_VOLUME}" > /dev/null 2>&1 \
        || fail "The volume ${OFFICIAL_VOLUME} does not exist. Has Official run on this machine?"

    local base
    base="$(sed -n 's/^FROM[[:space:]]\{1,\}\([^[:space:]]\{1,\}\)[[:space:]]\{1,\}AS[[:space:]]\{1,\}sdk[[:space:]]*$/\1/p' "${DOCKERFILE}" | head -n 1)"
    [[ -n "${base}" ]] || fail "Could not read the base image from ${DOCKERFILE}."

    work="$(mktemp -d)"
    docker run \
        --rm \
        --volume "${OFFICIAL_VOLUME}:/mnt/official:ro" \
        --env "DROP=${drop}" \
        --entrypoint bash \
        "${base}" -c '
            set -euo pipefail
            if [[ -z "${DROP}" ]]; then
                DROP="$(ls -1 /mnt/official | grep -E "^[0-9]{4}\.[0-9]{2}\.[0-9]{2}\.[0-9]{3}$" | sort | tail -n 1 || true)"
                [[ -n "${DROP}" ]] || { echo "error: there are no drops on this machine." >&2; exit 1; }
            fi
            tools="/mnt/official/${DROP}/tools"
            [[ -x "${tools}/remotebuildtool" && -x "${tools}/remotebuildtool-service" ]] \
                || { echo "error: drop ${DROP} has no tools. Use a drop made by an Official run that published them." >&2; exit 1; }
            echo "Taking the RemoteBuildTool tools from drop ${DROP}" >&2
            tar -C "${tools}" --exclude=remotedeploytool -cf - .
        ' | tar -xf - -C "${work}"

    replace_tools "${work}"
}

tools_from_release() {
    local repository="${REPOSITORY:-}"
    [[ -n "${repository}" ]] || repository="$(git -C "${REPO_ROOT}" remote get-url origin 2> /dev/null || true)"
    repository="$(printf '%s\n' "${repository}" | sed -E 's|^git@github\.com:|https://github.com/|; s|\.git$||; s|/$||')"
    [[ -n "${repository}" ]] \
        || fail "${TOOLS_DIR} does not hold the RemoteBuildTool tools, and there is no repository to download them from. Pass --drop, or set REPOSITORY in ${CONFIG_FILE}."

    local name="bitenovac-runner-${tool_version}"
    local url="${repository}/releases/download/${name}"

    work="$(mktemp -d)"
    printf 'Downloading RemoteBuildTool %s from %s\n' "${tool_version}" "${url}" >&2
    curl -fsSL -o "${work}/${name}.tar.gz" "${url}/${name}.tar.gz" \
        && curl -fsSL -o "${work}/${name}.tar.gz.sha256" "${url}/${name}.tar.gz.sha256" \
        || fail "RemoteBuildTool ${tool_version} has no runner bundle at ${repository}/releases. Publish it first, or pass --drop to take the tools from an Official drop on this machine."
    (cd "${work}" && sha256sum --check --quiet "${name}.tar.gz.sha256") \
        || fail "${name}.tar.gz does not match its checksum."

    tar -xzf "${work}/${name}.tar.gz" -C "${work}" runner/tools \
        || fail "${name}.tar.gz carries no RemoteBuildTool tools; it was published before bundles held them."

    replace_tools "${work}/runner/tools"
}

if [[ "${from_drop}" == true ]]; then
    tools_from_drop
elif ! has_tools "${TOOLS_DIR}"; then
    tools_from_release
fi

has_tools "${TOOLS_DIR}" \
    || fail "${TOOLS_DIR} does not hold remotebuildtool and remotebuildtool-service."

readonly CANDIDATE="bitenovac-remotebuildtool-runner:${sdk_version}-${tool_version}"

docker build \
    --file "${DOCKERFILE}" \
    --build-arg "DOTNET_SDK_VERSION=${sdk_version}" \
    --tag "${CANDIDATE}" \
    "${docker_args[@]}" \
    "${REPO_ROOT}"

if ! docker image inspect "bitenovac-remotebuildtool-runner:${sdk_version}" > /dev/null 2>&1; then
    bash "${REPO_ROOT}/runner/images.sh" promote "${CANDIDATE}"
    if [[ -r /etc/remotebuildtool/.env ]]; then
        bash "${REPO_ROOT}/runner/service.sh" start
    fi
fi
