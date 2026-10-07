namespace RepoReport.Reports;

/// <summary>
/// A rounded gradient card with a big number and a caption, used for the stats row.
/// </summary>
public class StatTile : SkiaShape
{
    SkiaLabel _value;
    SkiaLabel _caption;

    public StatTile()
    {
        Type = ShapeType.Rectangle;
        CornerRadius = 14;
        Padding = new Thickness(18, 14);
        HorizontalOptions = LayoutOptions.Fill;
        UseCache = SkiaCacheType.None;

        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 2,
                Children = new List<SkiaControl>
                {
                    new SkiaLabel("0")
                    {
                        FontFamily = "FontTextTitle",
                        FontSize = 34,
                        TextColor = Colors.White,
                    }.Assign(out _value),
                    new SkiaLabel("")
                    {
                        FontFamily = "FontText",
                        FontSize = 13,
                        TextColor = Color.Parse("#E6FFFFFF"),
                    }.Assign(out _caption),
                }
            }
        };

        ApplyAccent(Accent);
    }

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(string), typeof(StatTile), "0",
        propertyChanged: (b, o, n) => ((StatTile)b)._value.Text = (string)n ?? "");

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly BindableProperty CaptionProperty = BindableProperty.Create(
        nameof(Caption), typeof(string), typeof(StatTile), "",
        propertyChanged: (b, o, n) => ((StatTile)b)._caption.Text = (string)n ?? "");

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public static readonly BindableProperty AccentProperty = BindableProperty.Create(
        nameof(Accent), typeof(Color), typeof(StatTile), Color.Parse("#4C8DFF"),
        propertyChanged: (b, o, n) => ((StatTile)b).ApplyAccent((Color)n));

    /// <summary>
    /// Base color of the gradient, the bottom edge is a darker shade of it.
    /// </summary>
    public Color Accent
    {
        get => (Color)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    void ApplyAccent(Color accent)
    {
        FillGradient = new SkiaGradient
        {
            Type = GradientType.Linear,
            StartXRatio = 0, StartYRatio = 0,
            EndXRatio = 1, EndYRatio = 1,
            Colors = new List<Color> { accent, accent.AddLuminosity(-0.18f) },
        };
    }
}
