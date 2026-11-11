#!/usr/bin/env bash
#
# Packs what the runner installer needs into bitenovac-runner-<version>.tar.gz, with a SHA-256 file
# beside it: the setup scripts, the Docker context the image is built from, the SDK pin in
# global.json, the RemoteBuildTool version in src/RemoteBuildTool's Directory.Build.props, and the
# two RemoteBuildTool packages at that version, as runner/packages. The archive keeps the
# repository's own paths, so the scripts in it find all of it where they expect it.
#
# Usage:
#   bash runner/make-bundle.sh <packages directory> [output directory]
#
#   packages directory   Where the two packages are, for example an Official drop's packages folder.
#   output directory     Defaults to the current directory.
#
# Extract the archive into an empty directory on the machine, then run runner/setup-runner.sh from
# there. Nothing is downloaded from a package feed.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

source "${REPO_ROOT}/runner/lib/common.sh"

fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

packages="${1:-}"
[[ -n "${packages}" ]] || fail "Name the directory that holds the RemoteBuildTool packages."
[[ -d "${packages}" ]] || fail "${packages} is not a directory."

output="${2:-.}"
[[ -d "${output}" ]] || fail "${output} is not a directory."

version="$(tool_version_in "${REPO_ROOT}")"
[[ -n "${version}" ]] || fail "Could not read <VersionPrefix> from src/RemoteBuildTool/Directory.Build.props."

has_tool_packages "${packages}" "${version}" \
    || fail "${packages} does not hold both RemoteBuildTool packages at ${version}, the version in src/RemoteBuildTool/Directory.Build.props."

readonly BUNDLE_FILES=(
    runner/lib/common.sh
    runner/setup-runner.sh
    runner/build-image.sh
    runner/service.sh
    runner/telemetry.sh
    docker/ci-runner.Dockerfile
    docker/ci-runner.Dockerfile.dockerignore
    docker/entrypoint.sh
    global.json
    src/RemoteBuildTool/Directory.Build.props
)

stage="$(mktemp -d)"
trap 'rm -rf "${stage}"' EXIT

mkdir -p "${stage}/runner/packages"
for id in "${TOOL_PACKAGES[@]}"; do
    cp "${packages}/${id}.${version}.nupkg" "${stage}/runner/packages/"
done

name="bitenovac-runner-${version}"
tar -czf "${output}/${name}.tar.gz" -C "${REPO_ROOT}" "${BUNDLE_FILES[@]}" -C "${stage}" runner/packages
(cd "${output}" && sha256sum "${name}.tar.gz" > "${name}.tar.gz.sha256")

printf 'Wrote %s/%s.tar.gz and its .sha256\n' "${output}" "${name}"
