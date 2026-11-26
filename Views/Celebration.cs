using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SafariInc.Views;

/// <summary>
/// Belohnungseffekte: Bildchen, die auf einer Ebene über dem Spiel fliegen und sich danach selbst entfernen.
/// </summary>
public sealed class Celebration
{
    private const string ConfettiGlyphs = "★●◆";

    private static readonly Brush[] ConfettiColors =
    {
        Frozen(0xE5, 0x39, 0x35), Frozen(0xFB, 0x8C, 0x00), Frozen(0xFF, 0xD6, 0x00),
        Frozen(0x43, 0xA0, 0x47), Frozen(0x1E, 0x88, 0xE5), Frozen(0x8E, 0x24, 0xAA),
    };

    private readonly Canvas _layer;
    private readonly FontFamily _font;
    private readonly Random _rng = new();

    public Celebration(Canvas layer)
    {
        _layer = layer;
        _font = (FontFamily)layer.FindResource("EmojiFont");
    }

    /// <summary>Bildchen springen von einem Punkt in alle Richtungen und fallen herunter.</summary>
    public void Burst(Point origin, int count, string glyph, Brush color)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = _rng.NextDouble() * 2 * Math.PI;
            double distance = 80 + _rng.NextDouble() * 220;
            var target = new Point(origin.X + Math.Cos(angle) * distance,
                                   origin.Y + Math.Sin(angle) * distance + 120); // Schwerkraft

            Spawn(glyph, color, Between(20, 42), origin, target, lift: Between(40, 120),
                  duration: Between(800, 1300), delay: 0, spin: Between(-360, 360), fade: true);
        }
    }

    /// <summary>Bunte Formen springen von einem Punkt in alle Richtungen.</summary>
    public void Confetti(Point origin, int count)
    {
        for (int i = 0; i < count; i++)
            Burst(origin, 1, RandomGlyph(), RandomColor());
    }

    /// <summary>Bunte Formen regnen über das ganze Fenster.</summary>
    public void Rain(int count)
    {
        for (int i = 0; i < count; i++)
        {
            double x = _rng.NextDouble() * _layer.ActualWidth;
            Spawn(RandomGlyph(), RandomColor(), Between(20, 42), new Point(x, -50),
                  new Point(x + Between(-80, 80), _layer.ActualHeight + 50), lift: 0,
                  duration: Between(1400, 2400), delay: Between(0, 900), spin: Between(-540, 540), fade: false);
        }
    }

    /// <summary>Bildchen fliegen nacheinander zu einem Ziel, z. B. Münzen zur Münzanzeige.</summary>
    public void FlyTo(Point from, Point to, int count, string glyph, Brush color, Action? onArrived = null)
    {
        const double stepDelay = 45;
        for (int i = 0; i < count; i++)
        {
            Spawn(glyph, color, 30, from, to, lift: Between(30, 90), duration: 650, delay: i * stepDelay,
                  spin: 0, fade: false, onDone: i == count - 1 ? onArrived : null);
        }
    }

    private void Spawn(string glyph, Brush color, double size, Point from, Point to, double lift,
        double duration, double delay, double spin, bool fade, Action? onDone = null)
    {
        var length = TimeSpan.FromMilliseconds(duration);
        var begin = TimeSpan.FromMilliseconds(delay);

        var rotate = new RotateTransform();
        var move = new TranslateTransform(from.X, from.Y);
        var transform = new TransformGroup();
        transform.Children.Add(rotate);
        transform.Children.Add(move);

        var particle = new TextBlock
        {
            Text = glyph,
            FontFamily = _font,
            FontSize = size,
            Foreground = color,
            Opacity = 0,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = transform,
        };
        _layer.Children.Add(particle);

        move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(to.X, length) { BeginTime = begin });
        move.BeginAnimation(TranslateTransform.YProperty, VerticalPath(from, to, lift, length, begin));
        rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(spin, length) { BeginTime = begin });

        var opacity = new DoubleAnimationUsingKeyFrames { BeginTime = begin, Duration = length };
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.05)));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.7)));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(fade ? 0 : 1, KeyTime.FromPercent(1)));
        opacity.Completed += (_, _) =>
        {
            _layer.Children.Remove(particle);
            onDone?.Invoke();
        };
        particle.BeginAnimation(UIElement.OpacityProperty, opacity);
    }

    /// <summary>Mit lift ein Bogen (erst hoch, dann runter), sonst ein gerader Fall.</summary>
    private static AnimationTimeline VerticalPath(Point from, Point to, double lift, TimeSpan length, TimeSpan begin)
    {
        if (lift <= 0)
        {
            return new DoubleAnimation(to.Y, length)
            {
                BeginTime = begin,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
            };
        }

        var arc = new DoubleAnimationUsingKeyFrames { BeginTime = begin, Duration = length };
        arc.KeyFrames.Add(new EasingDoubleKeyFrame(Math.Min(from.Y, to.Y) - lift, KeyTime.FromPercent(0.4),
            new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        arc.KeyFrames.Add(new EasingDoubleKeyFrame(to.Y, KeyTime.FromPercent(1),
            new QuadraticEase { EasingMode = EasingMode.EaseIn }));
        return arc;
    }

    private double Between(double min, double max) => min + _rng.NextDouble() * (max - min);

    private string RandomGlyph() => ConfettiGlyphs[_rng.Next(ConfettiGlyphs.Length)].ToString();

    private Brush RandomColor() => ConfettiColors[_rng.Next(ConfettiColors.Length)];

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
