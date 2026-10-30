using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using SafariInc.Audio;
using SafariInc.Events;
using SafariInc.Model;

namespace SafariInc.Views;

public record PayoutRow(string Name, Brush Color, string Chance, int Payout);

public partial class MerchantView : UserControl
{
    private const double Radius = 150;
    private const double Center = 160;

    private readonly Random _rng = new();
    private bool _spinning;

    public MerchantView()
    {
        InitializeComponent();
        DrawWheel();
    }

    // ---------- Tunneling: Eingaben während der Drehung blockieren ----------

    /// <summary>
    /// PreviewMouseLeftButtonDown läuft vom Fenster nach unten zu dem Element unter der Maus.
    /// Da diese View auf dem Weg liegt, kann sie jeden Klick in sich abfangen, bevor
    /// ein Button oder die ListBox ihn bekommt.
    /// </summary>
    private void View_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_spinning) e.Handled = true;
    }

    // ---------- Auswahl ----------

    private void View_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && AnimalList.SelectedItem == null) SelectFirst();
    }

    private void SelectFirst()
    {
        if (AnimalList.Items.Count > 0) AnimalList.SelectedIndex = 0;
        UpdateOffer();
    }

    private void AnimalList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateOffer();

    private void UpdateOffer()
    {
        NoAnimalsText.Visibility = AnimalList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (AnimalList.SelectedItem is not ZooAnimal animal)
        {
            Offer.Visibility = Visibility.Collapsed;
            PayoutPanel.Visibility = Visibility.Collapsed;
            NoSelection.Visibility = Visibility.Visible;
            return;
        }

        var s = animal.Species;
        Offer.Visibility = Visibility.Visible;
        PayoutPanel.Visibility = Visibility.Visible;
        NoSelection.Visibility = Visibility.Collapsed;

        OfferEmoji.Text = s.Emoji;
        OfferEmoji.Foreground = s.RarityInfo.Color;
        OfferName.Text = s.Name;
        OfferRarity.Text = s.RarityInfo.Name;
        OfferRarity.Foreground = s.RarityInfo.Color;

        SafeText.Text = $"Garantiert {s.BaseValue} Münzen";
        WheelText.Text = $"Zwischen {Catalog.Payout(s, Catalog.Pech)} und {Catalog.Payout(s, Catalog.Jackpot)} Münzen";
        ShelterText.Text = "Du bekommst: " + string.Join(", ",
            s.RarityInfo.ShelterReward.Select(r => $"{r.Count}× {Catalog.Bait(r.Bait).Name}"));

        int slices = Catalog.WheelSlices.Length;
        PayoutTable.ItemsSource = Catalog.WheelOutcomes.Select(o => new PayoutRow(
            o.Name, o.Color,
            $"{Catalog.WheelSlices.Count(x => x == o) * 100 / slices} %",
            Catalog.Payout(s, o))).ToList();
    }

    // ---------- Verkaufen ----------

    /// <summary>Bubbling: ein Handler für die drei Buttons sicher / Glücksrad / Tierheim.</summary>
    private void Actions_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Button { Tag: string action } button) return;
        e.Handled = true;

        if (AnimalList.SelectedItem is not ZooAnimal animal)
        {
            button.RaiseEvent(new GameMessageEventArgs("Wähle zuerst links ein Tier aus.", true));
            return;
        }

        switch (action)
        {
            case "safe":
                button.RaiseEvent(new AnimalSoldEventArgs(animal, SaleMode.Safe));
                SelectFirst();
                break;

            case "shelter":
                button.RaiseEvent(new AnimalEventArgs(GameEvents.AnimalDonatedEvent, animal.Species, animal));
                SelectFirst();
                break;

            case "wheel":
                // Der GameManager legt das Ergebnis fest und schreibt es in die EventArgs zurück.
                var request = new RouletteEventArgs(animal);
                button.RaiseEvent(request);
                if (request.SliceIndex < 0) return;
                Spin(request.SliceIndex, () => FinishRoulette(button, animal, request));
                break;
        }
    }

    private void FinishRoulette(UIElement source, ZooAnimal animal, RouletteEventArgs request)
    {
        var outcome = Catalog.WheelSlices[request.SliceIndex];
        WheelResult.Text = $"{outcome.Name}! {request.Payout} Münzen";
        WheelResult.Foreground = outcome == Catalog.Jackpot ? Brushes.DarkGoldenrod : outcome.Color;

        source.RaiseEvent(new AnimalSoldEventArgs(animal, SaleMode.Roulette));

        if (outcome == Catalog.Jackpot) SoundService.Jackpot();
        else if (outcome == Catalog.Pech) SoundService.Fail();

        SelectFirst();
    }

    // ---------- Glücksrad ----------

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

            // Beschriftung am Rand, radial ausgerichtet
            var label = new TextBlock
            {
                Text = slices[i] == Catalog.Jackpot ? "★" : $"×{slices[i].Multiplier:0.##}",
                FontSize = slices[i] == Catalog.Jackpot ? 22 : 12,
                FontWeight = FontWeights.Bold,
                Foreground = slices[i] == Catalog.Jackpot ? Brushes.SaddleBrown : Brushes.White,
                Width = 44,
                TextAlignment = TextAlignment.Center,
                RenderTransform = new RotateTransform(a0 + step / 2, 22, Radius - 10),
            };
            Canvas.SetLeft(label, Center - 22);
            Canvas.SetTop(label, Center - Radius + 10);
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
        WheelResult.Text = "Das Rad dreht sich …";
        WheelResult.Foreground = Brushes.Gray;

        double step = 360.0 / Catalog.WheelSlices.Length;
        double sliceMiddle = sliceIndex * step + step / 2 + (_rng.NextDouble() - 0.5) * step * 0.6;

        // Das Rad so weit drehen, dass die Feldmitte unter dem Zeiger (12 Uhr) landet, plus 6 volle Runden.
        double current = WheelRotation.Angle;
        double target = current - (current + sliceMiddle) % 360 + 6 * 360;

        var spin = new DoubleAnimation(current, target, TimeSpan.FromSeconds(4.5))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        spin.Completed += (_, _) =>
        {
            _spinning = false;
            onFinished();
        };
        WheelRotation.BeginAnimation(RotateTransform.AngleProperty, spin);
    }
}
