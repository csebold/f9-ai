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

- [x] Send the instructions along with the first chat in a conversation
  - LLM requests now treat project instructions as a system message on the first successful turn of each conversation (or retry until one succeeds).
  - Switching chats or clearing history resets the flag so fresh sessions automatically resend the project guidance.
- [x] Anthropic uses a "system" parameter; use that, particularly with the first message in a new chat: https://docs.claude.com/en/docs/build-with-claude/prompt-engineering/system-prompts#how-to-give-claude-a-role
- [x] OpenAI
  - [x] Responses API: https://community.openai.com/t/system-prompt-in-responses-api/1144116
  - [x] Chat Completions API: Is there a "system" role?
- [x] OpenRouter
  - [x] However Chat Completions for OpenAI works, this should work
- [x] Ollama API
  - [x] Use role of "system" for first message and that should work for prompt sending


### Files

- [ ] Projects have files and they will be sent along, or referred to, in the first chat in a conversation
  - File summary plumbing exists in the request pipeline, but the current project-file workflow is unreliable; fix upload/persistence before re-enabling this milestone.
  - Sidebar management UI is in place (`+ Add file...` and per-file delete), yet the backend needs additional work so uploaded files consistently land in `projects/<id>/files` and are available for context injection.
- [ ] Chats will be able to upload files that are specific to that chat
  - [ ] Anthropic might just use a message of type "text", not sure
  - [ ] OpenAI
  - [ ] Ollama

### Persistence of memory in a single chat

- [ ] For APIs that do not have a built-in memory functionality, send some kind of memory of this chat with each message.
- [ ] For APIs that do have some sort of memory functionality, take advantage of it.

### Availability of other chat memories in a project

- [ ] Part of initialization of a chat will be to let the LLM know about other chats we have had in some way.

### Customization of how we summarize

- [ ] Options for summarization using the current model or another, cheaper LLM model will be available.
- [ ] Chat summarization works along these lines:
  - [ ] Every message goes into the "actual" chat history, that is kept with the chat, in the project, in the app
  - [ ] Every message is also added to the "virtual" chat history, which is the context being sent to the LLM with new chat messages
  - [ ] For each new message, divide text up into words
  - [ ] Every 700 words = 1000 tokens according to our calculations
  - [ ] If we get over 4000 tokens in a virtual chat history, we start removing the older messages from the virtual chat history (not the opening summary if there is one) and summarizing them
    - [ ] If there is a cheap external LLM configured for summarization, use that
    - [ ] Better still, if ollama is installed and configured to be used for summarization, use that
    - [ ] The algorithm should be such that "previous summary" + "oldest messages" --> "new summary" that is around 1000 words total, or 1,500 tokens
    - [ ] Remove messages from the chat history, starting with the oldest ones, and add them to the summary using resummarization by the LLM until the threshold is down to 3000 tokens again
    - [ ] In the end, the virtual chat history is summary + most recent messages + the message we're sending now


## Markdown Support

- [ ] Format and display Markdown in responses.
- [ ] Optionally display source in responses.
- [ ] Optionally display sent messages as formatted Markdown.

## Implementation Plan

1. ✅ **Unify model discovery**: expose refreshable provider model lists in global settings and per-project overrides; normalize provider inference when models or endpoints imply Ollama.
2. ✅ **Automated Ollama model catalog**: native catalog service merges installed tags with the online library, flags recommended models, and drives download/install prompts.
3. ✅ **Ollama lifecycle management**: detect missing daemon, start/stop it when switching models, and surface background process state/terminal access through the UI.
4. ✅ **Provider branding**: cache provider favicons, refresh them on startup, and display icons plus tooltips alongside the active model name.
5. 🔄 **Conversation bootstrap**: attach project instructions and relevant file metadata to the first message for new chats. (Instructions and conversation history are in place; file metadata awaits the project file fix.)
6. 🔄 **File handling**: separate project files from chat uploads, expose them for the LLM on demand, and manage retention policies.
7. 🔄 **Memory persistence**: persist short-term chat memories for providers without native support and integrate with native capabilities when they exist.
8. 🔄 **Cross-chat context**: surface summaries of related chats during session initialization based on project history.
9. 🔄 **Summarization controls**: allow choosing summary models/tiers, run summaries asynchronously, and add UI/API toggles.
10. 🔄 **Markdown experience**: implement consistent Markdown rendering for responses and sent messages, including optional source view.
11. 🔄 **Validation and rollout**: expand automated coverage, instrumentation, and documentation for the new capabilities.
