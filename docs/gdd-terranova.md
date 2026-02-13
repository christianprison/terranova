# Terranova – Game Design Document (GDD)

> **Version**: 0.9
> **Zuletzt aktualisiert**: 2026-02-11
> **Status**: Konzeptphase

### Begleitdokumente
| Dokument | Inhalt |
|----------|--------|
| [epochs.md](epochs.md) | Alle 29 Epochen in 4 Ären, Übergangs-Mechanik |
| [biomes.md](biomes.md) | 20 Biom-Typen, Verteilungsregeln, Einfluss auf Entdeckungen |
| [research.md](research.md) | Forschungssystem, Entdeckungen pro Epoche (ab I.1 detailliert) |
| [terraforming.md](terraforming.md) | Einheitliche Mechanik, Baukosten-Rückkopplung, Ären-Progression |

---

## 1. Vision & Übersicht

### Elevator Pitch
Ein Echtzeit-Aufbaustrategiespiel für Tablets, in dem der Spieler eine Zivilisation durch 29 Epochen entwickelt – von der Werkzeug-Feuer-Kultur bis in die spekulative Zukunft – auf einem prozedural generierten Planeten mit veränderbarem Terrain. Individuelle Siedler wuseln durch die Welt, organisieren sich selbst und machen eigenständig Entdeckungen. Jede Facette des Planeten ist ein einzigartiges Biom, das bestimmt, welche Entdeckungen wahrscheinlich werden – keine Partie gleicht der anderen.

### Design Pillars
Die drei unverhandelbaren Kern-Erlebnisse:

1. **Aufbau-Faszination**: Der Spieler soll das befriedigende Gefühl erleben, eine Zivilisation von Grund auf wachsen zu sehen – von der ersten Hütte bis zur Metropole. Individuelle Siedler, die eigenständig arbeiten, handeln und leben, erzeugen den "Wuselfaktor", der zum Zuschauen und Optimieren einlädt.
2. **Strategische Tiefe durch Terrain**: Die Welt ist nicht nur Kulisse, sondern strategischer Faktor. Wo man baut, welches Biom man besiedelt und wie man das Terrain formt, hat echte Konsequenzen – bis hin zu den Baukosten und der Forschungsrichtung.
3. **Epochen-Progression**: Der Fortschritt durch die Epochen soll sich wie ein Zivilisationssprung anfühlen – neue Möglichkeiten, neues Aussehen, neue Strategien. Die Art zu forschen entwickelt sich selbst mit: von zufälligen Entdeckungen am Lagerfeuer bis zu systematischer Wissenschaft.

### Referenzspiele & Inspiration
| Spiel | Was wir übernehmen | Was wir anders machen |
|-------|-------------------|----------------------|
| Empire Earth | Epochen-System, Technologie-Progression | Probabilistisches Entdeckungssystem statt deterministischem Tech Tree |
| Die Siedler | Warenketten, Wuselfaktor, indirekte Steuerung | Prozeduraler Planet, Epochen-Progression |
| Anno 1800 | Komplexe Wirtschaftskreisläufe | Individuelle sichtbare Siedler statt abstrakter Bevölkerung |
| Northgard | Biom-Strategie, Übersichtlichkeit | Keine begrenzten Inseln sondern Planeten-Setup, Terraforming-Baukosten-Rückkopplung |
| Minecraft | Prozedurale Generierung, Biome, veränderbares Terrain | Kein Survival, kein First-Person, RTS-Optik statt Block-Look, einheitliches Terraforming statt freie Block-Manipulation |
| Civilization VI (iPad) | Referenz für komplexe Strategie auf Tablet | Echtzeit statt Rundenbasiert, prozeduraler Planet statt Hex-Karte |
| Planetary Annihilation | Spielbare Planetenoberfläche (Kugel-Geometrie) | Goldberg-Polyeder statt echte Kugel, volumetrisches Terrain statt Oberflächen-Mesh |

---

## 2. Kern-Systeme

### 2.1 Terrain & Welt

#### Weltgeometrie: Goldberg-Polyeder

> **Entscheidung (v0.4)**: Die Spielwelt hat die Form eines Goldberg-Polyeders – ein Körper aus Fünf- und Sechsecken, der eine Kugelform annähert.

Der Planet ist kein flaches Terrain und keine echte Kugel. Jede Facette (Fünf- oder Sechseck) ist intern flach – dort funktioniert unser Chunk-basiertes Terrain-System ohne Anpassung. Die Planetenform entsteht auf Makro-Ebene durch die Winkel zwischen den Facetten.

**Planetengröße** skaliert über Polyeder-Auflösung: Von GP(1,0) mit 32 Facetten (schnelle Partie) bis GP(4,0)+ mit 162+ Facetten (planetare Skala). Jede Facette = ein Biom.

**Zoom-Stufen**: Nah (Spielebene, flache Facette) → Mittel (LOD für Nachbarn) → Planetar (Facetten als Farbflächen, kein Terrain-Detail).

**Koordinatensystem**: Lokale 2D-Koordinaten pro Facette, globale Facetten-ID. Koordinaten-Transformation an Facetten-Kanten über Mercator-Projektion.
[DEFINIEREN: Details mit Unity Developer Agent – Wegfindung über Kanten, Facetten-Datenstruktur]

> **Hinweis für Unity Developer**: Vertical Slice zunächst mit einer einzelnen Facette, Polyeder-Integration als zweiter Meilenstein.

#### Terrain-System (intern: volumetrisches Chunk-System)

> **Entscheidung (v0.9)**: Das Terrain wird intern als volumetrische Datenstruktur gespeichert (Chunks aus Blöcken), die dynamisches Laden, Terraforming und Ressourcen im Untergrund ermöglicht. Das Rendering erzeugt daraus **geglättete, texturierte Meshes** – der Spieler sieht keine Blöcke.

- **Blockgröße (intern)**: 1x1x1 Meter Einheiten – die kleinste Einheit für Terraforming und Ressourcen-Daten
- **Chunk-Größe**: 16x16x256 Blöcke
- **Sichtweite**: 12 Chunks in jede Richtung (192m)
- **Terrain-Höhe**: 0-256 Blöcke (Meeresspiegel bei Block 64)
- **Facetten-Größe**: Kantenlänge = Sichtweite = 12 Chunks (192m). Von einer Ecke aus kann man genau bis zu den nächsten zwei Ecken sehen.
- **Dynamisches Laden**: Chunks werden erst generiert, wenn der Spieler sich dem Rand der sichtbaren Welt nähert

#### Art Style

> **Entscheidung (v0.9)**: Realistisch-stilisiert, orientiert an Empire Earth und Northgard. Kein Block-/Voxel-Look.

**Visuelles Ziel**: Die Welt soll wie ein lebendiger, realistisch anmutender Planet wirken – mit organischen Landschaftsformen, texturierten Oberflächen, natürlicher Vegetation und atmosphärischer Beleuchtung. Die RTS-Kamera zeigt eine Welt, die sich anfühlt wie Empire Earth oder Northgard, nicht wie Minecraft.

**Technik → Rendering**: Das interne volumetrische System (Blöcke/Chunks) ist eine Datenstruktur – wie eine Tabellenkalkulation hinter einer hübschen Grafik. Das Rendering erzeugt daraus geglättete Meshes mit Texturen, Normalsmaps und LOD. Terraforming-Operationen verändern die interne Datenstruktur, das Rendering aktualisiert das sichtbare Mesh entsprechend.

| Aspekt | Intern (Daten) | Visuell (Rendering) |
|--------|---------------|-------------------|
| Terrain | Block-Raster (1m³) | Geglättete, texturierte Landschaft |
| Terraforming | Blöcke entfernen/hinzufügen/transformieren | Organische Verformung sichtbar (Grube, Hügel, Feld) |
| Ressourcen | Blocktyp speichert Ressource (Erz, Lehm, etc.) | Visueller Hinweis an Oberfläche (Gesteinsfarbe, Vegetation) |
| Gebäude | Footprint in Blöcken | 3D-Modelle im Epochen-Stil |

#### Biome

Jede Facette wird bei Weltgenerierung einem Biom-Typ zugewiesen. 20 Biom-Typen: Grasland, Wald, Wüste, Tundra, Gebirge, Ozean, Küste, Regenwald, Steppe, Vulkanisch, Savanne, Sumpf/Moor, Taiga, Mangroven, Hochplateau, Flusstal/Aue, Korallenriff, Gletscher/Eiswüste, Karst, Fjord. Biome bestimmen nicht nur Ressourcen, sondern beeinflussen maßgeblich, welche Entdeckungen möglich sind (z.B. Gebirge → Feuerstein → Werkzeuge, Wald → verschiedene Holzarten → Reibungsfeuer).

→ **Details**: [biomes.md](biomes.md)

#### Erforschung & Kartenaufdeckung

> **Entscheidung (v0.5/v0.6)**: Erforschung jederzeit frei möglich. Reichweite durch Fortbewegungsgeschwindigkeit begrenzt. Sichtbarkeit durch Terrain und Bauwerke bestimmt.

Der Spieler kann von Beginn an Siedler in jede Richtung losschicken – zu Fuß, schwimmend, kletternd. Was die Erkundung begrenzt, ist die Reisegeschwindigkeit, Gefahren und die verfügbaren Fortbewegungsmittel (zu Fuß → Reittiere → Schiffe → Flugzeuge → Satelliten).

**Sichtlinien**: Sichtbarkeit hängt von physischen Sichtlinien ab – Siedler auf Hügeln sehen weiter, Wald blockiert Sicht, Bauwerke (Wachturm, Leuchtturm, Satellit) erweitern Sichtbereiche permanent. Drei Stufen: Unbekannt → Gesehen → Erkundet.

#### Terraforming

> **Entscheidung (v0.3/v0.6)**: Einheitliche Mechanik für alle Terrain-Modifikationen. Drei Operationen: Entfernen, Hinzufügen, Transformieren. Terrain beeinflusst Baukosten.

Bergwerke, Kanäle, Staudämme, Festungsgräben, Felder – alles Anwendungsfälle derselben drei Operationen. Spieler, Gebäude-Betrieb und Events (Erdbeben, GenAI) nutzen dasselbe System. Bauen am Hang ist möglich, aber teurer – das Vorschau-System zeigt den Terraforming-Aufwand beim Platzieren.

→ **Details**: [terraforming.md](terraforming.md)

#### Prozedurale Generierung

> **Entscheidung (v0.8)**: Drei-Ebenen-Generierung mit jeweils eigenem Algorithmus, um auf jeder Skala den am besten geeigneten Ansatz zu verwenden.

| Ebene | Skala | Aufgabe | Algorithmus-Ansatz |
|-------|-------|---------|-------------------|
| **Facetten-Verteilung** | Planet (Makro) | Biom-Zuweisung auf Polyeder-Facetten, klimatische Logik (Pole, Äquator, Ozeane) | [DEFINIEREN – Regelbasiert, WFC (Wave Function Collapse), Constraint-basiert?] |
| **Chunk-Terrain** | Facette (Meso) | Höhenprofil, Geländeformen (Berge, Täler, Flüsse, Seen) innerhalb einer Facette | [DEFINIEREN – Perlin/Simplex Noise, Erosions-Simulation, Hydraulic Erosion?] |
| **Block-Detail** | Chunk (Mikro) | Ressourcenverteilung, Vegetationsplatzierung, Höhlenstrukturen, Erz-Adern | [DEFINIEREN – Cellular Automata, Poisson Disk Sampling, Noise-basiert?] |

**Seed-System**: Ein Master-Seed bestimmt alle drei Ebenen deterministisch. Spieler kann Seeds eingeben für reproduzierbare Welten.

---

### 2.2 Epochen-System

> **Entscheidung (v0.5)**: 29 Epochen in 4 Ären, definiert durch technologische Durchbrüche.

**4 Ären**: Frühgeschichte (10 Epochen: Werkzeug-Feuer bis Weberei), Antike & Vormoderne (8: Rad bis Buchdruck), Industrie & Moderne (8: Dampf bis KI/Robotik), Spekulative Zukunft (3: AGI, Interstellar, Post-Biologisch).

**Epochen-Übergänge** sind fließend: Nicht eine einzelne Schlüsseltechnologie, sondern eine Schwelle von thematisch passenden Entdeckungen löst den Übergang aus. Welche konkreten Entdeckungen das sind, variiert von Partie zu Partie.

→ **Details**: [epochs.md](epochs.md)

---

### 2.3 Ressourcen & Wirtschaft

> **Entscheidung (v0.8)**: Ressourcen bilden einen Abhängigkeitsbaum. Die Wurzel sind sammelbare Ressourcen – alles andere erfordert Wissen, Werkzeuge, Gebäude, spezialisierte Arbeiter oder andere Ressourcen.

#### Ressourcen-Hierarchie

**Sammelbare Ressourcen (Root)** – können ohne Voraussetzungen direkt aus der Umgebung gewonnen werden:
Steine, Stöcke, Beeren, Kräuter, Wasser, Lehm, Pflanzenfasern, Muscheln, Federn, etc.
Was genau sammelbar ist, hängt vom Biom ab.

**Verarbeitete Ressourcen** – erfordern jeweils eine Kombination aus:

| Voraussetzung | Beispiel |
|---------------|---------|
| **Wissen** (Entdeckung nötig) | Feuer → Kochen möglich. Lederbearbeitung → Felle werden zu Leder. |
| **Werkzeuge** | Steinwerkzeug → Holz fällen möglich. Axt → effizienter als Faustkeile. |
| **Gebäude** | Brennofen → Keramik. Schmelzhütte → Bronze. |
| **Spezialisierte Arbeiter** | Steinmetz, Bäcker, Schmied – Siedler mit erlerntem Beruf. |
| **Andere Ressourcen** | Bronze = Kupfer + Zinn. Brot = Mehl + Wasser + Feuer. |

**Beispiel-Kette (Ära I):**
```
Sammelbar: Stöcke, Steine → (Entdeckung: Verbundwerkzeug) → Steinaxt
Steinaxt + Bäume → Holz (Stämme)
Holz + (Entdeckung: Feuer) → Holzkohle
Wildtier + Jäger mit Speer → Fleisch + Fell
Fell + (Entdeckung: Lederbearbeitung) + Werkzeug → Leder
```

Jede neue Entdeckung eröffnet neue Äste im Ressourcenbaum. Die Warenketten wachsen organisch mit den Entdeckungen, nicht über vordefinierte Epochen-Kategorien. Waren werden von individuellen Siedlern physisch transportiert – Wege, Entfernungen und Lagerkapazitäten sind strategisch relevant.

---

### 2.4 Gebäude

[DEFINIEREN pro Ära/Epoche]

**Beispiel Epoche I.1** (Werkzeug-Feuer-Kultur):

| Gebäude | Funktion | Kosten |
|---------|----------|--------|
| Lagerfeuer | Zentrum, Sammelstelle | 5 Holz |
| Holzfällerhütte | Produziert Holz | 10 Holz, 5 Stein |
| Jägerhütte | Produziert Nahrung | 8 Holz |
| Einfache Hütte | Wohnraum (2 Siedler) | 15 Holz, 5 Stein |

---

### 2.5 Bevölkerungssystem

> **Entscheidung (v0.2/v0.8)**: Individuelle Siedler mit indirekter Steuerung, vollständigem Lebenszyklus und Wissensweitergabe.

**Kernprinzip: Indirekte Steuerung**. Der Spieler steuert niemals einzelne Siedler direkt. Stattdessen beeinflusst er das Verhalten indirekt durch Gebäude-Platzierung, Berufsausbildung, Prioritäten, Infrastruktur, Ausrüstung und Leitlinien.

**Siedler-Eigenschaften**: Beruf, Fähigkeiten (verbessern sich über Zeit), Bedürfnisse, Alter, Gesundheit, Zufriedenheit, Wissen (erlernte Entdeckungen/Fähigkeiten).

#### Lebenszyklus

> **Entscheidung (v0.8)**: Siedler werden geboren, altern und sterben. Fortpflanzung und Wissensweitergabe sind Kern-Mechaniken.

**Fortpflanzung** erfordert:
- Zufriedenheit (Grundbedürfnisse erfüllt)
- Unterkunft (Wohnraum für Familie)
- Gesundheit (kein Hunger, keine Krankheit)

**Alter & Tod**: Siedler durchlaufen Lebensphasen (Kind → Erwachsener → Alter). Kinder können nicht arbeiten, müssen ernährt werden, lernen aber von den Erwachsenen. Alte Siedler werden langsamer, sterben aber mit viel Erfahrung/Wissen. Tod durch Alter, Hunger, Krankheit, Unfälle, wilde Tiere.

**Wissensweitergabe & Vererbung**: Eltern geben Fähigkeiten und erlerntes Wissen an Kinder weiter. Kinder starten nicht bei Null – sie profitieren von der Erfahrung der Eltern. Stirbt ein erfahrener Siedler, bevor er sein Wissen weitergeben konnte, geht dieses Wissen verloren (bis Höhlenmalerei als Speicher wirkt, ab I.3). Siedler mit langer Berufserfahrung geben bessere Fähigkeiten weiter als Novizen.

**Designkonsequenz**: Bevölkerungswachstum wird zu einer strategischen Ressource. Der Spieler muss in Zufriedenheit, Wohnraum und Gesundheit investieren, um Nachwuchs zu bekommen. Wissensweitergabe macht alte Siedler wertvoll – ihr Tod ist ein echter Verlust.

**Emergentes Gameplay**: Gebäude weit von Ressourcen = längere Wege = ineffizienter. Der Spieler lernt, strategisch zu platzieren.

#### Bevölkerungsskalierung

> **Entscheidung (v0.8)**: In frühen Epochen maximal ~100 individuelle Siedler. In späteren Epochen Übergang zu Bevölkerungsgruppen, um höhere Zahlen zu ermöglichen.

- **Ära I–II**: Jeder Siedler ist individuell simuliert (max. ~100)
- **Ab Ära III**: [DEFINIEREN – Abstraktionskonzept für Bevölkerungsgruppen, die als Einheit agieren, während Schlüssel-Individuen weiter individuell bleiben?]

---

### 2.6 Einheiten & Konflikt

Einheiten sind Siedler mit spezialisiertem Beruf. Beispiele: Holzfäller, Jäger, Baumeister, Kundschafter (alle ab I.1), Fischer (I.5), Bauer (I.7).

> **Entscheidung (v0.8)**: Kein dediziertes Kampfsystem. Konfliktverhalten entsteht emergent aus Selbstverteidigung und Jagd.

**Grundverhalten**: Jeder Siedler verteidigt sich selbst, wenn er angegriffen wird (von Wildtieren, Naturgefahren, später von feindlichen Siedlern). Benachbarte Siedler eilen zu Hilfe.

**Emergente Kampfentwicklung**:
- **Ära I**: Selbstverteidigung + Nachbarschaftshilfe. Koordinierte Jagdtechniken (Treibjagd → koordinierter Angriff) entstehen als Entdeckungen.
- **Spätere Ären**: Jagd-Koordination wird zu Kriegsstrategie weiterentwickelt. Aus "Treibjagd" wird "Flankenmanöver", aus "Fallen stellen" wird "Hinterhalt".
- [DEFINIEREN: Ab wann gibt es feindliche Siedlungen/NPCs?]

---

### 2.7 Forschung & Entdeckungen

> **Entscheidung (v0.6/v0.7)**: Kein Tech Tree. Probabilistisches Entdeckungssystem. Die Art zu forschen entwickelt sich selbst über die Epochen.

**Ära I (Frühgeschichte)**: Keine aktive Forschung. Entdeckungen durch Beobachtung, Imitation, Trial-and-Error. Zwei Entdeckungstypen: **biom-getrieben** (was in der Umgebung ist) und **aktivitäts-getrieben** (was die Siedler tun). Beide überlappen sich. Ab I.3 wirken Höhlenmalereien als Wissensmultiplikator.

**Ära II+ (ab Schrift)**: Erste gezielte Forschung möglich. Spieler weist Forschungsgebiete zu, Ergebnisse bleiben probabilistisch.

**Epochen-Übergänge**: Nicht durch eine Schlüsseltechnologie, sondern durch eine Schwelle thematisch passender Entdeckungen. Verschiedene Entdeckungs-Kombinationen können denselben Übergang auslösen.

→ **Details**: [research.md](research.md)

---

## 3. Spielerfahrung

### 3.1 Kamera & Steuerung

> **Entscheidung (v0.2)**: Tablet-first. Touch-Steuerung als primäre Eingabemethode.

**Kamera**: RTS-Perspektive (schräg von oben), frei drehbar, stufenlos zoombar. Von Übersichtsperspektive bis Nahansicht einzelner Siedler.

**Touch**: Ein-Finger-Pan, Zwei-Finger-Rotation, Pinch-Zoom, Tap-Select, Long-Press-Kontextmenü. Referenz: Civilization VI iPad.

**Design-Grundsatz**: Komplexität der Systeme auf PC-Niveau – die Interaktion ist tablet-optimiert, nicht der Inhalt.

### 3.2 UI/HUD

> **Designprinzip: Layered UI** – Informationen in Schichten organisiert.

- **Layer 1 (immer sichtbar)**: Ressourcen-Leiste, Minimap, Epochen-Indikator, Speed-Widget, Benachrichtigungen
- **Layer 2 (bei Bedarf)**: Bau-Menü, Objekt-Info-Panel, Prioritäten-Panel
- **Layer 3 (Vollbild)**: Forschungsübersicht, Statistiken, Welt-Karte

**Regeln**: Min. 44x44pt Touch-Targets, keine Hover-States, wichtige Aktionen in Daumenzone.

### 3.3 Spielmotivation & Systeminteraktion

> **Entscheidung (v0.8)**: Kein klassischer Core Loop. Motivation entsteht durch emergente Situationen aus interagierenden Systemen – wie in RimWorld oder Dwarf Fortress.

Die Systeme (Terrain, Bevölkerung, Ressourcen, Entdeckungen, Biome, Events) interagieren frei miteinander. Es gibt keinen vorgeschriebenen Zyklus, sondern eine Dynamik, die sich aus den Umständen ergibt:

- Terrain bestimmt Ressourcen → Ressourcen bestimmen mögliche Entdeckungen → Entdeckungen eröffnen neue Ressourcen
- Bevölkerungswachstum erzeugt neue Bedürfnisse → Spieler muss reagieren → neue Gebäude, neue Entdeckungen
- Entdeckungen verändern, was möglich ist → Spieler passt Strategie an
- Katastrophen/Events verändern die Situation → Spieler muss umdenken
- Tod erfahrener Siedler → Wissensverlust → Druck zur Wissenssicherung (Höhlenmalerei, Schrift)

**Motivation entsteht durch**: Überraschende Situationen, die der Spieler nicht geplant hat. "Mein bester Jäger ist gestorben und hat sein Wissen nicht weitergegeben." "Ein Blitzschlag hat uns Feuer gebracht, aber auch den halben Wald abgefackelt." "Wir haben Bronze entdeckt, aber kein Zinn in der Nähe."

**Tablet-spezifisch**: Unterstützt aktive Sessions (Aufbau, Reaktion auf Krisen) und passive Momente (Siedlung beobachten, Wuselfaktor genießen).

### 3.4 Spielgeschwindigkeit

> **Entscheidung (v0.3)**: Vier Stufen: Pause (0x), Normal (1x), Schnell (2x), Sehr Schnell (3x).

**Pause als Planungswerkzeug**: Gebäude platzieren, Aufträge in Warteschlange, Terraforming markieren – alles ohne Zeitdruck. Beim Fortsetzen: "Jetzt geht's los"-Moment.

[DEFINIEREN: Automatische Verlangsamung bei Events – Opt-in oder Standard?]

---

## 4. Zukunftsvision

### 4.1 GenAI-Events

> Spätere Ausbaustufe. Alle Kernsysteme so designt, dass Integration möglich ist.

Generative KI erzeugt einzigartige Spielereignisse (Naturkatastrophen, Diplomatie, Entdeckungen, kulturelle Umbrüche) basierend auf Spielstand, Verlauf und Zufall. Nutzt dasselbe Event-System wie spontane Entdeckungen und Terraforming-Events.

**Technisch**: Event-Bus-System, standardisiertes Event-Schema, Validierung von GenAI-Output, Fallback auf vordefinierte Events.

### 4.2 AR/MR Local Multiplayer

> Langfristige Vision. Spielwelt als MR-Diorama auf einem Tisch. Mehrere Headsets und/oder iPads, verschiedene Perspektiven.

**Zielplattform**: Meta Quest 3 oder vergleichbare HMDs für Mixed Reality, parallel zu iPad-Version.

**Voraussetzungen**: Netzwerk-fähige Architektur, MR-Kamera-Modus, saubere Trennung Simulation/Darstellung.

---

## 5. Technische Rahmenbedingungen

| Aspekt | Entscheidung |
|--------|-------------|
| Engine | Unity (2022 LTS / Unity 6) – [DEFINIEREN: Welche Version?] |
| Sprache | C# |
| Render Pipeline | URP |
| Zielplattform | iPad (primär), perspektivisch Meta Quest 3 / vergleichbare HMDs für MR-Multiplayer |
| Min. Spezifikation | iPad mit M4-Prozessor oder höher (Modell MWR63NF/A) |
| Terrain-System | Volumetrisches Chunk-System (intern), geglättetes Mesh-Rendering (visuell) |
| Networking | Singleplayer first. Architektur für spätere Multiplayer-Erweiterung. |
| Save System | [DEFINIEREN] |
| Input | Touch (primär), Apple Pencil (optional), Quest-Controller (MR-Modus) |

---

## 6. Geschäftsmodell

> **Entscheidung (v0.4)**: Free-to-Play-Kern mit Epochen als In-App-Purchases und integriertem Community-Crowdfunding.

### Grundprinzip: Bezahle für die Zukunft, nicht für die Vergangenheit

Startepoche I.1 + alle fertig entwickelten Epochen = **kostenlos**. Zukünftige Epochen als einzelne IAPs oder Ären-Bundles. 29 Epochen = bis zu 28 potenzielle IAPs. Kein Pay-to-Win.

### Integriertes Community-Crowdfunding

In-Game "Weltkongress"-Bereich: Spieler sehen kommende Epochen, stimmen über Prioritäten ab, tragen finanziell bei, verfolgen Fortschritt. Backer-Belohnungen: nur kosmetisch.

**Prinzipien**: Fair, transparent, community-driven, nachhaltig.

[DEFINIEREN: Preismodell, Crowdfunding-Backend, Offline-Fähigkeit]

---

## 7. Offene Fragen & Entscheidungen

### Entschieden

| Nr. | Frage | Entscheidung |
|-----|-------|-------------|
| 1 | Singleplayer/Multiplayer? | Singleplayer first. MP als Ausbaustufe. (v0.2) |
| 2 | Wie viele Epochen? | 29 in 4 Ären. (v0.5) |
| 3 | Terrain-Freiheit? | Einheitliche Terraforming-Mechanik. (v0.3/v0.6) |
| 4 | Kampfsystem? | Kein dediziertes System. Emergent aus Selbstverteidigung und Jagd-Koordination. (v0.8) |
| 6 | Bevölkerungssystem? | Individuelle Siedler, indirekte Steuerung, Lebenszyklus mit Wissensweitergabe. (v0.2/v0.8) |
| 7 | Kartengröße? | Goldberg-Polyeder, skalierbare Auflösung. (v0.4) |
| 9 | Spielname? | Terranova. (v0.2) |
| 10 | Min. iPad-Spezifikation? | M4-Prozessor oder höher (MWR63NF/A). (v0.8) |
| 12 | Bevölkerungswachstum? | Geburten (erfordert Zufriedenheit + Unterkunft + Gesundheit). Alter und Tod simuliert. Wissensweitergabe an Kinder. (v0.8) |
| 13 | Maximale Siedlerzahl? | Frühe Epochen: max. ~100 individuell. Spätere Epochen: Bevölkerungsgruppen-Abstraktion. (v0.8) |
| 18 | Facetten-Größe? | Kantenlänge = Sichtweite = 12 Chunks (192m). Von Ecke zu Ecke sichtbar. (v0.8) |
| 19 | Koordinaten-Transformation? | Mercator-Projektion an Facetten-Kanten. (v0.8) |
| 20 | Simulation entfernter Facetten? | Nachbar-Facetten erkundeter Grenzen: simuliert. Weiter als 1 Facette entfernt: nicht simuliert. (v0.8) |
| 24 | Forschungsgebiete? | Wird in research.md epochenweise definiert. (v0.8) |
| 25 | Pechschutz? | Ja – garantierte Entdeckung nach X Aktivitätszyklen. Konkreter Wert per Playtesting. (v0.8) |
| 27 | Sichtlinien-Komplexität? | Hoch – Erkundung ist der Hauptreiz des Spiels. Echte Sichtlinienberechnung. (v0.8) |
| 30 | Biome: Welche 20 Typen? | 10 bestehende + 10 neue: Savanne, Sumpf/Moor, Taiga, Mangroven, Hochplateau, Flusstal/Aue, Korallenriff, Gletscher/Eiswüste, Karst, Fjord. (v0.8) |
| 5 | Art Style? | Realistisch-stilisiert (Empire Earth / Northgard). Intern volumetrisches Chunk-System, visuell geglättete texturierte Meshes. Kein Block-Look. (v0.9) |

### Offen

| Nr. | Frage | Priorität |
|-----|-------|-----------|
| 8 | Unity Version: 2022 LTS oder Unity 6? | Hoch |
| 11 | Apple Pencil Support? | Niedrig |
| 14 | Wasserfluss-Simulation Komplexität? | Mittel |
| 15 | Bodenstabilität (Hangrutsch, Tunnel-Abstützung)? | Niedrig |
| 16 | Auto-Verlangsamung bei Events? | Niedrig |
| 17 | Terraforming-Nachnutzung Freiheitsgrad? | Mittel |
| 21 | IAP-Preismodell? | Mittel |
| 22 | Crowdfunding-Backend? | Mittel |
| 23 | Offline-Fähigkeit? | Mittel |
| 26 | Spontane Entdeckungen: Häufigkeit, negative Folgen? | Mittel |
| 28 | Wetter & Tageszeit? | Mittel |
| 29 | Terrain-Baukosten: Pro Block oder vereinfachte Zonen? | Mittel |
| 31 | Bevölkerungsgruppen-Abstraktion (ab Ära III): Wie genau? | Mittel |
| 32 | Ab wann feindliche Siedlungen/NPCs? | Mittel |

---

## Änderungslog

| Version | Datum | Änderung |
|---------|-------|----------|
| 0.1 | 2026-02-11 | Erste Struktur erstellt |
| 0.2 | 2026-02-11 | iPad als Zielplattform. Individuelle Siedler. Spielname Terranova. Touch-UI. |
| 0.3 | 2026-02-11 | Gerichtetes Terraforming. Spielgeschwindigkeit mit aktivem Pause-Modus. |
| 0.4 | 2026-02-11 | Goldberg-Polyeder als Weltgeometrie. Geschäftsmodell (Epochen als IAP, Crowdfunding). |
| 0.5 | 2026-02-11 | 29 Epochen in 4 Ären. Bewegungsbasierte Erforschung. Voxel-Werte konkretisiert. |
| 0.6 | 2026-02-11 | Sichtlinien statt Reveal-Moment. Einheitliches Terraforming. Baukosten-Rückkopplung. Probabilistisches Forschungssystem. |
| 0.7 | 2026-02-11 | Dokument-Restrukturierung in Begleitdokumente. Forschungssystem: Ära I emergent, Höhlenmalerei, flexible Epochenübergänge. |
| 0.8 | 2026-02-11 | Breites Update: Northgard-Differenzierung. 20 Biome. Drei-Ebenen-Generierung. Ressourcen-Abhängigkeitsbaum. Bevölkerungs-Lebenszyklus. Emergenter Kampf. Interagierende Systeme statt Core Loop. Quest 3 als MR-Plattform. Viele Fragen entschieden. |
| 0.9 | 2026-02-13 | **Art Style entschieden**: Realistisch-stilisiert (Empire Earth / Northgard). Durchgängige Bereinigung der "Voxel"-Terminologie: Trennung von interner Datenstruktur (volumetrisches Chunk-System) und visuellem Rendering (geglättete, texturierte Meshes). Kein Block-/Minecraft-Look. "Voxel-System" → "Terrain-System" mit Art-Style-Sektion. Frage #5 entschieden. |
