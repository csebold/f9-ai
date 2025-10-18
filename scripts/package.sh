#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
PROJECT="${REPO_ROOT}/ChatClient/ChatClient.csproj"
CONFIG="${CONFIG:-Release}"
VERSION="${APP_VERSION:-}"
VERSION_FILE="${REPO_ROOT}/VERSION"

if [[ -z "${VERSION}" ]]; then
  if [[ -f "${VERSION_FILE}" ]]; then
    VERSION="$(tr -d '\r\n' < "${VERSION_FILE}")"
  fi
fi

if [[ -z "${VERSION}" ]]; then
  if git -C "${REPO_ROOT}" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    VERSION="$(git -C "${REPO_ROOT}" describe --tags --dirty --always 2>/dev/null || true)"
  fi
fi

if [[ -z "${VERSION}" ]]; then
  VERSION="$(date +%Y%m%d%H%M)"
fi

DEFAULT_RIDS=(
  "win-x64"
  "win-arm64"
  "osx-x64"
  "osx-arm64"
  "linux-x64"
  "linux-arm64"
)

if [[ "$#" -gt 0 ]]; then
  RIDS_TO_BUILD=("$@")
else
  RIDS_TO_BUILD=("${DEFAULT_RIDS[@]}")
fi

PUBLISH_ROOT="${REPO_ROOT}/artifacts/publish"
PACKAGE_ROOT="${REPO_ROOT}/artifacts/packages/${VERSION}"

mkdir -p "${PUBLISH_ROOT}"
mkdir -p "${PACKAGE_ROOT}"

for rid in "${RIDS_TO_BUILD[@]}"; do
  publish_dir="${PUBLISH_ROOT}/${rid}"
  package_name="ChatClient-${VERSION}-${rid}.zip"
  package_path="${PACKAGE_ROOT}/${package_name}"

  echo ":: Packaging ${rid}"
  rm -rf "${publish_dir}"
  mkdir -p "${publish_dir}"

  dotnet publish "${PROJECT}" \
    -c "${CONFIG}" \
    -r "${rid}" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PublishTrimmed=false \
    -o "${publish_dir}"

  (
    cd "${publish_dir}"
    rm -f "${package_path}"
    zip -qry "${package_path}" .
  )

  echo "   -> ${package_path}"
done

echo ":: Completed packaging to ${PACKAGE_ROOT}"
