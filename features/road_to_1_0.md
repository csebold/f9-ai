# Application Plan: The Road to 1.0

## Milestone 1: Development Environment Ready
- **Status:** Completed
- Install the .NET 8 SDK on all target platforms to ensure compatible builds.
- Pull in Avalonia templates via `dotnet new install Avalonia.Templates` so new projects use the latest scaffolding.
- Create the starter MVVM project with `dotnet new avalonia.mvvm -o ChatClient`.
- Run `dotnet restore` and `dotnet run` from the project directory to confirm the baseline window launches.

## Milestone 2: Core Chat UI in Place
- **Status:** Completed
- Maintain the Avalonia MVVM structure by binding `MainWindow.axaml` to `MainWindowViewModel`.
- Replace the scaffolded content with a flexibly sized chat layout: history panel, growing input area, and send button.
- Bind the chat history to an `ObservableCollection<Message>` so updates reflect immediately.
- Add `Prompt` and `SendCommand` properties; bind the input `TextBox` to `Prompt` and the send button to `SendCommand`.

## Milestone 3: LLM Service Integration
- **Status:** Completed
- Added an `ILlmClient` abstraction plus provider-specific clients for OpenAI, Anthropic, and OpenRouter.
- Implemented a factory that constructs clients from persisted app settings, providing consistent defaults and validation.
- Introduced a persisted settings JSON file, a dedicated settings service, and a runtime settings window to manage provider, API keys, and models.
- Added a model catalog service that queries each provider for available models so the user can select one interactively.
- Updated the chat view-model to send prompts asynchronously, append assistant replies, and surface errors as system messages while keeping the UI responsive.
- Added automatic system status messages announcing the active LLM provider/model whenever the app starts or the provider changes.

## Milestone 4: Responsive and Stateful Experience and Projects
- **Status:** In Progress

### Goals
- Deliver a polished startup and session bootstrapping flow (frameless splash, live status log).
- Provide responsive feedback while the assistant is thinking (typing indicator, disabled inputs, retry).
- Persist chat state on disk and restore it safely across launches.
- Keep long-running sessions usable (auto-scroll, timestamp formatting, cross-platform polish).

### Task Breakdown
1. **Startup Experience**
   - Finalize the frameless splash screen animation and service status log wiring.
   - Gate main window launch on initialization completion with timeout and cancel affordances.
   - Add unit coverage for the initialization sequence to protect against regression.
2. **Conversation Responsiveness**
   - Expose an `IsResponding` flag from the view-model and bind it to typing indicator visuals.
   - Disable send/input controls while requests are in-flight and surface a retry command on failure.
   - Surface provider latency and error summaries in the status bar for quick debugging.
3. **Session Persistence**
   - Serialize the `ObservableCollection<Message>` to JSON via `System.Text.Json` with versioning.
   - Restore the most recent sessions at startup and trim history to configurable limits.
   - Extend settings UI/service to toggle persistence and purge stored chats, with integration tests.
4. **Conversation Polish**
   - Implement auto-scroll that respects user scroll-up pauses and resumes on new messages.
   - Normalize timestamp display (local time, relative formatting for recent messages).
   - Audit visual states across dark/light themes and OS density settings.

### Dependencies & Risks
- Leverages Milestone 3 settings service hooks; confirm it supports new persistence toggles.
- Disk I/O for persistence must run off the UI thread with cancellation to avoid UI freezes.
- Retry logic needs mockable LLM client paths to keep tests fast and deterministic.

### Definition of Done
- Task breakdown items implemented with accompanying unit/integration coverage where feasible.
- Manual QA checklist executed on macOS and Windows.
- Release notes updated summarizing startup, responsiveness, and persistence improvements.

## Milestone 5: Cross-Platform Packaging and Delivery
- **Status:** Pending
- Validate the application on Windows, macOS, and Linux to confirm consistent behavior.
- Prepare self-contained builds using `dotnet publish` with RID-specific profiles for each OS.
- Automate packaging scripts (PowerShell/Bash) to produce installers or zip bundles as needed.
- Document deployment steps, update notes, and system requirements for beta testers.

## Natural Next Steps
- Provision API keys for the target providers and verify end-to-end responses in each environment.
- Extend the UI polish (assistant/user theming, auto-scroll, typing indicators) to improve readability.
- Begin Milestone 4 work: persist conversation history locally and surface connection state (loading/error) to users.
- Plan packaging requirements per platform in preparation for Milestone 5.
