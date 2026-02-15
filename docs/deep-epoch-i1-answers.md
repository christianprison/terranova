# Deep Epoch I.1 – Game Designer Antworten

> **Version**: 0.1
> **Autor**: Game Designer
> **Basiert auf**: deep-epoch-i1-design-v02.md, GDD v0.9
> **Ziel**: Antworten auf die 12 offenen Fragen + Klappbuch-UI-Konzept für Auftrags-Grammatik

---

## Frage 1: Durst-Mechanik

### Entscheidung

Siedler müssen ca. alle **2 Spielminuten** trinken. Ohne Behälter kein Wassertransport – sie müssen physisch zum Wasser laufen. Die Behälter-Entdeckung (Birkenrinde-Gefäß oder Lehm-Schale) ermöglicht erstmals Wassertransport und vergrößert den Aktionsradius drastisch.

### Detaildesign

| Parameter | Wert | Begründung |
|-----------|------|------------|
| Trinkintervall | ~2 Spielminuten | Häufig genug um Standortwahl relevant zu machen, selten genug um nicht zu nerven |
| Trinken dauert | 3–5 Sekunden | Siedler kniet am Wasser, kurze Animation |
| Ohne Wasser: Durstig | Nach 3 Min | -20% Leistung, Siedler bricht aktuelle Aufgabe ab und sucht Wasser |
| Ohne Wasser: Dehydriert | Nach 5 Min | -60% Leistung, kann keine Aufträge mehr annehmen |
| Ohne Wasser: Tod | Nach 8 Min | Siedler stirbt |
| Behälter (nach Entdeckung) | 1 Füllung = 2 Trinkzyklen | Verdoppelt effektiven Aktionsradius |

### Spielmechanische Konsequenz

Durst ist der **unsichtbare Zaun** um die Siedlung. Ohne Behälter kann kein Siedler weiter als ~1 Minute Laufzeit vom Wasser weg arbeiten (weil er zum Rücklaufen dieselbe Zeit braucht). Das erzeugt drei natürliche Phasen:

1. **Früh**: Siedlung muss am Wasser sein. Aktionsradius winzig.
2. **Nach Behälter-Entdeckung**: Radius verdoppelt. Fühlt sich wie Durchbruch an.
3. **Spätere Epochen**: Brunnen, Zisternen, Wasserleitungen erweitern den Radius immer weiter.

### Referenz

Dwarf Fortress nutzt ein ähnliches System – Zwerge müssen regelmäßig trinken, was Brunnen und Tavernen zu strategisch relevanten Gebäuden macht. Wir nutzen denselben Effekt, aber expliziter als Progressionsmechanik.

---

## Frage 2: Jahreszeiten

### Entscheidung

Ja, aber vereinfacht: **Zwei Jahreszeiten** statt vier. Die konkrete Ausprägung hängt vom Biom ab.

### Detaildesign

| Biom-Typ | Warme Jahreszeit | Kalte/Trockene Jahreszeit |
|-----------|-----------------|--------------------------|
| Gemäßigt (Wald, Grasland) | Sommer: Beeren, reichlich Nahrung | Winter: Nahrungsknappheit, Kältegefahr |
| Tundra, Taiga, Gebirge | Kurzer Sommer: einzige Zeit zum Sammeln | Langer Winter: Überleben wird zur Hauptaufgabe |
| Steppe, Savanne | Regenzeit: Vegetation, Wasser | Trockenzeit: Wasserstellen schrumpfen, Herden wandern |
| Wüste | Gemäßigt: erträgliche Hitze | Heiß: Wasser noch knapper, nur nachts arbeiten |
| Küste, Fjord | Sommer: Fisch, Muscheln, ruhige See | Winter: Sturm, gefährliche Wellen, weniger Fisch |
| Regenwald, Mangroven | Regenzeit: Überschwemmung, Insekten | Trockenzeit: Leichterer Zugang, mehr Früchte |

### Auswirkungen

| System | Effekt der Jahreszeit |
|--------|----------------------|
| **Nahrung** | Beeren/Pflanzen nur in warmer Jahreszeit. Jagdwild wandert. Fisch variiert. |
| **Temperatur** | Kalte Jahreszeit: Schutz-Bedürfnis steigt, Kälteschäden ohne Unterschlupf/Feuer |
| **Tag-Nacht-Länge** | Warme JZ: lange Tage, mehr Arbeitszeit. Kalte JZ: kurze Tage, früh dunkel. |
| **Vegetation** | Bäume verlieren Blätter (Laubwald) → mehr Sicht, weniger Deckung |
| **Wasser** | Quellen/Bäche können in Trockenzeit schrumpfen → Standortwahl! |

### Zykluslänge

Ein kompletter Zyklus (warm + kalt) = ca. **15–20 Spielminuten**. Das bedeutet in einer typischen I.1-Partie (~30 Min) erlebt der Spieler 1,5–2 Zyklen. Der erste Winter ist ein natürlicher Spannungsbogen: "Haben wir genug Vorräte? Haben wir Feuer?"

### Spielmechanische Konsequenz

Jahreszeiten erzeugen **natürlichen Zeitdruck** ohne künstliche Timer. Der Spieler denkt: "Der Winter kommt – wir brauchen Feuer und Nahrungsvorräte." Das treibt Entdeckungen (Trocknen, Räuchern, Feuer) organisch voran und macht Vorratshaltung zu einer strategisch sichtbaren Entscheidung.

### Scope-Hinweis

Zwei Jahreszeiten statt vier geben ca. 80% des Gameplay-Effekts bei ca. 30% der Implementierungskomplexität. Vegetation, Tierwanderung und Temperatur brauchen jeweils nur zwei Zustände, keine Übergangskurven.

---

## Frage 3: Raubtier-Gefahr

### Entscheidung

Beides – **Zufalls-Events** für überraschende Begegnungen und **sichtbare Tiere** in der Welt mit eigenem Verhalten. Events zuerst implementieren, sichtbare Tiere als zweiter Schritt.

### Detaildesign

**Typ A: Zufalls-Events (während Aktivität)**

| Event | Auslöser | Häufigkeit | Effekt ohne Verteidigung | Effekt mit Keule/Waffe |
|-------|----------|------------|--------------------------|------------------------|
| Schlangenbiss | Sammeln in Unterholz | Selten | Vergiftung (krank) | Vermeidbar |
| Wildschwein-Angriff | Sammeln im Wald | Gelegentlich | Verletzung | Abwehr, evtl. Fleisch |
| Wolf-Begegnung | Allein unterwegs, nachts | Selten | Schwere Verletzung | Abwehr, Wolf flieht |
| Bärenbegegnung | Höhle erkunden | Sehr selten | Tödlich ohne Hilfe | Flucht möglich |
| Insektenschwarm | Honig sammeln | Häufig | Stiche (leichte Verletzung) | Rauch vertreibt (nach Feuer) |

**Typ B: Sichtbare Tiere (eigenes Verhalten)**

| Tier | Verhalten | Interaktion | Scope |
|------|-----------|-------------|-------|
| Hirsch/Reh | Grast, flieht bei Annäherung | Jagdbeute (nach Speere) | MS Priorität 2 |
| Bison/Wisent | Herden in Steppe, ignoriert Siedler | Jagdbeute (Kooperation nötig) | MS Priorität 2 |
| Wolf | Patrouilliert, meidet Feuer | Gefahr bei Nacht, meidet Lager mit Feuer | MS Priorität 2 |
| Fische | Sichtbar im Wasser | Fangbar (per Hand, später Fallen) | MS Priorität 1 |
| Vögel/Kleintiere | Ambient | Atmosphäre, Eier sammeln | MS Priorität 3 |

### Spielmechanische Konsequenz

Die Kombination erzeugt zwei verschiedene Spannungsmomente: Die Schlange beim Beerensammeln (Überraschung → Fehlschlag → Entdeckung) und der Wolf am Waldrand (strategische Entscheidung → Meiden-Auftrag oder Jagen?). Sichtbare Tiere geben dem Spieler außerdem die Möglichkeit, **vorausschauend** zu handeln – er sieht die Hirschherde und kann einen Jäger losschicken.

**Feuer als Schutz**: Sichtbare Raubtiere meiden Feuerstellen. Das macht Feuer-Entdeckung noch wertvoller und gibt dem Spieler ein sichtbares "vorher/nachher"-Erlebnis.

---

## Frage 4: Werkzeugverschleiß

### Entscheidung

**Pro Nutzung**, nicht pro Zeiteinheit. Ein Werkzeug das herumliegt geht nicht kaputt.

### Detaildesign

| Werkzeug-Qualität | Material | Haltbarkeit (Nutzungen) | Herstellungsaufwand |
|-------------------|----------|------------------------|---------------------|
| Q1 Einfacher Faustkeil | Flussstein | ~15 | Sofort (Start) |
| Q2 Geschlagener Faustkeil | Feuerstein | ~25 | 1 Siedler, ~30 Sek |
| Q3 Feuerstein-Klinge | Feuerstein + Technik | ~35 | 1 erfahrener Siedler, ~45 Sek |
| Q4 Verbundwerkzeug | Stein + Holz + Bindung | ~60 | 1 erfahrener Siedler, ~90 Sek |
| Q5 Spezialisiertes | Klinge + Hartholz + Sehne | ~100 | 1 Spezialist, ~120 Sek |

### Verschleiß-Feedback

- **75%**: Kein visuelles Feedback (Werkzeug funktioniert normal)
- **25%**: Werkzeug sieht beschädigt aus (visueller Hinweis)
- **0%**: Werkzeug bricht → Siedler steht ohne da → muss neues herstellen oder holen

### Spielmechanische Konsequenz

Regelmäßige Werkzeugherstellung erzeugt einen **natürlichen Erfahrungskreislauf**: Werkzeug nutzen → Werkzeug bricht → neues herstellen → +Erfahrung → bessere Werkzeuge entdecken. Obsidian (extrem scharf, bricht schnell) vs. Granit (stumpfer, hält lange) wird zu einer echten strategischen Entscheidung.

**Kein Reparieren** in I.1 – kaputt ist kaputt. Das ist historisch korrekt (Steinwerkzeuge werden nicht repariert, sondern neu gemacht) und hält die Herstellungsschleife aktiv.

---

## Frage 5: Siedler-Persönlichkeiten

### Entscheidung

Ja, aber **minimal**: 1–2 Traits pro Siedler, die Gameplay spürbar beeinflussen.

### Detaildesign

Jeder Siedler startet mit **einem** zufälligen Trait. Traits sind nicht änderbar – sie definieren die Persönlichkeit.

| Trait | Effekt | Gameplay-Konsequenz |
|-------|--------|---------------------|
| **Neugierig** | +25% Entdeckungschance | Ideal als Erkunder. Findet öfter neue Dinge. |
| **Vorsichtig** | -30% Fehlschlag-Schwere (Verletzungen weniger schlimm) | Ideal für gefährliche Aufgaben (Jagd, Klettern). |
| **Geschickt** | +20% Werkzeug-Effizienz, +15% Haltbarkeit | Ideal für Werkzeugherstellung und Steinbearbeitung. |
| **Robust** | +30% Gesundheit, schnellere Heilung | Überlebt mehr Fehlschläge. Tank des Stammes. |
| **Ausdauernd** | -20% Hunger/Durst-Rate | Kann weiter vom Lager weg arbeiten. Ideal als Erkunder. |

### Anzeige

Trait wird als **kleines Icon** am Siedler-Portrait angezeigt. Kein Textdump – der Spieler lernt schnell: Auge = neugierig, Schild = vorsichtig, Hand = geschickt.

### Spielmechanische Konsequenz

Traits erzeugen **Bindung und meaningful choice**: "Kael ist neugierig – er soll erkunden, nicht Holz sammeln." Der Spieler besetzt Rollen basierend auf Persönlichkeit. Wenn Kael der Neugierige stirbt, ist das ein spürbarer Verlust – nicht nur ein Siedler weniger, sondern der beste Entdecker ist weg.

**Warum nur 1 Trait**: Bei 5 Siedlern reicht ein Trait, um jeden unterscheidbar zu machen. Mehr wäre Komplexität ohne Mehrwert in I.1. In späteren Epochen mit mehr Siedlern können Traits erweitert werden.

---

## Frage 6: Aktionsradius

### Entscheidung

**Dynamisch**, primär durch Wasserreichweite begrenzt. Kein fester Radiusparameter.

### Mechanik

Der Aktionsradius ergibt sich aus: "Wie weit kann ein Siedler vom Wasser weg, bevor er verdurstet?" Das ist kein sichtbarer Kreis – es ist ein emergentes Ergebnis der Durst-Mechanik.

| Phase | Effektiver Radius | Warum |
|-------|-------------------|-------|
| Start (kein Behälter) | ~1 Minute Laufweg vom Wasser | Siedler muss trinken und zurücklaufen können |
| Nach Behälter-Entdeckung | ~2–3 Min Laufweg | 1 Füllung = 2 Trinkzyklen |
| Trampelpfade vorhanden | +30% Radius (schnellere Bewegung) | Siedler laufen Pfade schneller |
| Spätere Epochen | Brunnen, Zisternen, Wasserleitungen | Komplett neue Infrastruktur |

### Spielmechanische Konsequenz

Der Spieler merkt den Radius nie als Zahl – er merkt ihn daran, dass seine Siedler ständig zum Wasser laufen. Die Erkenntnis "Ich muss näher am Wasser bauen" kommt natürlich. Die Behälter-Entdeckung fühlt sich befreiend an, weil plötzlich der ganze Beeren-Hügel 2 Minuten weiter erreichbar wird.

---

## Frage 7: Karte / Minimap

### Entscheidung

**Keine Minimap in I.1.** Erkundung passiert mit der Kamera und den Siedlern.

### Begründung

Passt zu drei Designentscheidungen:
1. **Sichtlinien-System** (GDD #27: hohe Komplexität) – der Spieler soll die Welt durch die Augen seiner Siedler erleben
2. **Erkundung als Hauptreiz** – eine Minimap nimmt das Entdeckungsgefühl
3. **Historische Authentizität** – Steinzeit-Menschen hatten keine Karten

### Progression

| Epoche | Kartenfeature |
|--------|---------------|
| I.1 | Keine Karte. Nur Kamera + Sichtlinien. |
| I.3 (Höhlenmalerei) | **Entdeckung "Landkarte"**: Siedler malt bekanntes Terrain auf Felsen. Einfache, abstrakte Übersichtskarte. Zeigt nur bereits erkundetes Gebiet, handgemalt-stilisiert. |
| II.3 (Schrift) | Detailliertere Karte mit Symbolen für Ressourcen |
| II.7 (Navigation) | Kompass, präzise Kartographie |
| III.5 (Satellit) | Vollständige Planetenübersicht |

### Spielmechanische Konsequenz

Die Karte wird selbst zu einer **Entdeckung**, die sich verdient anfühlt. In I.1 muss der Spieler sich merken, wo die Beerenbusche waren – oder einen Siedler losschicken, um nachzuschauen. Das erzeugt genau das "verloren in der Wildnis"-Gefühl aus der Vision.

---

## Frage 8: Schwierigkeitsgrad

### Entscheidung

Regelmäßige **Rückschläge**, aber kein regelmäßiges **Scheitern**. Das Spiel ist fordernd, aber fair.

### Design

**Sicherheitsnetze (verhindern frühes Totalscheitern):**
- Wasser, Grundnahrung und Unterschlupf in Startnähe garantiert
- Erste 2 Spielminuten: keine Raubtier-Events
- Pechschutz: garantierte Basisentdeckungen (essbare Pflanzen, Grabstock) innerhalb der ersten 5 Minuten
- Durst-Tod erst nach 8 Minuten (genug Zeit zum Reagieren)

**Gewollte Rückschläge (treiben Gameplay):**
- Giftige Beeren, Verletzungen, Werkzeugbruch, verdorbene Nahrung
- Raubtier-Begegnungen (nach den ersten 2 Minuten)
- Jahreszeiten-Wechsel als Druckmoment

**Erwartete Erfahrung für einen Neuling:**
- Minuten 0–5: Orientierung, erste kleine Rückschläge (Beeren-Vergiftung)
- Minuten 5–15: Rückschläge treiben Entdeckungen, Spieler versteht das System
- Minuten 15–30: Durchbrüche (Feuer!), Gefühl von Kompetenz
- Totaler Stammestod: Möglich, aber unwahrscheinlich bei minimaler Aufmerksamkeit

### Zahlen-Ankerpunkt

In einer durchschnittlichen I.1-Partie sollte der Spieler ca. **5–8 Fehlschläge** und **2–3 Verletzungen/Krankheiten** erleben, aber **0–1 Todesfälle**. Jeder Fehlschlag führt spürbar zu einer Entdeckung.

---

## Frage 9: Alle Siedler tot

### Entscheidung

**Neue Siedler wandern ein.** Kein kompletter Neustart. Entdeckungen verloren, Terrain-Veränderungen bleiben.

### Mechanik

| Element | Nach Stammestod |
|---------|----------------|
| **Terrain** | Bleibt: Trampelpfade, Lichtungen, Gruben, Feuerstellen |
| **Strukturen** | Bleiben: Windschirme (verfallen langsam), Trockengestelle, Steinkreise |
| **Werkzeuge** | Liegen am Boden, aufsammelbar → neuer Stamm hat sofort bessere Werkzeuge |
| **Entdeckungen** | **Verloren.** Neuer Stamm muss alles neu lernen. |
| **Nahrungsvorräte** | Teilweise verdorben, aber Rest nutzbar |
| **Chronik** | Neues Kapitel: "Ein neuer Stamm findet die Überreste einer verlassenen Siedlung..." |

### Spielmechanische Konsequenz

Der neue Stamm (wieder 5 Siedler) startet in derselben Landschaft, findet aber **Spuren des Vorgängers**: Pfade, Werkzeuge, eine kalte Feuerstelle. Das ist narrativ stark und belohnt den Spieler für bisherige Terrain-Arbeit. Gleichzeitig ist der Wissensverlust schmerzhaft genug, um Tod ernst zu nehmen.

**Wichtig**: Der Spieler wird nicht bestraft (kein "Game Over"-Screen), sondern bekommt eine **neue Geschichte**. "Was ist mit dem alten Stamm passiert?" wird zur Narration.

### Variante für Hardmode (spätere Option)

Permadeath: Bei Stammestod ist die Partie vorbei. Nur für Spieler, die es wollen. Nicht Standard.

---

## Frage 10: Gleichzeitige Aufträge

### Entscheidung

**Keine künstliche Begrenzung.** Bei 5 Siedlern regelt sich das natürlich.

### Begründung

- Maximal 5 individuelle Aufträge (einer pro Siedler) + "Alle"-Aufträge
- Die Auftrags-Übersicht (Sektion 3.8 im Dokument) skaliert natürlich
- Ein künstliches Limit wäre frustrierend: "Warum kann ich keinen 6. Auftrag geben?"
- In späteren Epochen mit mehr Siedlern: Auftrags-Übersicht bekommt Filter und Kategorien

### Prioritäten

Wenn ein Siedler mehrere anwendbare Aufträge hat, gilt:

1. Automatische Bedürfnisse (Durst, akuter Hunger, Lebensgefahr) – nicht überschreibbar
2. Spezifischer Auftrag an diesen Siedler ("Kael erkundet Norden")
3. Gruppenauftrag ("Alle sammeln am Bach")
4. Freie Entscheidung (Siedler wählt selbst basierend auf Erfahrung)

---

## Frage 11: Anführer-System

### Entscheidung

**Nein, kein Anführer in I.1.** Emergente Spezialisierung statt formaler Hierarchie.

### Begründung

1. **Historisch**: Kleine Stammesgruppen (5 Personen) hatten keine formalen Hierarchien. Führung war situativ – der beste Jäger führt bei der Jagd, der erfahrenste Sammler beim Sammeln.
2. **Design**: Widerspricht dem Prinzip "indirekte Steuerung". Ein Anführer impliziert direkte Befehlsgewalt.
3. **Emergenz reicht**: Durch Traits und Erfahrung wird der Siedler mit der meisten Jagd-Erfahrung de facto der beste Jäger. Das passiert ohne explizites System.

### Progression

| Epoche | Hierarchie-Feature |
|--------|-------------------|
| I.1 | Keine. Gleichberechtigter Stamm. |
| I.3 (Symbolische Revolution) | Erste Hierarchien: Ältester/Erfahrenster bekommt Sonderstatus |
| I.6+ (Sesshaftigkeit) | Häuptling/Ältestenrat als Entdeckung |
| II.3 (Schrift & Verwaltung) | Formale Verwaltungsstrukturen |

---

## Frage 12: Auftrags-Grammatik UI – Das Klappbuch

### Entscheidung

**Klappbuch (Flip-Book) Interface** – drei unabhängig scrollbare Spalten, die zu einem Auftrag kombiniert werden.

### Kernkonzept

Inspiriert von den Kinderbüchern, in denen man Kopf, Körper und Beine unabhängig umblättern kann, um lustige Kombinationen zu erzeugen. Auf dem iPad werden drei Spalten (WER / TUT / WAS-WO) per Swipe unabhängig durchgescrollt:

```
┌─────────────────────────────────────────────────────┐
│                                                     │
│  ┌─────────┐  ┌───────────┐  ┌───────────────────┐ │
│  │    ↑    │  │     ↑     │  │        ↑          │ │
│  │         │  │           │  │                    │ │
│  │  Kael   │  │ Sammeln   │  │  Beeren am Bach   │ │
│  │         │  │           │  │                    │ │
│  │    ↓    │  │     ↓     │  │        ↓          │ │
│  └─────────┘  └───────────┘  └───────────────────┘ │
│   WER          TUT            WAS / WO              │
│                                                     │
│  ┌─────────────────────────────────────────────────┐│
│  │  "Kael sammelt Beeren am Bach"                  ││
│  └─────────────────────────────────────────────────┘│
│                                                     │
│            [ ✓ Auftrag erteilen ]                   │
│                                                     │
└─────────────────────────────────────────────────────┘
```

### Spalten-Details

**Spalte 1 – WER (Subjekt)**

```
Scrollbare Liste:
  Alle
  Nächster Freier
  ──────────────
  Kael ⚡ (neugierig)
  Mira 🛡️ (vorsichtig)
  Rana ✋ (geschickt)
  Taro 💪 (robust)
  Linh 🏃 (ausdauernd)
```

Trait-Icons direkt sichtbar. Siedler die gerade beschäftigt sind: ausgegraut mit aktuellem Auftrag als Tooltip.

**Spalte 2 – TUT (Prädikat)**

```
Scrollbare Liste (wächst mit Entdeckungen):
  Sammeln
  Erkunden
  Meiden
  ──────────────        ← Linie trennt Basis von Entdecktem
  Jagen (klein) 🔓
  Bauen 🔓
  Bearbeiten 🔓
  ──────────────
  Kochen 🔒              ← Sichtbar aber gesperrt (Feuer fehlt)
  Räuchern 🔒
  Fällen 🔒
```

Drei Zustände:
- **Verfügbar**: Normal auswählbar
- **Entdeckt (🔓)**: Durch Entdeckung freigeschaltet, auswählbar
- **Gesperrt (🔒)**: Sichtbar aber ausgegraut. Zeigt was noch möglich wird. Tap zeigt: "Erfordert: Feuer"

**Spalte 3 – WAS/WO (Objekte)**

Kontextabhängig – filtert basierend auf gewähltem Prädikat:

```
Wenn Prädikat = "Sammeln":
  Alles in der Nähe
  Beeren
  Wurzeln
  Steine
  ──────────────
  + am Bach
  + am Hang
  + am Felsvorsprung
```

```
Wenn Prädikat = "Herstellen":
  Verbundwerkzeug
    → Feuerstein + Hartholz + Harz    (Materialauswahl klappt auf)
    → Feuerstein + Weichholz + Schnur
  Grabstock
    → Hartholz
```

Bei **Herstellen** und **Bauen** wird die dritte Spalte breiter und zeigt Materialoptionen als Sub-Auswahl.

### Negation

Swipe nach links auf dem Prädikat kippt zu "NICHT":

```
  Sammeln  ←→  NICHT Sammeln
  Jagen    ←→  NICHT Jagen
```

Visuell: Durchgestrichen, roter Hintergrund. Ergebnis: "Alle jagen NICHT" (Verbot).

### Ergebnis-Zeile

Am unteren Rand wird der Auftrag als lesbarer Satz zusammengebaut:

```
"Kael sammelt Beeren am Bach"
"Alle jagen NICHT"
"Nächster Freier baut Windschirm hier"
"Mira stellt Verbundwerkzeug her aus Feuerstein, Hartholz und Harz"
```

Der Spieler sieht sofort, was er zusammengebaut hat. Tap auf "Auftrag erteilen" → Auftrag wird aktiv.

### Kontextuelles Öffnen

Das Klappbuch öffnet sich kontextabhängig mit **vorausgefüllter Spalte**:

| Spieleraktion | Vorausgefüllt | Spieler wählt |
|---------------|---------------|---------------|
| Tap auf Boden | WAS/WO = "hier" (Tap-Position) | WER + TUT |
| Tap auf Siedler | WER = Siedler-Name | TUT + WAS/WO |
| Tap auf Ressource | WAS = Ressource | WER + TUT |
| Long Press auf Boden | TUT = "Bauen", WAS/WO = "hier" | WER + Struktur |
| Aus Menü | Alles leer | Alles wählen |

So verbindet das Klappbuch die drei UI-Varianten (Ort-First, Siedler-First, Long-Press) aus dem Designdokument in ein einziges Interface – der Einstiegspunkt bestimmt nur, welche Spalte vorausgefüllt ist.

### Animation & Feel

- Spalten scrollen mit **Trägheit** (Flick-Scroll wie iOS Picker)
- **Haptic Feedback** beim Einrasten auf einer Auswahl
- Ungültige Kombinationen: Ergebnis-Zeile wird orange, "Auftrag erteilen" ausgegraut
- Gültige Kombination: Ergebnis-Zeile wird grün, Button aktiv
- **Sound**: Leises Klick-Geräusch beim Einrasten (wie Zahlenschloss)

### Spätere Erweiterungen (nicht I.1-Scope)

| Feature | Ab Epoche | Beschreibung |
|---------|-----------|-------------|
| Favoriten | I.6+ | Häufige Aufträge als Preset speichern ("★ Alle sammeln am Bach") |
| Filter | II.3+ | WER filtern nach Beruf, Standort, Erfahrung |
| Gesetze | II.3+ | Daueraufträge ("Jeder neue Erwachsene → Grundausbildung Sammeln") |
| Auftragsketten | II.1+ | "Erst sammeln, dann liefern an..." |
| Vorlagen | III+ | Komplexe Produktionsketten als wiederverwendbare Aufträge |

### Scope-Einschätzung

**M** – Die Grundmechanik (3 scrollbare Listen, Ergebnis-Satz, Bestätigungsbutton) ist ein Standard-Picker-Pattern auf iOS. Die Komplexität liegt in der kontextabhängigen Filterung der dritten Spalte und der Validierung gültiger Kombinationen. Empfehlung: Prototyp mit nur den Start-Prädikaten (Sammeln, Erkunden, Meiden) bauen und auf echtem iPad testen.

### Referenzen

- **Scribblenauts**: Wörter-basiertes Gameplay, bei dem der Spieler durch Kombination von Begriffen die Spielwelt beeinflusst
- **iOS UIPickerView**: Natives Scroll-Wheel-Pattern, das jeder iPad-Nutzer kennt
- **Reigns**: Simplifizierte Entscheidungs-UI die trotzdem komplexe Ergebnisse erzeugt

---

## Zusammenfassung: Alle Entscheidungen

| Nr. | Frage | Entscheidung |
|-----|-------|-------------|
| 1 | Durst | ~2 Min Intervall, physisch zum Wasser laufen, Behälter als Durchbruch-Entdeckung |
| 2 | Jahreszeiten | Ja, vereinfacht: 2 Jahreszeiten (biom-abhängig), ~15–20 Min pro Zyklus |
| 3 | Raubtiere | Beides: Zufalls-Events (sofort) + sichtbare Tiere mit eigenem Verhalten (MS Prio 2) |
| 4 | Werkzeugverschleiß | Pro Nutzung, kein Reparieren, Haltbarkeit steigt mit Qualität |
| 5 | Persönlichkeiten | 1 Trait pro Siedler (Neugierig, Vorsichtig, Geschickt, Robust, Ausdauernd) |
| 6 | Aktionsradius | Dynamisch durch Wasserreichweite, kein fester Parameter |
| 7 | Karte/Minimap | Keine in I.1, primitive Karte als Entdeckung in I.3 |
| 8 | Schwierigkeitsgrad | Rückschläge ja, Totalscheitern nein. Sicherheitsnetze + Pechschutz. |
| 9 | Alle tot | Neue Siedler wandern ein. Terrain bleibt, Wissen verloren, Werkzeuge am Boden. |
| 10 | Auftrags-Limit | Keine künstliche Begrenzung. Prioritätensystem für Konflikte. |
| 11 | Anführer | Nein in I.1. Emergente Spezialisierung. Hierarchien ab I.3. |
| 12 | Auftrags-UI | Klappbuch / Flip-Book: 3 scrollbare Spalten (WER / TUT / WAS-WO) |
