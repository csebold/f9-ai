# Version 1.1 Roadmap

## Project Support

### Instructions

* We will send the instructions along with the first chat in a conversation

### Files

* Projects have files and they will be sent along, or referred to, in the first chat in a conversation

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

1. Align Requirements: confirm scope for instructions delivery, file sharing, chat memory handling, cross-chat summaries, and Markdown rendering options.
2. Design Data Flow: map how instructions, files, and chat memories enter the system; define schemas and APIs for persistence and retrieval.
3. Update Backend: extend conversation initialization to attach instructions, file references, and prior memory payloads; add summarization mode controls.
4. Enhance Memory Layer: store per-chat transcripts and metadata; implement retrieval hooks that respect API capabilities (with/without native memory).
5. Surface Cross-Chat Context: build registry of related chats; expose selection/injection mechanism during session start.
6. Implement Summarization Options: add configuration to choose model/cost tier, execute summaries asynchronously, cache results, and expose toggles in UI/API.
7. Expand Markdown Renderer: upgrade response pipeline to format Markdown, optionally include source blocks, and render sent messages with the same engine.
8. QA & Tooling: write integration tests for initialization payloads, regression tests for summarization choices, and snapshot tests for Markdown output.
9. Rollout & Docs: update user-facing documentation and developer guides; provide migration notes for new initialization payloads and configuration flags.
10. Post-Launch Monitoring: instrument feature usage and error hooks, verify model cost impacts, and schedule feedback review after initial release.
