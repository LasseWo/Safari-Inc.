using System.IO;
using System.Text.Json;
using SafariInc.Model;

namespace SafariInc.Game;

/// <summary>Einfache Speicherung des Spielstands als JSON in %AppData%\SafariInc.</summary>
public static class SaveGame
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SafariInc", "spielstand.json");

    private record SaveData(
        int Coins,
        Dictionary<BaitType, int> Baits,
        int UnlockedEnclosures,
        List<SavedAnimal> Animals);

    private record SavedAnimal(string SpeciesId, int EnclosureIndex);

    public static void Save(GameState state)
    {
        var data = new SaveData(
            state.Coins,
            state.Baits.ToDictionary(b => b.Info.Type, b => b.Count),
            state.Enclosures.Count(e => e.IsUnlocked),
            state.Enclosures.Where(e => e.Animal != null)
                .Select(e => new SavedAnimal(e.Animal!.Species.Id, e.Index)).ToList());

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException)
        {
            // Speichern ist nice-to-have; das Spiel läuft weiter.
        }
    }

    /// <summary>Lädt den Spielstand in <paramref name="state"/>. Gibt false zurück, wenn es keinen gibt.</summary>
    public static bool TryLoad(GameState state)
    {
        try
        {
            if (!File.Exists(FilePath)) return false;
            var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(FilePath));
            if (data == null) return false;

            state.Coins = data.Coins;
            foreach (var bait in state.Baits)
                bait.Count = data.Baits.GetValueOrDefault(bait.Info.Type);
            foreach (var enclosure in state.Enclosures)
                enclosure.IsUnlocked = enclosure.Index < data.UnlockedEnclosures;
            foreach (var saved in data.Animals)
            {
                var species = Catalog.Animals.FirstOrDefault(a => a.Id == saved.SpeciesId);
                if (species != null && saved.EnclosureIndex < state.Enclosures.Count)
                    state.PlaceAnimal(new ZooAnimal(species), state.Enclosures[saved.EnclosureIndex]);
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return false;
        }
    }
}
