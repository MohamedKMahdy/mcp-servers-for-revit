import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerDuplicateElementTool(server: McpServer) {
  server.tool(
    "duplicate_element",
    "Copy a Revit element and offset the copy by (dx, dy, dz) in mm. Returns the new element ID.",
    {
      elementId: z.string().describe("Source element ID to copy"),
      dx: z.number().optional().default(0).describe("X offset in mm"),
      dy: z.number().optional().default(0).describe("Y offset in mm"),
      dz: z.number().optional().default(0).describe("Z offset in mm"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("duplicate_element", {
            elementId: args.elementId,
            dx: args.dx ?? 0,
            dy: args.dy ?? 0,
            dz: args.dz ?? 0,
          });
        });

        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return {
          content: [{
            type: "text",
            text: `duplicate_element failed: ${error instanceof Error ? error.message : String(error)}`,
          }],
        };
      }
    }
  );
}
