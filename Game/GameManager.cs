using System.Windows;
using SafariInc.Audio;
using SafariInc.Events;
using SafariInc.Model;

namespace SafariInc.Game;

/// <summary>Eine laufende Safari: das gesuchte Tier und die vier Antwortmöglichkeiten.</summary>
public record HuntInfo(AnimalSpecies Target, IReadOnlyList<AnimalSpecies> Options, BaitType Bait);

/// <summary>
/// Enthält die Spielregeln. Der GameManager kennt keine Buttons oder Views –
/// er hängt sich nur mit AddHandler an das Wurzelelement (das Fenster) und
/// reagiert auf die Routed Events, die aus der Oberfläche hochblubbern.
/// </summary>
public class GameManager
{
    private readonly UIElement _root;
    private readonly Random _rng = new();

    private HuntInfo? _currentHunt;
    private (ZooAnimal Animal, int Payout, int SliceIndex)? _pendingRoulette;
    private int? _pendingFreeSpin;

    public GameManager(UIElement root)
    {
        _root = root;

        // Tunneling: läuft vor BaitUsed von oben nach unten.
        root.AddHandler(GameEvents.PreviewBaitUsedEvent, new BaitEventHandler(OnPreviewBaitUsed));

        // Bubbling: Anfragen aus der Oberfläche.
        root.AddHandler(GameEvents.BaitUsedEvent, new BaitEventHandler(OnBaitUsed));
        root.AddHandler(GameEvents.BaitBoughtEvent, new BaitEventHandler(OnBaitBought));
        root.AddHandler(GameEvents.AnimalGuessedEvent, new AnimalGuessedEventHandler(OnAnimalGuessed));
        root.AddHandler(GameEvents.AnimalSoldEvent, new AnimalSoldEventHandler(OnAnimalSold));
        root.AddHandler(GameEvents.RouletteRequestedEvent, new RouletteEventHandler(OnRouletteRequested));
        root.AddHandler(GameEvents.EnclosureBoughtEvent, new EnclosureEventHandler(OnEnclosureBought));
        root.AddHandler(GameEvents.NewGameRequestedEvent, new RoutedEventHandler(OnNewGameRequested));
        root.AddHandler(GameEvents.AnimalWashedEvent, new AnimalEventHandler(OnAnimalWashed));
        root.AddHandler(GameEvents.AnimalMovedEvent, new AnimalMovedEventHandler(OnAnimalMoved));
        root.AddHandler(GameEvents.FreeSpinRequestedEvent, new FreeSpinEventHandler(OnFreeSpinRequested));
        root.AddHandler(GameEvents.FreeSpinCompletedEvent, new RoutedEventHandler(OnFreeSpinCompleted));

        if (!SaveGame.TryLoad(State))
            ResetState();
    }

    public GameState State { get; } = new();

    // ---------- Safari ----------

    private void OnPreviewBaitUsed(object sender, BaitEventArgs e)
    {
        if (State.IsBusy)
            Cancel(e, "⏳");
        else if (State.Bait(e.Bait).Count == 0)
            Cancel(e, $"{Catalog.Bait(e.Bait).Emoji} ✖");
        else if (State.FreeEnclosure == null)
            Cancel(e, "🏞 ✖");
    }

    private static void Cancel(BaitEventArgs e, string reason)
    {
        e.CancelReason = reason;
        e.Handled = true;
    }

    private void OnBaitUsed(object sender, BaitEventArgs e)
    {
        State.Bait(e.Bait).Count--;
        State.IsBusy = true;

        var target = RollAnimal(Catalog.Bait(e.Bait));
        var options = Catalog.Animals
            .Where(a => a != target)
            .OrderBy(_ => _rng.Next())
            .Take(3)
            .Append(target)
            .OrderBy(_ => _rng.Next())
            .ToList();

        _currentHunt = new HuntInfo(target, options, e.Bait);
        e.Hunt = _currentHunt; // Rückgabe an die auslösende View
        e.Handled = true;
        Save();
    }

    private AnimalSpecies RollAnimal(BaitInfo bait)
    {
        // Erst die Seltenheit nach den Köder-Chancen würfeln, dann ein Tier dieser Seltenheit.
        int roll = _rng.Next(bait.Chances.Sum());
        var rarity = Rarity.Gewoehnlich;
        for (int i = 0; i < bait.Chances.Length; i++)
        {
            if (roll < bait.Chances[i]) { rarity = (Rarity)i; break; }
            roll -= bait.Chances[i];
        }

        var candidates = Catalog.Animals.Where(a => a.Rarity == rarity).ToList();
        return candidates[_rng.Next(candidates.Count)];
    }

    private void OnAnimalGuessed(object sender, AnimalGuessedEventArgs e)
    {
        e.Handled = true;
        if (_currentHunt == null) return;

        var target = _currentHunt.Target;
        _currentHunt = null;
        State.IsBusy = false;

        var enclosure = State.FreeEnclosure;
        if (e.GuessedSpeciesId == target.Id && enclosure != null)
        {
            var animal = new ZooAnimal(target);
            State.PlaceAnimal(animal, enclosure);
            bool earnedFreeSpin = AdvanceCatchProgress();
            Raise(new AnimalEventArgs(GameEvents.AnimalCapturedEvent, target, animal));
            if (earnedFreeSpin) Raise(new RoutedEventArgs(GameEvents.FreeSpinEarnedEvent));
        }
        else
        {
            Raise(new AnimalEventArgs(GameEvents.HuntFailedEvent, target));
            CheckBankrupt();
        }
        Save();
    }

    /// <summary>Jedes gefangene Tier bringt das Freispiel näher. Gibt true zurück, wenn es jetzt verdient ist.</summary>
    private bool AdvanceCatchProgress()
    {
        State.CatchProgress++;
        if (State.CatchProgress < Catalog.CatchesPerFreeSpin) return false;

        State.CatchProgress = 0;
        State.FreeSpins++;
        return true;
    }

    // ---------- Zoo-Karte ----------

    /// <summary>Die erste Dusche bringt Köder. Das Tier bleibt im Zoo, jede weitere Dusche ist nur noch zum Spaß.</summary>
    private void OnAnimalWashed(object sender, AnimalEventArgs e)
    {
        e.Handled = true;
        if (State.IsBusy || e.Animal == null || !State.Animals.Contains(e.Animal)) return;

        SoundService.Shower();
        if (e.Animal.IsClean)
        {
            Message($"{e.Species.Emoji} ✨");
            return;
        }

        e.Animal.IsClean = true;
        var rarity = e.Species.RarityInfo;
        State.Bait(rarity.ShowerReward.Bait).Count += rarity.ShowerReward.Count;
        Message($"{e.Species.Emoji} ✨ ➜ {rarity.RewardIcons}");
        Save();
    }

    private void OnAnimalMoved(object sender, AnimalMovedEventArgs e)
    {
        e.Handled = true;
        if (State.IsBusy || !e.Target.IsUnlocked || !e.Target.IsEmpty) return;

        State.MoveAnimal(e.Animal, e.Target);
        Save();
    }

    // ---------- Freispiele ----------

    private void OnFreeSpinRequested(object sender, FreeSpinEventArgs e)
    {
        e.Handled = true;
        if (State.IsBusy || !State.HasFreeSpins) return;

        // Die Spiellogik entscheidet das Ergebnis, die View animiert nur dorthin.
        int slice = _rng.Next(Catalog.WheelSlices.Length);
        int payout = Catalog.Payout(Catalog.FreeSpinBaseValue, Catalog.WheelSlices[slice]);

        _pendingFreeSpin = payout;
        State.IsBusy = true;
        e.SliceIndex = slice;
        e.Payout = payout;
    }

    private void OnFreeSpinCompleted(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_pendingFreeSpin is not { } payout) return;

        // Erst hier abziehen: Wird das Spiel während der Drehung geschlossen, bleibt das Freispiel erhalten.
        _pendingFreeSpin = null;
        State.FreeSpins--;
        State.IsBusy = false;
        ChangeCoins(payout);
        Save();
    }

    // ---------- Verkauf ----------

    private void OnRouletteRequested(object sender, RouletteEventArgs e)
    {
        e.Handled = true;
        if (State.IsBusy || !State.Animals.Contains(e.Animal)) return;

        // Die Spiellogik entscheidet das Ergebnis, die View animiert nur dorthin.
        int slice = _rng.Next(Catalog.WheelSlices.Length);
        int payout = Catalog.Payout(e.Animal.Species.BaseValue, Catalog.WheelSlices[slice]);

        _pendingRoulette = (e.Animal, payout, slice);
        State.IsBusy = true;
        e.SliceIndex = slice;
        e.Payout = payout;
    }

    private void OnAnimalSold(object sender, AnimalSoldEventArgs e)
    {
        e.Handled = true;
        if (!State.Animals.Contains(e.Animal)) return;

        int payout;
        if (e.Mode == SaleMode.Safe)
        {
            if (State.IsBusy) return;
            payout = e.Animal.Species.BaseValue;
            Message($"{e.Animal.Species.Emoji} ➜ 🪙 {payout}");
        }
        else
        {
            if (_pendingRoulette?.Animal != e.Animal) return;
            payout = _pendingRoulette.Value.Payout;
            _pendingRoulette = null;
            State.IsBusy = false;
            // Das Ergebnis zeigt das WheelPopup direkt neben dem Rad an.
        }

        State.RemoveAnimal(e.Animal);
        ChangeCoins(payout);
        Save();
    }

    // ---------- Laden ----------

    private void OnBaitBought(object sender, BaitEventArgs e)
    {
        e.Handled = true;
        var info = Catalog.Bait(e.Bait);
        if (State.Coins < info.Price)
        {
            SoundService.Locked();
            Message("🪙 ✖", true);
            return;
        }

        State.Bait(e.Bait).Count++;
        ChangeCoins(-info.Price);
        Save();
    }

    private void OnEnclosureBought(object sender, EnclosureEventArgs e)
    {
        e.Handled = true;
        if (e.Enclosure.IsUnlocked) return;

        // Gehege nur der Reihe nach freischalten.
        var next = State.Enclosures.First(x => !x.IsUnlocked);
        if (next != e.Enclosure)
        {
            SoundService.Locked();
            Message("🔒", true);
            return;
        }
        if (State.Coins < e.Enclosure.Price)
        {
            SoundService.Locked();
            Message("🪙 ✖", true);
            return;
        }

        e.Enclosure.IsUnlocked = true;
        ChangeCoins(-e.Enclosure.Price);
        Message("🔓");
        Save();
    }

    private void OnNewGameRequested(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (State.IsBusy) return;
        ResetState();
    }

    // ---------- Hilfsmethoden ----------

    private void ResetState()
    {
        foreach (var animal in State.Animals.ToList())
            State.RemoveAnimal(animal);
        foreach (var enclosure in State.Enclosures)
            enclosure.IsUnlocked = enclosure.Index < Catalog.StartEnclosures;
        foreach (var bait in State.Baits)
            bait.Count = bait.Info.Type switch { BaitType.Fleisch => 3, BaitType.Premium => 1, _ => 0 };
        State.Coins = Catalog.StartCoins;
        State.FreeSpins = 0;
        State.CatchProgress = 0;
        State.IsBusy = false;
        _currentHunt = null;
        _pendingRoulette = null;
        _pendingFreeSpin = null;
        Save();
    }

    private void ChangeCoins(int delta)
    {
        int old = State.Coins;
        State.Coins += delta;
        Raise(new CurrencyChangedEventArgs(old, State.Coins));
        CheckBankrupt();
    }

    /// <summary>Ohne Geld, Köder und Tiere ginge nichts mehr – der Händler hat Mitleid.</summary>
    private void CheckBankrupt()
    {
        bool stuck = State.Animals.Count == 0
                     && State.Baits.All(b => b.Count == 0)
                     && State.Coins < Catalog.Baits.Min(b => b.Price);
        if (!stuck) return;

        var gift = State.Bait(BaitType.Fleisch);
        gift.Count++;
        Message($"🎁 {gift.Info.Emoji}");
    }

    private void Raise(RoutedEventArgs e) => _root.RaiseEvent(e);

    private void Message(string text, bool warning = false)
        => Raise(new GameMessageEventArgs(text, warning));

    public void Save() => SaveGame.Save(State);
}
