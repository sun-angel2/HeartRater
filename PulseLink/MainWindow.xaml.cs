using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using PulseLink.ViewModels;

namespace PulseLink;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsGhostMode))
        {
            SetClickThrough(_viewModel.IsGhostMode);
        }
    }

    // Allow dragging the borderless window
    private void Window_MouseDown(object sender, MouseButtonEventArgs e) 
    { 
        if (e.ChangedButton == MouseButton.Left) 
            DragMove(); 
    }

    // --- Window Control Button Handlers ---
    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void Tray_Click(object sender, RoutedEventArgs e)
    {
        // For now, just minimize. Proper tray implementation requires a NotifyIcon.
        this.WindowState = WindowState.Minimized; 
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }


    // --- Mouse Passthrough Logic (Win32 API) ---
    private void SetClickThrough(bool isClickThrough)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        
        if (isClickThrough)
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
        }
        else
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
        }
    }

    // Win32 Constants & Imports
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
}