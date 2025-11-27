using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using PulseLink.ViewModels;

namespace PulseLink;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    // Allow dragging the borderless window
    private void Window_MouseDown(object sender, MouseButtonEventArgs e) 
    { 
        if (e.ChangedButton == MouseButton.Left) 
            DragMove(); 
    }

    // --- Mouse Passthrough Logic (Win32 API) ---
    private void ToggleClickThrough(object sender, RoutedEventArgs e)
    {
        var checkBox = sender as CheckBox;
        if (checkBox == null) return;

        var hwnd = new WindowInteropHelper(this).Handle;
        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        
        if (checkBox.IsChecked == true)
        {
            // Add Transparent flag (Click-through)
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
            // Ensure Topmost
            this.Topmost = true;
        }
        else
        {
            // Remove Transparent flag
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
        }
    }

    // Win32 Constants
    const int GWL_EXSTYLE = -20;
    const int WS_EX_TRANSPARENT = 0x00000020;

    [DllImport("user32.dll")]
    static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
}