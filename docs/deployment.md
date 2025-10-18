# Deployment Guide

This guide outlines how to produce release-ready builds of the Avalonia chat client, verify the output on each supported operating system, and distribute artifacts to beta testers.

## Build Artifacts

- Packaging scripts output zipped, self-contained binaries that do not require the .NET runtime.
- Artifacts land in `artifacts/packages/<version>/ChatClient-<version>-<rid>.zip`.
- Runtime identifiers (`rid`) covered by default: `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, `linux-arm64`.
- Version metadata originates from the repository `VERSION` file (patch incremented on each commit). You can override it at packaging time with `APP_VERSION=...`.

## Running the Packaging Scripts

- **macOS / Linux**  
  ```bash
  ./scripts/package.sh            # builds all default RIDs in Release mode
  APP_VERSION=1.0.0 ./scripts/package.sh linux-x64 osx-arm64
  CONFIG=Debug ./scripts/package.sh win-x64
  ```

- **Windows (PowerShell 7+)**  
  ```powershell
  pwsh ./scripts/package.ps1                   # builds all default RIDs
  pwsh ./scripts/package.ps1 -Configuration Release -RuntimeIdentifiers win-x64
  $env:APP_VERSION="1.0.0-beta1"; pwsh ./scripts/package.ps1
  ```

- The scripts clean per-RID publish directories before each build and overwrite existing zip files with the same name.

## Verification Checklist

- Unzip each artifact on its target OS and launch the chat client:
  - Confirm splash screen progress messages render and transition to the main window.
  - Send a prompt and observe live assistant streaming, typing indicators, and retry affordances.
  - Switch between projects/sessions to ensure state restoration and persistence behave as expected.
  - Inspect timestamps for relative formatting and validate auto-scroll resumes after interacting with history.
- Smoke-test settings:
  - Toggle providers/models and ensure system announcements update.
  - Disable and re-enable chat persistence to validate disk IO paths.
- Record results in the release log before distributing builds.

## System Requirements for Beta Testers

- **Windows:** Windows 10 (1809) or greater, x64 or ARM64 CPU, hardware acceleration optional.
- **macOS:** macOS 12 Monterey or later on Intel or Apple Silicon hardware.
- **Linux:** Ubuntu 22.04+, Fedora 38+, or comparable distro with GTK3 and libSkia dependencies; x64 or ARM64.
- A GPU that supports OpenGL 3.0 or Vulkan is recommended for smooth rendering but not required.
- Network access to configured LLM providers via HTTPS.

## Release Notes Template

- **Focus:** Responsive chat UX, persistent sessions, cross-platform packaging scripts.
- **Builds:** Attach zipped binaries per RID from `artifacts/packages/<version>`.
- **Validation:** Link to the verification checklist above with dated results.
- **Known Issues:** Document provider limitations or OS-specific quirks encountered during testing.

Update this guide as tooling or distribution targets evolve.
