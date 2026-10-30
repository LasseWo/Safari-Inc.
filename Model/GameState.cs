using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
public class ZooAnimal
{
    public ZooAnimal(AnimalSpecies species) => Species = species;

    public AnimalSpecies Species { get; }
}

public class Enclosure : ObservableObject
{
    private bool _isUnlocked;
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
}

public class BaitStock : ObservableObject
{
    private int _count;

    public BaitStock(BaitInfo info, int count)
    {
        Info = info;
        _count = count;
    }

    public BaitInfo Info { get; }

    public int Count
    {
        get => _count;
        set => Set(ref _count, value);
    }

    public string ChancesText =>
        string.Join("  ", Catalog.Rarities.Values.Zip(Info.Chances, (r, c) => $"{r.Name} {c} %"));
}

/// <summary>Der komplette Spielstand. Die Oberfläche bindet sich per Data Binding daran.</summary>
public class GameState : ObservableObject
{
    private int _coins;
    private bool _isBusy;

    public GameState()
    {
        Baits = new ObservableCollection<BaitStock>(Catalog.Baits.Select(b => new BaitStock(b, 0)));
        Enclosures = new ObservableCollection<Enclosure>(
            Catalog.EnclosurePrices.Select((price, i) => new Enclosure(i, price, price == 0)));
    }

    public int Coins
    {
        get => _coins;
        set => Set(ref _coins, value);
    }

    /// <summary>Läuft gerade eine Safari oder dreht sich das Glücksrad?</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => Set(ref _isBusy, value);
    }

    public ObservableCollection<BaitStock> Baits { get; }
    public ObservableCollection<Enclosure> Enclosures { get; }

    /// <summary>Alle Tiere im Zoo (für die Händler-Liste).</summary>
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
}
