using SkiaSharp.Views.Maui;
using RepoReport.Models;

namespace RepoReport.Reports;

/// <summary>
/// One rounded bar split into colored segments, one per language, like the GitHub repo page.
/// </summary>
public class LanguageBar : SkiaControl
{
    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items), typeof(IList<LanguageShare>), typeof(LanguageBar), null,
        propertyChanged: (b, o, n) => ((LanguageBar)b).Update());

    public IList<LanguageShare> Items
    {
        get => (IList<LanguageShare>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    protected override void Paint(DrawingContext ctx)
    {
        base.Paint(ctx);

        var canvas = ctx.Context.Canvas;
        var rect = ctx.Destination;
        var radius = rect.Height / 2f;

        using var paint = new SKPaint { IsAntialias = true };

        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(rect, radius, radius), antialias: true);

        var items = Items;
        if (items == null || items.Count == 0)
        {
            paint.Color = SKColor.Parse("#E3E7EE");
            canvas.DrawRect(rect, paint);
        }
        else
        {
            var total = Math.Max(1, items.Sum(i => i.Bytes));
            var x = rect.Left;
            foreach (var item in items)
            {
                var w = rect.Width * item.Bytes / (float)total;
                paint.Color = item.Color.ToSKColor();
                canvas.DrawRect(x, rect.Top, w, rect.Height, paint);
                x += w;
            }
        }

        canvas.Restore();
    }
}
