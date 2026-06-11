import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetElementInfoTool(server: McpServer) {
  server.tool(
    "get_element_info",
    "Get detailed information about a Revit element: family name, type name, category, level, host, and bounding box.",
    {
      elementId: z.string().describe("The element ID to inspect"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_element_info", { elementId: args.elementId });
        });

        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return {
          content: [{
            type: "text",
            text: `get_element_info failed: ${error instanceof Error ? error.message : String(error)}`,
          }],
        };
      }
    }
  );
}
