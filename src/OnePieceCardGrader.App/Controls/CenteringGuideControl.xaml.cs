using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using OnePieceCardGrader.App.Services;
using OnePieceCardGrader.Core.Models;

namespace OnePieceCardGrader.App.Controls;

public partial class CenteringGuideControl : UserControl
{
    public static readonly DependencyProperty ImagePathProperty =
        DependencyProperty.Register(nameof(ImagePath), typeof(string), typeof(CenteringGuideControl), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty GuideProperty =
        DependencyProperty.Register(nameof(Guide), typeof(CenteringGuide), typeof(CenteringGuideControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnChanged));

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(CenteringGuideControl), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnChanged));

    private string? _loadedPath;
    private string? _activeLine;
    private bool _panning;
    private ZoomPanScroll.PanState _panState;
    private bool _updatingStage;

    public CenteringGuideControl()
    {
        InitializeComponent();
        Overlay.MouseMove += (_, e) => UpdateMagnifier(e.GetPosition(Overlay));
        Loaded += (_, _) => Redraw();
    }

    public string? ImagePath
    {
        get => (string?)GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public CenteringGuide? Guide
    {
        get => (CenteringGuide?)GetValue(GuideProperty);
        set => SetValue(GuideProperty, value);
    }

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CenteringGuideControl control)
        {
            control.Redraw();
        }
    }

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        BitmapSource? bitmap;
        if (_loadedPath != ImagePath || Photo.Source is null)
        {
            bitmap = BitmapLoader.Load(ImagePath);
            Photo.Source = bitmap;
            _loadedPath = ImagePath;
        }
        else
        {
            bitmap = Photo.Source as BitmapSource;
        }
        UpdateStageSize(bitmap);
        Overlay.Children.Clear();
        ZoomHint.Text = $"Ctrl+휠 확대  {Zoom * 100:0}%  ·  Shift+휠 좌우  ·  가운데 버튼 드래그 이동";
        if (Guide is null || bitmap is null)
        {
            return;
        }

        AddLine("left", Guide.PrintLeft * Stage.Width, vertical: true);
        AddLine("right", Guide.PrintRight * Stage.Width, vertical: true);
        AddLine("top", Guide.PrintTop * Stage.Height, vertical: false);
        AddLine("bottom", Guide.PrintBottom * Stage.Height, vertical: false);
    }

    private void UpdateStageSize(BitmapSource? bitmap)
    {
        if (_updatingStage || bitmap is null)
        {
            return;
        }

        var viewportW = Scroller.ActualWidth > 1 ? Scroller.ActualWidth : Math.Max(ActualWidth, 1);
        var viewportH = Scroller.ActualHeight > 1 ? Scroller.ActualHeight : Math.Max(ActualHeight, 1);
        if (Zoom <= 1.001)
        {
            viewportW = Math.Max(1, viewportW - 4);
            viewportH = Math.Max(1, viewportH - 4);
        }

        var scale = ZoomPanScroll.FitScale(viewportW, viewportH, bitmap.PixelWidth, bitmap.PixelHeight) * Zoom;
        var stageW = Math.Max(1, bitmap.PixelWidth * scale);
        var stageH = Math.Max(1, bitmap.PixelHeight * scale);
        if (Math.Abs(Stage.Width - stageW) < 1 && Math.Abs(Stage.Height - stageH) < 1)
        {
            return;
        }

        _updatingStage = true;
        Stage.Width = stageW;
        Stage.Height = stageH;
        _updatingStage = false;
    }

    private void AddLine(string name, double position, bool vertical)
    {
        var line = new Line
        {
            Stroke = new SolidColorBrush(Color.FromRgb(94, 201, 122)),
            StrokeThickness = 2,
            Tag = name,
            Cursor = vertical ? Cursors.SizeWE : Cursors.SizeNS
        };
        if (vertical)
        {
            line.X1 = position;
            line.X2 = position;
            line.Y1 = 0;
            line.Y2 = Stage.Height;
        }
        else
        {
            line.Y1 = position;
            line.Y2 = position;
            line.X1 = 0;
            line.X2 = Stage.Width;
        }

        Overlay.Children.Add(line);
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Line line && line.Tag is string name)
        {
            _activeLine = name;
            Overlay.CaptureMouse();
        }
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        UpdateMagnifier(e.GetPosition(Overlay));
        if (_activeLine is null || Guide is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var pos = e.GetPosition(Overlay);
        var clone = Guide.Clone();
        switch (_activeLine)
        {
            case "left":
                clone.PrintLeft = Math.Clamp(pos.X / Stage.Width, 0.005, clone.PrintRight - 0.01);
                break;
            case "right":
                clone.PrintRight = Math.Clamp(pos.X / Stage.Width, clone.PrintLeft + 0.01, 0.995);
                break;
            case "top":
                clone.PrintTop = Math.Clamp(pos.Y / Stage.Height, 0.005, clone.PrintBottom - 0.01);
                break;
            case "bottom":
                clone.PrintBottom = Math.Clamp(pos.Y / Stage.Height, clone.PrintTop + 0.01, 0.995);
                break;
        }

        Guide = clone;
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        _activeLine = null;
        Overlay.ReleaseMouseCapture();
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ZoomPanScroll.TryHandleWheel(e, Scroller, Zoom, next => Zoom = next);
    }

    private void OnScrollPreviewDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle ||
            (e.ChangedButton == MouseButton.Right && e.OriginalSource is not Line))
        {
            ZoomPanScroll.BeginPan(Scroller, e.GetPosition(Scroller), out _panState);
            _panning = true;
            Scroller.CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnScrollPreviewMove(object sender, MouseEventArgs e)
    {
        if (!_panning)
        {
            return;
        }

        ZoomPanScroll.MovePan(Scroller, e.GetPosition(Scroller), _panState);
        e.Handled = true;
    }

    private void OnScrollPreviewUp(object sender, MouseButtonEventArgs e)
    {
        if (!_panning)
        {
            return;
        }

        _panning = false;
        Scroller.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void UpdateMagnifier(Point position)
    {
        if (Photo.Source is not BitmapSource bitmap)
        {
            return;
        }

        var nx = position.X / Math.Max(Stage.Width, 1);
        var ny = position.Y / Math.Max(Stage.Height, 1);
        var x = (int)Math.Clamp(nx * bitmap.PixelWidth - 40, 0, Math.Max(0, bitmap.PixelWidth - 80));
        var y = (int)Math.Clamp(ny * bitmap.PixelHeight - 40, 0, Math.Max(0, bitmap.PixelHeight - 80));
        Magnifier.Source = new CroppedBitmap(bitmap, new Int32Rect(x, y, Math.Min(80, bitmap.PixelWidth), Math.Min(80, bitmap.PixelHeight)));
    }
}
