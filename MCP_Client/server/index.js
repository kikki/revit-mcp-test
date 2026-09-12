#!/usr/bin/env node
import fs from "node:fs/promises";
import net from "node:net";
import os from "node:os";
import path from "node:path";
import readline from "node:readline";

const SERVER = { name: "waabe-revit-readonly", title: "WAABE Revit MCP Read-only", version: "0.1.0" };
const MODERN_PROTOCOL = "2026-07-28";
const LEGACY_PROTOCOLS = ["2025-11-25", "2025-06-18", "2024-11-05"];
const SUPPORTED_PROTOCOLS = [MODERN_PROTOCOL, ...LEGACY_PROTOCOLS];
const TOOL = {
  name: "revit_get_document_info",
  title: "Read active Revit document information",
  description: "Returns the Revit version/build and metadata for the active document. Read-only: no Transaction is opened and no element is modified.",
  inputSchema: { type: "object", properties: {}, additionalProperties: false },
  outputSchema: {
    type: "object",
    properties: {
      revitVersion: { type: "string" },
      revitBuild: { type: "string" },
      documentOpen: { type: "boolean" },
      title: { type: ["string", "null"] },
      path: { type: ["string", "null"] },
      isFamilyDocument: { type: "boolean" },
      isWorkshared: { type: "boolean" },
      instanceElementCount: { type: "integer" }
    },
    required: ["revitVersion", "revitBuild", "documentOpen", "isFamilyDocument", "isWorkshared", "instanceElementCount"],
    additionalProperties: false
  },
  annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false }
};

function send(message) {
  process.stdout.write(JSON.stringify(message) + "\n");
}
function result(id, value) { send({ jsonrpc: "2.0", id, result: value }); }
function error(id, code, message, data) {
  const payload = { code, message };
  if (data !== undefined) payload.data = data;
  send({ jsonrpc: "2.0", id, error: payload });
}
function protocolFrom(message) {
  return message?.params?._meta?.["io.modelcontextprotocol/protocolVersion"];
}
function assertModernProtocol(message) {
  const requested = protocolFrom(message);
  if (requested && !SUPPORTED_PROTOCOLS.includes(requested)) {
    error(message.id, -32022, "Unsupported MCP protocol version", { requested, supported: SUPPORTED_PROTOCOLS });
    return false;
  }
  return true;
}

async function readDiscovery() {
  const year = process.env.REVIT_TARGET_YEAR || "2026";
  if (!/^(2023|2026)$/.test(year)) throw new Error("REVIT_TARGET_YEAR must be 2023 or 2026.");
  const localAppData = process.env.LOCALAPPDATA || path.join(os.homedir(), "AppData", "Local");
  const discoveryPath = path.join(localAppData, "Waabe", "RevitMcp", `bridge-${year}.json`);
  let text;
  try { text = await fs.readFile(discoveryPath, "utf8"); }
  catch { throw new Error(`Revit ${year} bridge not found. Start Revit ${year} with the WAABE add-in loaded.`); }
  const record = JSON.parse(text);
  if (record.schema !== "waabe-revit-mcp-discovery/1" || record.revitYear !== Number(year))
    throw new Error(`Invalid discovery record: ${discoveryPath}`);
  return record;
}

async function callBridge(method) {
  const record = await readDiscovery();
  return await new Promise((resolve, reject) => {
    const socket = net.createConnection({ host: "127.0.0.1", port: record.port });
    socket.setEncoding("utf8");
    socket.setTimeout(22000);
    let buffer = "";
    socket.on("connect", () => socket.write(JSON.stringify({ token: record.token, method }) + "\n"));
    socket.on("data", chunk => {
      buffer += chunk;
      const newline = buffer.indexOf("\n");
      if (newline < 0) return;
      socket.end();
      try {
        const response = JSON.parse(buffer.slice(0, newline));
        if (!response.ok) reject(new Error(`${response.error?.code || "BRIDGE_ERROR"}: ${response.error?.message || "Unknown bridge error"}`));
        else resolve(response.data);
      } catch (e) { reject(e); }
    });
    socket.on("timeout", () => { socket.destroy(); reject(new Error("Timed out waiting for the Revit add-in.")); });
    socket.on("error", reject);
  });
}

async function handle(message) {
  if (!message || message.jsonrpc !== "2.0" || typeof message.method !== "string") {
    if (message?.id !== undefined) error(message.id, -32600, "Invalid JSON-RPC request");
    return;
  }

  if (message.method === "server/discover") {
    result(message.id, {
      resultType: "complete",
      supportedVersions: SUPPORTED_PROTOCOLS,
      capabilities: { tools: {} },
      _meta: { "io.modelcontextprotocol/serverInfo": SERVER },
      instructions: "Read-only Revit playground. The only tool reads active-document metadata.",
      ttlMs: 60000,
      cacheScope: "private"
    });
    return;
  }

  if (message.method === "initialize") {
    const requested = message.params?.protocolVersion;
    const negotiated = SUPPORTED_PROTOCOLS.includes(requested) ? requested : LEGACY_PROTOCOLS[0];
    result(message.id, {
      protocolVersion: negotiated,
      capabilities: { tools: { listChanged: false } },
      serverInfo: SERVER,
      instructions: "Read-only Revit playground. Start the matching Revit 2023 or 2026 add-in before calling tools."
    });
    return;
  }

  if (message.method === "notifications/initialized" || message.method === "notifications/cancelled") return;
  if (!assertModernProtocol(message)) return;
  if (message.method === "ping") { result(message.id, {}); return; }
  if (message.method === "tools/list") {
    result(message.id, { resultType: "complete", tools: [TOOL] });
    return;
  }
  if (message.method === "tools/call") {
    if (message.params?.name !== TOOL.name) { error(message.id, -32602, "Unknown tool"); return; }
    const args = message.params?.arguments || {};
    if (Object.keys(args).length !== 0) { error(message.id, -32602, "This tool accepts no arguments"); return; }
    try {
      const data = await callBridge("get_document_info");
      result(message.id, {
        resultType: "complete",
        content: [{ type: "text", text: JSON.stringify(data, null, 2) }],
        structuredContent: data,
        isError: false
      });
    } catch (e) {
      result(message.id, {
        resultType: "complete",
        content: [{ type: "text", text: e instanceof Error ? e.message : String(e) }],
        isError: true
      });
    }
    return;
  }
  error(message.id, -32601, `Method not found: ${message.method}`);
}

const input = readline.createInterface({ input: process.stdin, crlfDelay: Infinity });
input.on("line", line => {
  if (!line.trim()) return;
  try { Promise.resolve(handle(JSON.parse(line))).catch(e => console.error(e)); }
  catch { error(null, -32700, "Parse error"); }
});
input.on("close", () => process.exit(0));
