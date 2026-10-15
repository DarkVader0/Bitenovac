FROM ubuntu:24.04 AS sdk

ARG DOTNET_SDK_VERSION

SHELL ["/bin/bash", "-euo", "pipefail", "-c"]

RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
        ca-certificates \
        curl \
        git \
        tzdata \
        libicu74 \
        unzip \
    && rm -rf /var/lib/apt/lists/*

RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --version "${DOTNET_SDK_VERSION}" --install-dir /usr/share/dotnet \
    && ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet \
    && rm /tmp/dotnet-install.sh

ARG DUCKDB_VERSION=1.5.6

RUN case "$(uname -m)" in x86_64) arch=amd64 ;; aarch64) arch=arm64 ;; *) echo "error: no DuckDB CLI for $(uname -m)" >&2; exit 1 ;; esac \
    && curl -fsSL -o /tmp/duckdb.zip "https://github.com/duckdb/duckdb/releases/download/v${DUCKDB_VERSION}/duckdb_cli-linux-${arch}.zip" \
    && unzip -q /tmp/duckdb.zip -d /usr/local/bin \
    && chmod +x /usr/local/bin/duckdb \
    && rm /tmp/duckdb.zip

ENV DOTNET_ROOT=/usr/share/dotnet \
    PATH="/usr/share/dotnet:/root/.dotnet/tools:${PATH}" \
    DOTNET_NOLOGO=true \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=true

FROM sdk

ARG REMOTEBUILDTOOL_VERSION
ARG REMOTEBUILDTOOL_SOURCE=https://api.nuget.org/v3/index.json

RUN test -n "${REMOTEBUILDTOOL_VERSION}" || { echo "error: pass --build-arg REMOTEBUILDTOOL_VERSION; runner/build-image.sh does." >&2; exit 1; } \
    && dotnet tool install Bitenovac.RemoteBuildTool \
        --version "${REMOTEBUILDTOOL_VERSION}" \
        --tool-path /opt/remotebuildtool \
        --add-source "${REMOTEBUILDTOOL_SOURCE}" \
    && dotnet tool install Bitenovac.RemoteBuildTool.Service \
        --version "${REMOTEBUILDTOOL_VERSION}" \
        --tool-path /opt/remotebuildtool \
        --add-source "${REMOTEBUILDTOOL_SOURCE}"

ENV PATH="/opt/remotebuildtool:${PATH}"

COPY docker/entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/entrypoint.sh

RUN mkdir -p /mnt/pr /mnt/official && chmod 1777 /mnt/pr /mnt/official

WORKDIR /repo
ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
