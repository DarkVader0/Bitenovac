#!/usr/bin/env bash
#
# The container side of runner/telemetry.sh, which mounts the telemetry share at ${SHARE}. Takes
# the same commands.

set -euo pipefail

readonly tables=(build stage target test machine)
readonly placeholder="date=0001-01-01"
readonly sql="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

columns() {
    duckdb -noheader -list -c "$(cat "${sql}/schema.sql") SET VARIABLE record = '$1'; $(cat "${sql}/columns.sql")"
}

compact() {
    local today table struct dir day compacting out part
    today="$(date -u +%F)"

    for table in "${tables[@]}"; do
        struct="$(columns "${table}")"
        mkdir -p "${SHARE}/${table}/${placeholder}" "${SHARE}/parquet/${table}/${placeholder}"
        touch "${SHARE}/${table}/${placeholder}/empty.jsonl"
        if [[ ! -f "${SHARE}/parquet/${table}/${placeholder}/schema.parquet" ]]; then
            duckdb -c "$(cat "${sql}/schema.sql")
                COPY schema_${table} TO '${SHARE}/parquet/${table}/${placeholder}/schema.parquet' (FORMAT parquet);"
        fi

        # Files move aside before they are read, so one a late stage writes meanwhile waits for the
        # next run instead of being deleted unread. A run that stopped part way left its moved
        # files in .compacting-<day>, which this one picks up.
        for dir in "${SHARE}/${table}"/date=* "${SHARE}/${table}"/.compacting-*; do
            [[ -d "${dir}" ]] || continue
            day="${dir##*/}"
            day="${day#date=}"
            day="${day#.compacting-}"
            [[ "${day}" != 0001-01-01 && "${day}" < "${today}" ]] || continue

            compacting="${SHARE}/${table}/.compacting-${day}"
            mkdir -p "${compacting}"
            if [[ -d "${SHARE}/${table}/date=${day}" ]]; then
                find "${SHARE}/${table}/date=${day}" -maxdepth 1 -name '*.jsonl' -exec mv -t "${compacting}" {} +
            fi

            if compgen -G "${compacting}/*.jsonl" > /dev/null; then
                out="${SHARE}/parquet/${table}/date=${day}"
                mkdir -p "${out}"
                part="${out}/part-$(date -u +%Y%m%dT%H%M%SZ)-${RANDOM}.parquet"
                duckdb -c "COPY (SELECT * FROM read_json('${compacting}/*.jsonl', format = 'newline_delimited', columns = ${struct}))
                    TO '${part}.partial' (FORMAT parquet, COMPRESSION zstd);"
                mv "${part}.partial" "${part}"
                rm -f "${compacting}"/*.jsonl
                echo "compacted ${table} ${day} into ${part#"${SHARE}"/}"
            fi

            rmdir "${compacting}" 2> /dev/null || true
            rmdir "${SHARE}/${table}/date=${day}" 2> /dev/null || true
        done
    done

    local views="" database
    for table in "${tables[@]}"; do
        views+="CREATE VIEW ${table} AS
            SELECT * FROM read_parquet('${SHARE}/parquet/${table}/*/*.parquet', union_by_name = true)
            UNION ALL BY NAME
            SELECT * FROM read_json('${SHARE}/${table}/date=*/*.jsonl', format = 'newline_delimited', columns = $(columns "${table}"));
        "
    done

    # Written beside the share and moved in whole, so a reader never opens half a database.
    database="$(mktemp -d)/telemetry.duckdb"
    duckdb "${database}" -c "${views} $(cat "${sql}/queries.sql")"
    cp "${database}" "${SHARE}/telemetry.duckdb.partial"
    mv "${SHARE}/telemetry.duckdb.partial" "${SHARE}/telemetry.duckdb"
    echo "rebuilt telemetry.duckdb"
}

case "$1" in
    compact) compact ;;
    shell) exec duckdb -readonly "${SHARE}/telemetry.duckdb" ;;
    query) exec duckdb -readonly "${SHARE}/telemetry.duckdb" -c "$2" ;;
esac
