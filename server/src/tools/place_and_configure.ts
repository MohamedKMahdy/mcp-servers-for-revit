import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

const ParameterPair = z.object({
  name: z.string(),
  value: z.union([z.string(), z.number(), z.boolean()]),
});

const PlacementItem = z.object({
  familyName: z.string().describe("Family name to place"),
  typeName: z.string().describe("Type name to place"),
  x: z.number().describe("X coordinate in mm"),
  y: z.number().describe("Y coordinate in mm"),
  z: z.number().optional().default(0).describe("Z coordinate in mm"),
  levelName: z.string().optional().describe("Level name; nearest level used if omitted"),
  parameters: z.array(ParameterPair).optional().default([]).describe("Parameters to set after placement"),
});

export function registerPlaceAndConfigureTool(server: McpServer) {
  server.tool(
    "place_and_configure",
    "Atomically place one or more elements AND set their parameters in a single TransactionGroup. More reliable than separate place + set_parameter calls.",
    {
      placements: z.array(PlacementItem).describe("List of elements to place with their parameters"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("place_and_configure", { placements: args.placements });
        });

        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return {
          content: [{
            type: "text",
            text: `place_and_configure failed: ${error instanceof Error ? error.message : String(error)}`,
          }],
        };
      }
    }
  );
}
