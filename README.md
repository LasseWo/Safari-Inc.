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
3. **Tier erraten:** 15 Sekunden Zeit, die Silhouette wird mit der Zeit schärfer. Falsche Antwort = Köder weg
4. **Zoo:** Karte mit 6 Gehegen (3 frei, 3 kaufbar), in denen die Tiere herumlaufen. Ein Klick auf ein Tier spielt sein Geräusch ab. Alles Weitere läuft über Ziehen: 🚿 Dusche (einmal je Tier Köder, das Tier bleibt), 💰 Verkauf (sicherer Preis), 🎡 Glücksrad (×0,25 / ×0,75 / ×1,25 / ×2,5, öffnet sich als Popup) oder ein freies Gehege
5. **Freispiele:** Jedes 3. gefangene Tier bringt einen kostenlosen Dreh am Glücksrad (10 bis 100 Münzen). Er kostet kein Tier, und falsch raten nimmt keinen Fortschritt weg

Der Spielstand wird automatisch in `%AppData%\SafariInc\spielstand.json` gespeichert.

## Spielen ohne Lesen

Das Spiel kommt ohne Text aus, die Kinder müssen nicht lesen können. Zahlen gibt es nur als Zusatz bei den
Münzen, gespielt werden kann auch ohne sie.

| Bild | Bedeutung |
|---|---|
| 🚙 / 🏞 | Safari / Zoo |
| ★ bis ★★★★ | Seltenheit: mehr Sterne = seltener und wertvoller. Bei Ködern: lockt seltenere Tiere an |
| 🍖🍖🍖 | Vorrat als Bilder: drei Köder = drei Bilder |
| ➕ 🪙 10 | Kaufen für 10 Münzen |
| 🔊 / ⏳ | Geräusch nochmal hören / verbleibende Zeit |
| ✔ / ✖ | geschafft / nicht geschafft |
| 💰 / 🎡 | Stationen auf der Karte: sicher verkaufen / Glücksrad. Beim Ziehen zeigen sie, was das Tier dort bringt |
| 😢 😐 😀 ⭐ | Felder des Glücksrads. Der Balken zeigt, wie viele Münzen es jeweils gibt |
| 🚿 / ✨ | Dusche auf der Karte: Die erste Dusche bringt Köder (★ 🍖, ★★ 🥩, ★★★ 🍍, ★★★★ 🍍🍍) / dieses Tier ist schon geduscht |
| 👆 | Hand auf der Karte: macht das Ziehen vor, bis das Kind zum ersten Mal selbst zieht |
| 🔥🔥🔥 ➜ 🎡 | Weg zum Freispiel: Jedes gefangene Tier lässt eine Flamme aufleuchten |
| 🎡 1 | Verdientes Freispiel. Ein Klick dreht sofort das Rad |
| 🔄 | Neues Spiel, mit Rückfrage: ✖ (abbrechen) oder 🗑 (alles löschen) |

So führt das Spiel ohne Worte:

- **Pulsieren:** Was als Nächstes dran ist, pulsiert (`Views/Attention.cs`). Was dran ist, berechnet
  `GameState.RefreshHints()`.
- **Abdunkeln:** Was gerade nicht geht, ist blass. Es bleibt klickbar und antwortet mit Ton und Bild-Meldung.
- **Bild-Meldungen:** `GameMessage` trägt nur noch Emojis, z. B. `🪙 ✖` (zu wenig Münzen). Sie erscheinen groß in
  der Mitte und verschwinden wieder.

## Belohnungen

| Anlass | Effekt |
|---|---|
| Tier gefangen | Konfetti (mehr Sterne = mehr Konfetti), eine Flamme fliegt nach oben, der Jubel klingt mit jeder Flamme höher |
| Legendäres Tier, Jackpot | Konfetti und Regen über das ganze Fenster, Fanfare |
| Freispiel verdient | Regen über das ganze Fenster, Fanfare, große Bild-Meldung |
| Münzen bekommen | Münzen fliegen zur Anzeige, mehr Gewinn = mehr Münzen |
| Glücksrad dreht | Klickt bei jedem Feld und wird mit dem Rad langsamer |
| Tier im Zoo angetippt | Herzen |
| Dusche | Wassertropfen und Rauschen, danach ✨ am Tier |
| Gehege gebaut, Glück am Rad | Konfetti |

Bewusst nicht eingebaut: manipulierte Beinahe-Treffer, Countdowns und Strafen für Pausen. Das Rad bleibt fair, und
ein Freispiel zahlt immer aus.

## Bilder und Farben

WPF zeichnet Emojis einfarbig. Darum hat jedes Tier eine eigene Farbe (`AnimalSpecies.Color`), damit es auch ohne
Namen erkennbar ist. Die Namen stehen weiter im Katalog, werden aber nur noch für Screenreader benutzt.

## Aufbau

| Ordner / Datei | Aufgabe |
|---|---|
| `Events/GameEvents.cs` | Registrierung aller Routed Events + EventArgs |
| `Game/GameManager.cs` | Spielregeln. Hängt sich per `AddHandler` an das Fenster |
| `Game/SaveGame.cs` | Speichern/Laden als JSON |
| `Model/Catalog.cs` | Alle Spieldaten: Tiere, Köder, Seltenheiten, Glücksrad, Gehegepreise |
| `Model/GameState.cs` | Spielstand (Data Binding mit `INotifyPropertyChanged`) |
| `Audio/SoundService.cs` | Geräusche: WAV-Datei aus `Sounds/` oder synthetisch erzeugt |
| `Views/*` | Safari, Zoo-Karte und Glücksrad-Popup als UserControls |
| `Views/Attention.cs` | Pulsieren, Hüpfen, Federn, Herumlaufen |
| `Views/Celebration.cs` | Belohnungseffekte: Konfetti, Regen, fliegende Münzen |

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
| `RouletteRequested` | Bubble | WheelPopup | GameManager legt das Ergebnis fest, das Popup animiert das Rad |
| `AnimalSold` | Bubble | ZooView, WheelPopup | Tier verkaufen (sicher oder Glücksrad) und aus dem Zoo entfernen |
| `AnimalWashed` | Bubble | ZooView | Dusche: Die erste bringt Köder, das Tier bleibt im Zoo |
| `AnimalMoved` | Bubble | ZooView | Tier in ein freies Gehege umsetzen |
| `AnimalWheelRequested` | Bubble | ZooView | Fenster öffnet das Glücksrad-Popup und dreht für dieses Tier |
| `EnclosureBought` | Bubble | ZooView | Gehege kaufen |
| `CurrencyChanged` | Bubble | GameManager | Münzstand geändert → „+125“-Animation + Sound |
| `GameMessage` | Bubble | alle | Bild-Meldung groß in der Mitte |
| `FreeSpinRequested` | Bubble | WheelPopup | GameManager legt das Ergebnis fest, das Popup animiert das Rad |
| `FreeSpinCompleted` | Bubble | WheelPopup | Rad steht: Freispiel abziehen und auszahlen |
| `FreeSpinEarned` | Bubble | GameManager | Freispiel verdient → Fanfare und Regen |
| `Celebration` | Bubble | Views | Belohnungseffekt, startet am auslösenden Element |
| `NewGameRequested` | Bubble | MainWindow | Spiel zurücksetzen |

### Weitere Stellen, an denen Routing genutzt wird

- **Bubbling von `Button.Click`:** Navigation, Köderkarten, Antwortbuttons und Gehege haben jeweils **einen**
  gemeinsamen Handler am Container (`ButtonBase.Click="..."`). Welcher Button gedrückt wurde, steht in
  `e.OriginalSource`.
- **Tunneling von `PreviewMouseLeftButtonDown`:**
  - `MainWindow`: blockiert die Navigation, solange eine Safari oder das Glücksrad läuft
  - `WheelPopup`: verschluckt jeden Klick im Popup, während sich das Rad dreht
  - `ZooView`: sieht die Maus vor den Gehege-Buttons und entscheidet, ob es ein Klick oder ein Ziehen wird
- **Rückgabewerte über EventArgs:** `BaitUsed` und `RouletteRequested` werden synchron ausgeführt. Der
  GameManager schreibt sein Ergebnis (`Hunt`, `SliceIndex`) in die EventArgs, und die View liest es danach aus.

## Erweitern

- **Neues Tier:** eine Zeile in `Catalog.Animals` mit Emoji und Farbe (optional `Sounds/<id>.wav` ablegen)
- **Neuer Köder:** Wert im Enum `BaitType` + eine Zeile in `Catalog.Baits`
- **Glücksrad ändern:** die Feld-Zeichenkette `WheelSlices` in `Catalog.cs` (alle Felder gleich groß, die Anzahl legt die Chance fest)
- **Neue Station auf der Karte:** `Border` mit neuem `Tag` in `ZooView.xaml`, im Konstruktor von `ZooView` eintragen und in `DropAnimal()` das passende Routed Event auslösen
- **Gehege verschieben:** `Catalog.EnclosureSpots`
- **Neue Aktion:** Routed Event in `GameEvents` registrieren, in der View mit `RaiseEvent` auslösen,
  im `GameManager` per `AddHandler` behandeln
