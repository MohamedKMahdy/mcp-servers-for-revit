import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

const SubCall = z.object({
  tool: z.string().describe("Tool/command name, e.g. 'set_element_parameter'"),
  params: z.record(z.unknown()).describe("Parameters for that tool call"),
});

export function registerExecuteTransactionGroupTool(server: McpServer) {
  server.tool(
    "execute_transaction_group",
    "Execute multiple sub-calls (set_element_parameter, tag_element, etc.) inside a single Revit TransactionGroup. Set dryRun:true to validate without committing.",
    {
      calls: z.array(SubCall).describe("Ordered list of {tool, params} sub-calls to execute atomically"),
      groupName: z.string().optional().default("MCP Transaction Group").describe("Name for the TransactionGroup in the Revit undo stack"),
      dryRun: z.boolean().optional().default(false).describe("If true, roll back all changes after validation"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("execute_transaction_group", {
            calls: args.calls,
            groupName: args.groupName ?? "MCP Transaction Group",
            dryRun: args.dryRun ?? false,
          });
        });

        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return {
          content: [{
            type: "text",
            text: `execute_transaction_group failed: ${error instanceof Error ? error.message : String(error)}`,
          }],
        };
      }
    }
  );
}
