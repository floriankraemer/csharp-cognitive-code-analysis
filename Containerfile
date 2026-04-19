# Dev image: run builds/tests via Makefile with the repo bind-mounted at /src.
# Same content as Dockerfile; Podman defaults to this filename for `podman build .`
#
# Docker: docker compose -f compose.yaml build && docker compose run --rm dev make ci
# Podman: podman compose -f compose.yaml build && podman compose run --rm dev make ci
#         podman-compose -f podman-compose.yaml build && podman-compose -f podman-compose.yaml run --rm dev make ci
#
# Projects target net8.0.
FROM mcr.microsoft.com/dotnet/sdk:8.0

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
    && apt-get install -y --no-install-recommends git make curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

CMD ["make", "help"]
