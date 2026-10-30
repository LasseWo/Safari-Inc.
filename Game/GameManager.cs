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
        root.AddHandler(GameEvents.AnimalDonatedEvent, new AnimalEventHandler(OnAnimalDonated));
        root.AddHandler(GameEvents.EnclosureBoughtEvent, new EnclosureEventHandler(OnEnclosureBought));
        root.AddHandler(GameEvents.NewGameRequestedEvent, new RoutedEventHandler(OnNewGameRequested));

        if (!SaveGame.TryLoad(State))
            ResetState();
    }

    public GameState State { get; } = new();

    // ---------- Safari ----------

    private void OnPreviewBaitUsed(object sender, BaitEventArgs e)
    {
        if (State.IsBusy)
            Cancel(e, "Es läuft schon eine Safari!");
        else if (State.Bait(e.Bait).Count == 0)
            Cancel(e, $"Du hast keinen {Catalog.Bait(e.Bait).Name} mehr. Kauf erst einen!");
        else if (State.FreeEnclosure == null)
            Cancel(e, "Dein Zoo ist voll! Verkaufe ein Tier oder kaufe ein neues Gehege.");
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
            Raise(new AnimalEventArgs(GameEvents.AnimalCapturedEvent, target, animal));
            Message($"Gefangen! {target.Name} zieht in Gehege {enclosure.Number} ein.");
        }
        else
        {
            Raise(new AnimalEventArgs(GameEvents.HuntFailedEvent, target));
            Message(e.GuessedSpeciesId == null
                ? $"Zu langsam! Gesucht war: {target.Name}. Der Köder ist weg."
                : $"Falsch geraten! Gesucht war: {target.Name}. Der Köder ist weg.", true);
            CheckBankrupt();
        }
        Save();
    }

    // ---------- Händler ----------

    private void OnRouletteRequested(object sender, RouletteEventArgs e)
    {
        e.Handled = true;
        if (State.IsBusy || !State.Animals.Contains(e.Animal)) return;

        // Die Spiellogik entscheidet das Ergebnis, die View animiert nur dorthin.
        int slice = _rng.Next(Catalog.WheelSlices.Length);
        int payout = Catalog.Payout(e.Animal.Species, Catalog.WheelSlices[slice]);

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
            Message($"{e.Animal.Species.Name} sicher verkauft für {payout} Münzen.");
        }
        else
        {
            if (_pendingRoulette?.Animal != e.Animal) return;
            payout = _pendingRoulette.Value.Payout;
            var outcome = Catalog.WheelSlices[_pendingRoulette.Value.SliceIndex];
            _pendingRoulette = null;
            State.IsBusy = false;

            int diff = payout - e.Animal.Species.BaseValue;
            Message($"{outcome.Name}! {e.Animal.Species.Name} bringt {payout} Münzen " +
                    (diff >= 0 ? $"(+{diff} gegenüber sicher)." : $"({diff} gegenüber sicher). Der Händler lacht."),
                    diff < 0);
        }

        State.RemoveAnimal(e.Animal);
        ChangeCoins(payout);
        Save();
    }

    private void OnAnimalDonated(object sender, AnimalEventArgs e)
    {
        e.Handled = true;
        if (State.IsBusy || e.Animal == null || !State.Animals.Contains(e.Animal)) return;

        State.RemoveAnimal(e.Animal);
        var rewards = e.Species.RarityInfo.ShelterReward;
        foreach (var (bait, count) in rewards)
            State.Bait(bait).Count += count;

        SoundService.Success();
        Message($"{e.Species.Name} ist jetzt im Tierheim. Du bekommst: " +
                string.Join(", ", rewards.Select(r => $"{r.Count}× {Catalog.Bait(r.Bait).Name}")));
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
            Message($"Zu wenig Münzen für {info.Name} ({info.Price}).", true);
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
            Message($"Kaufe zuerst Gehege {next.Number}.", true);
            return;
        }
        if (State.Coins < e.Enclosure.Price)
        {
            SoundService.Locked();
            Message($"Gehege {e.Enclosure.Number} kostet {e.Enclosure.Price} Münzen.", true);
            return;
        }

        e.Enclosure.IsUnlocked = true;
        ChangeCoins(-e.Enclosure.Price);
        Message($"Gehege {e.Enclosure.Number} gebaut!");
        Save();
    }

    private void OnNewGameRequested(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (State.IsBusy) return;
        ResetState();
        Message("Neues Spiel gestartet. Viel Glück auf Safari!");
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
        State.IsBusy = false;
        _currentHunt = null;
        _pendingRoulette = null;
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

        State.Bait(BaitType.Fleisch).Count++;
        Message("Pleite! Safari Inc. schenkt dir einen Fleischköder. Das Glück gehört dem Händler.", true);
    }

    private void Raise(RoutedEventArgs e) => _root.RaiseEvent(e);

    private void Message(string text, bool warning = false)
        => Raise(new GameMessageEventArgs(text, warning));

    public void Save() => SaveGame.Save(State);
}
