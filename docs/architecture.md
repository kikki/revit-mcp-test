# Architecture

```text
Claude Desktop
  -> MCPB-bundled Node stdio server (MCP JSON-RPC)
  -> token-protected loopback TCP request
  -> Revit add-in background listener
  -> ExternalEvent
  -> Revit UI thread / Revit API
```

There is no separately distributed server executable. The MCPB contains the
Node server. The Revit add-in is compiled once against the Revit 2023 API on
.NET Framework 4.8 and once against the Revit 2026 API on .NET 8.

The first tool is read-only. Its Revit handler never opens a `Transaction`.
ExternalEvent is still mandatory because a background listener is not a valid
Revit API context.

## References used as design input only

- Structure reference: https://github.com/kikki/MCP-Add-in-Autodesk_Navisworks_Manage_2026
- Revit architecture reference: https://github.com/HorizunGroup/horizun-revit-mcp
- MCP specification: https://modelcontextprotocol.io/specification/2026-07-28
- Legacy lifecycle used by current Claude clients: https://modelcontextprotocol.io/specification/2025-11-25/basic/lifecycle
- MCPB format: https://github.com/modelcontextprotocol/mcpb/blob/main/MANIFEST.md

No source code was copied from the reference repositories.
