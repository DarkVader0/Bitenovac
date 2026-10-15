#!/usr/bin/env bash
#
# Runs one Bitenovac.RemoteBuildTool command in a fresh container, discarded when the command returns.
#
# Usage, from a workflow step with the repository checked out:
#   bash runner/in-container.sh plan
#   bash runner/in-container.sh test Debug
#
# The workspace is mounted read-write: RemoteBuildTool writes obj/ and bin/ there for real projects to
# use. Volumes carry what does not belong in the workspace or does not survive the container:
# NuGet packages, main's artifact store, this run's own artifact store, and logs. The container
# runs as the calling user, so the next checkout can delete what it leaves behind.
#
# RemoteBuildTool itself is not built from this checkout. It is the release installed into the image (see
# runner/build-image.sh), so a pull request cannot change the RemoteBuildTool that judges it.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly NUGET_VOLUME="${REMOTEBUILDTOOL_NUGET_VOLUME:-bitenovac-runner-nuget}"
readonly MAIN_VOLUME="${REMOTEBUILDTOOL_MAIN_VOLUME:-bitenovac-main}"
readonly LOGS_VOLUME="${REMOTEBUILDTOOL_LOGS_VOLUME:-bitenovac-logs}"
readonly OFFICIAL_VOLUME="${REMOTEBUILDTOOL_OFFICIAL_VOLUME:-bitenovac-official}"
readonly TELEMETRY_VOLUME="${REMOTEBUILDTOOL_TELEMETRY_VOLUME:-bitenovac-telemetry}"
readonly STAGING_VOLUME="${REMOTEBUILDTOOL_STAGING_VOLUME:-bitenovac-staging}"
readonly RUN_STORE="${GITHUB_RUN_ID:-local}-${GITHUB_RUN_ATTEMPT:-1}"

cd "${REPO_ROOT}"

fail() { printf '\nerror: %s\n' "$*" >&2; exit 1; }

[[ $# -gt 0 ]] || fail "No command given. Pass a Bitenovac.RemoteBuildTool command, for example: test Debug"

# The run's folder in the logs volume, set by the workflow, as in PR_18/16473829-1. The
# pattern keeps a crafted value from escaping the volume.
[[ -z "${REMOTEBUILDTOOL_LOG_RUN:-}" || "${REMOTEBUILDTOOL_LOG_RUN}" =~ ^[A-Za-z]+_[A-Za-z0-9_]+/[0-9]+-[0-9]+$ ]] \
    || fail "REMOTEBUILDTOOL_LOG_RUN '${REMOTEBUILDTOOL_LOG_RUN}' is not <Kind>_<number>/<run id>-<attempt>."

sdk_version="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' global.json | head -n 1)"
[[ -n "${sdk_version}" ]] || fail "Could not read the SDK version from global.json."

readonly IMAGE="bitenovac-remotebuildtool-runner:${sdk_version}"

docker image inspect "${IMAGE}" > /dev/null 2>&1 \
    || fail "The image ${IMAGE} is missing. Run runner/build-image.sh on this machine; it builds the image from global.json."

# Setup puts it on the NFS share; until it has, a local volume owned by the job's account serves.
if ! docker volume inspect "${STAGING_VOLUME}" > /dev/null 2>&1; then
    docker volume create "${STAGING_VOLUME}" > /dev/null
    docker run --rm --user 0 --entrypoint chown --volume "${STAGING_VOLUME}:/mnt/staging" "${IMAGE}" "$(id -u):$(id -g)" /mnt/staging
fi

# main is read-only in every job: only the RemoteBuildTool service writes it.
run_args=(
    --rm
    --init
    --user "$(id -u):$(id -g)"
    --volume "${REPO_ROOT}:/repo"
    --volume "${NUGET_VOLUME}:/cache"
    --volume "${MAIN_VOLUME}:/mnt/main:ro"
    --volume "${STAGING_VOLUME}:/mnt/staging"
    --volume "${LOGS_VOLUME}:/mnt/logs"
    --workdir /repo
    # Running as a non-root user leaves no writable home, so both are pointed somewhere writable
    # instead of relying on one. NUGET_PACKAGES is what puts the cache on the mounted volume.
    --env NUGET_PACKAGES=/cache/nuget
    # NuGet arbitrates concurrent extraction with lock files under the temp directory, not under
    # the packages folder. Agents share the volume but not /tmp, so leaving this unset lets two
    # containers extract the same package into one directory with nothing between them, and the
    # loser's rename target disappears mid-restore.
    --env TMPDIR=/cache/tmp
    # .NET 10.0.13 and later keep named mutexes under HOME, so without it the CLI's first
    # NuGet-Migrations mutex fails before restore starts.
    --env HOME=/tmp
    --env DOTNET_CLI_HOME=/tmp
    --env DOTNET_NOLOGO=true
    --env DOTNET_CLI_TELEMETRY_OPTOUT=true
    --env TESTINGPLATFORM_TELEMETRY_OPTOUT=1
    --env REMOTEBUILDTOOL_REPO_ROOT=/repo
    --env REMOTEBUILDTOOL_MAIN_STORE=/mnt/main
    --env "REMOTEBUILDTOOL_PR_STORE=/mnt/staging/${RUN_STORE}"
    --env REMOTEBUILDTOOL_DROP_ROOT=/mnt/official
)

# Only 'release' writes the drops, so no other command can touch them.
[[ "${1}" == "release" ]] && run_args+=(--volume "${OFFICIAL_VOLUME}:/mnt/official")

# The agent's share of the machine, from its .env (see runner/setup-runner.sh). Swap is capped to
# the same value, so a job over its memory is killed rather than paging the host.
[[ -n "${REMOTEBUILDTOOL_CPUS:-}" ]] && run_args+=(--cpus "${REMOTEBUILDTOOL_CPUS}")
[[ -n "${REMOTEBUILDTOOL_MEMORY:-}" ]] && run_args+=(--memory "${REMOTEBUILDTOOL_MEMORY}" --memory-swap "${REMOTEBUILDTOOL_MEMORY}")

# Passed through when set.
for variable in GITHUB_ACTIONS GITHUB_RUN_ID GITHUB_RUN_ATTEMPT GITHUB_WORKFLOW GITHUB_JOB GITHUB_REPOSITORY GITHUB_REF GITHUB_SHA \
    REMOTEBUILDTOOL_MAX_CPU REMOTEBUILDTOOL_CACHELESS REMOTEBUILDTOOL_COVERAGE_HTML REMOTEBUILDTOOL_POOL REMOTEBUILDTOOL_LOG_RUN; do
    [[ -n "${!variable:-}" ]] && run_args+=(--env "${variable}=${!variable}")
done

[[ -n "${REMOTEBUILDTOOL_LOG_RUN:-}" ]] && run_args+=(--env "REMOTEBUILDTOOL_LOGS_DIR=/mnt/logs/${REMOTEBUILDTOOL_LOG_RUN}")

# Telemetry records name the runner, not the throwaway container the command runs in.
run_args+=(--env "REMOTEBUILDTOOL_MACHINE=${RUNNER_NAME:-$(hostname)}")

# Every command writes its telemetry to the share when this machine has one (see
# runner/setup-runner.sh); a machine set up before it simply records nothing.
if docker volume inspect "${TELEMETRY_VOLUME}" > /dev/null 2>&1; then
    run_args+=(--volume "${TELEMETRY_VOLUME}:/mnt/telemetry" --env REMOTEBUILDTOOL_TELEMETRY_DIR=/mnt/telemetry)
fi

# The step's run summary is a file the runner reads once the step ends; mounted on its own so the
# command can append to it and nothing else of the runner's directory is visible.
if [[ -n "${GITHUB_STEP_SUMMARY:-}" && -f "${GITHUB_STEP_SUMMARY}" ]]; then
    run_args+=(--volume "${GITHUB_STEP_SUMMARY}:/mnt/step-summary.md" --env GITHUB_STEP_SUMMARY=/mnt/step-summary.md)
fi

exec docker run "${run_args[@]}" --entrypoint bash "${IMAGE}" -c '
    set -euo pipefail
    mkdir -p "${TMPDIR}"

    # Hundredths as a fixed point number: 4271 -> 42.71.
    fixed() { printf "%d.%02d" $(( $1 / 100 )) $(( $1 % 100 )); }

    # Bytes in MiB below a GiB and in GiB from there.
    size() {
        if (( $1 < 1073741824 )); then
            printf "%d MiB" $(( $1 / 1048576 ))
        else
            printf "%s GiB" "$(fixed $(( $1 * 100 / 1073741824 )))"
        fi
    }

    # Milliseconds since the container started, which is when its cgroup began counting. PID 1 is
    # the --init process; field 22 of its stat is its start in clock ticks (100 a second) since
    # boot, which /proc/uptime counts in seconds to two decimals.
    elapsed_ms() {
        local stat uptime fields
        stat="$(< /proc/1/stat)"
        read -r -a fields <<< "${stat##*) }"
        read -r uptime _ < /proc/uptime
        printf "%d" $(( ${uptime%.*} * 1000 + 10#${uptime#*.} * 10 - fields[19] * 10 ))
    }

    # Reads the container'"'"'s own cgroup into cpu_usec, throttled_usec, oom_kills, memory_now,
    # memory_peak, memory_limit and cpu_limit (hundredths of a core); a limit is empty when there is
    # none. Docker mounts the container'"'"'s own cgroup at the root, under v2 and v1 alike, so this
    # needs nothing from the host. Both peaks count page cache, as the --memory limit does.
    counters() {
        local cgroup=/sys/fs/cgroup key value quota period
        cpu_usec=0 throttled_usec=0 oom_kills=0 memory_limit="" cpu_limit=""

        if [[ -r "${cgroup}/cpu.stat" && -r "${cgroup}/memory.peak" ]]; then
            while read -r key value; do
                case "${key}" in
                    usage_usec) cpu_usec="${value}" ;;
                    throttled_usec) throttled_usec="${value}" ;;
                esac
            done < "${cgroup}/cpu.stat"
            while read -r key value; do
                case "${key}" in
                    oom_kill) oom_kills="${value}" ;;
                esac
            done < "${cgroup}/memory.events"
            memory_now="$(< "${cgroup}/memory.current")"
            memory_peak="$(< "${cgroup}/memory.peak")"
            value="$(< "${cgroup}/memory.max")"
            if [[ "${value}" != max ]]; then
                memory_limit="${value}"
            fi
            read -r quota period < "${cgroup}/cpu.max"
            if [[ "${quota}" != max ]]; then
                cpu_limit=$(( quota * 100 / period ))
            fi
        elif [[ -r "${cgroup}/cpuacct/cpuacct.usage" && -r "${cgroup}/memory/memory.max_usage_in_bytes" ]]; then
            cpu_usec=$(( $(< "${cgroup}/cpuacct/cpuacct.usage") / 1000 ))
            if [[ -r "${cgroup}/cpu/cpu.stat" ]]; then
                while read -r key value; do
                    case "${key}" in
                        throttled_time) throttled_usec=$(( value / 1000 )) ;;
                    esac
                done < "${cgroup}/cpu/cpu.stat"
            fi
            while read -r key value; do
                case "${key}" in
                    oom_kill) oom_kills="${value}" ;;
                esac
            done < "${cgroup}/memory/memory.oom_control"
            memory_now="$(< "${cgroup}/memory/memory.usage_in_bytes")"
            memory_peak="$(< "${cgroup}/memory/memory.max_usage_in_bytes")"
            # cgroup v1 spells "no limit" as a number near the top of a 64-bit range.
            value="$(< "${cgroup}/memory/memory.limit_in_bytes")"
            if (( value < 1 << 62 )); then
                memory_limit="${value}"
            fi
            quota="$(< "${cgroup}/cpu/cpu.cfs_quota_us")"
            period="$(< "${cgroup}/cpu/cpu.cfs_period_us")"
            if (( quota > 0 )); then
                cpu_limit=$(( quota * 100 / period ))
            fi
        else
            return 1
        fi
    }

    resources() {
        if ! counters; then
            printf "resources: container not measured; its cgroup statistics are not readable\n"
            return
        fi

        local elapsed cores="no limit" memory
        elapsed="$(elapsed_ms)"
        if [[ -n "${cpu_limit}" ]]; then
            cores="$(fixed "${cpu_limit}")"
        fi
        memory="$(size "${memory_peak}")"
        if [[ -n "${memory_limit}" ]]; then
            memory+=" of $(size "${memory_limit}") ($(( memory_peak * 100 / memory_limit )) %)"
        fi

        printf "resources: container used %s s, CPU %s s (%s cores of %s), throttled %s s, peak memory %s, %s out-of-memory kill(s)\n" \
            "$(fixed $(( elapsed / 10 )))" \
            "$(fixed $(( cpu_usec / 10000 )))" \
            "$(fixed $(( elapsed > 0 ? cpu_usec / 10 / elapsed : 0 )))" \
            "${cores}" \
            "$(fixed $(( throttled_usec / 10000 )))" \
            "${memory}" \
            "${oom_kills}"
    }

    # One line every 5 seconds, for the curve of a step rather than its totals.
    timeline() {
        counters || return 0
        printf "seconds,memory_bytes,cpu_usec\n"
        while counters; do
            printf "%d,%d,%d\n" $(( $(elapsed_ms) / 1000 )) "${memory_now}" "${cpu_usec}"
            sleep 5
        done
    }

    # reportgenerator, which only the coverage merge in "test" runs.
    [[ "$1" == "test" ]] && dotnet tool restore > /dev/null

    status=0
    if [[ -z "${REMOTEBUILDTOOL_LOGS_DIR:-}" ]]; then
        /opt/remotebuildtool/remotebuildtool "$@" || status=$?
        resources
        exit "${status}"
    fi

    # One log per command, such as build-Debug.log, with build-Debug.resources.csv beside it;
    # pipefail makes the status the tool'"'"'s.
    mkdir -p "${REMOTEBUILDTOOL_LOGS_DIR}"
    name="$(IFS=-; printf "%s" "$*")"
    timeline > "${REMOTEBUILDTOOL_LOGS_DIR}/${name}.resources.csv" &
    sampler=$!
    /opt/remotebuildtool/remotebuildtool "$@" 2>&1 | tee -a "${REMOTEBUILDTOOL_LOGS_DIR}/${name}.log" || status=$?
    kill "${sampler}" 2> /dev/null || true
    resources | tee -a "${REMOTEBUILDTOOL_LOGS_DIR}/${name}.log"
    exit "${status}"
' ci "$@"
