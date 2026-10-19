#!/usr/bin/env bash
#
# Prepares the Web app from an Official drop for GitHub Pages.
#
# Usage, on the runner host:
#   bash src/DecompressionCalculator/deploy_web.sh <site> [drop]
#
#   site   An empty or missing directory to write the site to. The Deploy web workflow uploads it.
#   drop   The drop to deploy from, for example 2026.10.11.001. Defaults to the newest one.
#
# The app is served from the root of its own domain, so index.html keeps <base href="/">.
# Pages runs Jekyll unless told not to, and Jekyll drops _framework; .nojekyll turns it off. Pages
# answers a path it has no file for with 404.html, so that is a copy of index.html and the app
# routes a reloaded /plan/... itself. The precompressed .br and .gz copies are left out: Pages
# compresses on its own and would only serve them as downloads.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
readonly OFFICIAL_VOLUME="${REMOTEBUILDTOOL_OFFICIAL_VOLUME:-bitenovac-official}"
readonly APP="Bitenovac.DecompressionCalculator.Web"

fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

site="${1:-}"
[[ -n "${site}" ]] || fail "Name the directory to write the site to."
mkdir -p "${site}"
[[ -z "$(ls -A "${site}")" ]] || fail "${site} is not empty."
site="$(cd "${site}" && pwd)"

drop="${2:-}"
[[ -z "${drop}" || "${drop}" =~ ^[0-9]{4}\.[0-9]{2}\.[0-9]{2}\.[0-9]{3}$ ]] \
    || fail "'${drop}' is not a drop name. Drops are named yyyy.MM.dd.NNN."

sdk_version="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "${REPO_ROOT}/global.json" | head -n 1)"
[[ -n "${sdk_version}" ]] || fail "Could not read the SDK version from global.json."

readonly IMAGE="bitenovac-remotebuildtool-runner:${sdk_version}"

docker image inspect "${IMAGE}" > /dev/null 2>&1 \
    || fail "The image ${IMAGE} is missing. Run runner/build-image.sh on this machine."

docker volume inspect "${OFFICIAL_VOLUME}" > /dev/null 2>&1 \
    || fail "The volume ${OFFICIAL_VOLUME} does not exist. Has Official run on this machine?"

docker run \
    --rm \
    --init \
    --user "$(id -u):$(id -g)" \
    --volume "${OFFICIAL_VOLUME}:/mnt/official:ro" \
    --volume "${site}:/mnt/site" \
    --env "DROP=${drop}" \
    --env "APP=${APP}" \
    --entrypoint bash \
    "${IMAGE}" -c '
        set -euo pipefail
        fail() { printf "\nerror: %s\n" "$*" >&2; exit 1; }

        # The name filter also skips a .staging-* directory left by a release in progress.
        if [[ -z "${DROP}" ]]; then
            DROP="$(ls /mnt/official | grep -E "^[0-9]{4}\.[0-9]{2}\.[0-9]{2}\.[0-9]{3}$" | sort | tail -n 1 || true)"
            [[ -n "${DROP}" ]] || fail "There are no drops in /mnt/official."
        fi

        app="/mnt/official/${DROP}/apps/${APP}/wwwroot"
        [[ -f "${app}/index.html" ]] || fail "Drop ${DROP} has no published ${APP}."

        printf "==> Preparing %s from drop %s\n" "${APP}" "${DROP}"
        cp -R "${app}/." /mnt/site/
        find /mnt/site -type f \( -name "*.br" -o -name "*.gz" \) -delete
        cp /mnt/site/index.html /mnt/site/404.html
        touch /mnt/site/.nojekyll
    '

printf '==> %s files ready in %s\n' "$(find "${site}" -type f | wc -l | tr -d ' ')" "${site}"
