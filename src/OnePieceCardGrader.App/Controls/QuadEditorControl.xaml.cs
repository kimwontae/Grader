using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using OnePieceCardGrader.App.Services;
using OnePieceCardGrader.Core.Models;

namespace OnePieceCardGrader.App.Controls;

public partial class QuadEditorControl : UserControl
{
    public static readonly DependencyProperty ImagePathProperty =
        DependencyProperty.Register(nameof(ImagePath), typeof(string), typeof(QuadEditorControl), new PropertyMetadata(null, OnVisualChanged));

    public static readonly DependencyProperty CornersProperty =
        DependencyProperty.Register(nameof(Corners), typeof(QuadCorners), typeof(QuadEditorControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualChanged));

    public static readonly DependencyProperty ImageWidthPxProperty =
        DependencyProperty.Register(nameof(ImageWidthPx), typeof(double), typeof(QuadEditorControl), new PropertyMetadata(0d, OnVisualChanged));

    public static readonly DependencyProperty ImageHeightPxProperty =
        DependencyProperty.Register(nameof(ImageHeightPx), typeof(double), typeof(QuadEditorControl), new PropertyMetadata(0d, OnVisualChanged));

    private Ellipse? _dragHandle;
    private int _dragIndex = -1;

    public QuadEditorControl()
    {
        InitializeComponent();
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

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is QuadEditorControl control)
        {
            control.Redraw();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        Photo.Source = BitmapLoader.Load(ImagePath);
        Overlay.Children.Clear();
        if (Corners is null || ImageWidthPx <= 0 || ImageHeightPx <= 0)
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
            var handle = CreateHandle(ToCanvas(points[i]), i);
            Overlay.Children.Add(handle);
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

    private (double Scale, double OffsetX, double OffsetY) Fit()
    {
        var scale = Math.Min(Overlay.ActualWidth / ImageWidthPx, Overlay.ActualHeight / ImageHeightPx);
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
        {
            scale = 1;
        }

        var offsetX = (Overlay.ActualWidth - (ImageWidthPx * scale)) / 2.0;
        var offsetY = (Overlay.ActualHeight - (ImageHeightPx * scale)) / 2.0;
        return (scale, offsetX, offsetY);
    }

    private Point ToCanvas(ImagePoint point)
    {
        var fit = Fit();
        return new Point((point.X * fit.Scale) + fit.OffsetX, (point.Y * fit.Scale) + fit.OffsetY);
    }

    private ImagePoint ToImage(Point canvas)
    {
        var fit = Fit();
        var x = Math.Clamp((canvas.X - fit.OffsetX) / fit.Scale, 0, ImageWidthPx);
        var y = Math.Clamp((canvas.Y - fit.OffsetY) / fit.Scale, 0, ImageHeightPx);
        return new ImagePoint(x, y);
    }
}
