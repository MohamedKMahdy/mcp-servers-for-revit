using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Elements;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Elements;

public class GetElementInfoCommand : ExternalEventCommandBase
{
    private static readonly object _lock = new();
    private GetElementInfoEventHandler _handler => (GetElementInfoEventHandler)Handler;

    public override string CommandName => "get_element_info";

    public GetElementInfoCommand(UIApplication uiApp)
        : base(new GetElementInfoEventHandler(), uiApp) { }

    public override object Execute(JObject parameters, string requestId)
    {
        lock (_lock)
        {
            var elementId = parameters?["elementId"]?.Value<int>()
                ?? throw new ArgumentException("elementId is required.");

            _handler.SetParameters(elementId);

            if (RaiseAndWaitForCompletion(15000))
                return _handler.Result;

            throw new TimeoutException("get_element_info timed out.");
        }
    }
}
