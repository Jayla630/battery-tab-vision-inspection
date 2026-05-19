using System.Windows;
using System.Windows.Input;

namespace BatteryTabVision.App.Views;

/// <summary>主窗口壳，承载 Prism ContentRegion。</summary>
public partial class MainShellView : System.Windows.Window
{
    public MainShellView()
    {
        InitializeComponent();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void BtnClose_Click(object sender, RoutedEventArgs e)
        => Close();

    private void NavBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // 双击导航栏最大化/还原
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
        else if (e.ButtonState == MouseButtonState.Pressed)
        {
            if (WindowState == WindowState.Maximized)
            {
                // 最大化状态拖拽时先还原再拖
                WindowState = WindowState.Normal;
            }
            DragMove();
        }
    }
}
