# Revit MCP Playground

Kleine, eigenständige Spielwiese für ein MCP-Add-in in Autodesk Revit. Der
Startstand enthält genau ein **lesendes** Tool:

- `revit_get_document_info` – liest Revit-Version/-Build, aktives Dokument,
  Dokumenttyp, Worksharing-Status und Anzahl der Instanzelemente.

Es gibt keine Schreibwerkzeuge, keine Revit-`Transaction` und keine separate
Server-EXE.

## Struktur

```text
MCP_Client/                 MCPB-Manifest und gebündelter Node-stdio-Server
MCP_Server/                 C#-Revit-Add-in und lokale Bridge
created/generated outputs:
generated_MCP_Client/       erzeugte Claude-.mcpb
generated_MCP_Server/       getrennte Add-ins für Revit 2023 und 2026
scripts/                    reproduzierbarer Build
tests/                      MCP-Protokoll-Smoke-Test
```

Die Aufteilung orientiert sich konzeptionell an der Navisworks-Spielwiese:
Definition, Revit-Ausführung und generierte Artefakte bleiben getrennt. Es wurde
kein Quellcode aus den Referenzprojekten übernommen.

## Architektur

```text
Claude -> .mcpb/Node über stdio -> lokale TCP-Bridge -> ExternalEvent
       -> Revit-Hauptthread -> Revit API (nur lesen)
```

Der Node-Server spricht MCP über zeilenbegrenztes JSON-RPC. Er unterstützt die
aktuelle `server/discover`-Schnittstelle von MCP 2026-07-28 sowie den
`initialize`-Lebenszyklus 2025-11-25 für Claude-Kompatibilität. Das Tool ist mit
`readOnlyHint: true` und `destructiveHint: false` annotiert.

Die Revit-Bridge bindet ausschließlich an `127.0.0.1`, verwendet pro Revit-Jahr
einen eigenen Port und schreibt ein zufälliges Sitzungstoken nach
`%LOCALAPPDATA%\Waabe\RevitMcp\bridge-<year>.json`.

## Versionsmatrix

| Revit | Target Framework | API-Referenz | Bridge-Port |
|---|---|---|---|
| 2023 | `net48` | `C:\Program Files\Autodesk\Revit 2023\RevitAPI*.dll` | 48023 |
| 2026 | `net8.0-windows` | `C:\Program Files\Autodesk\Revit 2026\RevitAPI*.dll` | 48026 |

Andere Revit-Jahre werden im ersten Stand bewusst abgelehnt. Ein Build gegen
2023 ist nicht automatisch binär kompatibel mit 2026; beide Artefakte werden
separat gegen die jeweilige installierte API erzeugt.

## Bauen

Voraussetzungen: Windows, .NET SDK 8+, Node.js 18+, Revit 2023 und Revit 2026
mit installierten API-DLLs.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

Das Skript erzeugt keine EXE. Es baut zwei Add-in-DLL-Pakete, führt den
MCP-Protokoll-Smoke-Test aus und verpackt anschließend die Claude-Datei
`generated_MCP_Client\waabe-revit-readonly.mcpb`.

## Testen

1. Revit schließen.
2. Inhalt von `generated_MCP_Server\Revit2026\` nach
   `%APPDATA%\Autodesk\Revit\Addins\2026\` kopieren (für 2023 entsprechend).
3. Revit starten und ein Testmodell öffnen.
4. Die `.mcpb` in Claude Desktop importieren und dasselbe Revit-Jahr wählen.
5. `revit_get_document_info` aufrufen.

Für 2023 und 2026 getrennt testen. Das Beispiel verändert das Modell nicht.

## Referenzen (nur lesend)

- [Navisworks-Strukturvorlage](https://github.com/kikki/MCP-Add-in-Autodesk_Navisworks_Manage_2026)
- [Horizun Revit MCP](https://github.com/HorizunGroup/horizun-revit-mcp)
- [MCP 2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28)
- [MCPB Manifest 0.3](https://github.com/modelcontextprotocol/mcpb/blob/main/MANIFEST.md)

Der nächste Schritt nach erfolgreichem Lesen ist eine bewusst kleine,
transaktionsgeschützte Schreibfunktion. Sie ist noch nicht Bestandteil dieses
Startstands.
