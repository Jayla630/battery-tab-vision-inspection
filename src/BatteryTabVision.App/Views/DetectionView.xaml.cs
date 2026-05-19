using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BatteryTabVision.App.ViewModels;

namespace BatteryTabVision.App.Views;

/// <summary>
/// 检测视图代码后置。职责仅限：ROI 拖拽坐标转换 + 调用 ViewModel 方法；不做任何业务决策。
/// </summary>
public partial class DetectionView : UserControl
{
    private System.Windows.Point? _dragStart;

    private DetectionViewModel? Vm => DataContext as DetectionViewModel;

    public DetectionView()
    {
        InitializeComponent();
    }

    private void RoiCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm == null) return;
        _dragStart = e.GetPosition(RoiCanvas);
        RoiCanvas.CaptureMouse();
    }

    private void RoiCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart == null) return;
        var current = e.GetPosition(RoiCanvas);
        var screenRect = new Rect(_dragStart.Value, current);

        Canvas.SetLeft(RoiRect, screenRect.X);
        Canvas.SetTop(RoiRect, screenRect.Y);
        RoiRect.Width = screenRect.Width;
        RoiRect.Height = screenRect.Height;
    }

    private void RoiCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart == null || Vm == null) return;
        RoiCanvas.ReleaseMouseCapture();

        var endPos = e.GetPosition(RoiCanvas);
        var screenRect = new Rect(_dragStart.Value, endPos);
        Vm.ImageRoi = ScreenToImage(screenRect);
        _dragStart = null;
    }

    /// <summary>把控件坐标系的矩形反算回图像坐标系，计入 Stretch=Uniform 的 letterbox 偏移。</summary>
    private Rectangle ScreenToImage(Rect screen)
    {
        if (Vm == null || Vm.CurrentImageWidth <= 0) return Rectangle.Empty;

        double imgW = Vm.CurrentImageWidth, imgH = Vm.CurrentImageHeight;
        double ctrlW = ImageElement.ActualWidth, ctrlH = ImageElement.ActualHeight;
        double scale = Math.Min(ctrlW / imgW, ctrlH / imgH);
        double renderedW = imgW * scale, renderedH = imgH * scale;
        double offsetX = (ctrlW - renderedW) / 2;
        double offsetY = (ctrlH - renderedH) / 2;

        int x = (int)Math.Max(0, (screen.X - offsetX) / scale);
        int y = (int)Math.Max(0, (screen.Y - offsetY) / scale);
        int w = (int)Math.Min(imgW - x, screen.Width / scale);
        int h = (int)Math.Min(imgH - y, screen.Height / scale);

        return new Rectangle(x, y, w, h);
    }
}
