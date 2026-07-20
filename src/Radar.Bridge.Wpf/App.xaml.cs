using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yuexin.Radar.Bridge.Wpf.Logging;
using Yuexin.Radar.Bridge.Wpf.Services;
using Yuexin.Radar.Bridge.Wpf.ViewModels;
using Yuexin.Radar.Configuration;

namespace Yuexin.Radar.Bridge.Wpf;

public partial class App : Application
{
    private ServiceProvider? _services;
    private RadarAppConfiguration? _configuration;
    private string? _configurationPath;

    protected override async void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"RadarBridge 遇到未处理错误：\n\n{args.Exception.Message}",
                "RadarBridge 错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            _configurationPath = ReadArgument(eventArgs.Args, "--profile")
                ?? RadarConfigurationStore.GetDefaultUserConfigurationPath();
            _configuration = await RadarConfigurationStore.LoadAsync(_configurationPath);

            var services = new ServiceCollection();
            services.AddLogging(builder => builder
                .SetMinimumLevel(LogLevel.Information)
                .AddConsole()
                .AddProvider(new AsyncFileLoggerProvider(AsyncFileLoggerProvider.GetDefaultLogPath())));
            services.AddSingleton(_configuration);
            services.AddSingleton<RadarBridgeRuntime>();
            services.AddSingleton<IRadarBridgeRuntime>(provider => provider.GetRequiredService<RadarBridgeRuntime>());
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();
            _services = services.BuildServiceProvider(validateScopes: true);

            var runtime = _services.GetRequiredService<IRadarBridgeRuntime>();
            await runtime.StartInfrastructureAsync();
            var window = _services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
            if (eventArgs.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase))
            {
                window.WindowState = WindowState.Minimized;
            }

            if (int.TryParse(ReadArgument(eventArgs.Args, "--parent-pid"), out var parentProcessId))
            {
                _ = MonitorParentProcessAsync(parentProcessId);
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"RadarBridge 启动失败：\n\n{exception}",
                "RadarBridge 启动失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        try
        {
            if (_services?.GetService<IRadarBridgeRuntime>() is { } runtime)
            {
                runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            if (_configuration is not null && _configurationPath is not null)
            {
                RadarConfigurationStore.SaveAsync(_configurationPath, _configuration).GetAwaiter().GetResult();
            }
        }
        finally
        {
            _services?.Dispose();
            base.OnExit(eventArgs);
        }
    }

    private async Task MonitorParentProcessAsync(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            await process.WaitForExitAsync();
            await Dispatcher.InvokeAsync(() => Shutdown());
        }
        catch (ArgumentException)
        {
            await Dispatcher.InvokeAsync(() => Shutdown());
        }
    }

    private static string? ReadArgument(IReadOnlyList<string> arguments, string name)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return arguments[index + 1];
            }
        }

        return null;
    }
}
