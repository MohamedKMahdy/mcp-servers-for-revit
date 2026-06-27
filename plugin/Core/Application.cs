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
                testToolsButtonData.Image = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/test-16.png", UriKind.RelativeOrAbsolute));
                testToolsButtonData.LargeImage = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/test-32.png", UriKind.RelativeOrAbsolute));
                mcpPanel.AddItem(testToolsButtonData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[revit_mcp_plugin] Ribbon setup failed (non-fatal): {ex.Message}");
            }

            // Auto-start the MCP socket server (:8080) by default so it's up without the user having
            // to click "Revit MCP Switch" each session. The switch still toggles it off/on. We do this
            // on ApplicationInitialized rather than here because SocketService.Initialize needs a
            // UIApplication, and OnStartup only has a UIControlledApplication.
            application.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;

            return Result.Succeeded;
        }

        private void OnApplicationInitialized(object sender, Autodesk.Revit.DB.Events.ApplicationInitializedEventArgs e)
        {
            try
            {
                var app = sender as Autodesk.Revit.ApplicationServices.Application;
                if (app == null) return;
                var uiApp = new UIApplication(app);
                if (!SocketService.Instance.IsRunning)
                {
                    SocketService.Instance.Initialize(uiApp);
                    SocketService.Instance.Start();
                }
            }
            catch (Exception ex)
            {
                // Non-fatal: the user can still start it manually with the Switch button.
                System.Diagnostics.Debug.WriteLine($"[revit_mcp_plugin] MCP auto-start failed (non-fatal): {ex.Message}");
            }
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
