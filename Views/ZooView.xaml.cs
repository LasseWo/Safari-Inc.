using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SafariInc.Audio;
using SafariInc.Events;
using SafariInc.Model;

namespace SafariInc.Views;

/// <summary>
/// Die Zoo-Karte. Antippen und Kaufen laufen über Button.Click (Bubbling). Ziehen erkennt die Karte mit
/// Preview-Ereignissen (Tunneling), weil sie die Maus dadurch vor den Gehege-Buttons sieht.
/// Abgelegte Tiere lösen Routed Events aus; die Regeln bleiben im GameManager.
/// </summary>
public partial class ZooView : UserControl
{
    private const double DragThreshold = 10;
    private const double StationMargin = 14;
    private const double GhostCenter = 56;
    private const double HintCenter = 45;
    private static readonly Size PenSize = new(220, 200);
    private static readonly Brush HoverBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0x00));

    private readonly Border[] _stations;

    private Enclosure? _pressed;
    private Point _pressPoint;
    private bool _dragging;
    private Border? _hovered;
    private bool _hasDragged;

    public ZooView()
    {
        InitializeComponent();
        _stations = new[] { StationShower, StationSell, StationWheel };

        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is not GameState state) return;
            state.PropertyChanged += (_, change) =>
            {
                if (change.PropertyName == nameof(GameState.SuggestedView)) UpdateSellHint();
            };
        };
    }

    private GameState? State => DataContext as GameState;

    private void View_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsVisible)
        {
            HideDragHint();
            return;
        }

        UpdateSellHint();
        if (!_hasDragged) ShowDragHint();
    }

    // ---------- Antippen ----------

    /// <summary>Bubbling: Click eines beliebigen Geheges kommt hier an.</summary>
    private void Enclosures_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Button { DataContext: Enclosure enclosure } button) return;
        e.Handled = true;

        if (!enclosure.IsUnlocked)
        {
            button.RaiseEvent(new EnclosureEventArgs(enclosure));
            if (enclosure.IsUnlocked)
                button.RaiseEvent(new CelebrationEventArgs(CelebrationKind.Confetti, 2));
        }
        else if (enclosure.Animal is { } animal)
        {
            SoundService.Play(animal.Species.Id, animal.Species.Sound);
            Attention.Bounce(button);
            button.RaiseEvent(new CelebrationEventArgs(CelebrationKind.Hearts));
        }
        else
        {
            button.RaiseEvent(new GameMessageEventArgs("🚙"));
        }
    }

    // ---------- Ziehen ----------

    private void Map_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement source) return;

        if (source.DataContext is Enclosure { Animal: not null } enclosure)
        {
            _pressed = enclosure;
            _pressPoint = e.GetPosition(Map);
        }
        else if (_stations.Any(s => s == source || s.IsAncestorOf(source)))
        {
            // Ein Kind tippt die Station an: Die Hand zeigt, dass man Tiere dorthin zieht.
            ShowDragHint();
        }
    }

    private void Map_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressed == null) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndDrag();
            return;
        }

        var point = e.GetPosition(Map);
        if (!_dragging)
        {
            if ((point - _pressPoint).Length < DragThreshold) return;
            BeginDrag(_pressed);
        }

        Canvas.SetLeft(Ghost, point.X - GhostCenter);
        Canvas.SetTop(Ghost, point.Y - GhostCenter);
        SetHovered(StationAt(point));
    }

    private void Map_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var from = _pressed;
        if (!_dragging || from?.Animal is not { } animal)
        {
            _pressed = null; // war kein Ziehen: Der Button meldet gleich sein Click
            return;
        }

        e.Handled = true;
        var point = e.GetPosition(Map);
        EndDrag();
        DropAnimal(animal, from, point);
    }

    /// <summary>
    /// Zum Beispiel beim Fensterwechsel: Das Tier bleibt einfach, wo es war.
    /// Das Ereignis blubbert: Auch der Button meldet es hier, wenn die Karte ihm beim Ziehen die Maus abnimmt.
    /// Darum zählt nur, wenn die Karte selbst die Maus verliert.
    /// </summary>
    private void Map_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_dragging && ReferenceEquals(e.OriginalSource, Map)) EndDrag();
    }

    private void BeginDrag(Enclosure enclosure)
    {
        var animal = enclosure.Animal!;
        _dragging = true;
        _hasDragged = true;
        Map.CaptureMouse(); // Der Button verliert die Maus und meldet deshalb kein Click.

        HideDragHint();
        Ghost.Text = animal.Species.Emoji;
        Ghost.Foreground = animal.Species.Color;
        Ghost.Visibility = Visibility.Visible;
        if (AnimalViewOf(enclosure) is { } view) view.Opacity = 0.25;

        ShowStationHints(animal);
        foreach (var station in _stations) Attention.SetPulse(station, true);
        SoundService.Play(animal.Species.Id, animal.Species.Sound);
    }

    private void EndDrag()
    {
        if (_pressed != null && AnimalViewOf(_pressed) is { } view) view.Opacity = 1;

        _dragging = false;
        _pressed = null;
        Map.ReleaseMouseCapture();

        Ghost.Visibility = Visibility.Collapsed;
        SetHovered(null);
        foreach (var hint in new[] { ShowerHint, SellHint, WheelHint })
            hint.Visibility = Visibility.Hidden;
        foreach (var station in _stations) Attention.SetPulse(station, false);
        UpdateSellHint();
    }

    private void DropAnimal(ZooAnimal animal, Enclosure from, Point point)
    {
        var station = StationAt(point);
        switch (station?.Tag as string)
        {
            case "shower":
                station.RaiseEvent(new AnimalEventArgs(GameEvents.AnimalWashedEvent, animal.Species, animal));
                station.RaiseEvent(new CelebrationEventArgs(CelebrationKind.Splash));
                Attention.Bounce(station);
                break;

            case "sell":
                station.RaiseEvent(new AnimalSoldEventArgs(animal, SaleMode.Safe));
                Attention.Bounce(station);
                break;

            case "wheel":
                station.RaiseEvent(new AnimalEventArgs(GameEvents.AnimalWheelRequestedEvent, animal.Species, animal));
                break;

            default:
                DropOnPen(animal, from, point);
                break;
        }
    }

    private void DropOnPen(ZooAnimal animal, Enclosure from, Point point)
    {
        var target = State?.Enclosures.FirstOrDefault(e => new Rect(e.MapSpot, PenSize).Contains(point));
        if (target == null || target == from) return;

        if (target.IsUnlocked && target.IsEmpty)
            Map.RaiseEvent(new AnimalMovedEventArgs(animal, target));
        else
            SoundService.Locked();
    }

    private Border? StationAt(Point point)
    {
        foreach (var station in _stations)
        {
            var area = BoundsOf(station);
            area.Inflate(StationMargin, StationMargin);
            if (area.Contains(point)) return station;
        }
        return null;
    }

    private static Rect BoundsOf(Border station)
        => new(Canvas.GetLeft(station), Canvas.GetTop(station), station.Width, station.Height);

    private void SetHovered(Border? station)
    {
        if (station == _hovered) return;
        _hovered?.ClearValue(Border.BorderBrushProperty);
        _hovered = station;
        if (station != null) station.BorderBrush = HoverBrush;
    }

    /// <summary>Beim Ziehen zeigt jede Station, was das Tier dort bringt.</summary>
    private void ShowStationHints(ZooAnimal animal)
    {
        int value = animal.Species.BaseValue;
        ShowerHint.Text = animal.IsClean ? "✨" : animal.Species.RarityInfo.RewardIcons;
        SellHint.Text = $"🪙 {value}";
        WheelHint.Text = $"🪙 {Catalog.Payout(value, Catalog.Pech)}–{Catalog.Payout(value, Catalog.Jackpot)}";

        foreach (var hint in new[] { ShowerHint, SellHint, WheelHint })
            hint.Visibility = Visibility.Visible;
    }

    private FrameworkElement? AnimalViewOf(Enclosure enclosure)
    {
        var container = PenList.ItemContainerGenerator.ContainerFromItem(enclosure) as ContentPresenter;
        return container?.ContentTemplate.FindName("AnimalView", container) as FrameworkElement;
    }

    private bool SaleIsNext => State?.SuggestedView == "Zoo";

    /// <summary>Geht auf Safari gerade nichts, pulsiert der Verkauf: Dort gibt es Platz und Münzen.</summary>
    private void UpdateSellHint()
    {
        if (!_dragging) Attention.SetPulse(StationSell, SaleIsNext);
    }

    // ---------- Hand, die das Ziehen vormacht ----------

    private void ShowDragHint()
    {
        var pen = State?.Enclosures.FirstOrDefault(e => e.Animal != null);
        if (pen?.Animal is not { } animal) return;

        // Wenn Verkaufen dran ist zum Verkauf, sonst zur Dusche: Duschen kostet das Kind nichts.
        var station = SaleIsNext ? StationSell : StationShower;
        var area = BoundsOf(station);
        var from = new Point(pen.MapSpot.X + PenSize.Width / 2 - HintCenter, pen.MapSpot.Y + PenSize.Height / 2 - HintCenter);
        var to = new Point(area.X + area.Width / 2 - HintCenter, area.Y + area.Height / 2 - HintCenter);

        DragHintAnimal.Text = animal.Species.Emoji;
        DragHintAnimal.Foreground = animal.Species.Color;
        DragHint.Visibility = Visibility.Visible;

        var shift = new TranslateTransform();
        DragHint.RenderTransform = shift;
        shift.BeginAnimation(TranslateTransform.XProperty, HintPath(from.X, to.X));
        shift.BeginAnimation(TranslateTransform.YProperty, HintPath(from.Y, to.Y));

        var fade = new DoubleAnimationUsingKeyFrames { Duration = HintDuration, RepeatBehavior = RepeatBehavior.Forever };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.12)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.82)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        DragHint.BeginAnimation(OpacityProperty, fade);
    }

    private static readonly Duration HintDuration = TimeSpan.FromSeconds(2.4);

    private static DoubleAnimationUsingKeyFrames HintPath(double from, double to)
    {
        var path = new DoubleAnimationUsingKeyFrames { Duration = HintDuration, RepeatBehavior = RepeatBehavior.Forever };
        path.KeyFrames.Add(new LinearDoubleKeyFrame(from, KeyTime.FromPercent(0)));
        path.KeyFrames.Add(new LinearDoubleKeyFrame(from, KeyTime.FromPercent(0.15)));
        path.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromPercent(0.72),
            new SineEase { EasingMode = EasingMode.EaseInOut }));
        path.KeyFrames.Add(new LinearDoubleKeyFrame(to, KeyTime.FromPercent(1)));
        return path;
    }

    private void HideDragHint()
    {
        if (DragHint.RenderTransform is TranslateTransform shift)
        {
            shift.BeginAnimation(TranslateTransform.XProperty, null);
            shift.BeginAnimation(TranslateTransform.YProperty, null);
        }
        DragHint.BeginAnimation(OpacityProperty, null);
        DragHint.Visibility = Visibility.Collapsed;
    }
}
