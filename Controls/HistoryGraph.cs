using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SysPulse.Controls;

public class HistoryGraph : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<HistoryGraph, IReadOnlyList<double>?>(nameof(Values));

    public static readonly StyledProperty<IBrush?> StrokeBrushProperty =
        AvaloniaProperty.Register<HistoryGraph, IBrush?>(nameof(StrokeBrush), new SolidColorBrush(Color.FromRgb(0, 210, 255)));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<HistoryGraph, double>(nameof(StrokeThickness), 2.0);

    public static readonly StyledProperty<double> MaxValueProperty =
        AvaloniaProperty.Register<HistoryGraph, double>(nameof(MaxValue), 100.0);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<HistoryGraph, bool>(nameof(ShowGrid), true);

    public static readonly StyledProperty<bool> ShowFillProperty =
        AvaloniaProperty.Register<HistoryGraph, bool>(nameof(ShowFill), true);

    static HistoryGraph()
    {
        AffectsRender<HistoryGraph>(
            ValuesProperty,
            StrokeBrushProperty,
            StrokeThicknessProperty,
            MaxValueProperty,
            ShowGridProperty,
            ShowFillProperty);
    }

    public IReadOnlyList<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IBrush? StrokeBrush
    {
        get => GetValue(StrokeBrushProperty);
        set => SetValue(StrokeBrushProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public double MaxValue
    {
        get => GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public bool ShowFill
    {
        get => GetValue(ShowFillProperty);
        set => SetValue(ShowFillProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 1 || bounds.Height <= 1)
            return;

        // Draw background grid lines
        if (ShowGrid)
        {
            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), 1.0);
            for (int i = 1; i <= 3; i++)
            {
                var y = bounds.Height * (i / 4.0);
                context.DrawLine(gridPen, new Point(0, y), new Point(bounds.Width, y));
            }
        }

        var values = Values;
        if (values == null || values.Count < 2)
            return;

        var count = values.Count;
        var stepX = bounds.Width / (count - 1);
        var max = MaxValue <= 0 ? 100.0 : MaxValue;

        var points = new Point[count];
        for (int i = 0; i < count; i++)
        {
            var val = Math.Clamp(values[i], 0.0, max);
            var normalizedY = 1.0 - (val / max);
            points[i] = new Point(i * stepX, normalizedY * (bounds.Height - 4) + 2);
        }

        // Draw area fill
        if (ShowFill && StrokeBrush is ISolidColorBrush solid)
        {
            var fillColor = Color.FromArgb(40, solid.Color.R, solid.Color.G, solid.Color.B);
            var transparent = Color.FromArgb(0, solid.Color.R, solid.Color.G, solid.Color.B);
            var fillBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(fillColor, 0),
                    new GradientStop(transparent, 1)
                }
            };

            var fillGeometry = new StreamGeometry();
            using (var ctx = fillGeometry.Open())
            {
                ctx.BeginFigure(new Point(0, bounds.Height), isFilled: true);
                ctx.LineTo(points[0]);
                for (int i = 1; i < count; i++)
                {
                    ctx.LineTo(points[i]);
                }
                ctx.LineTo(new Point(bounds.Width, bounds.Height));
                ctx.EndFigure(isClosed: true);
            }

            context.DrawGeometry(fillBrush, null, fillGeometry);
        }

        // Draw stroke line
        var strokePen = new Pen(StrokeBrush ?? Brushes.Cyan, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var lineGeometry = new StreamGeometry();
        using (var ctx = lineGeometry.Open())
        {
            ctx.BeginFigure(points[0], isFilled: false);
            for (int i = 1; i < count; i++)
            {
                ctx.LineTo(points[i]);
            }
            ctx.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, strokePen, lineGeometry);

        // Draw current value indicator circle on the last point
        var lastPoint = points[count - 1];
        var dotBrush = StrokeBrush ?? Brushes.Cyan;
        context.DrawEllipse(dotBrush, null, lastPoint, 3.5, 3.5);
    }
}
