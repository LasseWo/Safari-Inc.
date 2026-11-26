using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using SafariInc.Audio;
using SafariInc.Events;
using SafariInc.Model;

namespace SafariInc.Views;

public record PayoutRow(string Face, Brush Color, double BarWidth, int Payout);

/// <summary>
/// Das Glücksrad als Popup über der aktuellen Seite. Das Fenster öffnet es, wenn ein Tier auf der Karte
/// aufs Rad gezogen oder ein Freispiel eingelöst wird. Das Rad dreht sofort los.
/// </summary>
public partial class WheelPopup : UserControl
{
    private const double Radius = 150;
    private const double Center = 160;
    private const double MaxBarWidth = 180;
    private const double LabelWidth = 72;
    private const double LabelInset = 22;
    private static readonly TimeSpan MinTickGap = TimeSpan.FromMilliseconds(70);
    private static readonly Brush GiftBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA0, 0x00));

    private readonly Random _rng = new();
    private bool _spinning;

    public WheelPopup()
    {
        InitializeComponent();
        DrawWheel();
    }

    // ---------- Öffnen und Schließen ----------

    /// <summary>Dreht das Rad für ein Tier. Es wird verkauft, sobald das Rad steht.</summary>
    public void SpinAnimal(ZooAnimal animal)
    {
        // Der GameManager legt das Ergebnis fest und schreibt es in die EventArgs zurück.
        var request = new RouletteEventArgs(animal);
        RaiseEvent(request);
        if (request.SliceIndex < 0) return;

        var species = animal.Species;
        Open(species.Emoji, species.Color, species.RarityInfo.Stars, species.RarityInfo.Color, species.BaseValue);
        Spin(request.SliceIndex, () => FinishRoulette(animal, request));
    }

    /// <summary>Dreht das Rad, ohne dass ein Tier abgegeben wird.</summary>
    public void SpinFree()
    {
        var request = new FreeSpinEventArgs();
        RaiseEvent(request);
        if (request.SliceIndex < 0) return;

        Open("🎁", GiftBrush, "", GiftBrush, Catalog.FreeSpinBaseValue);
        Spin(request.SliceIndex, () =>
        {
            RaiseEvent(new RoutedEventArgs(GameEvents.FreeSpinCompletedEvent));
            ShowWheelResult(Catalog.WheelSlices[request.SliceIndex], request.Payout);
        });
    }

    public void Close()
    {
        if (_spinning) return;
        Attention.SetPulse(DoneButton, false);
        Visibility = Visibility.Collapsed;
    }

    private void Open(string icon, Brush iconColor, string stars, Brush starsColor, int baseValue)
    {
        SubjectIcon.Text = icon;
        SubjectIcon.Foreground = iconColor;
        SubjectStars.Text = stars;
        SubjectStars.Foreground = starsColor;
        ShowPayouts(baseValue);

        WheelResult.Text = "";
        Attention.SetPulse(DoneButton, false);
        DoneButton.Visibility = Visibility.Hidden;

        bool opening = Visibility != Visibility.Visible;
        Visibility = Visibility.Visible;
        if (opening) Attention.Pop(Dialog);
    }

    private void ShowPayouts(int baseValue)
    {
        double maxMultiplier = Catalog.WheelOutcomes.Max(o => o.Multiplier);
        PayoutTable.ItemsSource = Catalog.WheelOutcomes.Select(o => new PayoutRow(
            o.Face, o.Color, MaxBarWidth * o.Multiplier / maxMultiplier, Catalog.Payout(baseValue, o))).ToList();
    }

    private void Done_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Nur der Hintergrund selbst schließt, nicht ein Klick auf das Rad oder die Balken.</summary>
    private void Backdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, sender)) Close();
    }

    /// <summary>
    /// Tunneling: PreviewMouseLeftButtonDown läuft vom Fenster nach unten zu dem Element unter der Maus.
    /// Da das Popup auf dem Weg liegt, kann es jeden Klick abfangen, solange sich das Rad dreht.
    /// </summary>
    private void Popup_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_spinning) e.Handled = true;
    }

    // ---------- Ergebnis ----------

    private void FinishRoulette(ZooAnimal animal, RouletteEventArgs request)
    {
        var outcome = Catalog.WheelSlices[request.SliceIndex];
        RaiseEvent(new AnimalSoldEventArgs(animal, SaleMode.Roulette));
        ShowWheelResult(outcome, request.Payout);

        // Nur beim Verkauf kann man Pech haben. Ein Freispiel ist immer ein Gewinn.
        if (outcome == Catalog.Pech) SoundService.Fail();
    }

    private void ShowWheelResult(WheelOutcome outcome, int payout)
    {
        WheelResult.Text = $"{outcome.Face} 🪙 {payout}";
        WheelResult.Foreground = outcome == Catalog.Jackpot ? Brushes.DarkGoldenrod : outcome.Color;
        Attention.Pop(WheelResult);

        DoneButton.Visibility = Visibility.Visible;
        Attention.SetPulse(DoneButton, true);

        if (outcome == Catalog.Jackpot)
        {
            SoundService.Jackpot();
            WheelResult.RaiseEvent(new CelebrationEventArgs(CelebrationKind.Jackpot));
        }
        else if (outcome == Catalog.Glueck)
        {
            WheelResult.RaiseEvent(new CelebrationEventArgs(CelebrationKind.Confetti));
        }
    }

    // ---------- Das Rad ----------

    private void DrawWheel()
    {
        var slices = Catalog.WheelSlices;
        double step = 360.0 / slices.Length;

        for (int i = 0; i < slices.Length; i++)
        {
            double a0 = i * step, a1 = (i + 1) * step;
            var figure = new PathFigure { StartPoint = new Point(Center, Center), IsClosed = true };
            figure.Segments.Add(new LineSegment(PointAt(a0, Radius), true));
            figure.Segments.Add(new ArcSegment(PointAt(a1, Radius), new Size(Radius, Radius), 0, false,
                SweepDirection.Clockwise, true));

            Wheel.Children.Add(new Path
            {
                Data = new PathGeometry(new[] { figure }),
                Fill = slices[i].Color,
                Stroke = Brushes.White,
                StrokeThickness = 2,
            });

            // Gesicht am Rand, radial ausgerichtet
            var label = new TextBlock
            {
                Text = slices[i].Face,
                FontFamily = (FontFamily)FindResource("EmojiFont"),
                FontSize = 48,
                Foreground = slices[i] == Catalog.Jackpot ? Brushes.SaddleBrown : Brushes.White,
                Width = LabelWidth,
                TextAlignment = TextAlignment.Center,
                RenderTransform = new RotateTransform(a0 + step / 2, LabelWidth / 2, Radius - LabelInset),
            };
            Canvas.SetLeft(label, Center - LabelWidth / 2);
            Canvas.SetTop(label, Center - Radius + LabelInset);
            Wheel.Children.Add(label);
        }

        Wheel.Children.Add(new Ellipse
        {
            Width = 2 * Radius, Height = 2 * Radius,
            Stroke = new SolidColorBrush(Color.FromRgb(0x6D, 0x4C, 0x41)), StrokeThickness = 6,
            Margin = new Thickness(Center - Radius, Center - Radius, 0, 0),
        });
    }

    /// <summary>Punkt auf dem Kreis; Winkel ab 12 Uhr im Uhrzeigersinn.</summary>
    private static Point PointAt(double angleDeg, double r)
    {
        double rad = angleDeg * Math.PI / 180;
        return new Point(Center + r * Math.Sin(rad), Center - r * Math.Cos(rad));
    }

    private void Spin(int sliceIndex, Action onFinished)
    {
        _spinning = true;

        double step = 360.0 / Catalog.WheelSlices.Length;
        double sliceMiddle = sliceIndex * step + step / 2 + (_rng.NextDouble() - 0.5) * step * 0.6;

        // Das Rad so weit drehen, dass die Feldmitte unter dem Zeiger (12 Uhr) landet, plus 6 volle Runden.
        double current = WheelRotation.Angle;
        double target = current - (current + sliceMiddle) % 360 + 6 * 360;

        var spin = new DoubleAnimation(current, target, TimeSpan.FromSeconds(4.5))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        var ticker = StartTicking(step);
        spin.Completed += (_, _) =>
        {
            ticker.Stop();
            _spinning = false;
            onFinished();
        };
        WheelRotation.BeginAnimation(RotateTransform.AngleProperty, spin);
    }

    /// <summary>Klickt bei jedem Feld wie ein echtes Glücksrad und wird mit dem Rad langsamer.</summary>
    private DispatcherTimer StartTicking(double sliceAngle)
    {
        int lastSlice = (int)(WheelRotation.Angle / sliceAngle);
        var lastTick = DateTime.MinValue;

        var ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        ticker.Tick += (_, _) =>
        {
            int slice = (int)(WheelRotation.Angle / sliceAngle);
            if (slice == lastSlice || DateTime.Now - lastTick < MinTickGap) return;

            lastSlice = slice;
            lastTick = DateTime.Now;
            SoundService.Tick();
        };
        ticker.Start();
        return ticker;
    }
}
