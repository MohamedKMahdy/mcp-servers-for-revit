import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerExportViewImageTool(server: McpServer) {
  server.tool(
    "export_view_image",
    "Export the active or a named Revit view as a PNG image to a local file path.",
    {
      outputPath: z.string().describe("Absolute file path for the output PNG, e.g. C:\\temp\\view.png"),
      viewName: z.string().optional().describe("View name to export; active view used if omitted"),
      pixelWidth: z.number().optional().default(1920).describe("Output image width in pixels"),
      pixelHeight: z.number().optional().default(1080).describe("Output image height in pixels"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("export_view_image", {
            outputPath: args.outputPath,
            viewName: args.viewName ?? "",
            pixelWidth: args.pixelWidth ?? 1920,
            pixelHeight: args.pixelHeight ?? 1080,
          });
        });

        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return {
          content: [{
            type: "text",
            text: `export_view_image failed: ${error instanceof Error ? error.message : String(error)}`,
          }],
        };
      }
    }
  );
}
