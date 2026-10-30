# Safari Inc. – Das Glück gehört dem Händler

WPF-Spiel (.NET 8) zur Lernsituation „Interaktives Spiel für Kinder“ mit Routed Events.

## Starten

```
dotnet run
```

Oder `SafariInc.csproj` in Visual Studio öffnen und mit F5 starten.

## Spielablauf

1. **Köder wählen** (Safari): Fleisch-, Premium- und Exotikköder mit unterschiedlichen Chancen pro Seltenheit
2. **Auf Safari:** Der Jeep fährt los, danach erklingt ein Tiergeräusch
3. **Tier erraten:** 10 Sekunden Zeit, die Silhouette wird mit der Zeit schärfer. Falsche Antwort = Köder weg
4. **Zoo:** 6 Gehege (3 frei, 3 kaufbar). Ein Klick auf ein Tier spielt sein Geräusch ab
5. **Händler:** sicher verkaufen, Glücksrad drehen (×0,25 / ×0,75 / ×1,25 / ×2,5) oder ans Tierheim abgeben (gibt Köder)

Der Spielstand wird automatisch in `%AppData%\SafariInc\spielstand.json` gespeichert.

## Aufbau

| Ordner / Datei | Aufgabe |
|---|---|
| `Events/GameEvents.cs` | Registrierung aller Routed Events + EventArgs |
| `Game/GameManager.cs` | Spielregeln. Hängt sich per `AddHandler` an das Fenster |
| `Game/SaveGame.cs` | Speichern/Laden als JSON |
| `Model/Catalog.cs` | Alle Spieldaten: Tiere, Köder, Seltenheiten, Glücksrad, Gehegepreise |
| `Model/GameState.cs` | Spielstand (Data Binding mit `INotifyPropertyChanged`) |
| `Audio/SoundService.cs` | Geräusche: WAV-Datei aus `Sounds/` oder synthetisch erzeugt |
| `Views/*` | Safari, Zoo und Händler als UserControls |

## Routed Events

**Prinzip:** Die Views kennen keine Spielregeln. Sie lösen ein Ereignis aus, das per **Bubbling** durch den
Elementbaum bis zum Fenster wandert. Dort hört der `GameManager` zu.

```
Button  →  ItemsControl  →  SafariView  →  Grid  →  MainWindow (GameManager)
```

| Routed Event | Strategie | Ausgelöst von | Aufgabe |
|---|---|---|---|
| `PreviewBaitUsed` | **Tunnel** | SafariView | Prüft vorab: Köder da? Zoo voll? Kann abbrechen (`Handled = true`) |
| `BaitUsed` | Bubble | SafariView | Köder abziehen, Tier würfeln, Safari starten |
| `BaitBought` | Bubble | SafariView | Köder kaufen |
| `AnimalGuessed` | Bubble | SafariView | Antwort prüfen (`null` = Zeit abgelaufen) |
| `AnimalCaptured` | Bubble | GameManager | Tier ist im Zoo → Ergebnis anzeigen |
| `HuntFailed` | Bubble | GameManager | Falsch geraten → Ergebnis anzeigen |
| `RouletteRequested` | Bubble | MerchantView | GameManager legt das Ergebnis fest, die View animiert das Rad |
| `AnimalSold` | Bubble | MerchantView | Tier verkaufen (sicher oder Glücksrad) und aus dem Zoo entfernen |
| `AnimalDonated` | Bubble | MerchantView | Tierheim: Tier gegen Köder tauschen |
| `EnclosureBought` | Bubble | ZooView | Gehege kaufen |
| `CurrencyChanged` | Bubble | GameManager | Münzstand geändert → „+125“-Animation + Sound |
| `GameMessage` | Bubble | alle | Meldung in der Statuszeile |
| `NewGameRequested` | Bubble | MainWindow | Spiel zurücksetzen |

### Weitere Stellen, an denen Routing genutzt wird

- **Bubbling von `Button.Click`:** Navigation, Köderkarten, Antwortbuttons, Gehege und Verkaufsbuttons haben
  jeweils **einen** gemeinsamen Handler am Container (`ButtonBase.Click="..."`). Welcher Button gedrückt wurde,
  steht in `e.OriginalSource`.
- **Tunneling von `PreviewMouseLeftButtonDown`:**
  - `MainWindow`: blockiert die Navigation, solange eine Safari oder das Glücksrad läuft
  - `MerchantView`: verschluckt jeden Klick in der View, während sich das Rad dreht
- **Rückgabewerte über EventArgs:** `BaitUsed` und `RouletteRequested` werden synchron ausgeführt. Der
  GameManager schreibt sein Ergebnis (`Hunt`, `SliceIndex`) in die EventArgs, und die View liest es danach aus.

## Erweitern

- **Neues Tier:** eine Zeile in `Catalog.Animals` (optional `Sounds/<id>.wav` ablegen)
- **Neuer Köder:** Wert im Enum `BaitType` + eine Zeile in `Catalog.Baits`
- **Glücksrad ändern:** die Feld-Zeichenkette `WheelSlices` in `Catalog.cs` (jedes Feld = 5 % Chance)
- **Neue Aktion:** Routed Event in `GameEvents` registrieren, in der View mit `RaiseEvent` auslösen,
  im `GameManager` per `AddHandler` behandeln
