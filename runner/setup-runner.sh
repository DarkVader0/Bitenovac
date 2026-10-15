#!/usr/bin/env bash
#
# Provisions this machine as a self-hosted CI runner for the repository. Run once, with root
# rights; every later pull request runs here without further input.
#
# Usage:
#   sudo runner/setup-runner.sh --token <registration token>
#   sudo runner/setup-runner.sh --token <registration token> --instances 4
#   sudo runner/setup-runner.sh --uninstall --token <registration token>
#
# Options:
#   --token TOKEN      Runner registration token. Take it from
#                      Settings > Actions > Runners > New self-hosted runner on the repository.
#                      Valid for one hour and needed only here: config.sh exchanges it for
#                      long-lived credentials that the agent refreshes itself, so the runner
#                      keeps working indefinitely without another token. One token registers
#                      every instance, provided they are all configured within that hour.
#   --instances N      How many agents to install. An agent accepts one job at a time, so this is
#                      how many workflow jobs the machine runs at once; at 1 the Debug and Release
#                      stages of a pull request serialise. Defaults to 1. See "Sizing" below.
#   --cpus N           Logical CPUs each agent's job containers may use. Defaults to the
#                      machine's CPUs divided by --instances.
#   --memory SIZE      Memory each agent's job containers may use, in docker's notation (6g).
#                      Unlimited by default.
#   --api-token TOKEN  GitHub token the RemoteBuildTool service reads the repository and its
#                      workflow runs with: a fine-grained token with read access to Contents,
#                      Pull requests and Actions. Without it the service is not installed, and
#                      nothing fills main.
#   --repo URL         Repository to serve. Defaults to the origin remote of this checkout.
#   --labels LIST      Comma separated labels the workflow selects on.
#                      Defaults to self-hosted,linux,x64,bitenovac.
#   --user NAME        Account the agents run as. Created if missing. Defaults to ci-runner.
#   --runner-version V Agent version. Defaults to the latest release.
#   --uninstall        Stop the services, deregister every agent and remove its files. Needs
#                      --token as well, since deregistering asks GitHub too.
#   --purge            With --uninstall, also delete the cached packages.
#
# Installs the agents as systemd services, the image jobs build in with the pinned RemoteBuildTool
# release in it, the NuGet cache and main as local volumes, an NFS share for staging, logs and
# telemetry, and, given --api-token, the RemoteBuildTool service.
#
# Sizing --instances
# ------------------
# One pull request runs plan, then the Debug and Release builds in parallel, then their tests in
# parallel, then a check of the results. Peak demand per open pull request is therefore 2; N
# instances runs N pull requests at that concurrency, the rest queued.
#
# Every agent shares this machine's cores, so past that point they only contend. Each agent's
# .env bounds its jobs to cores / instances, through REMOTEBUILDTOOL_MAX_CPU and the container's
# CPU limit.
#
# This is also the only machine in the pool: main and the NuGet cache are volumes local to this
# host, so a second machine first needs main on the share too.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly RUNNER_HOME="/opt/bitenovac-runner"
readonly NUGET_VOLUME="bitenovac-runner-nuget"
readonly MAIN_VOLUME="bitenovac-main"
readonly LOGS_VOLUME="bitenovac-logs"
readonly OFFICIAL_VOLUME="bitenovac-official"
readonly TELEMETRY_VOLUME="bitenovac-telemetry"
readonly WORK_VOLUME="bitenovac-service-work"
readonly STAGING_VOLUME="bitenovac-staging"
# Served over NFS, so the logs and telemetry paths records point at resolve the same from every
# machine that mounts the share. This machine serves it for now.
readonly SHARE_ROOT="/srv/bitenovac"

# Every agent lives in its own directory under RUNNER_HOME. The glob is what uninstall and
# reinstall sweep, so it must match every layout this script has ever produced.
readonly AGENT_DIR_GLOB="actions-runner*"

agent_dir_for() { printf '%s\n' "${RUNNER_HOME}/actions-runner-${1}"; }
runner_name_for() { printf '%s\n' "$(hostname)-${1}"; }

existing_agent_dirs() {
    shopt -s nullglob
    printf '%s\n' "${RUNNER_HOME}"/${AGENT_DIR_GLOB}
    shopt -u nullglob
}

# Stops the service, deregisters with GitHub and deletes the directory. Every step tolerates a
# half-installed agent, since that is exactly the state a failed run leaves behind.
remove_agent() {
    local dir="$1"
    [[ -d "${dir}" ]] || return 0

    if [[ -f "${dir}/svc.sh" ]]; then
        (cd "${dir}" && ./svc.sh stop > /dev/null 2>&1 || true)
        (cd "${dir}" && ./svc.sh uninstall > /dev/null 2>&1 || true)
    fi

    if [[ -f "${dir}/.runner" ]]; then
        (cd "${dir}" && sudo -u "${run_as}" ./config.sh remove --token "${token}" > /dev/null 2>&1 || true)
    fi

    rm -rf "${dir}"
    ok "removed ${dir##*/}"
}

log()  { printf '\n\033[1m==> %s\033[0m\n' "$*"; }
ok()   { printf '  \033[32mok\033[0m    %s\n' "$*"; }
warn() { printf '  \033[33mwarn\033[0m  %s\n' "$*"; }
fail() { printf '\n\033[31merror:\033[0m %s\n' "$*" >&2; exit 1; }

token=""
repo_url=""
labels="self-hosted,linux,x64,bitenovac"
run_as="ci-runner"
runner_version=""
instances=1
cpus=""
memory=""
api_token=""
uninstall=false
purge=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --token)          token="${2:-}";          shift 2 ;;
        --repo)           repo_url="${2:-}";       shift 2 ;;
        --labels)         labels="${2:-}";         shift 2 ;;
        --user)           run_as="${2:-}";         shift 2 ;;
        --runner-version) runner_version="${2:-}"; shift 2 ;;
        --instances)      instances="${2:-}";      shift 2 ;;
        --cpus)           cpus="${2:-}";           shift 2 ;;
        --memory)         memory="${2:-}";         shift 2 ;;
        --api-token)      api_token="${2:-}";      shift 2 ;;
        --uninstall)      uninstall=true;          shift ;;
        --purge)          purge=true;              shift ;;
        -h|--help)        sed -n '2,/^$/p' "${BASH_SOURCE[0]}" | sed 's/^# \?//'; exit 0 ;;
        *)                fail "Unknown argument: $1" ;;
    esac
done

[[ "${EUID}" -eq 0 ]] || fail "Run this with sudo. It creates a user and installs a systemd service."
command -v systemctl > /dev/null 2>&1 || fail "systemd is required; the agent is installed as a service."

# --------------------------------------------------------------------------------- uninstall ----

if [[ "${uninstall}" == true ]]; then
    [[ -n "${token}" ]] || fail "--uninstall needs --token as well, to deregister the runners with GitHub."

    bash "${REPO_ROOT}/runner/service.sh" stop > /dev/null
    ok "stopped remotebuildtool-service"

    log "Removing the runners"

    found=false
    while IFS= read -r dir; do
        [[ -n "${dir}" ]] || continue
        found=true
        remove_agent "${dir}"
    done < <(existing_agent_dirs)

    [[ "${found}" == true ]] || warn "no agent directory found under ${RUNNER_HOME}"

    rm -rf "${RUNNER_HOME}"
    ok "removed ${RUNNER_HOME}"

    if [[ "${purge}" == true ]]; then
        docker volume rm "${NUGET_VOLUME}" "${MAIN_VOLUME}" "${LOGS_VOLUME}" "${TELEMETRY_VOLUME}" "${WORK_VOLUME}" "${STAGING_VOLUME}" > /dev/null 2>&1 || true
        rm -rf "${SHARE_ROOT}/staging"
        rm -rf /etc/remotebuildtool
        rm -rf "${SHARE_ROOT}/logs"
        ok "deleted the package cache, main artifact store and logs"
        warn "kept the telemetry in ${SHARE_ROOT}/telemetry; delete it by hand once it is no longer wanted"
    else
        warn "kept ${NUGET_VOLUME}, ${MAIN_VOLUME} and ${LOGS_VOLUME}; pass --purge to delete them"
    fi

    warn "kept ${OFFICIAL_VOLUME}, which holds the Official drops; remove it with: docker volume rm ${OFFICIAL_VOLUME}"
    warn "kept the NFS export of ${SHARE_ROOT}; remove it with: sudo rm /etc/exports.d/bitenovac.exports && sudo exportfs -ra"

    log "Uninstalled."
    exit 0
fi

# ------------------------------------------------------------------------------------ inputs ----

[[ -n "${token}" ]] || fail "--token is required. Get one from Settings > Actions > Runners > New self-hosted runner."

[[ "${instances}" =~ ^[1-9][0-9]*$ ]] || fail "--instances must be a positive integer, got '${instances}'."

if [[ -z "${repo_url}" ]]; then
    repo_url="$(git -C "${REPO_ROOT}" remote get-url origin 2>/dev/null | sed -E 's|^git@github\.com:|https://github.com/|; s|\.git$||')" || true
fi
[[ -n "${repo_url}" ]] || fail "Could not determine the repository. Pass --repo https://github.com/OWNER/NAME."

case "$(uname -m)" in
    x86_64)  runner_arch="x64" ;;
    aarch64) runner_arch="arm64" ;;
    *)       fail "Unsupported architecture $(uname -m). The agent ships for x64 and arm64." ;;
esac

# ------------------------------------------------------------------------------------ docker ----

log "Docker"

apt-get update -qq
apt-get install --yes --no-install-recommends curl git tar ca-certificates > /dev/null

if command -v docker > /dev/null 2>&1; then
    ok "$(docker --version)"
else
    warn "docker is not installed; installing from the official repository"

    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
    chmod a+r /etc/apt/keyrings/docker.asc
    printf 'deb [arch=%s signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu %s stable\n' \
        "$(dpkg --print-architecture)" "$(. /etc/os-release && echo "${VERSION_CODENAME}")" \
        > /etc/apt/sources.list.d/docker.list
    apt-get update -qq
    apt-get install --yes docker-ce docker-ce-cli containerd.io > /dev/null
    ok "installed $(docker --version)"
fi

systemctl enable --now docker > /dev/null 2>&1 || true
docker info > /dev/null 2>&1 || fail "The Docker daemon is not running."

# -------------------------------------------------------------------------------------- user ----

log "Account"

if id "${run_as}" > /dev/null 2>&1; then
    ok "${run_as} exists"
else
    useradd --create-home --shell /bin/bash "${run_as}"
    ok "created ${run_as}"
fi

# The agent starts the job containers, so it needs the Docker socket. This is root-equivalent on
# this host: anything that can start a container can mount the filesystem into one.
usermod --append --groups docker "${run_as}"
ok "${run_as} is in the docker group"

readonly RUN_UID="$(id -u "${run_as}")"
readonly RUN_GID="$(id -g "${run_as}")"

host_cores="$(nproc)"

if [[ -n "${cpus}" ]]; then
    [[ "${cpus}" =~ ^[1-9][0-9]*$ ]] || fail "--cpus must be a positive integer, got '${cpus}'."
    (( cpus * instances <= host_cores )) || warn "${instances} x ${cpus} CPUs exceeds the ${host_cores} this machine has; jobs will contend"
    cpu_share="${cpus}"
else
    cpu_share=$(( host_cores / instances ))
    (( cpu_share < 1 )) && cpu_share=1
fi

# ------------------------------------------------------------------------------------- image ----

sdk_version="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "${REPO_ROOT}/global.json" | head -n 1)"
[[ -n "${sdk_version}" ]] || fail "Could not read the SDK version from ${REPO_ROOT}/global.json."

log "Building bitenovac-remotebuildtool-runner:${sdk_version} (.NET SDK ${sdk_version}, RemoteBuildTool)"
bash "${REPO_ROOT}/runner/build-image.sh"

# ------------------------------------------------------------------------------------- share ----

log "NFS share"

apt-get install --yes --no-install-recommends nfs-kernel-server > /dev/null
install -d -o "${run_as}" -g "${run_as}" "${SHARE_ROOT}" "${SHARE_ROOT}/logs" "${SHARE_ROOT}/telemetry" "${SHARE_ROOT}/staging"

# Whoever a client runs as, it writes as the agents' account, so job containers and the root
# container that migrates old logs leave files the agents can prune.
install -d /etc/exports.d
printf '%s 127.0.0.1(rw,sync,no_subtree_check,all_squash,anonuid=%s,anongid=%s)\n' \
    "${SHARE_ROOT}" "${RUN_UID}" "${RUN_GID}" > /etc/exports.d/bitenovac.exports
systemctl enable --now nfs-server > /dev/null 2>&1 || true
exportfs -ra || fail "Could not export ${SHARE_ROOT}. Under WSL, systemd must be on (see docs/Local_RemoteBuildTool_Setup.md)."
ok "exporting ${SHARE_ROOT} to this machine"

# NFS 4.1, not 4.2: the WSL kernel is built without 4.2, and Docker's mount of it fails with
# "invalid argument".
readonly SHARE_MOUNT_OPTIONS="addr=127.0.0.1,rw,nfsvers=4.1"

# A volume an earlier setup made locally moves onto the share first; one already on the share is
# made again when its mount options changed, which loses nothing since its files live on the
# share. Removing either fails while a job still has it mounted.
share_volume() {
    local name="$1" directory="$2" type
    if type="$(docker volume inspect --format '{{index .Options "type"}}' "${name}" 2> /dev/null)"; then
        if [[ "${type}" == nfs ]]; then
            if [[ "$(docker volume inspect --format '{{index .Options "o"}}' "${name}")" == "${SHARE_MOUNT_OPTIONS}" ]]; then
                ok "${name} is on the share"
                return
            fi
            docker volume rm "${name}" > /dev/null \
                || fail "${name} is in use. Stop the runners (sudo systemctl stop 'actions.runner.*') and run setup again."
            ok "remounting ${name} with ${SHARE_MOUNT_OPTIONS}"
        else
            docker run --rm --user 0 --entrypoint cp \
                --volume "${name}:/from:ro" \
                --volume "${SHARE_ROOT}/${directory}:/to" \
                "bitenovac-remotebuildtool-runner:${sdk_version}" -a /from/. /to/
            docker volume rm "${name}" > /dev/null \
                || fail "${name} is in use. Stop the runners (sudo systemctl stop 'actions.runner.*') and run setup again."
            ok "moved the contents of ${name} onto the share"
        fi
    fi

    docker volume create --driver local \
        --opt type=nfs \
        --opt "o=${SHARE_MOUNT_OPTIONS}" \
        --opt "device=:${SHARE_ROOT}/${directory}" \
        "${name}" > /dev/null
    ok "${name} -> ${SHARE_ROOT}/${directory}"
}
share_volume "${LOGS_VOLUME}" logs
share_volume "${TELEMETRY_VOLUME}" telemetry
share_volume "${STAGING_VOLUME}" staging

docker volume create "${NUGET_VOLUME}" > /dev/null
docker volume create "${MAIN_VOLUME}" > /dev/null
docker volume create "${OFFICIAL_VOLUME}" > /dev/null
# A new volume is owned by root, and the job containers run as the agent's user. Without this
# they cannot write to it, and for the NuGet cache specifically every restore downloads
# everything again.
docker run --rm --user 0 --entrypoint chown \
    --volume "${NUGET_VOLUME}:/cache" \
    --volume "${MAIN_VOLUME}:/mnt/main" \
    --volume "${OFFICIAL_VOLUME}:/mnt/official" \
    "bitenovac-remotebuildtool-runner:${sdk_version}" -R "${RUN_UID}:${RUN_GID}" /cache /mnt/main /mnt/official
ok "volumes ready: ${NUGET_VOLUME}, ${MAIN_VOLUME}, ${OFFICIAL_VOLUME} — owned by ${run_as}"

# ------------------------------------------------------------------------------------- agent ----

log "Agent"

# Every previous installation is removed rather than reconfigured: config.sh refuses to
# reconfigure a registered runner, and a half-updated directory is harder to reason about than a
# fresh one. Sweeping the whole glob also retires instances that a larger --instances left behind,
# and the single unsuffixed directory that earlier versions of this script produced.
while IFS= read -r dir; do
    [[ -n "${dir}" ]] || continue
    warn "an existing installation was found in ${dir##*/}; replacing it"
    remove_agent "${dir}"
done < <(existing_agent_dirs)

install -d -o "${run_as}" -g "${run_as}" "${RUNNER_HOME}"

if [[ -z "${runner_version}" ]]; then
    runner_version="$(curl -fsSL https://api.github.com/repos/actions/runner/releases/latest \
        | sed -n 's/.*"tag_name"[[:space:]]*:[[:space:]]*"v\([^"]*\)".*/\1/p' | head -n 1)"
    [[ -n "${runner_version}" ]] || fail "Could not resolve the latest agent release. Pass --runner-version."
fi

# Downloaded once and unpacked into each instance, rather than once per instance.
log "Installing ${instances} agent(s), version ${runner_version}"
curl -fsSL -o /tmp/actions-runner.tar.gz \
    "https://github.com/actions/runner/releases/download/v${runner_version}/actions-runner-linux-${runner_arch}-${runner_version}.tar.gz"

# System packages, so once for the machine rather than once per agent.
dependencies_installed=false

for (( instance = 1; instance <= instances; instance++ )); do
    agent_dir="$(agent_dir_for "${instance}")"
    runner_name="$(runner_name_for "${instance}")"

    install -d -o "${run_as}" -g "${run_as}" "${agent_dir}"
    tar -xzf /tmp/actions-runner.tar.gz -C "${agent_dir}"

    # The agent exports this file's contents into every job, and runner/in-container.sh forwards
    # it into the container. Deriving the share here rather than naming it in the workflow keeps a
    # core count that is only true of this machine out of the repository.
    {
        printf 'REMOTEBUILDTOOL_MAX_CPU=%s\n' "${cpu_share}"
        printf 'REMOTEBUILDTOOL_CPUS=%s\n' "${cpu_share}"
        [[ -n "${memory}" ]] && printf 'REMOTEBUILDTOOL_MEMORY=%s\n' "${memory}"
        printf 'REMOTEBUILDTOOL_POOL=docker\n'
    } > "${agent_dir}/.env"

    chown -R "${run_as}:${run_as}" "${agent_dir}"

    if [[ "${dependencies_installed}" == false ]]; then
        "${agent_dir}/bin/installdependencies.sh" > /dev/null
        ok "agent dependencies installed"
        dependencies_installed=true
    fi

    # The token is not spent by this: it stays valid for its hour and registers every instance.
    # What config.sh writes into .credentials is long lived and refreshed by the agent, so no
    # token is needed again for the runner to keep accepting jobs.
    log "Registering ${runner_name} with ${repo_url}"
    sudo -u "${run_as}" "${agent_dir}/config.sh" \
        --url "${repo_url}" \
        --token "${token}" \
        --name "${runner_name}" \
        --labels "${labels}" \
        --work "${agent_dir}/_work" \
        --unattended \
        --replace

    # Each runner name yields its own systemd unit, so the instances start and stop independently.
    (cd "${agent_dir}" && ./svc.sh install "${run_as}" > /dev/null && ./svc.sh start > /dev/null)
    ok "${runner_name}: service installed and started"
done

rm -f /tmp/actions-runner.tar.gz

# ----------------------------------------------------------------------------------- service ----

# The service is main's only writer. It finds main empty and rebuilds the branch from scratch
# into it straight away, then keeps it filled from merged pull requests and a rebuild every night.
if [[ -n "${api_token}" ]]; then
    log "RemoteBuildTool service"

    [[ "${api_token}" =~ ^[A-Za-z0-9_]+$ ]] || fail "--api-token does not look like a GitHub token."
    repository="${repo_url#https://github.com/}"
    [[ "${repository}" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]] \
        || fail "The service needs a GitHub repository, got '${repo_url}'."

    docker volume create "${WORK_VOLUME}" > /dev/null
    docker run --rm --user 0 --entrypoint chown --volume "${WORK_VOLUME}:/work" \
        "bitenovac-remotebuildtool-runner:${sdk_version}" "${RUN_UID}:${RUN_GID}" /work

    install -d -m 0755 /etc/remotebuildtool
    # Readable by the agents' account alone: it holds the token.
    install -m 0600 -o "${run_as}" -g "${run_as}" /dev/null /etc/remotebuildtool/config.json
    cat > /etc/remotebuildtool/config.json <<JSON
{
  "repository": "${repository}",
  "gitHubToken": "${api_token}",
  "pool": "docker",
  "machine": "$(hostname)-service",
  "workRoot": "/work",
  "mainStore": "/mnt/main",
  "stagingRoot": "/mnt/staging",
  "logsRoot": "/mnt/logs",
  "telemetryRoot": "/srv/bitenovac/telemetry",
  "maxCpu": ${cpu_share}
}
JSON
    {
        printf 'SERVICE_UID=%s\n' "${RUN_UID}"
        printf 'SERVICE_GID=%s\n' "${RUN_GID}"
        printf 'SERVICE_CPUS=%s\n' "${cpu_share}"
        printf 'SERVICE_MEMORY=%s\n' "${memory}"
        printf 'NUGET_VOLUME=%s\n' "${NUGET_VOLUME}"
        printf 'MAIN_VOLUME=%s\n' "${MAIN_VOLUME}"
        printf 'LOGS_VOLUME=%s\n' "${LOGS_VOLUME}"
        printf 'TELEMETRY_VOLUME=%s\n' "${TELEMETRY_VOLUME}"
        printf 'WORK_VOLUME=%s\n' "${WORK_VOLUME}"
        printf 'STAGING_VOLUME=%s\n' "${STAGING_VOLUME}"
    } > /etc/remotebuildtool/.env

    bash "${REPO_ROOT}/runner/service.sh" start > /dev/null
    ok "remotebuildtool-service is running; follow it with: sudo bash runner/service.sh logs"
else
    warn "no --api-token: the RemoteBuildTool service is not installed, so nothing fills main"
fi

# ------------------------------------------------------------------------------------- ready ----

log "Ready"
cat <<EOF
  Serving ${repo_url} with ${instances} agent(s), labelled: ${labels}
  $(for (( i = 1; i <= instances; i++ )); do printf '%s ' "$(runner_name_for "${i}")"; done)

  The workflow selects them with:  runs-on: [${labels//,/, }]

  ${instances} job(s) run at once. A pull request peaks at 2 (debug and release together), so
  ${instances} instance(s) run ${instances} pull request(s) at that concurrency; the rest queue.

  systemctl status 'actions.runner.*'    Service state, one unit per agent
  journalctl -u 'actions.runner.*' -f    Follow every agent
  docker ps                              The containers holding the current steps
  docker volume ls --filter 'name=bitenovac-'   Every volume this setup owns

  Jobs build in throwaway containers; ${NUGET_VOLUME}, ${MAIN_VOLUME}, ${LOGS_VOLUME} and
  ${OFFICIAL_VOLUME} persist. The Official workflow drops one directory per release into
  ${OFFICIAL_VOLUME}, named yyyy.MM.dd.NNN. Every PR, Official and service rebuild keeps its
  console output and MSBuild binary logs in ${LOGS_VOLUME}, and every stage writes its telemetry
  to ${TELEMETRY_VOLUME}; both are NFS volumes on ${SHARE_ROOT} — see
  docs/Local_RemoteBuildTool_Setup.md.
  Every run attempt keeps its own store in ${STAGING_VOLUME} for 24 hours; the service promotes
  it into main once its pull request is merged.

  Pull requests run the RemoteBuildTool release installed in the image. Once a bumped
  <VersionPrefix> in its csproj is on the feed, update it without
  re-registering the agents:  bash runner/build-image.sh
EOF
