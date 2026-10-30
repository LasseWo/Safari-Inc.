using System.Windows.Media;
using SafariInc.Audio;

namespace SafariInc.Model;

public enum Rarity { Gewoehnlich, Selten, Exotisch, Legendaer }

public enum BaitType { Fleisch, Premium, Exotik }

public record AnimalSpecies(
    string Id, string Name, string Emoji, Rarity Rarity, int BaseValue, SoundSegment[] Sound)
{
    public RarityInfo RarityInfo => Catalog.Rarities[Rarity];
}

public record RarityInfo(string Name, Brush Color, (BaitType Bait, int Count)[] ShelterReward);

/// <param name="Chances">Gewichte je Seltenheit: Gewöhnlich, Selten, Exotisch, Legendär.</param>
public record BaitInfo(BaitType Type, string Name, string Emoji, int Price, string Description, int[] Chances);

public record WheelOutcome(string Name, double Multiplier, Brush Color);

/// <summary>
/// Alle Spieldaten an einer Stelle. Neues Tier / neuer Köder = eine Zeile ergänzen.
/// </summary>
public static class Catalog
{
    private static Brush B(string hex)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    public static readonly Dictionary<Rarity, RarityInfo> Rarities = new()
    {
        [Rarity.Gewoehnlich] = new("Gewöhnlich", B("#7CB342"), new[] { (BaitType.Fleisch, 3) }),
        [Rarity.Selten] = new("Selten", B("#1E88E5"), new[] { (BaitType.Premium, 2) }),
        [Rarity.Exotisch] = new("Exotisch", B("#8E24AA"), new[] { (BaitType.Exotik, 1), (BaitType.Premium, 1) }),
        [Rarity.Legendaer] = new("Legendär", B("#FFB300"), new[] { (BaitType.Exotik, 4) }),
    };

    public static readonly BaitInfo[] Baits =
    {
        new(BaitType.Fleisch, "Fleischköder", "🍖", 10, "Günstig. Lockt vor allem gewöhnliche Tiere.", new[] { 70, 25, 5, 0 }),
        new(BaitType.Premium, "Premiumköder", "🥩", 40, "Bessere Chancen auf seltene Tiere.", new[] { 40, 40, 17, 3 }),
        new(BaitType.Exotik, "Exotikköder", "🍍", 120, "Sehr teuer. Exotische und legendäre Tiere!", new[] { 10, 35, 40, 15 }),
    };

    public static BaitInfo Bait(BaitType type) => Baits.First(b => b.Type == type);

    private static SoundSegment S(double f0, double f1, int ms, Wave w = Wave.Sine, double vol = 0.5,
        double vibHz = 0, double vibDepth = 0) => new(f0, f1, ms, w, vol, vibHz, vibDepth);

    private static SoundSegment P(int ms) => SoundSegment.Pause(ms);

    public static readonly AnimalSpecies[] Animals =
    {
        // Gewöhnlich
        new("zebra", "Zebra", "🦓", Rarity.Gewoehnlich, 30, new[]
            { S(600, 950, 130, Wave.Square, .3), P(40), S(800, 500, 140, Wave.Square, .3), P(60),
              S(600, 950, 130, Wave.Square, .3), P(40), S(800, 500, 160, Wave.Square, .3) }),
        new("affe", "Affe", "🐒", Rarity.Gewoehnlich, 25, new[]
            { S(300, 500, 150), P(60), S(300, 500, 150), P(60), S(500, 950, 220, Wave.Sine, .5, 9, 40), P(50),
              S(600, 1100, 300, Wave.Sine, .5, 9, 50) }),
        new("flamingo", "Flamingo", "🦩", Rarity.Gewoehnlich, 35, new[]
            { S(470, 430, 220, Wave.Square, .3), P(90), S(470, 430, 220, Wave.Square, .3), P(90),
              S(500, 420, 300, Wave.Square, .3) }),
        new("frosch", "Frosch", "🐸", Rarity.Gewoehnlich, 15, new[]
            { S(190, 120, 260, Wave.Square, .35, 30, 40), P(180), S(190, 120, 260, Wave.Square, .35, 30, 40) }),

        // Selten
        new("loewe", "Löwe", "🦁", Rarity.Selten, 100, new[]
            { S(130, 75, 1500, Wave.Growl, .7, 6, 12) }),
        new("giraffe", "Giraffe", "🦒", Rarity.Selten, 80, new[]
            { S(180, 185, 1300, Wave.Sine, .6, 4, 4), P(150), S(180, 170, 700, Wave.Sine, .6, 4, 4) }),
        new("krokodil", "Krokodil", "🐊", Rarity.Selten, 90, new[]
            { S(0, 0, 700, Wave.Noise, .3), P(100), S(65, 55, 900, Wave.Growl, .7, 3, 5) }),

        // Exotisch
        new("elefant", "Elefant", "🐘", Rarity.Exotisch, 180, new[]
            { S(400, 950, 300, Wave.Saw, .4), S(950, 820, 900, Wave.Saw, .4, 8, 35) }),
        new("nashorn", "Nashorn", "🦏", Rarity.Exotisch, 200, new[]
            { S(0, 0, 400, Wave.Noise, .45), P(150), S(0, 0, 300, Wave.Noise, .45), P(100), S(90, 70, 400, Wave.Growl, .6) }),
        new("nilpferd", "Nilpferd", "🦛", Rarity.Exotisch, 160, new[]
            { S(140, 90, 250, Wave.Growl, .6), P(100), S(140, 90, 250, Wave.Growl, .6), P(100), S(170, 80, 450, Wave.Growl, .6) }),

        // Legendär
        new("tiger", "Weißer Tiger", "🐅", Rarity.Legendaer, 500, new[]
            { S(170, 90, 900, Wave.Growl, .7, 5, 15), P(100), S(130, 80, 700, Wave.Growl, .7, 5, 10) }),
        new("gepard", "Goldener Gepard", "🐆", Rarity.Legendaer, 600, new[]
            // Geparden zirpen tatsächlich wie Vögel
            { S(1800, 2500, 90), P(90), S(1800, 2500, 90), P(90), S(1800, 2600, 110), P(250), S(1800, 2500, 90), P(90), S(1900, 2700, 130) }),
    };

    public static AnimalSpecies Animal(string id) => Animals.First(a => a.Id == id);

    // ---------- Glücksrad ----------

    public static readonly WheelOutcome Pech = new("Pech", 0.25, B("#E53935"));
    public static readonly WheelOutcome Normal = new("Normal", 0.75, B("#FB8C00"));
    public static readonly WheelOutcome Glueck = new("Glück", 1.25, B("#43A047"));
    public static readonly WheelOutcome Jackpot = new("Jackpot", 2.5, B("#FFD600"));

    public static readonly WheelOutcome[] WheelOutcomes = { Pech, Normal, Glueck, Jackpot };

    /// <summary>
    /// Die Felder des Rads. Jedes Feld ist gleich groß und gleich wahrscheinlich,
    /// d. h. die Anzahl der Felder legt die Wahrscheinlichkeit fest:
    /// Pech 6/20 = 30 %, Normal 8/20 = 40 %, Glück 5/20 = 25 %, Jackpot 1/20 = 5 %.
    /// Erwartungswert: 0,81 × Grundwert – das Glück gehört dem Händler.
    /// </summary>
    public static readonly WheelOutcome[] WheelSlices = "NPGNPNGPNJNPGNPNGPNG"
        .Select(c => c switch { 'P' => Pech, 'N' => Normal, 'G' => Glueck, _ => Jackpot })
        .ToArray();

    public static int Payout(AnimalSpecies species, WheelOutcome outcome)
        => (int)Math.Round(species.BaseValue * outcome.Multiplier);

    // ---------- Gehege ----------

    public const int StartEnclosures = 3;

    /// <summary>Preise der Gehege; 0 = von Anfang an frei.</summary>
    public static readonly int[] EnclosurePrices = { 0, 0, 0, 100, 250, 500 };

    public const int StartCoins = 100;
}
