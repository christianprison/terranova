# Terranova – Terraforming

> **Referenziert von**: [gdd-terranova.md](gdd-terranova.md), Sektion 2.1
> **Version**: 0.9
> **Zuletzt aktualisiert**: 2026-02-11

---

## Designprinzip

Eine einheitliche, allgemeingültige Terraforming-Mechanik. Dieselbe Grundmechanik wird von Siedlern, Gebäuden und Spielereignissen (Katastrophen, GenAI-Events) gleichermaßen genutzt. Keine Spezial-Subsysteme für einzelne Anwendungsfälle wie Bergbau oder Kanalbau – diese sind alle Anwendungsfälle derselben drei Grundoperationen.

---

## Grundmechanik: Drei Operationen

Das gesamte Terraforming basiert auf drei atomaren Operationen auf Block-Ebene:

| Operation | Beschreibung | Beispiele |
|-----------|-------------|-----------|
| **Entfernen** | Blöcke werden abgetragen | Graben ausheben, Stollen graben, Steinbruch, Hügel einebnen, Tagebau |
| **Hinzufügen** | Blöcke werden aufgeschüttet | Wall errichten, Damm bauen, Landgewinnung, Abraum aufschütten |
| **Transformieren** | Block-Typ wird geändert (ohne Geometrie-Änderung) | Erde → Acker, Sand → Fundament, Fels → Tunnel-Wand, Boden → Weg |

Alles andere – Bergwerke, Kanäle, Staudämme, Festungsgräben, Felder – sind **Anwendungsfälle** dieser drei Grundoperationen, keine eigenen Systeme.

---

## Wer kann Terraformen?

| Auslöser | Wie | Beispiele |
|----------|-----|-----------|
| **Spieler (indirekt)** | Markiert Gebiet + Auftrag, Siedler führen aus | Graben ausheben, Weg planieren, Feld anlegen |
| **Gebäude (automatisch)** | Gebäude-Betrieb verändert Terrain als Nebeneffekt | Steinbruch erzeugt Grube, Holzfäller lichtet Wald, Bergwerk gräbt Stollen |
| **Spielereignisse** | Events verändern Terrain über dieselbe Mechanik | Erdbeben → Risse und Erdrutsche, Überschwemmung → Erosion, Vulkan → Lava/Asche |
| **GenAI-Events** | Extern generierte Events nutzen dieselbe API | Meteoriteneinschlag, tektonische Verschiebung, Flussänderung |

---

## Terraforming-Aufwand & Kosten

Jede Terraforming-Operation hat Kosten, die vom Block-Typ und der verfügbaren Technologie abhängen:

| Faktor | Effekt auf Aufwand |
|--------|-------------------|
| **Block-Typ** | Erde ist leicht, Stein schwer, Fels sehr schwer. Wasser kann nicht direkt entfernt werden (nur verdrängt/umgeleitet). |
| **Werkzeug-Niveau** | Steinwerkzeug (langsam) → Bronze (schneller) → Eisen → Stahl → Maschinen (sehr schnell) |
| **Menge** | Linearer Aufwand pro Block. Große Projekte brauchen viele Siedler und Zeit. |
| **Tiefe** | Tiefere Operationen ggf. aufwändiger (Abtransport, Abstützung) |

---

## Rückkopplung auf das Bausystem

Terraforming und Bausystem sind direkt gekoppelt. Das Terrain unter einem Gebäude bestimmt die Baukosten und -dauer mit.

Jedes Gebäude hat einen **Terrain-Footprint** – die Fläche und Ebenheit, die es benötigt. Wenn der Bauplatz nicht den Anforderungen entspricht, muss Terraforming durchgeführt werden, bevor oder während gebaut wird.

| Terrain-Zustand | Effekt auf Bau |
|----------------|---------------|
| **Ebene Fläche, passender Untergrund** | Standard-Baukosten und -dauer |
| **Leichte Unebenheit** | Siedler planieren automatisch → leicht erhöhte Dauer |
| **Starke Neigung / Hang** | Terrassierung nötig → deutlich erhöhte Kosten und Dauer, ggf. Stützmauern als Zusatzressource |
| **Fels-Untergrund** | Abtragung nötig → stark erhöhte Dauer, bessere Werkzeuge erforderlich |
| **Sumpf / Wasser** | Drainage / Aufschüttung nötig → spezielle Materialien, hoher Aufwand |
| **Bereits terraformte Fläche** | Kein Zusatzaufwand → belohnt vorausschauende Planung |

**Designziel**: Der Spieler spürt den Unterschied zwischen "ich baue in der Ebene" und "ich baue am Berghang" – nicht als Verbot, sondern als Trade-off. Die Bergfestung ist teurer und dauert länger, aber strategisch wertvoll.

**Vorschau-System**: Beim Platzieren eines Gebäudes zeigt das Interface den geschätzten Terraforming-Aufwand an (zusätzliche Kosten, zusätzliche Zeit, benötigte Werkzeuge), sodass der Spieler informierte Entscheidungen treffen kann.

---

## Terraforming nach Ären

| Ära / Epoche | Verfügbare Terrain-Operationen | Typische Anwendungen |
|-------------|-------------------------------|---------------------|
| Ära I (früh) | Nur Nebeneffekte + einfaches Entfernen/Transformieren (Erde, Sand) | Bäume fällen → Lichtung. Steine sammeln. Einfache Fundamente. |
| Ära I (spät) | Gezieltes Entfernen und Hinzufügen (Erde) | Felder planieren. Bewässerungsgräben. Erdwälle. |
| Ära II (früh) | Steinbearbeitung, tiefere Operationen | Straßen, Bergwerksstollen, Hafenanlagen, Kanäle. |
| Ära II (spät) | Großes Volumen, komplexe Operationen | Befestigungsanlagen, Terrassenbau, Tagebau, Festungsgräben. |
| Ära III | Maschinelle Unterstützung, massives Volumen | Hügel einebnen, Staudämme, Tunnel, industrieller Tagebau, Eisenbahntrassen. |
| Ära IV | [DEFINIEREN] | Nano-Terraforming? Planetares Engineering? |

---

## Terraforming-Interface (Touch)

- Spieler wählt Terraforming-Werkzeug aus dem Bau-Menü
- Finger/Pencil malt den betroffenen Bereich auf die Karte
- System zeigt Vorschau: geschätzter Aufwand (Siedler, Zeit, Ressourcen), Block-Typen im Bereich
- Bestätigung → Siedler beginnen mit der Arbeit
- Fortschritt sichtbar in der Welt (Graben wird Stück für Stück tiefer, etc.)

---

## Systemische Auswirkungen

- **Bausystem**: Terrain bestimmt Baukosten mit (siehe oben)
- **Wegfindung**: Muss Terrain-Änderungen berücksichtigen (neue Wege, blockierte Pfade, Gräben als Hindernisse)
- **Sichtlinien**: Terrain-Änderungen beeinflussen Sichtbarkeit (Hügel abtragen → mehr Sicht, Wall errichten → Sicht blockiert)
- **Forschung**: Terraforming kann verborgene Ressourcen freilegen → biom-getriebene Entdeckungen auslösen (siehe [research.md](research.md))
- **Wasserfluss**: [DEFINIEREN – Simples System? Wasser fließt in Vertiefungen? Relevant für Bewässerung und Staudämme]
- **Bodenstabilität**: [DEFINIEREN – Können Hänge abrutschen? Brauchen Tunnel Stützstrukturen?]
- **Events**: Naturkatastrophen und GenAI-Events nutzen dieselbe Terraforming-API wie Spieleraktionen

---

## Hinweis für Unity Developer

Die einheitliche Terraforming-Mechanik (Entfernen/Hinzufügen/Transformieren) vereinfacht die Architektur gegenüber Spezial-Subsystemen. Alle Terrain-Modifikationen – ob durch Spieler, Gebäude-Betrieb oder Events – gehen durch dasselbe System. Chunk-Mesh-Updates sind dadurch vorhersehbar und regelbasiert. Frühzeitig testen: Performance bei großflächigen Operationen, die mehrere Chunks betreffen.
