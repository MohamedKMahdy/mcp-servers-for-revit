using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Elements;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Elements;

public class GetParameterDefinitionsCommand : ExternalEventCommandBase
{
    private static readonly object _lock = new();
    private GetParameterDefinitionsEventHandler _handler => (GetParameterDefinitionsEventHandler)Handler;

    public override string CommandName => "get_parameter_definitions";

    public GetParameterDefinitionsCommand(UIApplication uiApp)
        : base(new GetParameterDefinitionsEventHandler(), uiApp) { }

    public override object Execute(JObject parameters, string requestId)
    {
        lock (_lock)
        {
            var elementId = parameters?["elementId"]?.Value<int>()
                ?? throw new ArgumentException("elementId is required.");

            bool includeType = parameters?["includeTypeParameters"]?.Value<bool>() ?? true;

            _handler.SetParameters(elementId, includeType);

            if (RaiseAndWaitForCompletion(15000))
                return _handler.Result;

            throw new TimeoutException("get_parameter_definitions timed out.");
        }
    }
}
