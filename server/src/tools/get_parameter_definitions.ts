import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetParameterDefinitionsTool(server: McpServer) {
  server.tool(
    "get_parameter_definitions",
    "List all parameter definitions on a Revit element or its type: name, GUID, storageType, group, isShared, isReadOnly.",
    {
      elementId: z.string().describe("Element ID to inspect"),
      includeTypeParameters: z.boolean().optional().default(true).describe("Also return parameters from the element type"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_parameter_definitions", {
            elementId: args.elementId,
            includeTypeParameters: args.includeTypeParameters ?? true,
          });
        });

        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return {
          content: [{
            type: "text",
            text: `get_parameter_definitions failed: ${error instanceof Error ? error.message : String(error)}`,
          }],
        };
      }
    }
  );
}
