# Feature request: input niceties

## Chat input

- [x] hotkeys respect OS defaults (usually Emacs-style in text inputs for macOS)
   * example: `ctrl-a` goes to beginning of line in macOS but selects all in Windows
- [x] special app-specific hotkeys as part of settings UI, respected throughout
- [x] setting for whether `enter`, `shift-enter`, `control-enter`, or `command-enter` activate the "send" in chat
- [x] intelligent handling of enter:
   * If there is just one line in the chat, then `enter` is "send" command
   * If there is markdown in that one line (starts with a `#` or `*` or could be a markdown list item) then `enter` inserts a carriage return and we fall back to "modified enter only" as "send"

## Markdown input

 - [ ] Markdown editor option for input, as opposed to plain text (toggles with a button on the chat input component)
