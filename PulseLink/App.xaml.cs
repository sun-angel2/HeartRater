using Microsoft.Extensions.DependencyInjection;
using PulseLink.Services;
using PulseLink.ViewModels;
using System;
using System.Windows;

namespace PulseLink;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var serviceCollection = new ServiceCollection();

        // 1. Register Services
        serviceCollection.AddSingleton<IBluetoothService, BluetoothService>();
        serviceCollection.AddSingleton<StreamService>();
        serviceCollection.AddSingleton<LocalizationService>();

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


        // 5. Launch Main Window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}