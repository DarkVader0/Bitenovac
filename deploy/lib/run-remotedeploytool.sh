#!/usr/bin/env bash
#
# Runs RemoteDeployTool from the drop it deploys. deploy/remotedeploytool.sh runs this in the runner
# image, with the drops mounted; it can also run anywhere dotnet is, given where the drops are.
#
# Usage:
#   bash deploy/lib/run-remotedeploytool.sh <command> [arguments...]
#
# Environment:
#   REMOTEDEPLOYTOOL_DROP_ROOT   Where the drops are. Defaults to /mnt/official.
#   TOOLS_DIR                    Where the tool is installed. Defaults to a new temporary directory.
#   GITHUB_OUTPUT                When set, the drop a deploying command deploys is written to it
#                                as drop=<name>.
#
# The tool is installed from the packages of the drop the command deploys, so a drop is always
# deployed by the RemoteDeployTool built with it, and nothing is fetched from a feed. A command that
# deploys from a drop and names none is given the newest explicitly, so the drop installed from, the
# drop deployed and the drop reported are one and the same.

set -euo pipefail

fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

[[ $# -gt 0 ]] || fail "Name a RemoteDeployTool command, for example: drops"

export REMOTEDEPLOYTOOL_DROP_ROOT="${REMOTEDEPLOYTOOL_DROP_ROOT:-/mnt/official}"
readonly DROP_PATTERN='^[0-9]{4}\.[0-9]{2}\.[0-9]{2}\.[0-9]{3}$'

args=("$@")
drop=""
promotes=false
for (( index = 0; index < ${#args[@]}; index++ )); do
    case "${args[index]}" in
        --drop) drop="${args[index + 1]:-}" ;;
        --from) promotes=true ;;
    esac
done

# A promote deploys the drop the ledger names, so it is not one of these: only its tool comes from
# the drop chosen here.
deploys=false
case "${args[0]}" in
    nuget | pages | cloudflare | record) [[ "${promotes}" == true ]] || deploys=true ;;
esac

if [[ -z "${drop}" ]]; then
    drop="$(ls -1 "${REMOTEDEPLOYTOOL_DROP_ROOT}" 2> /dev/null | grep -E "${DROP_PATTERN}" | sort | tail -n 1 || true)"
    [[ -n "${drop}" ]] || fail "There are no drops in ${REMOTEDEPLOYTOOL_DROP_ROOT}."

    [[ "${deploys}" == false ]] || args+=(--drop "${drop}")
fi

[[ "${drop}" =~ ${DROP_PATTERN} ]] || fail "'${drop}' is not a drop name. Drops are named yyyy.MM.dd.NNN."

packages="${REMOTEDEPLOYTOOL_DROP_ROOT}/${drop}/packages"
shopt -s nullglob
found=("${packages}"/Bitenovac.RemoteDeployTool.[0-9]*.nupkg)
shopt -u nullglob
[[ ${#found[@]} -eq 1 ]] \
    || fail "Drop ${drop} holds ${#found[@]} RemoteDeployTool package(s), not one; it was made before RemoteDeployTool, or by a broken release."

tools="${TOOLS_DIR:-$(mktemp -d)}"
dotnet tool install Bitenovac.RemoteDeployTool --tool-path "${tools}" --source "${packages}" > /dev/null \
    || fail "Could not install RemoteDeployTool from drop ${drop}."

if [[ -n "${GITHUB_OUTPUT:-}" && "${deploys}" == true ]]; then
    printf 'drop=%s\n' "${drop}" >> "${GITHUB_OUTPUT}"
fi

exec "${tools}/remotedeploytool" "${args[@]}"
