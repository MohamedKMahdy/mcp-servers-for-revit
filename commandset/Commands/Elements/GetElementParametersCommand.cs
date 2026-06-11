using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Elements;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Elements;

public class GetElementParametersCommand : ExternalEventCommandBase
{
    private static readonly object _lock = new();
    private GetElementParametersEventHandler _handler => (GetElementParametersEventHandler)Handler;

    public override string CommandName => "get_element_parameters";

    public GetElementParametersCommand(UIApplication uiApp)
        : base(new GetElementParametersEventHandler(), uiApp) { }

    public override object Execute(JObject parameters, string requestId)
    {
        lock (_lock)
        {
            var elementId = parameters?["elementId"]?.Value<int>()
                ?? throw new ArgumentException("elementId is required.");

            var names = parameters?["parameterNames"]?.ToObject<List<string>>() ?? new();

            _handler.SetParameters(elementId, names);

            if (RaiseAndWaitForCompletion(15000))
                return _handler.Result;

            throw new TimeoutException("get_element_parameters timed out.");
        }
    }
}
