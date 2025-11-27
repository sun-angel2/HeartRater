using Microsoft.Extensions.DependencyInjection;
using PulseLink.Services;
using PulseLink.ViewModels;
using System;
using System.Windows;

namespace PulseLink;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    public App()
    {
        Startup += OnStartup;
        Exit += OnExit;

        // Add global exception handling
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogUnhandledException(e.Exception, "UI Thread Exception");
        e.Handled = true; // Mark the exception as handled to prevent the application from crashing
        MessageBox.Show("发生了一个未处理的UI错误，应用程序将尝试继续运行。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogUnhandledException(e.ExceptionObject as Exception, "Non-UI Thread Exception");
        MessageBox.Show("发生了一个未处理的错误，应用程序将退出。", "致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
        Environment.Exit(1); // Exit the application for non-UI thread unhandled exceptions
    }

    private void LogUnhandledException(Exception? exception, string type)
    {
        try
        {
            var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            System.IO.File.AppendAllText(logPath,
                $"{DateTime.Now}: {type}\n" +
                $"Message: {exception?.Message}\n" +
                $"Source: {exception?.Source}\n" +
                $"Stack Trace: {exception?.StackTrace}\n" +
                $"Target Site: {exception?.TargetSite}\n" +
                $"Inner Exception: {exception?.InnerException?.Message}\n\n");
        }
        catch (Exception ex)
        {
            // If logging fails, at least try to show a message box
            MessageBox.Show($"无法写入错误日志: {ex.Message}\n原始错误: {exception?.Message}", "日志错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var serviceCollection = new ServiceCollection();

        // 1. Register Services
        serviceCollection.AddSingleton<IBluetoothService, BluetoothService>();
        serviceCollection.AddSingleton<StreamService>();
        serviceCollection.AddSingleton<LocalizationService>();
        serviceCollection.AddSingleton(provider =>
        {
            var mainViewModel = provider.GetRequiredService<MainViewModel>();
            return new HttpServerService(() => mainViewModel.Bpm);
        });

        // 2. Register ViewModels
        serviceCollection.AddSingleton<MainViewModel>();
        serviceCollection.AddSingleton(provider => (ObservableStrings)Current.Resources["LocalizedStrings"]);

        // 3. Register Views
        serviceCollection.AddSingleton<MainWindow>();

        _serviceProvider = serviceCollection.BuildServiceProvider();
        
        // 4. Connect services
        var locService = _serviceProvider.GetRequiredService<LocalizationService>();
        var observableStrings = _serviceProvider.GetRequiredService<ObservableStrings>();
        locService.LanguageChanged += observableStrings.Refresh;

        // 5. Start HTTP Server (now independent of MainViewModel)
        try
        {
            var httpServer = _serviceProvider.GetRequiredService<HttpServerService>();
            httpServer.Start();
        }
        catch (Exception ex)
        {
            LogUnhandledException(ex, "HttpServerService Startup Error");
            // Optionally, inform the user that the HTTP server failed to start
            // MessageBox.Show($"HTTP Server failed to start: {ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // 6. Launch Main Window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

        private void OnExit(object sender, ExitEventArgs e)

        {

            // Ensure all disposable services are disposed on exit

            if (_serviceProvider is IDisposable disposable)

            {

                disposable.Dispose();

            }

            else

            {

                _serviceProvider?.GetService<HttpServerService>()?.Dispose();

                _serviceProvider?.GetService<IBluetoothService>()?.Dispose();

                _serviceProvider?.GetService<StreamService>()?.Dispose();

            }

        }

    }