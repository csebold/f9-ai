# Version 1.1 Roadmap

## Model Support

### ollama

- [x] Support local installations and routing hubs like Ollama
- [x] If Ollama is not running then start it in the background while we're running
- [x] Support showing a terminal where local tools or servers are running, actually multiple terminal support because we could be running multiple servers and tools at the same time; add this to the GUI with buttons marked with icons indicating if this is a server, an MCP, or what. We don't have to reinvent the wheel, each terminal can spawn a native terminal that is tailing the output of the server or tool or whatever that's running in another thread
- [x] When Ollama is unavailable, keep prior chat history visible and present a clear provider error instead of an empty conversation

#### Multiple models switching and downloading

- [x] Present refreshable provider-specific model lists in both global and project settings
- [x] Replace the prototype script with a native catalog fetch that scrapes https://ollama.com/library, merges installed tags, and highlights Apple Silicon friendly models
- [x] Have the model list indicate the ones you already have and the ones that you could download
- [x] Offer to download the model you want
- [x] Switching models should shut down the Ollama server and then start it back up again

## LLM Model Support

- [x] For each provider, get their favicon image and cache it
- [x] For each startup of the application, check to see if it changed, and update the cached version if it did
- [x] In the display in the window for what version LLM you're running, use the favicon image with a tooltip indicating what provider it is, followed by the text of the model itself, instead of provider text and model text

## Project Support

### Instructions

- [ ] Send the instructions along with the first chat in a conversation

### Files

- [ ] Projects have files and they will be sent along, or referred to, in the first chat in a conversation
- [ ] Chats will be able to upload files that are specific to that chat

### Persistence of memory in a single chat

- [ ] For APIs that do not have a built-in memory functionality, send some kind of memory of this chat with each message.
- [ ] For APIs that do have some sort of memory functionality, take advantage of it.

### Availability of other chat memories in a project

- [ ] Part of initialization of a chat will be to let the LLM know about other chats we have had in some way.

### Customization of how we summarize

- [ ] Options for summarization using the current model or another, cheaper LLM model will be available.

## Markdown Support

- [ ] Format and display Markdown in responses.
- [ ] Optionally display source in responses.
- [ ] Optionally display sent messages as formatted Markdown.

## Implementation Plan

1. ✅ **Unify model discovery**: expose refreshable provider model lists in global settings and per-project overrides; normalize provider inference when models or endpoints imply Ollama.
2. ✅ **Automated Ollama model catalog**: native catalog service merges installed tags with the online library, flags recommended models, and drives download/install prompts.
3. ✅ **Ollama lifecycle management**: detect missing daemon, start/stop it when switching models, and surface background process state/terminal access through the UI.
4. 🔄 **Provider branding**: cache provider favicons, refresh them on startup, and display icons plus tooltips alongside the active model name.
5. 🔄 **Conversation bootstrap**: attach project instructions and relevant file metadata to the first message for new chats.
6. 🔄 **File handling**: separate project files from chat uploads, expose them for the LLM on demand, and manage retention policies.
7. 🔄 **Memory persistence**: persist short-term chat memories for providers without native support and integrate with native capabilities when they exist.
8. 🔄 **Cross-chat context**: surface summaries of related chats during session initialization based on project history.
9. 🔄 **Summarization controls**: allow choosing summary models/tiers, run summaries asynchronously, and add UI/API toggles.
10. 🔄 **Markdown experience**: implement consistent Markdown rendering for responses and sent messages, including optional source view.
11. 🔄 **Validation and rollout**: expand automated coverage, instrumentation, and documentation for the new capabilities.
