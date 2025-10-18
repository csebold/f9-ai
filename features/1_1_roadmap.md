# Version 1.1 Roadmap

## Model Support

### ollama

* We will support local installations and routing hubs like Ollama

## Project Support

### Instructions

* We will send the instructions along with the first chat in a conversation

### Files

* Projects have files and they will be sent along, or referred to, in the first chat in a conversation
* Chats will be able to upload files that are specific to that chat

### Persistence of memory in a single chat

* For APIs that do not have a built-in memory functionality, we will send some kind of memory of this chat with each message.
* For APIs that do have some sort of memory functionality, we will take advantage of it.

### Availability of other chat memories in a project

* Part of initialization of a chat will be to let the LLM know about other chats we have had in some way.

### Customization of how we summarize

* Options for summarization using the current model or another, cheaper LLM model will be available.

## Markdown Support

* We will format and display Markdown in responses.
* We will optionally display source in responses.
* We will optionally display sent messages as formatted Markdown.

## Implementation Plan

1. Gather Integration Details: review current model adapters and confirm connectivity requirements for local Ollama installs and routing hubs, including auth and transport nuances.
2. Implement Ollama Adapter Updates: extend the model connector to target local and hub endpoints, expose configuration flags, and add smoke tests to validate prompt/response flow.
3. Bootstrap Conversations: update chat initialization so the first message bundles project instructions and file summaries, and define payload contracts for downstream clients.
4. Enhance File Handling: differentiate project-level files from chat uploads, ensure storage links are available during the first exchange, and document retention rules.
5. Build Memory Persistence: design storage for per-chat transcripts, add serialization that replays memories when the API lacks native recall, and short-circuit when native memory exists.
6. Surface Cross-Chat Context: create an index of related chats, implement selection heuristics, and inject summarized context during session startup when relevant.
7. Add Summarization Controls: provide configuration for model tier selection, run summaries asynchronously with caching, and surface toggles via API and UI.
8. Upgrade Markdown Rendering: wire a consistent renderer for responses and sent messages, add optional source view, and verify formatting in the display pipeline.
9. Validate and Launch: expand automated coverage for initialization payloads, summarization choices, and Markdown output; refresh documentation, instrumentation, and support playbooks ahead of rollout.
