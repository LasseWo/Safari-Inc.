using System.IO;
using System.Media;

namespace SafariInc.Audio;

public enum Wave { Sine, Square, Saw, Noise, Growl }

/// <summary>Ein Abschnitt eines synthetischen Geräuschs: Frequenz gleitet von F0 nach F1.</summary>
public record SoundSegment(
    double F0, double F1, int Ms,
    Wave Wave = Wave.Sine, double Volume = 0.5,
    double VibratoHz = 0, double VibratoDepth = 0)
{
    public static SoundSegment Pause(int ms) => new(0, 0, ms, Wave.Sine, 0);
}

/// <summary>
/// Spielt Geräusche ab. Liegt im Ordner "Sounds" neben der .exe eine Datei "&lt;id&gt;.wav",
/// wird diese benutzt (echte Tieraufnahmen). Sonst wird ein Geräusch synthetisch erzeugt.
/// </summary>
public static class SoundService
{
    private const int SampleRate = 22050;

    private static readonly Dictionary<string, byte[]> Cache = new();
    private static readonly Random Rng = new();
    private static SoundPlayer? _current; // Referenz halten, sonst räumt der GC beim Abspielen auf

    public static void Play(string id, IReadOnlyList<SoundSegment> fallback)
    {
        try
        {
            _current?.Stop();
            string file = Path.Combine(AppContext.BaseDirectory, "Sounds", id + ".wav");
            if (File.Exists(file))
            {
                _current = new SoundPlayer(file);
            }
            else
            {
                if (!Cache.TryGetValue(id, out var wav))
                    Cache[id] = wav = Synthesize(fallback);
                _current = new SoundPlayer(new MemoryStream(wav));
            }
            _current.Play();
        }
        catch (Exception)
        {
            // Kein Audiogerät o. ä. – das Spiel läuft auch ohne Ton weiter.
        }
    }

    public static void Stop() => _current?.Stop();

    // ---------- Effekte ----------

    public static void Coin() => Play("_coin", new[]
    {
        new SoundSegment(988, 988, 70, Wave.Square, 0.25),
        new SoundSegment(1319, 1319, 260, Wave.Square, 0.25),
    });

    public static void Jackpot() => Play("_jackpot", new[]
    {
        new SoundSegment(523, 523, 120, Wave.Square, 0.25),
        new SoundSegment(659, 659, 120, Wave.Square, 0.25),
        new SoundSegment(784, 784, 120, Wave.Square, 0.25),
        new SoundSegment(1047, 1047, 500, Wave.Square, 0.25, 6, 8),
    });

    public static void Fail() => Play("_fail", new[]
    {
        new SoundSegment(400, 300, 250, Wave.Saw, 0.3),
        new SoundSegment(300, 150, 500, Wave.Saw, 0.3),
    });

    public static void Success() => Play("_success", new[]
    {
        new SoundSegment(659, 659, 110, Wave.Square, 0.22),
        new SoundSegment(880, 880, 110, Wave.Square, 0.22),
        new SoundSegment(1319, 1319, 300, Wave.Square, 0.22),
    });

    public static void Jeep() => Play("_jeep", new[]
    {
        new SoundSegment(55, 75, 900, Wave.Growl, 0.45, 12, 6),
        new SoundSegment(75, 60, 1300, Wave.Growl, 0.45, 12, 6),
    });

    public static void Locked() => Play("_locked", new[]
    {
        new SoundSegment(200, 200, 90, Wave.Square, 0.25),
        SoundSegment.Pause(40),
        new SoundSegment(200, 200, 90, Wave.Square, 0.25),
    });

    // ---------- Synthese ----------

    private static byte[] Synthesize(IReadOnlyList<SoundSegment> segments)
    {
        var samples = new List<short>();
        double phase = 0;

        foreach (var s in segments)
        {
            int n = SampleRate * s.Ms / 1000;
            int fade = Math.Min(SampleRate * 15 / 1000, n / 2);

            for (int i = 0; i < n; i++)
            {
                double t = (double)i / n;
                double freq = s.F0 + (s.F1 - s.F0) * t;
                if (s.VibratoHz > 0)
                    freq += Math.Sin(2 * Math.PI * s.VibratoHz * i / SampleRate) * s.VibratoDepth;
                phase += 2 * Math.PI * freq / SampleRate;

                double cycle = phase / (2 * Math.PI) % 1.0;
                double v = s.Wave switch
                {
                    Wave.Sine => Math.Sin(phase),
                    Wave.Square => Math.Sin(phase) >= 0 ? 1 : -1,
                    Wave.Saw => 2 * cycle - 1,
                    Wave.Noise => Rng.NextDouble() * 2 - 1,
                    Wave.Growl => (2 * cycle - 1) * (0.5 + 0.5 * Rng.NextDouble()),
                    _ => 0
                };

                double env = 1.0;
                if (i < fade) env = (double)i / fade;
                else if (i > n - fade) env = (double)(n - i) / fade;

                samples.Add((short)(v * env * s.Volume * short.MaxValue));
            }
        }

        return ToWav(samples);
    }

    private static byte[] ToWav(List<short> samples)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int dataBytes = samples.Count * 2;

        w.Write("RIFF"u8.ToArray());
        w.Write(36 + dataBytes);
        w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray());
        w.Write(16);                 // Länge fmt-Block
        w.Write((short)1);           // PCM
        w.Write((short)1);           // Mono
        w.Write(SampleRate);
        w.Write(SampleRate * 2);     // Bytes pro Sekunde
        w.Write((short)2);           // Block-Ausrichtung
        w.Write((short)16);          // Bits pro Sample
        w.Write("data"u8.ToArray());
        w.Write(dataBytes);
        foreach (short s in samples) w.Write(s);

        w.Flush();
        return ms.ToArray();
    }
}
