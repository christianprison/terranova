# Deep Epoch I.1: Werkzeug-Feuer-Kultur – Game Design Document

> **Version**: 0.2
> **Autor**: Producer + Producer Assistant
> **Basiert auf**: GDD v0.9, research.md, epochs.md
> **Ziel**: Eine realistische, tiefe Steinzeit-Simulation mit universellen Mechaniken die über alle Epochen tragen

---

## 1. Vision

> "Dein kleiner Stamm kommt nach langer Wanderung in einer unbekannten Landschaft an. Alles was ihr habt sind eure Hände und ein paar Faustkeile. Überlebt."

Terranova Epoch I.1 ist **keine Städtebausimulation mit Steinzeit-Skin**. Es ist eine Überlebenssimulation, in der der Spieler einen kleinen Stamm durch die früheste Phase menschlicher Zivilisation führt. Der Spieler kontrolliert nicht direkt – er gibt Aufträge, setzt Prioritäten und reagiert auf das, was die Welt und seine Siedler ihm zeigen.

### Drei Säulen

| Säule | Bedeutung in I.1 |
|-------|-------------------|
| **Entdecken** | Siedler lernen durch Tun – und durch Scheitern. Jede Entdeckung schaltet neue Möglichkeiten frei. Der Spieler weiß nicht im Voraus, was entdeckt wird. |
| **Entwickeln** | Von Faustkeilen zu Verbundwerkzeugen. Von Aasfressern zu Jägern. Von Felsvorsprüngen zu Windschirmen. Jeder Schritt fühlt sich verdient an. |
| **Terraforming** | Die Landschaft formt das Gameplay – und das Gameplay formt langsam die Landschaft. Trampelpfade, Lichtungen, Feuerstellen. |

### Designprinzipien

1. **Die Natur ist der Gegner und der Verbündete.** Kein Feind-System nötig – Hunger, Durst, Kälte, Verletzungen und Unwissen sind tödlich genug.
2. **Materialien haben Eigenschaften, nicht nur Mengen.** "10 Holz" gibt es nicht. Es gibt Birkenholz (weich, brennt gut), Eichenholz (hart, für Werkzeuge), Weidenruten (biegsam, für Flechtwerk).
3. **Wissen ist die wertvollste Ressource.** Ein Stamm mit Feuerstein-Wissen überlebt besser als einer mit doppelt so vielen Siedlern.
4. **Scheitern ist Lernen.** Vergiftung, Verletzung, Hunger – jeder Rückschlag treibt Entdeckungen voran.
5. **Jede Partie erzählt eine andere Geschichte.** Durch Biom-Unterschiede, probabilistische Entdeckungen und Terrain-Randomisierung.
6. **Der Spieler soll staunen.** Wenn seine Siedler zum ersten Mal Feuer machen, soll sich das episch anfühlen.

---

## 2. Der universelle Game Loop

Dieses Muster gilt für **jede Epoche** von I.1 bis IV.3. Nur die Vokabeln ändern sich.

```
SITUATION → AUFTRAG → AKTION (mit verfügbaren Mitteln) → ERGEBNIS → ERFAHRUNG → ENTDECKUNG → NEUE MÖGLICHKEITEN
                                                            ↓
                                                     Erfolg ODER Fehlschlag
                                                     (beides erzeugt Erfahrung)
```

### Konkretes Beispiel I.1

```
SITUATION:    Hunger, Siedler sieht Wurzeln am Boden
AUFTRAG:      "Alle sammeln am Flussufer"
AKTION:       Graben mit Händen (langsam, ineffizient)
ERGEBNIS:     Erfolg (Wurzel gefunden) ODER Fehlschlag (giftige Wurzel → krank)
ERFAHRUNG:    +Sammeln, +Pflanzenkenntnis
ENTDECKUNG:   "Grabstock" (angespitzter Ast macht Graben effizienter)
NEUE MÖGLICHKEIT: Neues Prädikat "Graben mit Grabstock", neue Ressource "Grabstock" herstellbar
```

### Dasselbe Muster in späteren Epochen

```
I.7:  Siedler beobachtet Samen → Ackerbau entdeckt → Prädikat "Anpflanzen" freigeschaltet
II.4: Schmelzt Kupfer + Zinn → Bronze entdeckt → Prädikat "Schmieden" freigeschaltet
III.1: Wasserrad + Hitze → Dampfmaschine → Prädikat "Fabrik betreiben" freigeschaltet
```

### Ergebnis-Typen

| Typ | Häufigkeit | Effekt | Entdeckungs-Bonus |
|-----|------------|--------|-------------------|
| **success** | Häufig | Ressource gewonnen, Aufgabe erledigt | Normal (+1 Erfahrung) |
| **failure** | Regelmäßig | Krank, verletzt, Ressource verloren | Erhöht (+1.5 Erfahrung) |
| **critical_failure** | Selten | Schwer verletzt, Tod möglich | Stark erhöht (+2 Erfahrung) |
| **discovery** | Alle paar Minuten | Neue Fähigkeit/Ressource/Struktur | – |
| **major_discovery** | 3–5 pro Partie | Spielverändernde Entdeckung (Feuer, Verbundwerkzeuge) | – |

**Kernprinzip: Fehlschläge treiben Entdeckungen schneller an als Erfolge.** Ein Stamm der nie Probleme hat, entwickelt sich langsam. Ein Stamm der leidet, lernt schnell. Das ist historisch korrekt und spielerisch elegant.

### Fehlschlag-Beispiele

| Situation | Ohne Wissen | Fehlschlag | Erfahrungsgewinn | Mögliche Entdeckung |
|---|---|---|---|---|
| Hungrig, findet Wurzel | Isst sie roh | Giftig → krank | +Pflanzen, +Gift | "Essbare vs. giftige Pflanzen" |
| Wildschwein greift an | Kein Werkzeug | Verletzt | +Jagd, +Gefahr | "Keulen zur Verteidigung" |
| Beeren sammeln | Unbekannte Beeren | Manche machen krank | +Pflanzen | "Heilpflanzen" |
| Fisch fangen mit Händen | Greift ins Wasser | Fehlschlag, nass, kalt | +Fischen | "Fischfallen" |
| Stein bearbeiten | Falscher Winkel | Stein zerbricht nutzlos | +Steinbearbeitung | "Gesteinskunde" |
| Nacht ohne Schutz | Offenes Gelände | Kälte → krank | +Überleben | "Schutz suchen" (Priorität) |

**Der Spieler soll denken: "Mist, Mira wurde vergiftet... aber jetzt wissen wir welche Beeren giftig sind."**

---

## 3. Auftrags-Grammatik

### Prinzip: Spieler steuert durch Sätze

Statt ein Baumenü mit 15 Gebäuden gibt es **Aufträge**, formuliert als natürlichsprachige Sätze. Die verfügbaren Satzbausteine wachsen mit den Entdeckungen.

```
[WER]     +  [TUT]      +  [WAS/WO + ...]
Subjekt      Prädikat      Objekt(e)
```

Ein Auftrag kann **mehrere Objekte** haben:
- "Nächster Freier baut **Windschirm** + **hier**" (Struktur + Ort)
- "Kael bearbeitet **Feuerstein** + **Hartholz** + **Harz**" (Materialien kombinieren)
- "Alle sammeln **Beeren** + **am Flussufer**" (Ressource + Ort)

### 3.1 Beispiele I.1

| Subjekt | Prädikat | Objekt(e) | Auftrag |
|---|---|---|---|
| Alle | Sammeln | Beeren + am Bach | "Alle sammeln Beeren am Bach" |
| Kael | Erkunden | Richtung Norden | "Kael erkundet Richtung Norden" |
| Sammler | Meiden | Waldlichtung | "Sammler meiden die Waldlichtung" |
| Nächster Freier | Bauen | Windschirm + hier | "Nächster Freier baut Windschirm hier" |
| Alle | NICHT Jagen | – | "Alle jagen nicht" |
| Mira | Bearbeiten | Feuerstein + am Felsen | "Mira bearbeitet Feuerstein am Felsen" |
| Kael | Herstellen | Verbundwerkzeug + Feuerstein + Hartholz + Harz | "Kael stellt Verbundwerkzeug her aus Feuerstein, Hartholz und Harz" |

### 3.2 Beispiele spätere Epochen (gleiche Grammatik!)

| Epoche | Subjekt | Prädikat | Objekt(e) |
|---|---|---|---|
| I.7 | Bauern | Anpflanzen | Weizen + Feld Nord |
| II.1 | Transporteur | Liefern | Holz + zur Schmiede |
| II.4 | Schmied | Schmelzen | Kupfer + Zinn + im Brennofen |
| III.1 | Arbeiter | Bedienen | Dampfmaschine + Fabrik 3 |

**Die Vokabeln wachsen, die Grammatik bleibt identisch.**

### 3.3 Subjekt (WER) – wächst mit Stammgröße

| I.1 (5 Siedler) | I.7+ (50 Siedler) | II.3+ (200 Siedler) |
|---|---|---|
| Alle | Alle Bauern | Alle in Sektor Nord |
| [Name] | [Name] | [Name] |
| Nächster Freier | Nächste 3 Freie | Arbeitsgruppe "Holz" |
| Sammler | Jäger | Berufsgruppe "Schmiede" |
| – | – | Gesetz: "Jeder neue Erwachsene" |

### 3.4 Prädikat (TUT / TUT NICHT) – wächst mit Entdeckungen

| Spielstart | Nach Entdeckungen I.1 | Spätere Epochen |
|---|---|---|
| Sammeln | Sammeln (spezifisch) | Produzieren |
| Erkunden | Jagen | Handeln |
| Meiden | Bauen | Forschen |
| – | Bearbeiten | Transportieren |
| – | Herstellen | Verwalten |
| – | Trocknen / Räuchern | Verbieten (Gesetz) |
| – | Bewachen | Verteidigen |
| – | Kochen (nach Feuer) | – |

**Negation** ist immer verfügbar: Jedes Prädikat hat ein "NICHT".

### 3.5 Objekt (WAS / WO / WOMIT) – wächst mit Erkundung und Wissen

| Spielstart | Nach Entdeckungen | Spätere Epochen |
|---|---|---|
| "Hier" (Tap-Position) | Beeren + am Hang | Weizen + Feld 3 |
| "Richtung Nord" | Feuerstein + am Felsvorsprung | Kupfererz + Mine 2 |
| "Alles in der Nähe" | Windschirm + hier | Waren + Stadt B |
| – | Hartholz + Harz + Schnur | Stahl + Kohle + Hochofen |

### 3.6 Wie Entdeckungen neue Wörter freischalten

```
Spielstart:
  Prädikate: [Sammeln, Erkunden, Meiden]
  Objekte:   [Hier, Richtung, Alles in der Nähe]

Nach "Gesteinskunde":
  Objekte:   + [Feuerstein, Sandstein, Granit]  (vorher nur "Stein")

Nach "Keulen zur Verteidigung":
  Prädikate: + [Jagen (klein)]

Nach "Flechtwerk":
  Prädikate: + [Bauen]
  Objekte:   + [Windschirm, Korb]

Nach "Feuer":
  Prädikate: + [Kochen, Räuchern]
  Objekte:   + [Feuerstelle]

Nach "Verbundwerkzeug":
  Prädikate: + [Fällen, Graben, Herstellen]
  Objekte:   + [Baum, Grube, Verbundwerkzeug + Materialien]
```

**Der Spieler sieht seine Entdeckungen direkt in seinen Möglichkeiten.** Gestern konnte er nur "Sammeln" – heute kann er "Jagen", "Bauen" und "Herstellen".

### 3.7 UI: Kontextuelles Zusammenbauen

Kein Menü mit 50 Einträgen. Stattdessen kontextabhängig:

**Variante A: Ort-First (Tap auf Boden)**

```
1. Spieler tappt auf Flussufer
   → Zeigt verfügbare PRÄDIKATE für diesen Ort:
     [Sammeln] [Erkunden] [Meiden] [Bauen...]

2. Spieler wählt "Sammeln"
   → Zeigt verfügbare OBJEKTE an diesem Ort:
     [Beeren] [Wurzeln] [Schilf] [Alles]

3. Spieler wählt "Wurzeln"
   → Zeigt SUBJEKT-Auswahl:
     [Alle] [Nächster Freier] [Mira] [Kael]

4. Fertig: "Mira sammelt Wurzeln am Flussufer"
```

**Variante B: Siedler-First (Tap auf Siedler)**

```
1. Spieler tappt auf Kael
   → Info-Panel + [Neuer Auftrag]

2. "Neuer Auftrag" → Zeigt was Kael kann:
     [Sammeln] [Steine bearbeiten] [Erkunden]

3. Spieler wählt "Erkunden"
   → Kamera wird aktiv: "Wohin?"
   → Spieler tappt Richtung

4. Fertig: "Kael erkundet Richtung Nordwest"
```

**Variante C: Long Press (Struktur bauen)**

```
1. Spieler hält auf leerem Boden gedrückt
   → "Was kann ich hier bauen?"
   → Zeigt NUR was mit aktuellem Wissen + Materialien möglich ist
   → Am Anfang vielleicht nur [Sammelstelle markieren]
   → Später: [Windschirm] [Feuerstelle] [Trockengestell]

2. Spieler wählt "Windschirm"
   → Zeigt benötigte Materialien: Äste + Gras
   → Zeigt SUBJEKT: [Nächster Freier] [Kael] [Mira]

3. Fertig: "Nächster Freier baut Windschirm hier"
```

### 3.8 Auftrags-Übersicht (ab größeren Stämmen)

```
┌─────────────────────────────────────────────────┐
│ Aktive Aufträge                            [+]  │
│─────────────────────────────────────────────────│
│ ✓ Alle sammeln am Bach              [pausieren] │
│ ⚡ Kael erkundet Norden               [läuft...] │
│ ✓ Sammler meiden Waldlichtung       [aufheben]  │
│ ⏳ Mira baut Windschirm hier            [30%]   │
│ ✕ Alle: nicht jagen                 [aufheben]  │
└─────────────────────────────────────────────────┘
```

Sortierbar nach Subjekt, Prädikat, Objekt oder Status.

### 3.9 Datenmodell

```
OrderDefinition:
  subject:   SubjectType     // All | Named(settler) | Role | NextFree
  predicate: PredicateDefinition
  objects:   ObjectDefinition[]   // Ein oder mehrere!
  negated:   bool
  priority:  int
  status:    Active | Paused | Complete | Failed

PredicateDefinition:
  name:              "Sammeln"
  unlockedBy:        null | DiscoveryDefinition
  requiredTool:      null | ToolCategory
  experienceCategory: "Sammeln"

ObjectDefinition:
  name:       "Wurzeln"
  type:       Resource | Location | Structure | Material
  position:   WorldPosition | null
  unlockedBy: null | DiscoveryDefinition
```

---

## 4. Startbedingungen

### 4.1 Der Stamm

- **5 Siedler** (Erwachsene, gemischt)
- Jeder hat: Name, Alter, Hunger, Durst, Gesundheit, Erfahrungswerte pro Aktivität
- **Startausrüstung**: Primitive Faustkeile (Qualitätsstufe 1)
- **Startwissen**: Grundlegendes Steinschlagen
- **Start-Prädikate**: Sammeln, Erkunden, Meiden
- **Start-Objekte**: Hier, Richtung, Alles in der Nähe

### 4.2 Die Landschaft

**Zufällig generiert** – jede Partie einzigartig, aber mit Garantien:

| Garantiert | Variiert |
|---|---|
| Trinkwasser in Startnähe | Bach ODER See ODER Wasserfall ODER Quelle |
| Natürlicher Unterschlupf | Höhle ODER Felsvorsprung ODER dichtes Unterholz |
| Grundnahrung in Reichweite | Beeren, Wurzeln, Insekten (biom-abhängig) |
| Mindestens eine Steinquelle | Art des Gesteins variiert → bestimmt Werkzeugpfad |

**Biom bestimmt das Startgefühl:**
- Welche Wasserquelle → Zugang zu Lehm, Fisch, Schilf
- Welcher Unterschlupf → Kapazität, Schutzqualität
- Welche Gesteinsarten → Werkzeugpfad
- Welche Vegetation → Nahrungspfad und Entdeckungen

### 4.3 Die Start-Story

> Nach langer Wanderung hat der Stamm einen Ort gefunden, der Hoffnung macht. Wasser ist in der Nähe. Ein [Felsvorsprung / eine Höhle / dichtes Unterholz] bietet Schutz für die Nacht. Aber die Siedler kennen dieses Land nicht. Welche Pflanzen sind essbar? Wo findet man guten Stein? Was lauert im Wald?

---

## 5. Ressourcen & Materialien

### Prinzip: Materialien statt Mengen

Jedes Material hat **Eigenschaften**. Der Spieler und seine Siedler entdecken diese Eigenschaften im Lauf der Zeit. Am Anfang ist ein Stein nur ein Stein. Nach der Entdeckung "Gesteinskunde" unterscheiden die Siedler Feuerstein, Sandstein, Granit.

### 5.1 Holz & Pflanzen

**Wichtig**: Vor dem Verbundwerkzeug (Beil) fällt niemand einen Baum. Siedler sammeln **Totholz und brechen Äste ab**.

| Material | Vorkommen | Sammelmethode | Eigenschaften | Nutzen |
|---|---|---|---|---|
| Totholz / Äste | Überall mit Bäumen | Aufheben, Abbrechen | Leicht verfügbar | Grabstöcke, Keulen, Windschirme, Feuerholz |
| Hartholz (Eiche, Buche) | Wald, Mischwald | Äste abbrechen, erst mit Beil fällen | Hart, schwer, langsam brennend | Werkzeuggriffe, Keulen, Grabstöcke. Langlebig. |
| Weichholz (Birke, Kiefer) | Wald, Flussufer | Äste abbrechen | Leicht, biegsam, schnell brennend | Feuerholz, biegsame Stäbe, Rindennutzung |
| Weidenruten | Flussufer, Feuchtgebiete | Abbrechen | Sehr biegsam, zäh | Flechtwerk (Körbe, Windschirme, Fischfallen) |
| Birkenrinde | Birkenwald | Abziehen | Wasserabweisend | Behälter, Dachdeckung, Zunder |
| Bast / Pflanzenfasern | Grasland, Wald | Abstreifen, Sammeln | Reißbar in Streifen | Schnüre, Bindungen → Verbundwerkzeuge |
| Harz | Nadelwald | Abkratzen | Klebrig, aushärtend | Kleber für Verbundwerkzeuge. Game Changer. |
| Beeren | Wald, Gebüsch | Pflücken | Essbar (manche giftig!) | Nahrung. Fehlschlag: Vergiftung → Entdeckung |
| Wurzeln / Knollen | Grasland, Flussufer | Graben (Hände oder Grabstock) | Stärkehaltig, sättigend | Nahrung. Mit Grabstock deutlich effizienter. |
| Gräser / Schilf | Grasland, Seeufer | Sammeln | Biegsam, reichlich | Einstreu, Matten, Dachdeckung |

### 5.2 Gesteine

| Material | Vorkommen | Eigenschaften | Nutzen |
|---|---|---|---|
| Flussstein (rund) | Flussufer, Seeufer | Hart, rund, schwer | Hammer, Schlagstein. Nicht scharf. |
| Feuerstein | Gebirge, Kalkstein, Küste (Kreide) | Splittert kontrolliert, sehr hart | Klingen, Schaber, Bohrer. Beste Werkzeugqualität. Funken → Feuer. |
| Sandstein | Hügel, Flussnähe | Weich, abrasiv | Schleifen, Glätten. Verbessert Werkzeuge. |
| Obsidian | Vulkanisch (selten) | Extrem scharf, spröde | Beste Schneidwerkzeuge, aber zerbrechlich. Premium. |
| Granit | Gebirge | Sehr hart, schwer zu bearbeiten | Schwere Werkzeuge, Mahlsteine. |
| Kalkstein | Hügel, Küste | Weich | Kreideartig, für Markierungen. |

**Spielmechanik**: Anfangs sammeln Siedler einfach "Stein". Nach "Gesteinskunde" unterscheiden sie die Arten → neue Objekte für Aufträge.

### 5.3 Tierische Materialien

| Material | Quelle | Eigenschaften | Nutzen |
|---|---|---|---|
| Kleintier-Fleisch | Kleintiere, Vögel, Eier | Schnell verderblich | Nahrung (roh: wenig Nährwert + Krankheitsrisiko) |
| Großwild-Fleisch | Hirsch, Bison (erst nach Jagd-Entdeckung) | Viel, schnell verderblich | Nahrung für ganzen Stamm |
| Knochenmark | Kadaver, Jagdbeute | Hochkalorisch | Wertvollste Nahrung in I.1. Erfordert Werkzeug zum Knacken. |
| Knochen | Kadaver, Jagd | Hart, formbar | Nadeln, Ahlen, Spitzen. Voraussetzung für I.2. |
| Sehnen | Jagdbeute | Extrem reißfest | Bindungen (besser als Pflanzenfaser). Bogensehne (I.4). |
| Felle | Jagdbeute | Isolierend | Kleidung (I.2), Behälter. Muss geschabt werden (Schaber nötig). |
| Fisch | Fluss, See, Küste | Schnell verderblich | Nahrung. Zunächst per Hand, später Fallen/Netze. |

### 5.4 Sonstige

| Material | Vorkommen | Nutzen |
|---|---|---|
| Wasser | Flüsse, Seen, Quellen | Lebensnotwendig. Regelmäßig trinken. |
| Lehm | Flussufer, Küste | Abdichtung, Feuerstelle. Voraussetzung Keramik (I.9). |
| Erde | Überall | Gruben graben (Fallen, Feuerstellen). |
| Salz | Küste, bestimmte Felsen | Konservierung. Richtung I.6. |
| Ocker / Pigmente | Bestimmte Gesteine | Markierungen, später Höhlenmalerei (I.3). |

---

## 6. Werkzeugsystem

### Prinzip: Qualität bestimmt alles

Werkzeuge haben eine **Qualitätsstufe**. Bessere Werkzeuge = schnelleres Arbeiten, neue Möglichkeiten, neue Prädikate im Auftragssystem.

### 6.1 Qualitätsstufen

| Stufe | Name | Herstellung | Spieleffekt | Neue Prädikate |
|---|---|---|---|---|
| Q1 | Einfacher Faustkeil | Stein auf Stein | Basis. Langsam, grob. | Sammeln, Steine schlagen |
| Q2 | Geschlagener Faustkeil | Feuerstein + kontrolliertes Schlagen | +30% Effizienz | – |
| Q3 | Feuerstein-Klinge | Feuerstein + Schlagtechnik | +60% Effizienz | Schneiden, Schaben |
| Q4 | Verbundwerkzeug | Stein + Holz + Bindung (Schnur/Harz) | +100% Effizienz | Graben, Hacken, Fällen, Herstellen |
| Q5 | Spezialisiertes Werkzeug | Klinge + Hartholz + Sehne/Harz | +150% Effizienz | Bohren, spezialisiertes Bearbeiten |

### 6.2 Werkzeugtypen

| Werkzeug | Voraussetzung | Ermöglicht (neue Prädikate/Objekte) |
|---|---|---|
| Faustkeil | Start | Sammeln, Steine spalten |
| Grabstock | Totholz-Ast + Entdeckung | Wurzeln graben (effizient), Gruben ausheben |
| Keule | Dicker Ast + Entdeckung | Jagen (klein), Verteidigung |
| Schaber | Q3 + Entdeckung | Fell verarbeiten, Holz glätten |
| Handaxt / Beil | Q4 (Verbund) | Bäume fällen, Fleisch zerteilen |
| Bohrer | Q4 + Feuerstein | Löcher bohren, Reibungsfeuer-Technik |
| Speerspitze | Q3 + Bindung | Großwildjagd |
| Mahlstein | Granit + flacher Stein | Pflanzen zerreiben |
| Knochennadel | Knochen + Q3 | Voraussetzung für I.2 (Kleidung) |

### 6.3 Werkzeug-Verschleiß

- Werkzeuge nutzen sich ab (Haltbarkeit abhängig von Material)
- Feuerstein: scharf aber spröde → häufiger Ersatz
- Obsidian: extrem scharf, bricht schnell
- Verbundwerkzeuge: haltbarer, aufwendiger herzustellen
- **Spielmechanik**: Regelmäßige Werkzeugherstellung nötig → treibt Erfahrung → treibt Entdeckungen

---

## 7. Nahrungssystem

### Prinzip: Vielfalt, Saisonalität und Risiko

### 7.1 Nahrungsquellen

| Quelle | Biom | Nährwert | Risiko | Sammelmethode |
|---|---|---|---|---|
| Beeren | Wald, Gebüsch | Niedrig | Manche giftig → Krankheit | Pflücken |
| Wurzeln & Knollen | Grasland, Flussufer | Mittel | Erfordert Grabstock für Effizienz | Graben |
| Insekten & Larven | Überall | Niedrig | Keine | Suchen |
| Eier | Wald, Küste | Mittel | Saisonal, begrenzt | Sammeln |
| Kleintiere | Überall | Niedrig | Erfordert Fangen/Keule | Jagen (nach Entdeckung) |
| Aas / Kadaver | Zufällig | Hoch (Mark!) | Raubtiere, Krankheit bei altem Aas | Werkzeug zum Knacken |
| Fisch (per Hand) | Fluss, See | Mittel | Nur flaches Wasser, oft Fehlschlag | Per Hand |
| Fisch (Fallen) | Fluss, See | Mittel–Hoch | Erfordert Flechtwerk-Entdeckung | Fallen bauen |
| Großwild | Grasland, Wald | Sehr Hoch | Verletzung! Erfordert Speere + Kooperation | Jagen (nach Entdeckung) |
| Honig | Wald | Hoch | Stiche → Verletzung | Sammeln |

### 7.2 Nahrungszustände

| Zustand | Zeitfenster | Effekt |
|---|---|---|
| Frisch | 0–4h (Spielzeit) | Voller Nährwert |
| Abgestanden | 4–8h | -50% Nährwert |
| Verdorben | > 8h | Krankheit bei Verzehr → Fehlschlag → Entdeckung! |
| Getrocknet | Unbegrenzt (nach Entdeckung) | -20% Nährwert, lagerfähig |
| Geräuchert | Unbegrenzt (nach Feuer) | Voller Nährwert, lagerfähig |

### 7.3 Bedürfnisse

| Bedürfnis | Stufen | Effekt bei Mangel |
|---|---|---|
| **Durst** | Satt → Durstig → Dehydriert → Tod | Schneller tödlich als Hunger. Siedler müssen regelmäßig zum Wasser. |
| **Hunger** | Satt → Hungrig (-20%) → Erschöpft (-50%) → Verhungernd → Tod | Langsamer als Durst, aber Leistungseinbruch |
| **Schutz** | Geschützt → Exponiert → Unterkühlt → Krank | Nachts und bei Wetter relevant |

---

## 8. Unterschlüpfe & Strukturen

### Prinzip: Die Landschaft IST dein Gebäude

In I.1 gibt es keine Häuser. Der Spieler **findet und verbessert** natürliche Schutzorte. Erst durch Entdeckungen werden einfache Strukturen möglich.

### 8.1 Natürliche Unterschlüpfe (vom Terrain generiert)

| Typ | Schutz | Kapazität | Vorkommen |
|---|---|---|---|
| Höhle | Exzellent | 5–10 | Gebirge, Hügel. Selten. Premium. |
| Felsvorsprung | Gut | 3–5 | Hügel, Steilhänge |
| Dichtes Unterholz | Mäßig | 2–3 | Wald |
| Umgestürzter Baum | Mäßig | 2–3 | Wald |
| Offenes Gelände | Kein Schutz | – | Standard |

### 8.2 Baubare Strukturen (durch Aufträge, nach Entdeckungen)

| Struktur | Entdeckung nötig | Auftrag | Material (Objekte) | Schutz |
|---|---|---|---|---|
| Windschirm | Flechtwerk | Bauen + Windschirm + hier | Äste + Gras/Schilf | Mäßig (Wind) |
| Laubhütte | Flechtwerk + Verbundwerkzeug | Bauen + Laubhütte + hier | Äste + Laub + Bindung | Gut |
| Offene Feuerstelle | Feuer | Bauen + Feuerstelle + hier | Steine (Ring) + Holz | Wärme, Licht, Tierschutz |
| Eingegrabene Feuerstelle | Feuer + Graben | Bauen + Feuerstelle + Grube + hier | Grube + Steine + Holz | Bessere Wärme |
| Trockengestell | Schnur + Holz | Bauen + Trockengestell + hier | Stangen + Bindung | Nahrungstrocknung |
| Steinkreis-Windschutz | Verbundwerkzeug + Kooperation | Bauen + Steinkreis + hier | Große Steine (Transport!) | Gut (Wind) |
| Vorratsgrube | Graben | Bauen + Vorratsgrube + hier | Grube + Abdeckung | Nahrungslager |

**Keine Holzfällerhütten, keine Wohnhäuser** – das kommt erst in I.2+.

### 8.3 Standortwahl

Der Spieler muss abwägen:

| Faktor | Warum wichtig |
|---|---|
| **Wassernähe** | Überlebenswichtig. Siedler müssen regelmäßig trinken. |
| **Natürlicher Schutz** | Höhle spart enormen Bauaufwand |
| **Nahrungsangebot** | Biom-abhängig |
| **Steinvorkommen** | Für Werkzeuge essentiell |
| **Holzverfügbarkeit** | Für Strukturen und Feuer |
| **Höhenlage** | Überblick, aber exponiert |
| **Tiergefahr** | Raubtiere stehlen Nahrung, verletzen Siedler |

---

## 9. Terraforming in I.1

### Prinzip: Die Landschaft verändert sich durch Nutzung

Kein aktives Terraform-Tool. Stattdessen **emergente Veränderungen**.

### 9.1 Passive Veränderungen

| Veränderung | Auslöser | Effekt |
|---|---|---|
| **Trampelpfade** | Siedler laufen wiederholt eine Route | Pfad entsteht → +30% Bewegung darauf |
| **Lichtung** | Viel Totholz sammeln / Äste brechen | Unterholz lichtet sich, offenere Fläche |
| **Abgeerntete Zone** | Beeren/Wurzeln erschöpft | Nahrung weg, wächst langsam nach |
| **Steinbruch** | Viel Stein sammeln | Felsformation schrumpft, Grube entsteht |

### 9.2 Aktive Veränderungen (nach Entdeckungen)

| Aktion | Voraussetzung | Auftrag-Form |
|---|---|---|
| Vegetation entfernen | Verbundwerkzeug (Beil) | "[Wer] entfernt Vegetation + hier" |
| Grube graben | Grabstock / Verbundwerkzeug | "[Wer] gräbt Grube + hier" |
| Steine bewegen | Mehrere Siedler | "[Alle] bewegen Steine + hierhin" |
| Pfade anlegen | Häufige Nutzung + Entdeckung | "[Wer] legt Pfad an + von hier + nach dort" |

### 9.3 Rückkopplung

- **Trampelpfade** → Siedler bevorzugen sie → verstärken sich → Siedlung formt sich organisch
- **Lichtungen** → Mehr Sonne → andere Pflanzen → neues Nahrungsangebot
- **Übernutzung** → Ressourcen erschöpft → Siedler müssen weiter → Expansion oder Umzug
- **Feuerstellen** → Rauch vertreibt Insekten/Tiere → sicherer, aber Wild zieht weg

---

## 10. Entdeckungssystem (erweitert)

### Phase A: Erste Tage (0–5 Spielminuten)

Schnelle, oft durch Fehlschläge getriebene Entdeckungen.

| Entdeckung | Auslöser | Typ | Effekt: Neue Wörter |
|---|---|---|---|
| Trinkwasserquelle | Siedler findet Wasser | discovery | Objekt: [Trinkwasser] |
| Essbare Pflanzen (lokal) | Sammeln (+ evtl. Vergiftung) | discovery | Objekte: [Beeren], [Wurzeln] statt [Alles] |
| Unterschlupf gefunden | Erkunden in Startnähe | discovery | Objekt: [Höhle/Felsvorsprung] |
| Grabstock | Siedler gräbt + nutzt Ast | discovery | Werkzeug + Prädikat: [Graben mit Grabstock] |

### Phase B: Anpassung (5–15 Spielminuten)

Biom-abhängige Entdeckungen.

| Entdeckung | Auslöser | Biom | Typ | Neue Wörter |
|---|---|---|---|---|
| Gesteinskunde | Viel Steine bearbeiten | Jedes | discovery | Objekte: [Feuerstein], [Sandstein], [Granit] |
| Verschiedene Holzarten | Viel Äste sammeln | Wald | discovery | Objekte: [Hartholz], [Weichholz] |
| Feuerstein (Fundort) | Steine sammeln in Gebirge | Gebirge | discovery | Objekt: [Feuerstein] + Q3 Werkzeuge |
| Pflanzenfasern | Gras/Schilf sammeln | Grasland | discovery | Objekt: [Pflanzenfasern] → Schnur möglich |
| Keulen zur Verteidigung | Wildschwein-Angriff (Fehlschlag!) | Jedes | discovery | Prädikat: [Jagen (klein)], Werkzeug: Keule |
| Knochenmark-Nutzung | Kadaver finden | Jedes | discovery | Objekt: [Knochenmark] |
| Harz | Holz sammeln Nadelwald | Nadelwald | discovery | Objekt: [Harz] |

### Phase C: Durchbrüche (15–30 Spielminuten)

Die großen Momente.

| Entdeckung | Voraussetzung | Typ | Neue Wörter |
|---|---|---|---|
| Schnur-Herstellung | Pflanzenfasern | discovery | Objekt: [Schnur] → Verbundwerkzeuge möglich |
| Verbundwerkzeug | Schnur ODER Harz + Holz + Stein | **major_discovery** | Prädikate: [Graben, Fällen, Herstellen], Q4 Werkzeuge |
| Reibungsfeuer | Viel Holzarbeit + versch. Holzarten | **major_discovery** | Prädikate: [Kochen, Räuchern], Objekt: [Feuerstelle] |
| Funkenfeuer | Viel Steinarbeit + Feuerstein | **major_discovery** | (wie Reibungsfeuer, alternativer Pfad) |
| Blitzschlag-Feuer | Zufallsevent, Siedler in Nähe | **major_discovery** | (wie oben, Glückstreffer) |
| Flechtwerk | Viel mit Fasern/Ruten | discovery | Prädikat: [Bauen], Objekte: [Windschirm, Korb, Fischfalle] |
| Einfache Tierfallen | Jagd-Erfahrung + Graben/Flechtwerk | discovery | Objekt: [Tierfalle] |
| Speerwurf | Jagd + Verbundwerkzeug | discovery | Prädikat: [Jagen (groß)], Objekt: [Speer] |
| Fellverarbeitung | Jagd + Schaber (Q3) | discovery | Objekt: [Fell], Prädikat: [Schaben] → Richtung I.2 |
| Nahrungstrocknung | Schnur + Gestell | discovery | Prädikat: [Trocknen], Objekt: [Trockengestell] |
| Räuchern | Feuer + Trocknung | discovery | Prädikat: [Räuchern] |

### Phase D: Späte I.1 (30+ Spielminuten)

| Entdeckung | Voraussetzung | Typ | Richtung |
|---|---|---|---|
| Spezialisierte Werkzeuge | Q4 + versch. Materialien | discovery | Q5 Werkzeuge |
| Knochennadel | Knochen + Q3 | discovery | → I.2 (Kleidung) |
| Lederbearbeitung | Schaber + Felle | discovery | → I.2 (Leder) |
| Heilpflanzen | Viel Pflanzen + Vergiftungen überstanden | discovery | Grundmedizin |
| Salz | Küsten-/Gebirgsbiom | discovery | Konservierung → I.6 |
| Ocker/Pigmente | Gesteinskunde + bestimmte Felsen | discovery | Markierungen → I.3 |

---

## 11. Inszenierung: Darstellung für den Spieler

### 11.1 Event-Darstellung nach Typ

| Typ | Häufigkeit | Darstellung |
|---|---|---|
| success | Ständig | Kein explizites Feedback (Siedler arbeiten) |
| failure | Regelmäßig | Visuell am Siedler: rot blinken, humpeln, krank-Partikel |
| critical_failure | Selten | Warnung: "Mira wurde schwer verletzt!" + Siedler-Highlight |
| discovery | Alle paar Minuten | Toast-Nachricht + Glow am Siedler |
| **major_discovery** | 3–5 pro Partie | **Kamera schwenkt zum Siedler, Zeitlupe, dramatisches Popup, Sound-Effekt.** Episch. |

### 11.2 Stammeschronik

Kein trockenes Event-Log, sondern eine **erzählte Geschichte** des Stammes:

```
🌅 Tag 1
Wir haben einen Felsvorsprung am Bach gefunden. Es gibt
Beeren in der Nähe. Rana hat Wurzeln gesucht, aber eine
hat sie krank gemacht.

🌅 Tag 2
Kael hat verschiedene Steine untersucht. Er hat gelernt,
dass die dunklen, glatten Steine sich besser spalten lassen.
→ Entdeckung: Gesteinskunde

🌅 Tag 3
Ein Wildschwein hat Mira angegriffen! Sie hatte nichts zur
Verteidigung und wurde verletzt. Seitdem tragen die Sammler
schwere Äste mit sich.
→ Entdeckung: Keulen zur Verteidigung

🔥 Tag 7
KAEL HAT FEUER GEMACHT! Er hat zwei verschiedene Hölzer
aneinander gerieben, bis es rauchte. Der ganze Stamm hat
zugesehen. Alles verändert sich jetzt.
→ ★ Große Entdeckung: Reibungsfeuer
```

### 11.3 Chronik-Datenmodell

```
ChronicleEntry:
  day:                int (Spieltag)
  type:               event | discovery | major_discovery | failure | death
  settler:            SettlerName
  narrativeTemplate:  "{{settler}} hat {{action}}, aber {{failure_result}}"
  linkedDiscovery:    DiscoveryDefinition | null
  icon:               normal | warning | skull | lightbulb | fire
```

Texte aus **Templates + Kontext** – vorgeschriebene Satzbausteine, dynamisch durch Siedler-Name, Ressource, Biom. Keine KI-Generierung (das ist MS9).

### 11.4 Chronik als Tutorial

Die Chronik erklärt Mechaniken durch Narration statt durch Tutorial-Popups:
- "Mira wurde vergiftet, aber jetzt wissen wir welche Beeren sicher sind" → erklärt Fehlschlag-als-Lernquelle
- "Kael hat verschiedene Steine untersucht" → erklärt Erfahrungssystem
- "Seitdem tragen die Sammler schwere Äste" → erklärt Auswirkung von Entdeckungen

---

## 12. Biome

### 12.1 Wald

**Stärken**: Totholz reichlich, Beeren, Wild, Harz, Schutz durch Unterholz
**Schwächen**: Begrenzte Sicht, wenig guter Stein, Raubtiere
**Entdeckungspfad**: Holzarten → Reibungsfeuer → Harz → Verbundwerkzeuge
**Spielgefühl**: Geschützt aber eingeengt

### 12.2 Gebirge / Hügel

**Stärken**: Feuerstein!, Höhlen, Überblick, verschiedene Gesteine
**Schwächen**: Wenig Holz und Nahrung, kalt, steiles Gelände
**Entdeckungspfad**: Feuerstein → bessere Werkzeuge → Funkenfeuer
**Spielgefühl**: Hart am Anfang, strategisch stark

### 12.3 Küste / Flussufer

**Stärken**: Fisch, Wasser, Lehm, Salz, Muscheln, Feuerstein (Kreide)
**Schwächen**: Wind-exponiert, wenig Schutz, begrenztes Holz
**Entdeckungspfad**: Fisch → Pflanzenfasern → Netze → Fischfallen
**Spielgefühl**: Nahrung einfach, Schutz schwierig

### 12.4 Grasland / Steppe

**Stärken**: Großwild-Herden, Pflanzenfasern, weite Sicht
**Schwächen**: Kein Holz (!), kein Unterschlupf, Wind
**Entdeckungspfad**: Fasern → Flechtwerk → mobile Strukturen → Jagd
**Spielgefühl**: Schwierigster Start, beste Jagd. Holzmangel zwingt zur Improvisation.

---

## 13. Siedler-Verhalten

### 13.1 Autonomie + Aufträge

Siedler handeln eigenständig nach Grundbedürfnissen, befolgen aber Aufträge des Spielers:

**Automatisch (nicht überschreibbar):**
1. Durst → Wasser suchen
2. Akuter Hunger → nächste bekannte Nahrung
3. Lebensgefahr → fliehen

**Vom Spieler steuerbar (durch Aufträge):**
4. Zugewiesene Aufgabe (Sammeln, Erkunden, Bauen, ...)
5. Freie Aufgabe (wenn kein Auftrag: selbst entscheiden basierend auf Erfahrung)

### 13.2 Erfahrung & Spezialisierung

- Jeder Siedler sammelt **Erfahrung** pro Aktivität
- Höhere Erfahrung = schneller + höhere Entdeckungswahrscheinlichkeit
- Siedler die viel jagen werden zu guten Jägern
- **Spieler-Einfluss**: Durch Aufträge bestimmt der Spieler, wer was tut → wer sich worin spezialisiert

### 13.3 Gesundheit

| Zustand | Ursache | Effekt | Heilung |
|---|---|---|---|
| Gesund | Normal | Volle Leistung | – |
| Verletzt | Jagd, Sturz, Raubtier | -50%, bestimmte Arbeit nicht möglich | Zeit (langsam) oder Heilpflanzen |
| Krank | Verdorbene Nahrung, Gift, Kälte | -70%, kann andere anstecken | Zeit oder Heilpflanzen |
| Tödlich verletzt | Schwerer Unfall, Raubtier | Stirbt ohne Hilfe | Nur Heilpflanzen + erfahrener Heiler |

---

## 14. Spielablauf einer typischen I.1-Partie

### Minute 0–5: Ankunft
- 5 Siedler in unbekanntem Terrain
- Automatische Suche nach Wasser und Nahrung
- Spieler erkundet: Wo ist Wasser? Schutz? Stein?
- Erste Aufträge: "Alle sammeln hier", "Kael erkundet Richtung Norden"
- Erste Fehlschläge möglich: giftige Beere, nasse Füße beim Fischversuch

### Minute 5–10: Etablierung
- Spieler identifiziert besten Standort
- Erste Entdeckungen: Essbare Pflanzen, Grabstock, evtl. Gesteinskunde
- Trampelpfade entstehen zwischen Wasser, Nahrung und Schlafplatz
- Auftragsliste wächst: neue Prädikate und Objekte verfügbar
- Chronik: "Rana hat gelernt welche Wurzeln essbar sind"

### Minute 10–20: Anpassung
- Biom-spezifische Entdeckungen
- Werkzeugqualität steigt (Q2 → Q3)
- Vielleicht ein Wildschwein-Angriff → "Keulen zur Verteidigung" entdeckt
- Erste gebaute Struktur möglich (Windschirm nach Flechtwerk)
- Spieler setzt gezieltere Aufträge: "Kael bearbeitet Feuerstein am Felsen"

### Minute 20–30: Durchbrüche
- **FEUER** wird entdeckt → **Major Discovery**: Kamera, Zeitlupe, epischer Moment
- Verbundwerkzeuge verändern die Effizienz drastisch
- Siedlung nimmt Form an: Feuerstelle, Trockengestell, Windschirm
- Trampelpfade deutlich sichtbar
- Chronik erzählt eine packende Geschichte

### Minute 30+: Konsolidierung
- Nahrungsüberschuss durch Trocknung/Räuchern
- Großwildjagd möglich
- Vorratsgrube angelegt
- Fellverarbeitung → Richtung I.2
- **Spielgefühl**: Von "verloren in der Wildnis" zu "wir beherrschen unsere Umgebung"

---

## 15. Abgrenzung: Was I.1 NICHT hat

| Feature | Warum nicht | Kommt in |
|---|---|---|
| Feste Gebäude (Hütten, Häuser) | Erst mit Leder/Zeltbau | I.2 |
| Bäume fällen (ohne Beil) | Verbundwerkzeug erst durch Entdeckung | Spätes I.1 |
| Aktive Forschung | Entdeckungen sind emergent | II.3 (Schrift) |
| Ackerbau | Jäger und Sammler | I.7 |
| Metallverarbeitung | Steinzeit | II.4 (Bronze) |
| Kampf/Krieg | Natur ist der Gegner | I.4+ |
| Viehzucht | Tiere sind Beute | I.8 |
| Rad/Transport | Alles wird getragen | II.1 |
| Handel mit anderen Stämmen | Kein Kontakt in I.1 | I.6+ |

---

## 16. Universelles Datenmodell (epochen-übergreifend)

Alle Mechaniken basieren auf denselben abstrakten Strukturen:

```
ActionOutcome:
  type:             success | failure | critical_failure
  probability:      basiert auf Erfahrung + Werkzeug + Zufall
  successEffect:    { resource: "Wurzel", amount: 1 }
  failureEffect:    { condition: "krank", duration: 300 }
  criticalEffect:   { condition: "schwer_verletzt", duration: 600 }
  experienceGain:   IMMER (auch bei Fehlschlag!)
  discoveryBonus:   failure = +50%, critical = +100%

EventDefinition:
  name:     "Wildschwein-Angriff"
  trigger:  { activity: "Sammeln", biome: "Wald", random: 0.05 }
  check:    settler.hasTool("Keule")
  success:  +Jagd-Erfahrung, Fleisch
  failure:  Siedler verletzt
  discoveryChance:
    success: "Aktive Jagd" +20%
    failure: "Keulen zur Verteidigung" +40%

OrderDefinition:
  subject:   SubjectType
  predicate: PredicateDefinition
  objects:   ObjectDefinition[]
  negated:   bool
  priority:  int
  status:    Active | Paused | Complete | Failed

DiscoveryDefinition:
  name:           "Verbundwerkzeug"
  triggers:       [{ experience: "Steinbearbeitung", threshold: 30 },
                   { hasDiscovery: "Schnur" OR "Harz" }]
  type:           major_discovery
  unlocksPredicates: ["Fällen", "Graben", "Herstellen"]
  unlocksObjects:    ["Verbundwerkzeug", "Grube", "Baum"]
  unlocksTools:      [Q4_Werkzeuge]
  chronicleTemplate: "{{settler}} hat zum ersten Mal Stein, Holz und {{binding}}
                      zu einem Werkzeug verbunden. Es ist stärker als alles,
                      was der Stamm bisher hatte."
```

Dieses Modell trägt von I.1 bis IV.3. Eine Bronzeschmiede ist genauso definiert wie der Grabstock – nur mit anderen Werten und Voraussetzungen.

---

## 17. Offene Fragen für den Game Designer

| Nr. | Frage |
|---|---|
| 1 | Wie genau funktioniert Durst? Wie oft zum Wasser? Kann man Wasser transportieren (erst nach Behälter-Entdeckung)? |
| 2 | Jahreszeiten in I.1? Beeinflusst Nahrungsangebot, Temperatur, Tag-Nacht-Länge? |
| 3 | Raubtier-Gefahr: Sichtbare Tiere mit eigenem Verhalten oder Zufalls-Events? |
| 4 | Werkzeugverschleiß: Pro Nutzung oder pro Zeiteinheit? Wie granular? |
| 5 | Sollen Siedler individuelle Persönlichkeiten haben (mutig/ängstlich, neugierig/vorsichtig)? |
| 6 | Aktionsradius: Feste Größe oder dynamisch durch Trampelpfade/Entdeckungen? |
| 7 | Karte/Minimap oder nur Kamera-Erkundung? |
| 8 | Schwierigkeitsgrad: Soll ein Neuling regelmäßig scheitern? |
| 9 | Was passiert wenn alle sterben? Neustart oder neue Siedler wandern ein? |
| 10 | Wie viele gleichzeitige Aufträge soll der Spieler vergeben können? |
| 11 | Soll der Spieler einen Siedler als "Anführer" bestimmen können (mehr Einfluss)? |
| 12 | Wie wird die Auftrags-Grammatik in der UI am besten dargestellt auf einem iPad? |
