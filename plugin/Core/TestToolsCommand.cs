using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using revit_mcp_plugin.UI;

namespace revit_mcp_plugin.Core
{
    [Transaction(TransactionMode.Manual)]
    public class TestToolsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var window = new TestToolsWindow();
            // MODELESS, not ShowDialog(): a modal dialog blocks Revit's UI thread, so the
            // IExternalEvents that every tool raises never get processed and they all hit
            // their RaiseAndWaitForCompletion timeout. Show() keeps Revit's UI thread free
            // to service those events; the WindowInteropHelper owner keeps it atop Revit.
            // (Same pattern as Settings.cs.)
            _ = new System.Windows.Interop.WindowInteropHelper(window)
            {
                Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle
            };
            window.Show();
            return Result.Succeeded;
        }
    }
}
