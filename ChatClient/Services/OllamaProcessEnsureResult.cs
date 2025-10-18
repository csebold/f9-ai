namespace ChatClient.Services;

public sealed record OllamaProcessEnsureResult(bool AlreadyRunningExternally, BackgroundProcessSnapshot? ManagedProcess);
