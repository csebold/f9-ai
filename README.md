# F9 AI Chat Client

Cross-platform Avalonia desktop client that delivers a responsive chat experience across multiple LLM providers. The roadmap toward 1.0 lives in `features/road_to_1_0.md` and tracks milestone progress.

## Quick Start

- Install the .NET 9 SDK (`dotnet --list-sdks` should list 9.x).
- Restore and run the desktop client:
  ```bash
  dotnet restore
  dotnet run --project ChatClient
  ```
- Run unit tests:
  ```bash
  dotnet test
  ```

## Project Highlights

- MVVM architecture with streaming chat, retry flows, typing indicators, and persistent multi-project sessions.
- Settings service with runtime UI for provider, model, and chat persistence configuration.
- Auto-scroll, timestamp formatting, dark/light polish, and startup splash sequence for a smooth UX.

## Packaging & Distribution

- Self-contained single-file bundles are produced per runtime identifier using the scripts in `scripts/`.
- Default targets: `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, `linux-arm64`.
- Create release artifacts:
  ```bash
  ./scripts/package.sh
  # or on Windows
  pwsh ./scripts/package.ps1
  ```
- Artifacts land in `artifacts/packages/<version>`. Override the version stamp by exporting `APP_VERSION`.
- See `docs/deployment.md` for the verification checklist, system requirements, and release notes template.

## Developer Workflow

- The chat client lives in `ChatClient/`; supporting integration and unit tests sit inside `ChatClient.Tests/`.
- Message, session, and configuration models are centralized under `Models/`.
- Avalonia resources (styles, icons, fonts) reside beneath `ChatClient/Assets/`.

## Roadmap & Status

- Current milestone status, goals, and risks are tracked in `features/road_to_1_0.md`.
- Feature-specific design notes are documented in `features/visual_features.md` and `features/input_specific_niceties.md`.

## Contributing

- Ensure new behavior is covered by unit or integration tests where feasible.
- Run `dotnet format` if you modify C# source files.
- Open a merge request with a brief description of the change and validation performed.
