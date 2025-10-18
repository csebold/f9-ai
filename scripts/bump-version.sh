#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
VERSION_FILE="${REPO_ROOT}/VERSION"
MANIFEST_FILE="${REPO_ROOT}/ChatClient/app.manifest"

function current_version() {
  if [[ -f "${VERSION_FILE}" ]]; then
    tr -d '\r\n' < "${VERSION_FILE}"
  else
    echo "0.0.0"
  fi
}

function increment_patch() {
  local version="$1"
  IFS='.' read -r major minor patch <<< "${version}"
  if [[ -z "${major}" || -z "${minor}" || -z "${patch}" ]]; then
    echo "Invalid version '${version}'. Expected semantic version (e.g. 1.2.3)." >&2
    exit 1
  fi
  patch=$((patch + 1))
  echo "${major}.${minor}.${patch}"
}

function update_manifest() {
  local version="$1"
  if [[ -f "${MANIFEST_FILE}" ]]; then
    local manifest_version="${version}.0"
    cat > "${MANIFEST_FILE}" <<EOF
﻿<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <!-- This manifest is used on Windows only.
       Don't remove it as it might cause problems with window transparency and embedded controls.
       For more details visit https://learn.microsoft.com/en-us/windows/win32/sbscs/application-manifests -->
  <assemblyIdentity version="${manifest_version}" name="ChatClient.Desktop"/>

  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <!-- A list of the Windows versions that this application has been tested on
           and is designed to work with. Uncomment the appropriate elements
           and Windows will automatically select the most compatible environment. -->

      <!-- Windows 10 -->
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
</assembly>
EOF
  fi
}

OLD_VERSION="$(current_version)"
NEW_VERSION="$(increment_patch "${OLD_VERSION}")"

echo "${NEW_VERSION}" > "${VERSION_FILE}"
update_manifest "${NEW_VERSION}"

echo "Version bumped: ${OLD_VERSION} -> ${NEW_VERSION}"
