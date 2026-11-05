#!/usr/bin/env bash
#
# Packs what the runner installer needs into bitenovac-runner-<version>.tar.gz, with a SHA-256 file
# beside it. The archive keeps the repository's own paths, so the scripts in it find the two pins
# they read: the SDK in global.json and the RemoteBuildTool release in src/RemoteBuildTool's
# Directory.Build.props. The Docker context the image is built from comes with it.
#
# Usage:
#   bash runner/make-bundle.sh [output directory]
#
# The output directory defaults to the current directory. Extract the archive into an empty
# directory on the machine, then run runner/setup-runner.sh from there.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

source "${REPO_ROOT}/runner/lib/common.sh"

fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

output="${1:-.}"
[[ -d "${output}" ]] || fail "${output} is not a directory."

version="$(tool_version_in "${REPO_ROOT}")"
[[ -n "${version}" ]] || fail "Could not read <VersionPrefix> from src/RemoteBuildTool/Directory.Build.props."

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

name="bitenovac-runner-${version}"
tar -czf "${output}/${name}.tar.gz" -C "${REPO_ROOT}" "${BUNDLE_FILES[@]}"
(cd "${output}" && sha256sum "${name}.tar.gz" > "${name}.tar.gz.sha256")

printf 'Wrote %s/%s.tar.gz and its .sha256\n' "${output}" "${name}"
