using System;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Services;
using ChatClient.ViewModels;

namespace ChatClient;

public partial class App
{
    internal static IDisposable OverrideStartupTimeoutForTesting(TimeSpan timeout)
    {
        _startupTimeoutOverride = timeout;
        return new RestoreScope(() => _startupTimeoutOverride = null);
    }

    internal static IDisposable OverrideUiInvokerForTesting(Func<Action, Task> invoker)
    {
        if (invoker is null)
        {
            throw new ArgumentNullException(nameof(invoker));
        }

        var previous = _uiInvokeAsync;
        _uiInvokeAsync = invoker;
        return new RestoreScope(() => _uiInvokeAsync = previous);
    }

    internal Task WatchForTimeoutAsyncForTesting(
        Task initializationTask,
        SplashScreenViewModel splashViewModel,
        CancellationToken token,
        Action<string> postStatus) =>
        WatchForTimeoutAsync(initializationTask, splashViewModel, token, postStatus);

    internal void SetServicesForTesting(
        IModelCatalogService? modelCatalogService,
        IOllamaProcessManager? ollamaProcessManager,
        IBackgroundProcessService? backgroundProcessService)
    {
        _modelCatalogService = modelCatalogService!;
        _ollamaProcessManager = ollamaProcessManager!;
        _backgroundProcessService = backgroundProcessService!;
    }

    internal void DisposeServicesForTesting()
    {
        if (_modelCatalogService is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _ollamaProcessManager?.Dispose();
        _backgroundProcessService?.Dispose();
        OllamaProcessRegistry.Clear();
    }

    internal void DisableValidationForTesting() => DisableAvaloniaDataAnnotationValidation();

    private sealed class RestoreScope : IDisposable
    {
        private readonly Action _callback;

        public RestoreScope(Action callback)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public void Dispose() => _callback();
    }
}
