using SkiaSharp.Views.Maui;
namespace RepoReport.Reports;

/// <summary>
/// Weekly commits as rounded bars, drawn with raw Skia. Vectors in the PDF, no bitmap.
/// </summary>
public class BarChart : SkiaControl
{
    public static readonly BindableProperty ValuesProperty = BindableProperty.Create(
        nameof(Values), typeof(IList<int>), typeof(BarChart), null,
        propertyChanged: (b, o, n) => ((BarChart)b).Update());

    public IList<int> Values
    {
        get => (IList<int>)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public static readonly BindableProperty BarColorProperty = BindableProperty.Create(
        nameof(BarColor), typeof(Color), typeof(BarChart), Color.Parse("#4C8DFF"),
        propertyChanged: (b, o, n) => ((BarChart)b).Update());

    public Color BarColor
    {
        get => (Color)GetValue(BarColorProperty);
        set => SetValue(BarColorProperty, value);
    }

    protected override void Paint(DrawingContext ctx)
    {
        base.Paint(ctx);

        var values = Values;
        var canvas = ctx.Context.Canvas;
        var rect = ctx.Destination;
        var scale = ctx.Scale;

        using var gridPaint = new SKPaint { Color = SKColor.Parse("#E3E7EE"), StrokeWidth = 1 * scale, IsAntialias = true };
        var bottom = rect.Bottom - 1 * scale;
        for (var i = 0; i <= 3; i++)
        {
            var y = rect.Top + (rect.Height - 2 * scale) * i / 3f;
            canvas.DrawLine(rect.Left, y, rect.Right, y, gridPaint);
        }
        canvas.DrawLine(rect.Left, bottom, rect.Right, bottom, gridPaint);

        if (values == null || values.Count == 0)
            return;

        var max = Math.Max(1, values.Max());
        var slot = rect.Width / values.Count;
        var gap = Math.Max(1 * scale, slot * 0.25f);
        var barWidth = slot - gap;
        var radius = Math.Min(barWidth / 2f, 3 * scale);

        using var barPaint = new SKPaint { Color = BarColor.ToSKColor(), IsAntialias = true };
        using var emptyPaint = new SKPaint { Color = SKColor.Parse("#D6DBE4"), IsAntialias = true };

        for (var i = 0; i < values.Count; i++)
        {
            var x = rect.Left + i * slot + gap / 2f;
            var h = (rect.Height - 2 * scale) * values[i] / (float)max;
            if (values[i] == 0)
            {
                canvas.DrawRoundRect(x, bottom - 2 * scale, barWidth, 2 * scale, 1 * scale, 1 * scale, emptyPaint);
                continue;
            }
            canvas.DrawRoundRect(x, bottom - h, barWidth, h, radius, radius, barPaint);
        }
    }
}
