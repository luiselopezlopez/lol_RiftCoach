using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using LoLCoach.Services;

namespace LoLCoach.Controls;

[TemplatePart(Name = "PART_TitleBar", Type = typeof(Thumb))]
[TemplatePart(Name = "PART_Close", Type = typeof(ButtonBase))]
public class FloatingPanel : ContentControl
{
    private const double VisibleMargin = 80;
    private const double TitleBarHeight = 36;

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(FloatingPanel), new PropertyMetadata(""));

    public static readonly DependencyProperty HeaderContentProperty =
        DependencyProperty.Register(nameof(HeaderContent), typeof(object), typeof(FloatingPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(FloatingPanel),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public object? HeaderContent { get => GetValue(HeaderContentProperty); set => SetValue(HeaderContentProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    public double Left => Canvas.GetLeft(this) is var left && double.IsFinite(left) ? left : 0;
    public double Top => Canvas.GetTop(this) is var top && double.IsFinite(top) ? top : 0;

    public event EventHandler? LayoutEdited;

    public FloatingPanel()
    {
        MinWidth = 220; MinHeight = 140;
        AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(OnDragDelta));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Close") is ButtonBase close) close.Click += (_, _) => IsOpen = false;
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs args)
    {
        base.OnPreviewMouseLeftButtonDown(args);
        BringToFront();
    }

    public void BringToFront()
    {
        if (Parent is not Panel host) return;
        var top = host.Children.OfType<UIElement>().Where(child => child != this).Select(Panel.GetZIndex).DefaultIfEmpty(0).Max();
        if (Panel.GetZIndex(this) <= top) Panel.SetZIndex(this, top + 1);
    }

    public PanelLayout GetLayout() => new(Left, Top, Size(Width, ActualWidth), Size(Height, ActualHeight), IsOpen, Panel.GetZIndex(this));

    public void ApplyLayout(PanelLayout layout)
    {
        Width = Math.Max(MinWidth, layout.Width);
        Height = Math.Max(MinHeight, layout.Height);
        Canvas.SetLeft(this, layout.Left);
        Canvas.SetTop(this, layout.Top);
        IsOpen = layout.IsOpen;
        KeepInside();
        Panel.SetZIndex(this, layout.ZIndex);
    }

    /// <summary>Keeps at least the title bar reachable inside the workspace.</summary>
    public void KeepInside()
    {
        if (Parent is not FrameworkElement host || host.ActualWidth <= 0 || host.ActualHeight <= 0) return;
        if (Size(Width, ActualWidth) > host.ActualWidth) Width = Math.Max(MinWidth, host.ActualWidth);
        if (Size(Height, ActualHeight) > host.ActualHeight) Height = Math.Max(MinHeight, host.ActualHeight);
        MoveTo(Left, Top);
    }

    private static double Size(double declared, double actual) => double.IsFinite(declared) ? declared : actual;

    private void MoveTo(double left, double top)
    {
        if (Parent is FrameworkElement host && host.ActualWidth > 0 && host.ActualHeight > 0)
        {
            left = Math.Clamp(left, Math.Min(0, VisibleMargin - Size(Width, ActualWidth)), Math.Max(0, host.ActualWidth - VisibleMargin));
            top = Math.Clamp(top, 0, Math.Max(0, host.ActualHeight - TitleBarHeight));
        }
        Canvas.SetLeft(this, left);
        Canvas.SetTop(this, top);
    }

    private void OnDragDelta(object sender, DragDeltaEventArgs args)
    {
        if (args.OriginalSource is not Thumb thumb) return;
        var edge = thumb.Name == "PART_TitleBar" ? "Move" : thumb.Tag as string;
        if (edge is null) return;
        args.Handled = true;
        LayoutEdited?.Invoke(this, EventArgs.Empty);
        if (edge == "Move") { MoveTo(Left + args.HorizontalChange, Top + args.VerticalChange); return; }
        var width = Size(Width, ActualWidth);
        var height = Size(Height, ActualHeight);
        var host = Parent as FrameworkElement;
        var maxWidth = host is { ActualWidth: > 0 } ? Math.Max(MinWidth, host.ActualWidth) : double.PositiveInfinity;
        var maxHeight = host is { ActualHeight: > 0 } ? Math.Max(MinHeight, host.ActualHeight - Top) : double.PositiveInfinity;
        if (edge is "Left" or "BottomLeft")
        {
            var newWidth = Math.Clamp(width - args.HorizontalChange, MinWidth, maxWidth);
            Canvas.SetLeft(this, Left + width - newWidth);
            Width = newWidth;
        }
        if (edge is "Right" or "BottomRight")
            Width = Math.Clamp(width + args.HorizontalChange, MinWidth, maxWidth);
        if (edge is "Bottom" or "BottomLeft" or "BottomRight")
            Height = Math.Clamp(height + args.VerticalChange, MinHeight, maxHeight);
    }

    private static void OnIsOpenChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var panel = (FloatingPanel)sender;
        var open = (bool)args.NewValue;
        panel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        if (!open) return;
        panel.BringToFront();
        panel.KeepInside();
    }
}
