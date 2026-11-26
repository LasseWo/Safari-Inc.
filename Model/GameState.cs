using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SafariInc.Model;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Ein gefangenes Tier im Zoo.</summary>
public class ZooAnimal : ObservableObject
{
    private bool _isClean;

    public ZooAnimal(AnimalSpecies species) => Species = species;

    public AnimalSpecies Species { get; }

    /// <summary>Schon geduscht? Nur die erste Dusche bringt Köder.</summary>
    public bool IsClean
    {
        get => _isClean;
        set => Set(ref _isClean, value);
    }
}

public class Enclosure : ObservableObject
{
    private bool _isUnlocked;
    private bool _canBuy, _pulseBuy;
    private ZooAnimal? _animal;

    public Enclosure(int index, int price, bool unlocked)
    {
        Index = index;
        Price = price;
        _isUnlocked = unlocked;
    }

    public int Index { get; }
    public int Number => Index + 1;
    public int Price { get; }
    public Point MapSpot => Catalog.EnclosureSpots[Index];

    public bool IsUnlocked
    {
        get => _isUnlocked;
        set => Set(ref _isUnlocked, value);
    }

    public ZooAnimal? Animal
    {
        get => _animal;
        set
        {
            if (Set(ref _animal, value)) OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public bool IsEmpty => _animal == null;

    /// <summary>Ist als Nächstes dran und genug Münzen sind da.</summary>
    public bool CanBuy
    {
        get => _canBuy;
        set => Set(ref _canBuy, value);
    }

    /// <summary>Kaufbar und der Zoo ist voll: Dieses Gehege ist der nächste sinnvolle Schritt.</summary>
    public bool PulseBuy
    {
        get => _pulseBuy;
        set => Set(ref _pulseBuy, value);
    }
}

public class BaitStock : ObservableObject
{
    private const int MaxStockIcons = 6;

    private int _count;
    private bool _canAfford, _canHunt, _pulseBuy, _pulseHunt;

    public BaitStock(BaitInfo info, int count)
    {
        Info = info;
        _count = count;
    }

    public BaitInfo Info { get; }

    public int Count
    {
        get => _count;
        set
        {
            if (Set(ref _count, value)) OnPropertyChanged(nameof(StockIcons));
        }
    }

    /// <summary>Der Vorrat als Bilder: drei Köder = drei Bilder.</summary>
    public string StockIcons =>
        string.Concat(Enumerable.Repeat(Info.Emoji, Math.Min(_count, MaxStockIcons)))
        + (_count > MaxStockIcons ? "+" : "");

    public bool CanAfford
    {
        get => _canAfford;
        set => Set(ref _canAfford, value);
    }

    public bool CanHunt
    {
        get => _canHunt;
        set => Set(ref _canHunt, value);
    }

    /// <summary>Dieser Kaufen-Button ist der nächste sinnvolle Schritt.</summary>
    public bool PulseBuy
    {
        get => _pulseBuy;
        set => Set(ref _pulseBuy, value);
    }

    /// <summary>Dieser Safari-Button ist der nächste sinnvolle Schritt.</summary>
    public bool PulseHunt
    {
        get => _pulseHunt;
        set => Set(ref _pulseHunt, value);
    }
}

/// <summary>Der komplette Spielstand. Die Oberfläche bindet sich per Data Binding daran.</summary>
public class GameState : ObservableObject
{
    private int _coins;
    private bool _isBusy;
    private string? _suggestedView;
    private int _freeSpins;
    private int _catchProgress;

    public GameState()
    {
        Baits = new ObservableCollection<BaitStock>(Catalog.Baits.Select(b => new BaitStock(b, 0)));
        Enclosures = new ObservableCollection<Enclosure>(
            Catalog.EnclosurePrices.Select((price, i) => new Enclosure(i, price, price == 0)));

        foreach (var bait in Baits)
            bait.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(BaitStock.Count)) RefreshHints();
            };
        foreach (var enclosure in Enclosures)
            enclosure.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(Enclosure.IsUnlocked) or nameof(Enclosure.Animal)) RefreshHints();
            };
    }

    public int Coins
    {
        get => _coins;
        set
        {
            if (Set(ref _coins, value)) RefreshHints();
        }
    }

    /// <summary>Läuft gerade eine Safari oder dreht sich das Glücksrad?</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => Set(ref _isBusy, value);
    }

    /// <summary>Verdiente, noch nicht gedrehte Freispiele.</summary>
    public int FreeSpins
    {
        get => _freeSpins;
        set
        {
            if (Set(ref _freeSpins, value)) OnPropertyChanged(nameof(HasFreeSpins));
        }
    }

    public bool HasFreeSpins => _freeSpins > 0;

    /// <summary>Gefangene Tiere seit dem letzten Freispiel.</summary>
    public int CatchProgress
    {
        get => _catchProgress;
        set
        {
            if (!Set(ref _catchProgress, value)) return;
            OnPropertyChanged(nameof(ProgressLit));
            OnPropertyChanged(nameof(ProgressDim));
        }
    }

    /// <summary>Der Fortschritt als Bilder: leuchtende und noch dunkle Flammen.</summary>
    public string ProgressLit => Flames(_catchProgress);

    public string ProgressDim => Flames(Catalog.CatchesPerFreeSpin - _catchProgress);

    private static string Flames(int count) => string.Concat(Enumerable.Repeat("🔥", Math.Max(count, 0)));

    /// <summary>Wohin als Nächstes? "Safari", "Zoo" (zum Verkaufen oder Bauen) oder null.</summary>
    public string? SuggestedView
    {
        get => _suggestedView;
        private set => Set(ref _suggestedView, value);
    }

    public ObservableCollection<BaitStock> Baits { get; }
    public ObservableCollection<Enclosure> Enclosures { get; }

    /// <summary>Alle Tiere im Zoo.</summary>
    public ObservableCollection<ZooAnimal> Animals { get; } = new();

    public BaitStock Bait(BaitType type) => Baits.First(b => b.Info.Type == type);

    public Enclosure? FreeEnclosure => Enclosures.FirstOrDefault(e => e.IsUnlocked && e.IsEmpty);

    public void PlaceAnimal(ZooAnimal animal, Enclosure enclosure)
    {
        enclosure.Animal = animal;
        Animals.Add(animal);
    }

    public void RemoveAnimal(ZooAnimal animal)
    {
        var enclosure = Enclosures.FirstOrDefault(e => e.Animal == animal);
        if (enclosure != null) enclosure.Animal = null;
        Animals.Remove(animal);
    }

    public void MoveAnimal(ZooAnimal animal, Enclosure target)
    {
        Enclosures.First(e => e.Animal == animal).Animal = null;
        target.Animal = animal;
    }

    /// <summary>
    /// Berechnet, was gerade möglich ist und was als Nächstes dran ist. Kinder, die nicht
    /// lesen können, sehen daran, welche Buttons leuchten und welcher pulsiert.
    /// </summary>
    private void RefreshHints()
    {
        bool zooHasRoom = FreeEnclosure != null;
        var huntBait = zooHasRoom ? Baits.FirstOrDefault(b => b.Count > 0) : null;
        var buyBait = zooHasRoom && huntBait == null ? Baits.FirstOrDefault(b => Coins >= b.Info.Price) : null;

        foreach (var bait in Baits)
        {
            bait.CanAfford = Coins >= bait.Info.Price;
            bait.CanHunt = zooHasRoom && bait.Count > 0;
            bait.PulseHunt = bait == huntBait;
            bait.PulseBuy = bait == buyBait;
        }

        var nextLocked = Enclosures.FirstOrDefault(e => !e.IsUnlocked);
        foreach (var enclosure in Enclosures)
        {
            enclosure.CanBuy = enclosure == nextLocked && Coins >= enclosure.Price;
            enclosure.PulseBuy = enclosure.CanBuy && !zooHasRoom;
        }

        bool hasAnimals = Enclosures.Any(e => !e.IsEmpty);
        SuggestedView = huntBait != null || buyBait != null ? "Safari"
            : hasAnimals ? "Zoo"
            : null;
    }
}
