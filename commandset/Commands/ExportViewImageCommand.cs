using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands;

public class ExportViewImageCommand : ExternalEventCommandBase
{
    private static readonly object _lock = new();
    private ExportViewImageEventHandler _handler => (ExportViewImageEventHandler)Handler;

    public override string CommandName => "export_view_image";

    public ExportViewImageCommand(UIApplication uiApp)
        : base(new ExportViewImageEventHandler(), uiApp) { }

    public override object Execute(JObject parameters, string requestId)
    {
        lock (_lock)
        {
            var outputPath = parameters?["outputPath"]?.Value<string>()
                ?? throw new ArgumentException("outputPath is required.");
            string viewName = parameters?["viewName"]?.Value<string>() ?? "";
            int pixelWidth = parameters?["pixelWidth"]?.Value<int>() ?? 1920;
            int pixelHeight = parameters?["pixelHeight"]?.Value<int>() ?? 1080;

            _handler.SetParameters(outputPath, viewName, pixelWidth, pixelHeight);

            if (RaiseAndWaitForCompletion(60000))
                return _handler.Result;

            throw new TimeoutException("export_view_image timed out.");
        }
    }
}
