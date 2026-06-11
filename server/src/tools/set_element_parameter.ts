import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

const ParameterPair = z.object({
  name: z.string().describe("Parameter name"),
  value: z.union([z.string(), z.number(), z.boolean()]).describe("Parameter value"),
});

export function registerSetElementParameterTool(server: McpServer) {
  server.tool(
    "set_element_parameter",
    "Set one or more parameters on a Revit element by element ID. Use 'parameters' array for batch updates.",
    {
      elementId: z.string().describe("The element ID to modify"),
      parameterName: z.string().optional().describe("Single parameter name (use instead of 'parameters' for one param)"),
      value: z.union([z.string(), z.number(), z.boolean()]).optional().describe("Value for the single parameter"),
      parameters: z.array(ParameterPair).optional().describe("Array of {name, value} pairs for batch update"),
    },
    async (args, extra) => {
      try {
        const params: Record<string, unknown> = { elementId: args.elementId };
        if (args.parameters && args.parameters.length > 0) {
          params.parameters = args.parameters;
        } else if (args.parameterName !== undefined) {
          params.parameters = [{ name: args.parameterName, value: args.value }];
        } else {
          return { content: [{ type: "text", text: "Provide either 'parameters' array or 'parameterName'+'value'." }] };
        }

        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("set_element_parameter", params);
        });

        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return {
          content: [{
            type: "text",
            text: `set_element_parameter failed: ${error instanceof Error ? error.message : String(error)}`,
          }],
        };
      }
    }
  );
}
