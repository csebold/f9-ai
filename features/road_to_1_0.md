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
- **Status:** Pending
- Define an abstraction such as `ILlmClient` to decouple the UI from the backend provider.
- Implement an `HttpClient`-based service (e.g., `HttpLlmClient`) targeting the selected LLM endpoint (OpenAI, Azure, local, etc.).
- In `SendCommand`, append the user message, await the LLM response, and add the assistant reply.
- Handle faults gracefully with `try`/`catch`, surfacing usable errors to the UI and logs.
- Load secrets (API keys, endpoints) exclusively from environment variables or .NET user secrets.

## Milestone 4: Responsive and Stateful Experience
- **Status:** Pending
- Offload network calls and other heavy work onto background tasks or async commands so the UI thread stays responsive.
- Show progress indicators (e.g., disable send button, display typing indicator) during LLM calls.
- Serialize and persist the `Messages` collection (JSON via `System.Text.Json`) to provide session continuity.
- Reload prior chats at startup, with sensible limits on stored history to manage disk usage.

## Milestone 5: Cross-Platform Packaging and Delivery
- **Status:** Pending
- Validate the application on Windows, macOS, and Linux to confirm consistent behavior.
- Prepare self-contained builds using `dotnet publish` with RID-specific profiles for each OS.
- Automate packaging scripts (PowerShell/Bash) to produce installers or zip bundles as needed.
- Document deployment steps, update notes, and system requirements for beta testers.

## Natural Next Steps
- Select the LLM backend and provision credentials.
- Flesh out XAML bindings and finish the chat layout polish.
- Implement the LLM service and plug it into `SendCommand`.
- Add quality-of-life enhancements (loading indicator, markdown rendering, configuration dialog) once the end-to-end flow works.
