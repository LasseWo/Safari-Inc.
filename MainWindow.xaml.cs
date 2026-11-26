using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SafariInc.Audio;
using SafariInc.Events;
using SafariInc.Game;
using SafariInc.Model;
using SafariInc.Views;

namespace SafariInc;

public partial class MainWindow : Window
{
    private static readonly Brush ActiveNav = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D));
    private static readonly Brush InactiveNav = new SolidColorBrush(Color.FromRgb(0x55, 0x8B, 0x2F));
    private static readonly Brush InfoBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xE6, 0xC9));
    private static readonly Brush WarningBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xCD, 0xD2));
    private static readonly Brush CoinBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00));
    private static readonly Brush FlameBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0x8C, 0x00));
    private static readonly Brush HeartBrush = new SolidColorBrush(Color.FromRgb(0xEC, 0x40, 0x7A));
    private static readonly Brush WaterBrush = new SolidColorBrush(Color.FromRgb(0x29, 0x9B, 0xE6));

    private readonly GameManager _game;
    private readonly Celebration _fx;
    private string _currentView = "Safari";

    public MainWindow()
    {
        InitializeComponent();
        _fx = new Celebration(FxLayer);

        // Der GameManager hängt seine Handler an das Fenster (Wurzel des Element-Baums).
        _game = new GameManager(this);
        DataContext = _game.State;

        // Ergebnis-Ereignisse des GameManagers an die Oberfläche weiterreichen.
        AddHandler(GameEvents.GameMessageEvent, new GameMessageEventHandler(OnGameMessage));
        AddHandler(GameEvents.CurrencyChangedEvent, new CurrencyChangedEventHandler(OnCurrencyChanged));
        AddHandler(GameEvents.AnimalCapturedEvent, new AnimalEventHandler(OnAnimalCaptured));
        AddHandler(GameEvents.HuntFailedEvent, new AnimalEventHandler((_, e) => SafariPage.ShowResult(false, e.Species)));
        AddHandler(GameEvents.FreeSpinEarnedEvent, new RoutedEventHandler(OnFreeSpinEarned));
        AddHandler(GameEvents.CelebrationEvent, new CelebrationEventHandler(OnCelebration));
        AddHandler(GameEvents.AnimalWheelRequestedEvent, new AnimalEventHandler(OnAnimalWheelRequested));

        _game.State.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(GameState.SuggestedView) or nameof(GameState.IsBusy)
                or nameof(GameState.HasFreeSpins))
                UpdateNavHint();
        };

        ShowView("Safari");
        Closing += (_, _) => _game.Save();
    }

    // ---------- Navigation ----------

    /// <summary>Tunneling: wird VOR dem Button ausgelöst und kann den Klick verschlucken.</summary>
    private void NavBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_game.State.IsBusy) return;

        e.Handled = true; // Button bekommt den Klick nie
        SoundService.Locked();
        ShowMessage("⏳", true);
    }

    /// <summary>Bubbling: ein Handler für alle Navigations-Buttons.</summary>
    private void NavBar_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Button { Tag: string view }) return;
        WheelDialog.Close();
        ShowView(view);
    }

    private void ShowView(string view)
    {
        _currentView = view;
        SafariPage.Visibility = view == "Safari" ? Visibility.Visible : Visibility.Collapsed;
        ZooPage.Visibility = view == "Zoo" ? Visibility.Visible : Visibility.Collapsed;

        foreach (var button in NavBar.Children.OfType<Button>())
        {
            bool active = (string)button.Tag == view;
            button.Background = active ? ActiveNav : InactiveNav;
            button.Foreground = active ? (Brush)FindResource("Ink") : Brushes.White;
        }
        UpdateNavHint();
    }

    /// <summary>Der Navigations-Button, der als Nächstes dran ist, pulsiert. Ebenso ein verdientes Freispiel.</summary>
    private void UpdateNavHint()
    {
        var state = _game.State;
        string? target = state.IsBusy ? null : state.SuggestedView;
        foreach (var button in NavBar.Children.OfType<Button>())
            Attention.SetPulse(button, (string)button.Tag == target && target != _currentView);

        Attention.SetPulse(FreeSpinButton, state.HasFreeSpins && !state.IsBusy);
    }

    /// <summary>Ein Klick dreht sofort das Rad: Das Freispiel soll ohne Umweg belohnen.</summary>
    private void FreeSpin_Click(object sender, RoutedEventArgs e)
    {
        if (RefuseWhileBusy()) return;
        WheelDialog.SpinFree();
    }

    /// <summary>Tier wurde auf der Karte aufs Glücksrad gezogen.</summary>
    private void OnAnimalWheelRequested(object sender, AnimalEventArgs e)
    {
        e.Handled = true;
        if (e.Animal == null || RefuseWhileBusy()) return;
        WheelDialog.SpinAnimal(e.Animal);
    }

    private bool RefuseWhileBusy()
    {
        if (!_game.State.IsBusy) return false;
        SoundService.Locked();
        ShowMessage("⏳", true);
        return true;
    }

    // ---------- Neues Spiel ----------

    private void NewGame_Click(object sender, RoutedEventArgs e)
    {
        if (RefuseWhileBusy()) return;
        ConfirmOverlay.Visibility = Visibility.Visible;
    }

    private void CancelNewGame_Click(object sender, RoutedEventArgs e)
        => ConfirmOverlay.Visibility = Visibility.Collapsed;

    private void ConfirmNewGame_Click(object sender, RoutedEventArgs e)
    {
        ConfirmOverlay.Visibility = Visibility.Collapsed;
        WheelDialog.Close();
        ((Button)sender).RaiseEvent(new RoutedEventArgs(GameEvents.NewGameRequestedEvent));
        SafariPage.Reset();
        ShowView("Safari");
    }

    // ---------- Ergebnis-Ereignisse ----------

    private void OnGameMessage(object sender, GameMessageEventArgs e)
    {
        ShowMessage(e.Message, e.IsWarning);
        e.Handled = true;
    }

    private void ShowMessage(string pictures, bool warning)
    {
        ToastText.Text = pictures;
        Toast.Background = warning ? WarningBrush : InfoBrush;

        var fade = new DoubleAnimationUsingKeyFrames();
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1500))));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1900))));
        Toast.BeginAnimation(OpacityProperty, fade);
        Attention.Pop(Toast);
    }

    // ---------- Belohnungen ----------

    private void OnAnimalCaptured(object sender, AnimalEventArgs e)
    {
        SafariPage.ShowResult(true, e.Species, _game.State.CatchProgress);

        // Eine Flamme fliegt vom Tier in die Anzeige oben.
        _fx.FlyTo(CenterOf(null), CenterOf(ProgressChip), 1, "🔥", FlameBrush, () => Attention.Bounce(ProgressChip));
    }

    private void OnFreeSpinEarned(object sender, RoutedEventArgs e)
    {
        SoundService.Fanfare();
        ShowMessage("🔥🔥🔥 ➜ 🎡", false);
        _fx.Rain(60);
    }

    /// <summary>Bubbling: Jede View kann feiern. Die Effekte starten dort, wo das Ereignis ausgelöst wurde.</summary>
    private void OnCelebration(object sender, CelebrationEventArgs e)
    {
        e.Handled = true;
        var origin = CenterOf(e.OriginalSource as FrameworkElement);
        switch (e.Kind)
        {
            case CelebrationKind.Hearts:
                _fx.Burst(origin, 10, "♥", HeartBrush);
                break;
            case CelebrationKind.Confetti:
                _fx.Confetti(origin, 14 * e.Strength);
                break;
            case CelebrationKind.Jackpot:
                _fx.Confetti(origin, 40);
                _fx.Rain(70);
                break;
            case CelebrationKind.Splash:
                _fx.Burst(origin, 16, "💧", WaterBrush);
                break;
        }
    }

    /// <summary>Mittelpunkt eines Elements auf der Effekt-Ebene; ohne Element die Fenstermitte.</summary>
    private Point CenterOf(FrameworkElement? element)
    {
        if (element is not { IsVisible: true })
            return new Point(FxLayer.ActualWidth / 2, FxLayer.ActualHeight / 2);
        return element.TranslatePoint(new Point(element.ActualWidth / 2, element.ActualHeight / 2), FxLayer);
    }

    private void OnCurrencyChanged(object sender, CurrencyChangedEventArgs e)
    {
        if (e.Delta == 0) return;
        SoundService.Coin();

        // Mehr Gewinn = mehr fliegende Münzen.
        if (e.Delta > 0)
        {
            _fx.FlyTo(CenterOf(null), CenterOf(CoinChip), Math.Clamp(e.Delta / 20, 3, 18), "🪙", CoinBrush,
                () => Attention.Bounce(CoinChip));
        }

        CoinDelta.Text = e.Delta > 0 ? $"+{e.Delta}" : e.Delta.ToString();
        CoinDelta.Foreground = e.Delta > 0 ? Brushes.Gold : Brushes.OrangeRed;

        var duration = TimeSpan.FromSeconds(1.4);
        CoinDelta.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, duration));
        CoinDeltaShift.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(0, 30, duration) { EasingFunction = new QuadraticEase() });
    }
}
