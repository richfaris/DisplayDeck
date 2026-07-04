using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DisplayDeck.App.ViewModels;

namespace DisplayDeck.App.Views;

public partial class DisplaysView : UserControl
{
    private DisplayItemViewModel? _dragItem;
    private System.Windows.Point _dragStart;
    private double _origLeft;
    private double _origTop;
    private bool _dragMoved;

    public DisplaysView()
    {
        InitializeComponent();
    }

    private void OnTileMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not DisplayItemViewModel item)
            return;
        if (DataContext is not MainViewModel vm || vm.Displays.Count < 2)
            return; // nothing to rearrange with a single display

        _dragItem = item;
        _dragStart = e.GetPosition(MapHost);
        _origLeft = item.MapLeft;
        _origTop = item.MapTop;
        _dragMoved = false;
        fe.CaptureMouse();
        e.Handled = true;
    }

    private void OnTileMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragItem is null || e.LeftButton != MouseButtonState.Pressed)
            return;
        if (DataContext is not MainViewModel vm)
            return;

        var p = e.GetPosition(MapHost);
        double dx = p.X - _dragStart.X;
        double dy = p.Y - _dragStart.Y;

        if (Math.Abs(dx) > 3 || Math.Abs(dy) > 3)
            _dragMoved = true;

        double nl = _origLeft + dx;
        double nt = _origTop + dy;

        // Keep the tile inside the map.
        nl = Math.Max(0, Math.Min(nl, vm.MapCanvasWidth - _dragItem.MapWidth));
        nt = Math.Max(0, Math.Min(nt, vm.MapCanvasHeight - _dragItem.MapHeight));

        // Snap edges to neighbouring tiles for a clean, Windows-like feel.
        const double snap = 10;
        foreach (var other in vm.Displays)
        {
            if (ReferenceEquals(other, _dragItem))
                continue;

            if (Math.Abs(nl - (other.MapLeft + other.MapWidth)) < snap)
                nl = other.MapLeft + other.MapWidth;
            if (Math.Abs(nl + _dragItem.MapWidth - other.MapLeft) < snap)
                nl = other.MapLeft - _dragItem.MapWidth;
            if (Math.Abs(nt - (other.MapTop + other.MapHeight)) < snap)
                nt = other.MapTop + other.MapHeight;
            if (Math.Abs(nt + _dragItem.MapHeight - other.MapTop) < snap)
                nt = other.MapTop - _dragItem.MapHeight;
            if (Math.Abs(nt - other.MapTop) < snap)
                nt = other.MapTop;
            if (Math.Abs(nl - other.MapLeft) < snap)
                nl = other.MapLeft;
        }

        _dragItem.MapLeft = nl;
        _dragItem.MapTop = nt;
        e.Handled = true;
    }

    private void OnTileMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe)
            fe.ReleaseMouseCapture();

        if (_dragItem is null)
            return;

        var item = _dragItem;
        _dragItem = null;

        if (DataContext is MainViewModel vm)
        {
            if (_dragMoved)
                vm.CommitDragArrange(item);
            else
                vm.SnapMapBack();
        }

        e.Handled = true;
    }
}
