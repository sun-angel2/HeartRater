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
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var serviceCollection = new ServiceCollection();

        // 1. Register Services
        serviceCollection.AddSingleton<IBluetoothService, BluetoothService>();
        serviceCollection.AddSingleton<StreamService>();
        serviceCollection.AddSingleton<LocalizationService>();
        serviceCollection.AddSingleton<HttpServerService>(); // Add the new service

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

        // 5. Start HTTP Server
        var httpServer = _serviceProvider.GetRequiredService<HttpServerService>();
        httpServer.Start();

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