import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import readline from "node:readline";

const child = spawn(process.execPath, ["MCP_Client/server/index.js"], { stdio: ["pipe", "pipe", "inherit"] });
const lines = readline.createInterface({ input: child.stdout, crlfDelay: Infinity });
const pending = [];
lines.on("line", line => pending.shift()?.(JSON.parse(line)));
const request = message => new Promise(resolve => { pending.push(resolve); child.stdin.write(JSON.stringify(message) + "\n"); });

const discover = await request({ jsonrpc: "2.0", id: 1, method: "server/discover", params: { _meta: { "io.modelcontextprotocol/protocolVersion": "2026-07-28", "io.modelcontextprotocol/clientCapabilities": {} } } });
assert.equal(discover.result.resultType, "complete");
assert.ok(discover.result.supportedVersions.includes("2026-07-28"));

const initialize = await request({ jsonrpc: "2.0", id: 2, method: "initialize", params: { protocolVersion: "2025-11-25", capabilities: {}, clientInfo: { name: "smoke", version: "1" } } });
assert.equal(initialize.result.protocolVersion, "2025-11-25");
child.stdin.write(JSON.stringify({ jsonrpc: "2.0", method: "notifications/initialized" }) + "\n");

const tools = await request({ jsonrpc: "2.0", id: 3, method: "tools/list", params: {} });
assert.equal(tools.result.tools.length, 1);
assert.equal(tools.result.tools[0].name, "revit_get_document_info");
assert.equal(tools.result.tools[0].annotations.readOnlyHint, true);

child.kill();
console.log("MCP protocol smoke test passed.");
