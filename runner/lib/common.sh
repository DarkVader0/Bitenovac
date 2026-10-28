#!/usr/bin/env bash
#
# Shared by the runner scripts. Sourced, never run: it sets the names this setup owns and the
# machine's configuration, with the values this machine has always used as defaults.
#
# Configuration lives in CONFIG_FILE. It is root-owned and holds no secrets, so the agents'
# account can read it too. A value set there overrides the default below; a command line flag
# overrides both, for that run only.

readonly CONFIG_FILE="${BITENOVAC_CONFIG:-/etc/bitenovac/runner.env}"

if [[ -r "${CONFIG_FILE}" ]]; then
    # shellcheck source=/dev/null
    source "${CONFIG_FILE}"
fi

: "${RUNNER_HOME:=/opt/bitenovac-runner}"
: "${SHARE_ROOT:=/srv/bitenovac}"
: "${RUN_AS:=ci-runner}"
: "${LABELS:=self-hosted,linux,x64,bitenovac}"
: "${RUNNER_NAME_PREFIX:=$(hostname)}"
: "${REPOSITORY:=}"
# Whether this setup serves the NFS share. Only a test machine without one sets it to false.
: "${MANAGE_SHARE:=true}"

: "${NUGET_VOLUME:=bitenovac-runner-nuget}"
: "${MAIN_VOLUME:=bitenovac-main}"
: "${LOGS_VOLUME:=bitenovac-logs}"
: "${OFFICIAL_VOLUME:=bitenovac-official}"
: "${TELEMETRY_VOLUME:=bitenovac-telemetry}"
: "${WORK_VOLUME:=bitenovac-service-work}"
: "${STAGING_VOLUME:=bitenovac-staging}"

# The SDK pinned by the repository's global.json, read from the given root.
sdk_version_in() { sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$1/global.json" | head -n 1; }

# The RemoteBuildTool release named by the <VersionPrefix> of the given root's props file.
tool_version_in() { sed -n 's/.*<VersionPrefix>[[:space:]]*\([^<[:space:]]*\)[[:space:]]*<\/VersionPrefix>.*/\1/p' "$1/src/RemoteBuildTool/Directory.Build.props" | head -n 1; }
