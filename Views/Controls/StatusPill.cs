namespace MyActivity.Views.Controls;

/// <summary>Small rounded coloured label, e.g. "High" or "Present".</summary>
public class StatusPill : Border
{
    private readonly Label _label;

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(StatusPill), string.Empty,
        propertyChanged: (b, _, n) => ((StatusPill)b)._label.Text = n as string ?? string.Empty);

    public static readonly BindableProperty PillColorProperty = BindableProperty.Create(
        nameof(PillColor), typeof(Color), typeof(StatusPill), Colors.Gray,
        propertyChanged: (b, _, n) => ((StatusPill)b).BackgroundColor = n as Color ?? Colors.Gray);

    public StatusPill()
    {
        _label = new Label { TextColor = Colors.White, FontSize = 12, FontFamily = "OpenSansSemibold" };
        Content = _label;
        Padding = new Thickness(10, 4);
        StrokeThickness = 0;
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(12) };
        VerticalOptions = LayoutOptions.Center;
        HorizontalOptions = LayoutOptions.Start;
        BackgroundColor = Colors.Gray;
    }

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public Color PillColor { get => (Color)GetValue(PillColorProperty); set => SetValue(PillColorProperty, value); }
}
