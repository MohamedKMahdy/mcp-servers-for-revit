using System;
using Autodesk.Revit.UI;
using System.Reflection;
using System.Windows.Media.Imaging;



namespace revit_mcp_plugin.Core
{
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            // Ribbon setup is best-effort: a failure here must NOT fail the whole add-in,
            // or Revit disables it entirely (including the OnShutdown socket cleanup). The
            // other two thesis add-ins (BIMAssistant, RevitLogger) guard their ribbon the
            // same way.
            try
            {
                // Share the single "BIM Personalization" tab with the other thesis add-ins
                // (BIMAssistant, generalBIMlog RevitLogger). Whichever add-in loads first
                // creates the tab; the rest catch the "already exists" and just add a panel.
                const string tabName = "BIM Personalization";
                try { application.CreateRibbonTab(tabName); } catch { /* tab already created by another add-in */ }
                RibbonPanel mcpPanel = application.CreateRibbonPanel(tabName, "MCP Server");

                PushButtonData pushButtonData = new PushButtonData("ID_EXCMD_TOGGLE_REVIT_MCP", "Revit MCP\r\n Switch",
                    Assembly.GetExecutingAssembly().Location, "revit_mcp_plugin.Core.MCPServiceConnection");
                pushButtonData.ToolTip = "Open / Close mcp server";
                pushButtonData.Image = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/icon-16.png", UriKind.RelativeOrAbsolute));
                pushButtonData.LargeImage = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/icon-32.png", UriKind.RelativeOrAbsolute));
                mcpPanel.AddItem(pushButtonData);

                PushButtonData mcp_settings_pushButtonData = new PushButtonData("ID_EXCMD_MCP_SETTINGS", "Settings",
                    Assembly.GetExecutingAssembly().Location, "revit_mcp_plugin.Core.Settings");
                mcp_settings_pushButtonData.ToolTip = "MCP Settings";
                mcp_settings_pushButtonData.Image = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/settings-16.png", UriKind.RelativeOrAbsolute));
                mcp_settings_pushButtonData.LargeImage = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/settings-32.png", UriKind.RelativeOrAbsolute));
                mcpPanel.AddItem(mcp_settings_pushButtonData);

                PushButtonData testToolsButtonData = new PushButtonData("ID_EXCMD_TEST_TOOLS", "Test\r\nTools",
                    Assembly.GetExecutingAssembly().Location, "revit_mcp_plugin.Core.TestToolsCommand");
                testToolsButtonData.ToolTip = "Run tests for all registered MCP tools";
                testToolsButtonData.Image = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/settings-16.png", UriKind.RelativeOrAbsolute));
                testToolsButtonData.LargeImage = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/settings-32.png", UriKind.RelativeOrAbsolute));
                mcpPanel.AddItem(testToolsButtonData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[revit_mcp_plugin] Ribbon setup failed (non-fatal): {ex.Message}");
            }

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                if (SocketService.Instance.IsRunning)
                {
                    SocketService.Instance.Stop();
                }
            }
            catch { }

            return Result.Succeeded;
        }
    }
}
