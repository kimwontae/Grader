using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using OnePieceCardGrader.App.Services;
using OnePieceCardGrader.Core.Models;

namespace OnePieceCardGrader.App.Controls;

public partial class QuadEditorControl : UserControl
{
    public static readonly DependencyProperty ImagePathProperty =
        DependencyProperty.Register(nameof(ImagePath), typeof(string), typeof(QuadEditorControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualChanged));

    public static readonly DependencyProperty CornersProperty =
        DependencyProperty.Register(nameof(Corners), typeof(QuadCorners), typeof(QuadEditorControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualChanged));

    public static readonly DependencyProperty ImageWidthPxProperty =
        DependencyProperty.Register(nameof(ImageWidthPx), typeof(double), typeof(QuadEditorControl),
            new PropertyMetadata(0d, OnVisualChanged));

    public static readonly DependencyProperty ImageHeightPxProperty =
        DependencyProperty.Register(nameof(ImageHeightPx), typeof(double), typeof(QuadEditorControl),
            new PropertyMetadata(0d, OnVisualChanged));

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(QuadEditorControl),
            new PropertyMetadata(1.0, OnVisualChanged));

    public static readonly DependencyProperty AllowImageReplaceProperty =
        DependencyProperty.Register(nameof(AllowImageReplace), typeof(bool), typeof(QuadEditorControl),
            new PropertyMetadata(false, OnVisualChanged));

    private Ellipse? _dragHandle;
    private int _dragIndex = -1;
    private bool _panning;
    private ZoomPanScroll.PanState _panState;
    private bool _updatingStage;

    public QuadEditorControl()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshVisual(reloadImage: true);
    }

    public string? ImagePath
    {
        get => (string?)GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public QuadCorners? Corners
    {
        get => (QuadCorners?)GetValue(CornersProperty);
        set => SetValue(CornersProperty, value);
    }

    public double ImageWidthPx
    {
        get => (double)GetValue(ImageWidthPxProperty);
        set => SetValue(ImageWidthPxProperty, value);
    }

    public double ImageHeightPx
    {
        get => (double)GetValue(ImageHeightPxProperty);
        set => SetValue(ImageHeightPxProperty, value);
    }

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public bool AllowImageReplace
    {
        get => (bool)GetValue(AllowImageReplaceProperty);
        set => SetValue(AllowImageReplaceProperty, value);
    }

    private double ContentWidth =>
        ImageWidthPx > 0 ? ImageWidthPx : (Photo.Source as BitmapSource)?.PixelWidth ?? 0;

    private double ContentHeight =>
        ImageHeightPx > 0 ? ImageHeightPx : (Photo.Source as BitmapSource)?.PixelHeight ?? 0;

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is QuadEditorControl control)
        {
            control.RefreshVisual(reloadImage: e.Property == ImagePathProperty);
        }
    }

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e) => RefreshVisual(reloadImage: false);

    private void RefreshVisual(bool reloadImage)
    {
        if (reloadImage || Photo.Source is null)
        {
            Photo.Source = BitmapLoader.Load(ImagePath);
        }

        var hasImage = Photo.Source is not null && ContentWidth > 0 && ContentHeight > 0;
        EmptyHint.Visibility = hasImage ? Visibility.Collapsed : Visibility.Visible;
        BrowseButton.Visibility = AllowImageReplace ? Visibility.Visible : Visibility.Collapsed;
        UpdateStageSize();
        DrawOverlay();
        ZoomHint.Text = $"Ctrl+휠 확대  {Zoom * 100:0}%  ·  Shift+휠 좌우  ·  가운데 버튼 드래그 이동";
    }

    private void UpdateStageSize()
    {
        if (_updatingStage)
        {
            return;
        }

        var width = ContentWidth;
        var height = ContentHeight;
        if (width <= 0 || height <= 0)
        {
            Stage.Width = 1;
            Stage.Height = 1;
            return;
        }

        var viewportW = Scroller.ActualWidth > 1 ? Scroller.ActualWidth : Math.Max(ActualWidth, 1);
        var viewportH = Scroller.ActualHeight > 1 ? Scroller.ActualHeight : Math.Max(ActualHeight, 1);
        if (Zoom <= 1.001)
        {
            viewportW = Math.Max(1, viewportW - 4);
            viewportH = Math.Max(1, viewportH - 4);
        }

        var scale = ZoomPanScroll.FitScale(viewportW, viewportH, width, height) * Zoom;
        var stageW = Math.Max(1, width * scale);
        var stageH = Math.Max(1, height * scale);
        if (Math.Abs(Stage.Width - stageW) < 1 && Math.Abs(Stage.Height - stageH) < 1)
        {
            return;
        }

        _updatingStage = true;
        Stage.Width = stageW;
        Stage.Height = stageH;
        _updatingStage = false;
    }

    private void DrawOverlay()
    {
        Overlay.Children.Clear();
        if (Corners is null || ContentWidth <= 0 || ContentHeight <= 0)
        {
            return;
        }

        var points = Corners.ToList();
        var polygon = new Polygon
        {
            Stroke = new SolidColorBrush(Color.FromRgb(201, 162, 39)),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(40, 201, 162, 39))
        };
        foreach (var point in points)
        {
            polygon.Points.Add(ToCanvas(point));
        }

        Overlay.Children.Add(polygon);
        for (var i = 0; i < points.Count; i++)
        {
            Overlay.Children.Add(CreateHandle(ToCanvas(points[i]), i));
        }
    }

    private Ellipse CreateHandle(Point canvasPoint, int index)
    {
        var handle = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = Brushes.White,
            Stroke = new SolidColorBrush(Color.FromRgb(201, 162, 39)),
            StrokeThickness = 2,
            Tag = index,
            Cursor = Cursors.Hand
        };
        Canvas.SetLeft(handle, canvasPoint.X - 7);
        Canvas.SetTop(handle, canvasPoint.Y - 7);
        return handle;
    }

    private void OnCanvasDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Ellipse ellipse && ellipse.Tag is int index)
        {
            _dragHandle = ellipse;
            _dragIndex = index;
            Overlay.CaptureMouse();
        }
    }

    private void OnCanvasMove(object sender, MouseEventArgs e)
    {
        if (_dragHandle is null || Corners is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var pos = e.GetPosition(Overlay);
        var imagePoint = ToImage(pos);
        var list = Corners.ToList().ToArray();
        list[_dragIndex] = imagePoint;
        Corners = new QuadCorners
        {
            TopLeft = list[0],
            TopRight = list[1],
            BottomRight = list[2],
            BottomLeft = list[3]
        };
    }

    private void OnCanvasUp(object sender, MouseButtonEventArgs e)
    {
        _dragHandle = null;
        _dragIndex = -1;
        Overlay.ReleaseMouseCapture();
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ZoomPanScroll.TryHandleWheel(e, Scroller, Zoom, next => Zoom = next);
    }

    private void OnScrollPreviewDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle ||
            (e.ChangedButton == MouseButton.Right && e.OriginalSource is not Ellipse))
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

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        if (!AllowImageReplace)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tif;*.tiff"
        };
        if (dialog.ShowDialog() == true)
        {
            ImagePath = dialog.FileName;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (!AllowImageReplace)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!AllowImageReplace)
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0 && File.Exists(files[0]))
        {
            ImagePath = files[0];
        }
    }

    private Point ToCanvas(ImagePoint point)
    {
        var width = ContentWidth;
        var height = ContentHeight;
        if (width <= 0 || height <= 0)
        {
            return new Point(point.X, point.Y);
        }

        return new Point(point.X / width * Stage.Width, point.Y / height * Stage.Height);
    }

    private ImagePoint ToImage(Point canvas)
    {
        var width = ContentWidth;
        var height = ContentHeight;
        var x = width <= 0 ? 0 : Math.Clamp(canvas.X / Stage.Width * width, 0, width);
        var y = height <= 0 ? 0 : Math.Clamp(canvas.Y / Stage.Height * height, 0, height);
        return new ImagePoint(x, y);
    }
}
