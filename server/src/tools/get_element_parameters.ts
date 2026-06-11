import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetElementParametersTool(server: McpServer) {
  server.tool(
    "get_element_parameters",
    "Read parameter values from a Revit element. Returns name, value, storageType, and isReadOnly for each parameter.",
    {
      elementId: z.string().describe("The element ID to inspect"),
      parameterNames: z.array(z.string()).optional().describe("Filter to specific parameter names; omit to return all"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_element_parameters", {
            elementId: args.elementId,
            parameterNames: args.parameterNames ?? [],
          });
        });

        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return {
          content: [{
            type: "text",
            text: `get_element_parameters failed: ${error instanceof Error ? error.message : String(error)}`,
          }],
        };
      }
    }
  );
}
