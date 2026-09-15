using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OnePieceCardGrader.App.Controls;

internal static class ZoomPanScroll
{
    public const double MinZoom = 1.0;
    public const double MaxZoom = 8.0;

    public static double Step(double zoom, int delta) =>
        Math.Clamp(zoom * (delta > 0 ? 1.15 : 1.0 / 1.15), MinZoom, MaxZoom);

    public static bool TryHandleWheel(
        MouseWheelEventArgs e,
        ScrollViewer scroller,
        double zoom,
        Action<double> setZoom)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            var next = Step(zoom, e.Delta);
            if (Math.Abs(next - zoom) > 0.0001)
            {
                ZoomTowardPointer(scroller, e.GetPosition(scroller), () => setZoom(next));
            }

            e.Handled = true;
            return true;
        }

        if (Keyboard.Modifiers == ModifierKeys.Shift)
        {
            scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset - e.Delta);
            e.Handled = true;
            return true;
        }

        if (scroller.ExtentHeight <= scroller.ViewportHeight + 1 &&
            scroller.ExtentWidth > scroller.ViewportWidth + 1)
        {
            scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset - e.Delta);
            e.Handled = true;
            return true;
        }

        return false;
    }

    public static void BeginPan(ScrollViewer scroller, Point mouse, out PanState state) =>
        state = new PanState(mouse, scroller.HorizontalOffset, scroller.VerticalOffset);

    public static void MovePan(ScrollViewer scroller, Point mouse, in PanState state)
    {
        scroller.ScrollToHorizontalOffset(state.HorizontalOffset - (mouse.X - state.Origin.X));
        scroller.ScrollToVerticalOffset(state.VerticalOffset - (mouse.Y - state.Origin.Y));
    }

    public static double FitScale(double viewportWidth, double viewportHeight, double contentWidth, double contentHeight)
    {
        if (contentWidth <= 0 || contentHeight <= 0)
        {
            return 1;
        }

        var scale = Math.Min(
            Math.Max(viewportWidth, 1) / contentWidth,
            Math.Max(viewportHeight, 1) / contentHeight);
        return double.IsFinite(scale) && scale > 0 ? scale : 1;
    }

    private static void ZoomTowardPointer(ScrollViewer scroller, Point pointer, Action applyZoom)
    {
        var extentW = Math.Max(scroller.ExtentWidth, 1);
        var extentH = Math.Max(scroller.ExtentHeight, 1);
        var fracX = (scroller.HorizontalOffset + pointer.X) / extentW;
        var fracY = (scroller.VerticalOffset + pointer.Y) / extentH;
        applyZoom();
        scroller.UpdateLayout();
        scroller.ScrollToHorizontalOffset((fracX * scroller.ExtentWidth) - pointer.X);
        scroller.ScrollToVerticalOffset((fracY * scroller.ExtentHeight) - pointer.Y);
    }

    internal readonly record struct PanState(Point Origin, double HorizontalOffset, double VerticalOffset);
}
