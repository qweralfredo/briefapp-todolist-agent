#!/usr/bin/env node
/**
 * briefapp-mcp-proxy.mjs
 * 
 * Stdio ↔ Streamable HTTP proxy for Briefapp MCP Server.
 * Bridges Antigravity (stdio) to Docker MCP (Streamable HTTP v1.27+).
 */

import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StreamableHTTPClientTransport } from "@modelcontextprotocol/sdk/client/streamableHttp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import {
  ListToolsRequestSchema,
  CallToolRequestSchema,
  ListResourcesRequestSchema,
  ListResourceTemplatesRequestSchema,
  ReadResourceRequestSchema,
  ListPromptsRequestSchema,
  GetPromptRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";

const MCP_ENDPOINT = process.env.MCP_ENDPOINT || "http://localhost:8481/mcp";

// ── Upstream (Streamable HTTP → Docker MCP) ─────────────────────────────────
const upstream = new Client({ name: "briefapp-proxy", version: "1.0.0" });
const transport = new StreamableHTTPClientTransport(new URL(MCP_ENDPOINT));
await upstream.connect(transport);

// ── Downstream (stdio → Antigravity) ────────────────────────────────────────
const server = new Server(
  { name: "briefapp-todo-list-mcp", version: "1.0.0" },
  {
    capabilities: {
      tools: { listChanged: false },
      resources: { subscribe: false, listChanged: false },
      prompts: { listChanged: false },
    },
  }
);

// Tools
server.setRequestHandler(ListToolsRequestSchema, async () => {
  const r = await upstream.listTools();
  return { tools: r.tools };
});

server.setRequestHandler(CallToolRequestSchema, async (req) => {
  const r = await upstream.callTool({
    name: req.params.name,
    arguments: req.params.arguments || {},
  });
  return { content: r.content, isError: r.isError };
});

// Resources
server.setRequestHandler(ListResourcesRequestSchema, async () => {
  const r = await upstream.listResources();
  return { resources: r.resources };
});

server.setRequestHandler(ListResourceTemplatesRequestSchema, async () => {
  const r = await upstream.listResourceTemplates();
  return { resourceTemplates: r.resourceTemplates };
});

server.setRequestHandler(ReadResourceRequestSchema, async (req) => {
  const r = await upstream.readResource({ uri: req.params.uri });
  return { contents: r.contents };
});

// Prompts
server.setRequestHandler(ListPromptsRequestSchema, async () => {
  const r = await upstream.listPrompts();
  return { prompts: r.prompts };
});

server.setRequestHandler(GetPromptRequestSchema, async (req) => {
  const r = await upstream.getPrompt({
    name: req.params.name,
    arguments: req.params.arguments || {},
  });
  return { description: r.description, messages: r.messages };
});

// ── Start ───────────────────────────────────────────────────────────────────
const stdioTransport = new StdioServerTransport();
await server.connect(stdioTransport);
process.stderr.write(`[briefapp-proxy] Connected to ${MCP_ENDPOINT}\n`);
