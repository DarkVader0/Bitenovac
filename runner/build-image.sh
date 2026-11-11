#!/usr/bin/env bash
#
# Builds the runner image, with RemoteBuildTool and its service installed from the two packages in
# runner/packages. No package feed is involved.
#
# Usage:
#   bash runner/build-image.sh [--drop [DROP]] [docker build options...]
#
# Options:
#   --drop [DROP]   Take the packages from an Official drop on this machine, for example
#                   2026.10.11.001. Without a name, from the newest drop.
#
# The release to install is <VersionPrefix> in src/RemoteBuildTool/Directory.Build.props. A runner
# bundle carries both packages at that version. A checkout does not: there, without --drop, they are
# downloaded from that version's runner bundle on the repository's GitHub Releases page.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

source "${REPO_ROOT}/runner/lib/common.sh"

readonly DOCKERFILE="${REPO_ROOT}/docker/ci-runner.Dockerfile"
readonly PACKAGES_DIR="${REPO_ROOT}/runner/packages"
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

replace_packages() {
    mkdir -p "${PACKAGES_DIR}"
    rm -f "${PACKAGES_DIR}"/*.nupkg
    local id
    for id in "${TOOL_PACKAGES[@]}"; do
        cp "$1/${id}.${tool_version}.nupkg" "${PACKAGES_DIR}/"
    done
}

packages_from_drop() {
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
        --env "VERSION=${tool_version}" \
        --env "PACKAGES=${TOOL_PACKAGES[*]}" \
        --entrypoint bash \
        "${base}" -c '
            set -euo pipefail
            if [[ -z "${DROP}" ]]; then
                DROP="$(ls -1 /mnt/official | grep -E "^[0-9]{4}\.[0-9]{2}\.[0-9]{2}\.[0-9]{3}$" | sort | tail -n 1 || true)"
                [[ -n "${DROP}" ]] || { echo "error: there are no drops on this machine." >&2; exit 1; }
            fi
            files=()
            for id in ${PACKAGES}; do
                file="${id}.${VERSION}.nupkg"
                [[ -f "/mnt/official/${DROP}/packages/${file}" ]] \
                    || { echo "error: drop ${DROP} has no ${file}. Was it built from a commit where RemoteBuildTool is ${VERSION}?" >&2; exit 1; }
                files+=("${file}")
            done
            echo "Taking RemoteBuildTool ${VERSION} from drop ${DROP}" >&2
            tar -C "/mnt/official/${DROP}/packages" -cf - "${files[@]}"
        ' | tar -xf - -C "${work}"

    replace_packages "${work}"
}

packages_from_release() {
    local repository="${REPOSITORY:-}"
    [[ -n "${repository}" ]] || repository="$(git -C "${REPO_ROOT}" remote get-url origin 2> /dev/null || true)"
    repository="$(printf '%s\n' "${repository}" | sed -E 's|^git@github\.com:|https://github.com/|; s|\.git$||; s|/$||')"
    [[ -n "${repository}" ]] \
        || fail "${PACKAGES_DIR} does not hold RemoteBuildTool ${tool_version}, and there is no repository to download it from. Pass --drop, or set REPOSITORY in ${CONFIG_FILE}."

    local name="bitenovac-runner-${tool_version}"
    local url="${repository}/releases/download/${name}"

    work="$(mktemp -d)"
    printf 'Downloading RemoteBuildTool %s from %s\n' "${tool_version}" "${url}" >&2
    curl -fsSL -o "${work}/${name}.tar.gz" "${url}/${name}.tar.gz" \
        && curl -fsSL -o "${work}/${name}.tar.gz.sha256" "${url}/${name}.tar.gz.sha256" \
        || fail "RemoteBuildTool ${tool_version} has no runner bundle at ${repository}/releases. Publish it first, or pass --drop to take the packages from an Official drop on this machine."
    (cd "${work}" && sha256sum --check --quiet "${name}.tar.gz.sha256") \
        || fail "${name}.tar.gz does not match its checksum."

    local id members=()
    for id in "${TOOL_PACKAGES[@]}"; do
        members+=("runner/packages/${id}.${tool_version}.nupkg")
    done
    tar -xzf "${work}/${name}.tar.gz" -C "${work}" "${members[@]}" \
        || fail "${name}.tar.gz carries no RemoteBuildTool packages; it was published before bundles held them."

    replace_packages "${work}/runner/packages"
}

if [[ "${from_drop}" == true ]]; then
    packages_from_drop
elif ! has_tool_packages "${PACKAGES_DIR}" "${tool_version}"; then
    packages_from_release
fi

has_tool_packages "${PACKAGES_DIR}" "${tool_version}" \
    || fail "${PACKAGES_DIR} does not hold both RemoteBuildTool packages at ${tool_version}."

docker build \
    --file "${DOCKERFILE}" \
    --build-arg "DOTNET_SDK_VERSION=${sdk_version}" \
    --build-arg "REMOTEBUILDTOOL_VERSION=${tool_version}" \
    --tag "bitenovac-remotebuildtool-runner:${sdk_version}" \
    "${docker_args[@]}" \
    "${REPO_ROOT}"

if [[ -r /etc/remotebuildtool/.env ]]; then
    bash "${REPO_ROOT}/runner/service.sh" start
fi
