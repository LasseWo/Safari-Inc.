using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using SafariInc.Audio;
using SafariInc.Events;
using SafariInc.Game;
using SafariInc.Model;

namespace SafariInc.Views;

public partial class SafariView : UserControl
{
    private const double GuessSeconds = 10;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private HuntInfo? _hunt;
    private AnimalSpecies? _lastTarget;
    private DateTime _guessStart;

    public SafariView()
    {
        InitializeComponent();
        _timer.Tick += Timer_Tick;
    }

    // ---------- 1. Köder auswählen ----------

    /// <summary>
    /// Bubbling: Der Click jedes Buttons in jeder Köder-Karte blubbert bis zur ItemsControl hoch.
    /// OriginalSource ist der geklickte Button, sein DataContext der passende Köder.
    /// </summary>
    private void BaitList_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Button { DataContext: BaitStock bait } button) return;
        e.Handled = true;

        if ((string)button.Tag == "buy")
            button.RaiseEvent(new BaitEventArgs(GameEvents.BaitBoughtEvent, bait.Info.Type));
        else
            StartHunt(button, bait.Info.Type);
    }

    private void StartHunt(UIElement source, BaitType bait)
    {
        // Erst das Tunneling-Event: Fenster -> ... -> Button. Ein Handler darf abbrechen.
        var preview = new BaitEventArgs(GameEvents.PreviewBaitUsedEvent, bait);
        source.RaiseEvent(preview);
        if (preview.Handled)
        {
            SoundService.Locked();
            source.RaiseEvent(new GameMessageEventArgs(preview.CancelReason ?? "Safari nicht möglich.", true));
            return;
        }

        // Dann das Bubbling-Event: Button -> ... -> Fenster (GameManager).
        var used = new BaitEventArgs(GameEvents.BaitUsedEvent, bait);
        source.RaiseEvent(used);
        if (used.Hunt == null) return;

        _hunt = used.Hunt;
        _lastTarget = used.Hunt.Target;
        source.RaiseEvent(new GameMessageEventArgs($"{Catalog.Bait(bait).Name} ausgelegt. Auf geht's!"));
        DriveJeep();
    }

    // ---------- 2. Auf Safari gehen ----------

    private void DriveJeep()
    {
        BaitPanel.Visibility = Visibility.Collapsed;
        HuntPanel.Visibility = Visibility.Visible;
        GuessArea.Visibility = Visibility.Collapsed;
        DrivingText.Visibility = Visibility.Visible;
        UpdateLayout();

        SoundService.Jeep();

        // Der Jeep fährt von rechts nach links durchs Bild und wackelt dabei.
        var drive = new DoubleAnimation(Landscape.ActualWidth + 20, -120, TimeSpan.FromSeconds(2.3));
        drive.Completed += (_, _) => ShowGuess();
        JeepShift.BeginAnimation(TranslateTransform.XProperty, drive);
        JeepShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -6, TimeSpan.FromMilliseconds(120))
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(TimeSpan.FromSeconds(2.3)),
        });
    }

    // ---------- 3. Tier erraten ----------

    private void ShowGuess()
    {
        if (_hunt == null) return;

        DrivingText.Visibility = Visibility.Collapsed;
        GuessArea.Visibility = Visibility.Visible;
        ResultPanel.Visibility = Visibility.Collapsed;
        QuestionText.Text = "Welches Tier hörst du?";

        GuessList.ItemsSource = _hunt.Options;
        GuessList.IsEnabled = true;
        GuessList.Visibility = Visibility.Visible;

        Silhouette.Text = _hunt.Target.Emoji;
        Silhouette.Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0x34, 0x2E));
        SilhouetteBlur.BeginAnimation(BlurEffect.RadiusProperty,
            new DoubleAnimation(45, 4, TimeSpan.FromSeconds(GuessSeconds)));

        PlayAnimalSound();
        _guessStart = DateTime.Now;
        TimeBar.Value = 1;
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        double elapsed = (DateTime.Now - _guessStart).TotalSeconds;
        TimeBar.Value = Math.Max(0, 1 - elapsed / GuessSeconds);
        if (elapsed >= GuessSeconds)
            SubmitGuess(null, GuessList); // Zeit abgelaufen
    }

    /// <summary>Bubbling: ein Handler für alle vier Antwort-Buttons.</summary>
    private void GuessList_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is Button { DataContext: AnimalSpecies guess } button)
            SubmitGuess(guess.Id, button);
        e.Handled = true;
    }

    private void SubmitGuess(string? speciesId, UIElement source)
    {
        if (_hunt == null) return;
        _hunt = null;
        _timer.Stop();
        GuessList.IsEnabled = false;

        // Der GameManager prüft die Antwort und antwortet mit AnimalCaptured oder HuntFailed.
        source.RaiseEvent(new AnimalGuessedEventArgs(speciesId));
    }

    /// <summary>Wird vom Fenster aufgerufen, wenn AnimalCaptured / HuntFailed ankommt.</summary>
    public void ShowResult(bool success, AnimalSpecies species)
    {
        SilhouetteBlur.BeginAnimation(BlurEffect.RadiusProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(400)));
        Silhouette.Foreground = species.RarityInfo.Color;

        QuestionText.Text = success ? "Gefangen!" : "Leider entwischt …";
        ResultText.Text = success
            ? $"{species.Name}! ({species.RarityInfo.Name}, Grundwert {species.BaseValue} Münzen). Es wartet jetzt in deinem Zoo."
            : $"Gesucht war: {species.Name} ({species.RarityInfo.Name}). Dein Köder ist verloren.";
        ResultText.Foreground = success ? new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)) : Brushes.Firebrick;
        GuessList.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Visible;

        if (success) SoundService.Success();
        else SoundService.Fail();
    }

    private void Replay_Click(object sender, RoutedEventArgs e) => PlayAnimalSound();

    private void PlayAnimalSound()
    {
        if (_lastTarget != null)
            SoundService.Play(_lastTarget.Id, _lastTarget.Sound);
    }

    private void Continue_Click(object sender, RoutedEventArgs e) => Reset();

    public void Reset()
    {
        _timer.Stop();
        _hunt = null;
        JeepShift.BeginAnimation(TranslateTransform.XProperty, null);
        JeepShift.BeginAnimation(TranslateTransform.YProperty, null);
        HuntPanel.Visibility = Visibility.Collapsed;
        BaitPanel.Visibility = Visibility.Visible;
    }
}
