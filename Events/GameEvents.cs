using System.Windows;
using SafariInc.Game;
using SafariInc.Model;

namespace SafariInc.Events;

/// <summary>
/// Zentrale Registrierung aller Spiel-Ereignisse als Routed Events.
///
/// Die Oberfläche (Views) löst Anfrage-Ereignisse aus, die per Bubbling bis zum
/// Fenster hochwandern. Dort hat der GameManager seine Handler registriert und
/// führt die Spielregeln aus. Ergebnis-Ereignisse (AnimalCaptured, CurrencyChanged, ...)
/// löst der GameManager auf dem Fenster aus.
///
/// Neue Aktion hinzufügen = neues RoutedEvent hier registrieren + Handler im GameManager.
/// </summary>
public static class GameEvents
{
    // ---------- Tunneling (Preview) ----------

    /// <summary>
    /// Läuft VOR BaitUsed vom Fenster nach unten zur Quelle. Setzt ein Handler Handled = true,
    /// wird die Safari gar nicht erst gestartet (z. B. kein Köder oder Zoo voll).
    /// </summary>
    public static readonly RoutedEvent PreviewBaitUsedEvent = EventManager.RegisterRoutedEvent(
        "PreviewBaitUsed", RoutingStrategy.Tunnel, typeof(BaitEventHandler), typeof(GameEvents));

    // ---------- Bubbling: Anfragen der Oberfläche ----------

    public static readonly RoutedEvent BaitUsedEvent = EventManager.RegisterRoutedEvent(
        "BaitUsed", RoutingStrategy.Bubble, typeof(BaitEventHandler), typeof(GameEvents));

    public static readonly RoutedEvent BaitBoughtEvent = EventManager.RegisterRoutedEvent(
        "BaitBought", RoutingStrategy.Bubble, typeof(BaitEventHandler), typeof(GameEvents));

    public static readonly RoutedEvent AnimalGuessedEvent = EventManager.RegisterRoutedEvent(
        "AnimalGuessed", RoutingStrategy.Bubble, typeof(AnimalGuessedEventHandler), typeof(GameEvents));

    public static readonly RoutedEvent AnimalSoldEvent = EventManager.RegisterRoutedEvent(
        "AnimalSold", RoutingStrategy.Bubble, typeof(AnimalSoldEventHandler), typeof(GameEvents));

    public static readonly RoutedEvent RouletteRequestedEvent = EventManager.RegisterRoutedEvent(
        "RouletteRequested", RoutingStrategy.Bubble, typeof(RouletteEventHandler), typeof(GameEvents));

    public static readonly RoutedEvent AnimalDonatedEvent = EventManager.RegisterRoutedEvent(
        "AnimalDonated", RoutingStrategy.Bubble, typeof(AnimalEventHandler), typeof(GameEvents));

    public static readonly RoutedEvent EnclosureBoughtEvent = EventManager.RegisterRoutedEvent(
        "EnclosureBought", RoutingStrategy.Bubble, typeof(EnclosureEventHandler), typeof(GameEvents));

    public static readonly RoutedEvent NewGameRequestedEvent = EventManager.RegisterRoutedEvent(
        "NewGameRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(GameEvents));

    // ---------- Bubbling: Ergebnisse des GameManagers ----------

    public static readonly RoutedEvent AnimalCapturedEvent = EventManager.RegisterRoutedEvent(
        "AnimalCaptured", RoutingStrategy.Bubble, typeof(AnimalEventHandler), typeof(GameEvents));

    public static readonly RoutedEvent HuntFailedEvent = EventManager.RegisterRoutedEvent(
        "HuntFailed", RoutingStrategy.Bubble, typeof(AnimalEventHandler), typeof(GameEvents));

    public static readonly RoutedEvent CurrencyChangedEvent = EventManager.RegisterRoutedEvent(
        "CurrencyChanged", RoutingStrategy.Bubble, typeof(CurrencyChangedEventHandler), typeof(GameEvents));

    /// <summary>Statusmeldung – darf von jeder View und vom GameManager ausgelöst werden.</summary>
    public static readonly RoutedEvent GameMessageEvent = EventManager.RegisterRoutedEvent(
        "GameMessage", RoutingStrategy.Bubble, typeof(GameMessageEventHandler), typeof(GameEvents));
}

// ---------- Delegates ----------

public delegate void BaitEventHandler(object sender, BaitEventArgs e);
public delegate void AnimalGuessedEventHandler(object sender, AnimalGuessedEventArgs e);
public delegate void AnimalSoldEventHandler(object sender, AnimalSoldEventArgs e);
public delegate void RouletteEventHandler(object sender, RouletteEventArgs e);
public delegate void AnimalEventHandler(object sender, AnimalEventArgs e);
public delegate void EnclosureEventHandler(object sender, EnclosureEventArgs e);
public delegate void CurrencyChangedEventHandler(object sender, CurrencyChangedEventArgs e);
public delegate void GameMessageEventHandler(object sender, GameMessageEventArgs e);

// ---------- EventArgs ----------

public class BaitEventArgs : RoutedEventArgs
{
    public BaitEventArgs(RoutedEvent routedEvent, BaitType bait) : base(routedEvent) => Bait = bait;

    public BaitType Bait { get; }

    /// <summary>Begründung, falls ein Preview-Handler die Aktion abbricht.</summary>
    public string? CancelReason { get; set; }

    /// <summary>Rückgabe des GameManagers bei BaitUsed: die gestartete Safari.</summary>
    public HuntInfo? Hunt { get; set; }
}

public class AnimalGuessedEventArgs : RoutedEventArgs
{
    public AnimalGuessedEventArgs(string? guessedSpeciesId) : base(GameEvents.AnimalGuessedEvent)
        => GuessedSpeciesId = guessedSpeciesId;

    /// <summary>null = Zeit abgelaufen.</summary>
    public string? GuessedSpeciesId { get; }
}

public enum SaleMode { Safe, Roulette }

public class AnimalSoldEventArgs : RoutedEventArgs
{
    public AnimalSoldEventArgs(ZooAnimal animal, SaleMode mode) : base(GameEvents.AnimalSoldEvent)
    {
        Animal = animal;
        Mode = mode;
    }

    public ZooAnimal Animal { get; }
    public SaleMode Mode { get; }
}

public class RouletteEventArgs : RoutedEventArgs
{
    public RouletteEventArgs(ZooAnimal animal) : base(GameEvents.RouletteRequestedEvent) => Animal = animal;

    public ZooAnimal Animal { get; }

    /// <summary>Rückgabe des GameManagers: auf welchem Feld das Rad stehen bleibt.</summary>
    public int SliceIndex { get; set; } = -1;
    public int Payout { get; set; }
}

public class AnimalEventArgs : RoutedEventArgs
{
    public AnimalEventArgs(RoutedEvent routedEvent, AnimalSpecies species, ZooAnimal? animal = null)
        : base(routedEvent)
    {
        Species = species;
        Animal = animal;
    }

    public AnimalSpecies Species { get; }
    public ZooAnimal? Animal { get; }
}

public class EnclosureEventArgs : RoutedEventArgs
{
    public EnclosureEventArgs(Enclosure enclosure) : base(GameEvents.EnclosureBoughtEvent) => Enclosure = enclosure;

    public Enclosure Enclosure { get; }
}

public class CurrencyChangedEventArgs : RoutedEventArgs
{
    public CurrencyChangedEventArgs(int oldValue, int newValue) : base(GameEvents.CurrencyChangedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public int OldValue { get; }
    public int NewValue { get; }
    public int Delta => NewValue - OldValue;
}

public class GameMessageEventArgs : RoutedEventArgs
{
    public GameMessageEventArgs(string message, bool isWarning = false) : base(GameEvents.GameMessageEvent)
    {
        Message = message;
        IsWarning = isWarning;
    }

    public string Message { get; }
    public bool IsWarning { get; }
}
