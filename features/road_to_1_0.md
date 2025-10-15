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
- Added an `ILlmClient` abstraction plus provider-specific clients for OpenAI, Anthropic, and OpenRouter driven by environment variables.
- Implemented a factory that selects the provider via `LLM_PROVIDER`, defaulting to OpenAI, and validates required keys/models.
- Updated the view-model to send prompts asynchronously, append assistant replies, and surface errors as system messages while keeping the UI responsive.
- Added automatic system status messages announcing the active LLM provider/model whenever the app starts or the provider changes.
- Ensured all secrets are supplied externally (environment variables) so nothing sensitive is hard-coded.

## Milestone 4: Responsive and Stateful Experience
- **Status:** Pending
- Add richer feedback (typing indicator, disabled inputs, retry affordances) while awaiting LLM responses.
- Serialize and persist the `Messages` collection (JSON via `System.Text.Json`) to provide session continuity.
- Reload prior chats at startup, with sensible limits on stored history to manage disk usage.
- Implement auto-scroll and timestamps formatting to keep the experience polished across providers.

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
