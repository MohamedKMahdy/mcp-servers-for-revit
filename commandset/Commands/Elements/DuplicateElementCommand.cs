using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Elements;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Elements;

public class DuplicateElementCommand : ExternalEventCommandBase
{
    private static readonly object _lock = new();
    private DuplicateElementEventHandler _handler => (DuplicateElementEventHandler)Handler;

    public override string CommandName => "duplicate_element";

    public DuplicateElementCommand(UIApplication uiApp)
        : base(new DuplicateElementEventHandler(), uiApp) { }

    public override object Execute(JObject parameters, string requestId)
    {
        lock (_lock)
        {
            var elementId = parameters?["elementId"]?.Value<int>()
                ?? throw new ArgumentException("elementId is required.");

            double dx = parameters?["dx"]?.Value<double>() ?? 0;
            double dy = parameters?["dy"]?.Value<double>() ?? 0;
            double dz = parameters?["dz"]?.Value<double>() ?? 0;

            _handler.SetParameters(elementId, dx, dy, dz);

            if (RaiseAndWaitForCompletion(15000))
                return _handler.Result;

            throw new TimeoutException("duplicate_element timed out.");
        }
    }
}
