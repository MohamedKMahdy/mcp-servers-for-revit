import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerTagElementTool(server: McpServer) {
  server.tool(
    "tag_element",
    "Tag a single Revit element in the active view. Auto-selects a matching tag family by category if tagTypeId is omitted.",
    {
      elementId: z.string().describe("The element ID to tag"),
      tagTypeId: z.string().optional().describe("Tag family type ID; auto-selected by category if omitted"),
      useLeader: z.boolean().optional().default(false).describe("Whether to show a leader line"),
      offsetX: z.number().optional().default(0).describe("Tag X offset from element center in mm"),
      offsetY: z.number().optional().default(500).describe("Tag Y offset from element center in mm"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("tag_element", {
            elementId: args.elementId,
            tagTypeId: args.tagTypeId ?? "",
            useLeader: args.useLeader ?? false,
            offsetX: args.offsetX ?? 0,
            offsetY: args.offsetY ?? 500,
          });
        });

        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return {
          content: [{
            type: "text",
            text: `tag_element failed: ${error instanceof Error ? error.message : String(error)}`,
          }],
        };
      }
    }
  );
}
