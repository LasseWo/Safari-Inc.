using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SafariInc.Views;

/// <summary>
/// Zeigt Kindern ohne Worte, wo es weitergeht: Das Element, das als Nächstes
/// dran ist, pulsiert. In XAML: views:Attention.Pulse="{Binding ...}".
/// </summary>
public static class Attention
{
    public static readonly DependencyProperty PulseProperty = DependencyProperty.RegisterAttached(
        "Pulse", typeof(bool), typeof(Attention), new PropertyMetadata(false, OnPulseChanged));

    public static bool GetPulse(DependencyObject d) => (bool)d.GetValue(PulseProperty);

    public static void SetPulse(DependencyObject d, bool value) => d.SetValue(PulseProperty, value);

    /// <summary>Lässt ein Tier langsam hin und her laufen. In XAML: views:Attention.Wander="True".</summary>
    public static readonly DependencyProperty WanderProperty = DependencyProperty.RegisterAttached(
        "Wander", typeof(bool), typeof(Attention), new PropertyMetadata(false, OnWanderChanged));

    public static bool GetWander(DependencyObject d) => (bool)d.GetValue(WanderProperty);

    public static void SetWander(DependencyObject d, bool value) => d.SetValue(WanderProperty, value);

    private static void OnWanderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element || e.NewValue is not true) return;

        // Zufällige Dauer, damit nicht alle Tiere im Gleichschritt laufen.
        var shift = new TranslateTransform();
        element.RenderTransform = shift;
        shift.BeginAnimation(TranslateTransform.XProperty, Drift(14, 2 + Random.Shared.NextDouble() * 2));
        shift.BeginAnimation(TranslateTransform.YProperty, Drift(4, 0.7 + Random.Shared.NextDouble() * 0.6));
    }

    private static DoubleAnimation Drift(double range, double seconds) => new(-range, range, TimeSpan.FromSeconds(seconds))
    {
        AutoReverse = true,
        RepeatBehavior = RepeatBehavior.Forever,
        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
    };

    private static void OnPulseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;

        if (e.NewValue is true)
        {
            Scale(element, 1.07, TimeSpan.FromMilliseconds(550), RepeatBehavior.Forever);
        }
        else
        {
            Stop(element);
            element.RenderTransform = Transform.Identity;
        }
    }

    /// <summary>Kurzes Hüpfen als Antwort auf einen Klick.</summary>
    public static void Bounce(UIElement element)
        => Scale(element, 1.1, TimeSpan.FromMilliseconds(130), new RepeatBehavior(2));

    /// <summary>Taucht federnd auf: von klein auf normale Größe, mit kurzem Überschwingen.</summary>
    public static void Pop(UIElement element)
    {
        Stop(element);

        var scale = new ScaleTransform();
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = scale;

        var animation = new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(450))
        {
            EasingFunction = new BackEase { Amplitude = 0.7, EasingMode = EasingMode.EaseOut },
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    /// <summary>Eine endlose Animation liefe sonst auch nach dem Austauschen der Transform weiter.</summary>
    private static void Stop(UIElement element)
    {
        if (element.RenderTransform is not ScaleTransform running) return;
        running.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        running.BeginAnimation(ScaleTransform.ScaleYProperty, null);
    }

    private static void Scale(UIElement element, double to, TimeSpan duration, RepeatBehavior repeat)
    {
        Stop(element);

        // Neue Transform statt der aus dem Template – Template-Objekte sind eingefroren.
        var scale = new ScaleTransform();
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = scale;

        var animation = new DoubleAnimation(1, to, duration) { AutoReverse = true, RepeatBehavior = repeat };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }
}
