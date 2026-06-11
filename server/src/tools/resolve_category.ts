import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";

// Static map: common names / aliases → Revit BuiltInCategory string
const CATEGORY_MAP: Record<string, string> = {
  // Doors
  door: "OST_Doors", doors: "OST_Doors", porte: "OST_Doors", portes: "OST_Doors",
  tür: "OST_Doors", türen: "OST_Doors", puerta: "OST_Doors", puertas: "OST_Doors",
  // Windows
  window: "OST_Windows", windows: "OST_Windows", fenêtre: "OST_Windows", fenêtres: "OST_Windows",
  fenster: "OST_Windows", ventana: "OST_Windows", ventanas: "OST_Windows",
  // Walls
  wall: "OST_Walls", walls: "OST_Walls", mur: "OST_Walls", murs: "OST_Walls",
  wand: "OST_Walls", wände: "OST_Walls", pared: "OST_Walls", paredes: "OST_Walls",
  // Floors
  floor: "OST_Floors", floors: "OST_Floors", plancher: "OST_Floors", dalles: "OST_Floors",
  boden: "OST_Floors", suelo: "OST_Floors",
  // Ceilings
  ceiling: "OST_Ceilings", ceilings: "OST_Ceilings", plafond: "OST_Ceilings",
  decke: "OST_Ceilings", techo: "OST_Ceilings",
  // Roofs
  roof: "OST_Roofs", roofs: "OST_Roofs", toit: "OST_Roofs", toiture: "OST_Roofs",
  dach: "OST_Roofs", tejado: "OST_Roofs",
  // Columns
  column: "OST_Columns", columns: "OST_Columns", colonne: "OST_Columns", colonnes: "OST_Columns",
  säule: "OST_Columns", columna: "OST_Columns",
  "structural column": "OST_StructuralColumns", "structural columns": "OST_StructuralColumns",
  // Beams / Framing
  beam: "OST_StructuralFraming", beams: "OST_StructuralFraming",
  framing: "OST_StructuralFraming", "structural framing": "OST_StructuralFraming",
  poutre: "OST_StructuralFraming", poutres: "OST_StructuralFraming",
  // Furniture
  furniture: "OST_Furniture", meuble: "OST_Furniture", meubles: "OST_Furniture",
  möbel: "OST_Furniture", mueble: "OST_Furniture",
  // Rooms
  room: "OST_Rooms", rooms: "OST_Rooms", pièce: "OST_Rooms", pièces: "OST_Rooms",
  raum: "OST_Rooms", räume: "OST_Rooms", habitación: "OST_Rooms",
  // Stairs
  stair: "OST_Stairs", stairs: "OST_Stairs", escalier: "OST_Stairs",
  treppe: "OST_Stairs", escalera: "OST_Stairs",
  // Railing
  railing: "OST_StairsRailing", railings: "OST_StairsRailing",
  garde: "OST_StairsRailing", geländer: "OST_StairsRailing",
  // Mechanical equipment
  "mechanical equipment": "OST_MechanicalEquipment",
  // Plumbing fixtures
  "plumbing fixture": "OST_PlumbingFixtures", "plumbing fixtures": "OST_PlumbingFixtures",
  // Lighting
  "lighting fixture": "OST_LightingFixtures", "lighting fixtures": "OST_LightingFixtures",
  luminaire: "OST_LightingFixtures", luminaires: "OST_LightingFixtures",
  // Electrical
  "electrical fixture": "OST_ElectricalFixtures", "electrical fixtures": "OST_ElectricalFixtures",
  // Casework
  casework: "OST_Casework",
  // Generic models
  "generic model": "OST_GenericModel", "generic models": "OST_GenericModel",
  // Mass
  mass: "OST_Mass",
  // Parking
  parking: "OST_Parking",
  // Site
  site: "OST_Site", topography: "OST_Topography",
};

export function registerResolveCategoryTool(server: McpServer) {
  server.tool(
    "resolve_category",
    "Convert a plain-language category name (in any language) to a Revit BuiltInCategory string like 'OST_Doors'. Zero Revit round-trip — pure static lookup.",
    {
      categoryName: z.string().describe("Human-readable category name, e.g. 'door', 'Doors', 'porte', 'fenêtre'"),
    },
    async (args, extra) => {
      const key = args.categoryName.trim().toLowerCase();
      const resolved = CATEGORY_MAP[key];

      if (resolved) {
        return {
          content: [{
            type: "text",
            text: JSON.stringify({ success: true, input: args.categoryName, category: resolved }, null, 2),
          }],
        };
      }

      // Try partial match for multi-word inputs like "Structural Columns"
      for (const [mapKey, mapVal] of Object.entries(CATEGORY_MAP)) {
        if (key.includes(mapKey) || mapKey.includes(key)) {
          return {
            content: [{
              type: "text",
              text: JSON.stringify({ success: true, input: args.categoryName, category: mapVal, matchType: "partial" }, null, 2),
            }],
          };
        }
      }

      return {
        content: [{
          type: "text",
          text: JSON.stringify({
            success: false,
            input: args.categoryName,
            message: `No mapping found for '${args.categoryName}'. Use the exact BuiltInCategory string (e.g. 'OST_Doors') or check Revit API docs.`,
            availableKeys: Object.keys(CATEGORY_MAP).slice(0, 30),
          }, null, 2),
        }],
      };
    }
  );
}
