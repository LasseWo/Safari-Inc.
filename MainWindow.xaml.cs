using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SafariInc.Audio;
using SafariInc.Events;
using SafariInc.Game;

namespace SafariInc;

public partial class MainWindow : Window
{
    private static readonly Brush ActiveNav = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D));
    private static readonly Brush InactiveNav = new SolidColorBrush(Color.FromRgb(0x55, 0x8B, 0x2F));
    private static readonly Brush InfoBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xE6, 0xC9));
    private static readonly Brush WarningBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xCD, 0xD2));

    private readonly GameManager _game;

    public MainWindow()
    {
        InitializeComponent();

        // Der GameManager hängt seine Handler an das Fenster (Wurzel des Element-Baums).
        _game = new GameManager(this);
        DataContext = _game.State;

        // Ergebnis-Ereignisse des GameManagers an die Oberfläche weiterreichen.
        AddHandler(GameEvents.GameMessageEvent, new GameMessageEventHandler(OnGameMessage));
        AddHandler(GameEvents.CurrencyChangedEvent, new CurrencyChangedEventHandler(OnCurrencyChanged));
        AddHandler(GameEvents.AnimalCapturedEvent, new AnimalEventHandler((_, e) => SafariPage.ShowResult(true, e.Species)));
        AddHandler(GameEvents.HuntFailedEvent, new AnimalEventHandler((_, e) => SafariPage.ShowResult(false, e.Species)));

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
        ShowMessage("Erst die Safari bzw. das Glücksrad zu Ende spielen!", true);
    }

    /// <summary>Bubbling: ein Handler für alle Navigations-Buttons.</summary>
    private void NavBar_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is Button { Tag: string view })
            ShowView(view);
    }

    private void ShowView(string view)
    {
        SafariPage.Visibility = view == "Safari" ? Visibility.Visible : Visibility.Collapsed;
        ZooPage.Visibility = view == "Zoo" ? Visibility.Visible : Visibility.Collapsed;
        MerchantPage.Visibility = view == "Merchant" ? Visibility.Visible : Visibility.Collapsed;

        foreach (var button in NavBar.Children.OfType<Button>())
        {
            bool active = (string)button.Tag == view;
            button.Background = active ? ActiveNav : InactiveNav;
            button.Foreground = active ? (Brush)FindResource("Ink") : Brushes.White;
        }
    }

    private void NewGame_Click(object sender, RoutedEventArgs e)
    {
        if (_game.State.IsBusy) return;
        var answer = MessageBox.Show(this, "Wirklich neu anfangen? Dein Zoo und deine Münzen sind dann weg.",
            "Neues Spiel", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

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

    private void ShowMessage(string text, bool warning)
    {
        StatusText.Text = text;
        StatusBar.Background = warning ? WarningBrush : InfoBrush;
        StatusText.BeginAnimation(OpacityProperty, new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(300)));
    }

    private void OnCurrencyChanged(object sender, CurrencyChangedEventArgs e)
    {
        if (e.Delta == 0) return;
        if (e.Delta > 0) SoundService.Coin();

        CoinDelta.Text = e.Delta > 0 ? $"+{e.Delta}" : e.Delta.ToString();
        CoinDelta.Foreground = e.Delta > 0 ? Brushes.Gold : Brushes.OrangeRed;

        var duration = TimeSpan.FromSeconds(1.4);
        CoinDelta.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, duration));
        CoinDeltaShift.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(0, 30, duration) { EasingFunction = new QuadraticEase() });
    }
}
