# Generated Revit add-ins

Run `powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1`.

The build creates separate `Revit2023` and `Revit2026` layouts. Copy the
`Waabe.RevitMcp.Loader.addin` file and adjacent `Waabe.RevitMcp.Addin` folder into
`%APPDATA%\Autodesk\Revit\Addins\<year>\` while Revit is closed.
