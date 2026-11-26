using System.Windows;
using System.Windows.Media;
using SafariInc.Audio;

namespace SafariInc.Model;

public enum Rarity { Gewoehnlich, Selten, Exotisch, Legendaer }

public enum BaitType { Fleisch, Premium, Exotik }

/// <param name="Name">Wird nie angezeigt, nur für Screenreader (AutomationProperties.Name).</param>
/// <param name="Color">
/// WPF zeichnet Emojis einfarbig. Die Farbe des Tieres macht es ohne Namen erkennbar.
/// </param>
public record AnimalSpecies(
    string Id, string Name, string Emoji, Brush Color, Rarity Rarity, int BaseValue, SoundSegment[] Sound)
{
    public RarityInfo RarityInfo => Catalog.Rarities[Rarity];
}

/// <param name="ShowerReward">Köder für die erste Dusche eines Tieres dieser Seltenheit.</param>
public record RarityInfo(string Stars, Brush Color, (BaitType Bait, int Count) ShowerReward)
{
    /// <summary>Die Belohnung der Dusche als Bilder, z. B. 🍍🍍.</summary>
    public string RewardIcons =>
        string.Concat(Enumerable.Repeat(Catalog.Bait(ShowerReward.Bait).Emoji, ShowerReward.Count));
}

/// <param name="Name">Wird nie angezeigt, nur für Screenreader (AutomationProperties.Name).</param>
/// <param name="Stars">Mehr Sterne = lockt seltenere Tiere an.</param>
/// <param name="Chances">Gewichte je Seltenheit: Gewöhnlich, Selten, Exotisch, Legendär.</param>
public record BaitInfo(BaitType Type, string Name, string Emoji, int Price, string Stars, int[] Chances);

public record WheelOutcome(string Face, double Multiplier, Brush Color);

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
        [Rarity.Gewoehnlich] = new("★", B("#2E7D32"), (BaitType.Fleisch, 1)),
        [Rarity.Selten] = new("★★", B("#1E88E5"), (BaitType.Premium, 1)),
        [Rarity.Exotisch] = new("★★★", B("#8E24AA"), (BaitType.Exotik, 1)),
        [Rarity.Legendaer] = new("★★★★", B("#FFB300"), (BaitType.Exotik, 2)),
    };

    public static readonly BaitInfo[] Baits =
    {
        new(BaitType.Fleisch, "Fleischköder", "🍖", 10, "★", new[] { 70, 25, 5, 0 }),
        new(BaitType.Premium, "Premiumköder", "🥩", 40, "★★", new[] { 40, 40, 17, 3 }),
        new(BaitType.Exotik, "Exotikköder", "🍍", 120, "★★★", new[] { 10, 35, 40, 15 }),
    };

    public static BaitInfo Bait(BaitType type) => Baits.First(b => b.Type == type);

    private static SoundSegment S(double f0, double f1, int ms, Wave w = Wave.Sine, double vol = 0.5,
        double vibHz = 0, double vibDepth = 0) => new(f0, f1, ms, w, vol, vibHz, vibDepth);

    private static SoundSegment P(int ms) => SoundSegment.Pause(ms);

    public static readonly AnimalSpecies[] Animals =
    {
        // Gewöhnlich
        new("zebra", "Zebra", "🦓", B("#37474F"), Rarity.Gewoehnlich, 30, new[]
            { S(600, 950, 130, Wave.Square, .3), P(40), S(800, 500, 140, Wave.Square, .3), P(60),
              S(600, 950, 130, Wave.Square, .3), P(40), S(800, 500, 160, Wave.Square, .3) }),
        new("affe", "Affe", "🐒", B("#795548"), Rarity.Gewoehnlich, 25, new[]
            { S(300, 500, 150), P(60), S(300, 500, 150), P(60), S(500, 950, 220, Wave.Sine, .5, 9, 40), P(50),
              S(600, 1100, 300, Wave.Sine, .5, 9, 50) }),
        new("flamingo", "Flamingo", "🦩", B("#EC407A"), Rarity.Gewoehnlich, 35, new[]
            { S(470, 430, 220, Wave.Square, .3), P(90), S(470, 430, 220, Wave.Square, .3), P(90),
              S(500, 420, 300, Wave.Square, .3) }),
        new("frosch", "Frosch", "🐸", B("#1B5E20"), Rarity.Gewoehnlich, 15, new[]
            { S(190, 120, 260, Wave.Square, .35, 30, 40), P(180), S(190, 120, 260, Wave.Square, .35, 30, 40) }),

        // Selten
        new("loewe", "Löwe", "🦁", B("#E65100"), Rarity.Selten, 100, new[]
            { S(130, 75, 1500, Wave.Growl, .7, 6, 12) }),
        new("giraffe", "Giraffe", "🦒", B("#F57F17"), Rarity.Selten, 80, new[]
            { S(180, 185, 1300, Wave.Sine, .6, 4, 4), P(150), S(180, 170, 700, Wave.Sine, .6, 4, 4) }),
        new("krokodil", "Krokodil", "🐊", B("#33691E"), Rarity.Selten, 90, new[]
            { S(0, 0, 700, Wave.Noise, .3), P(100), S(65, 55, 900, Wave.Growl, .7, 3, 5) }),

        // Exotisch
        new("elefant", "Elefant", "🐘", B("#607D8B"), Rarity.Exotisch, 180, new[]
            { S(400, 950, 300, Wave.Saw, .4), S(950, 820, 900, Wave.Saw, .4, 8, 35) }),
        new("nashorn", "Nashorn", "🦏", B("#455A64"), Rarity.Exotisch, 200, new[]
            { S(0, 0, 400, Wave.Noise, .45), P(150), S(0, 0, 300, Wave.Noise, .45), P(100), S(90, 70, 400, Wave.Growl, .6) }),
        new("nilpferd", "Nilpferd", "🦛", B("#7E57C2"), Rarity.Exotisch, 160, new[]
            { S(140, 90, 250, Wave.Growl, .6), P(100), S(140, 90, 250, Wave.Growl, .6), P(100), S(170, 80, 450, Wave.Growl, .6) }),

        // Legendär
        new("tiger", "Weißer Tiger", "🐅", B("#00ACC1"), Rarity.Legendaer, 500, new[]
            { S(170, 90, 900, Wave.Growl, .7, 5, 15), P(100), S(130, 80, 700, Wave.Growl, .7, 5, 10) }),
        new("gepard", "Goldener Gepard", "🐆", B("#FFB300"), Rarity.Legendaer, 600, new[]
            // Geparden zirpen tatsächlich wie Vögel
            { S(1800, 2500, 90), P(90), S(1800, 2500, 90), P(90), S(1800, 2600, 110), P(250), S(1800, 2500, 90), P(90), S(1900, 2700, 130) }),
    };

    public static AnimalSpecies Animal(string id) => Animals.First(a => a.Id == id);

    // ---------- Glücksrad ----------

    public static readonly WheelOutcome Pech = new("😢", 0.25, B("#E53935"));
    public static readonly WheelOutcome Normal = new("😐", 0.75, B("#FB8C00"));
    public static readonly WheelOutcome Glueck = new("😀", 1.25, B("#43A047"));
    public static readonly WheelOutcome Jackpot = new("⭐", 2.5, B("#FFD600"));

    public static readonly WheelOutcome[] WheelOutcomes = { Pech, Normal, Glueck, Jackpot };

    /// <summary>
    /// Die Felder des Rads. Jedes Feld ist gleich groß und gleich wahrscheinlich,
    /// d. h. die Anzahl der Felder legt die Wahrscheinlichkeit fest:
    /// Pech 2/8 = 25 %, Normal 3/8 = 37,5 %, Glück 2/8 = 25 %, Jackpot 1/8 = 12,5 %.
    /// Erwartungswert: 0,97 × Grundwert – das Glück gehört weiter dem Händler, nur knapper.
    /// </summary>
    public static readonly WheelOutcome[] WheelSlices = "NPGNPGNJ"
        .Select(c => c switch { 'P' => Pech, 'N' => Normal, 'G' => Glueck, _ => Jackpot })
        .ToArray();

    public static int Payout(int baseValue, WheelOutcome outcome)
        => (int)Math.Round(baseValue * outcome.Multiplier);

    // ---------- Freispiele ----------

    /// <summary>Jedes so vielte gefangene Tier bringt ein Freispiel. Falsch raten nimmt nichts weg.</summary>
    public const int CatchesPerFreeSpin = 3;

    /// <summary>Grundwert eines Freispiels: kostet kein Tier und zahlt immer aus (10, 30, 50 oder 100 Münzen).</summary>
    public const int FreeSpinBaseValue = 40;

    // ---------- Gehege ----------

    public const int StartEnclosures = 3;

    /// <summary>Preise der Gehege; 0 = von Anfang an frei.</summary>
    public static readonly int[] EnclosurePrices = { 0, 0, 0, 100, 250, 500 };

    /// <summary>Wo die Gehege auf der Zoo-Karte liegen (linke obere Ecke). Je Eintrag in EnclosurePrices einer.</summary>
    public static readonly Point[] EnclosureSpots =
    {
        new(30, 30), new(275, 20), new(520, 34),
        new(50, 400), new(295, 412), new(540, 396),
    };

    public const int StartCoins = 100;
}
