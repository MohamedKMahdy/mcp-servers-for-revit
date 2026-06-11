using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Elements;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Elements;

public class SetElementParameterCommand : ExternalEventCommandBase
{
    private static readonly object _lock = new();
    private SetElementParameterEventHandler _handler => (SetElementParameterEventHandler)Handler;

    public override string CommandName => "set_element_parameter";

    public SetElementParameterCommand(UIApplication uiApp)
        : base(new SetElementParameterEventHandler(), uiApp) { }

    public override object Execute(JObject parameters, string requestId)
    {
        lock (_lock)
        {
            var elementId = parameters?["elementId"]?.Value<int>()
                ?? throw new ArgumentException("elementId is required.");

            var paramList = new List<(string Name, object Value)>();
            var arr = parameters?["parameters"] as JArray;
            if (arr != null)
            {
                foreach (var item in arr)
                {
                    var name = item["name"]?.Value<string>()
                        ?? throw new ArgumentException("Each parameter entry must have a 'name'.");
                    var val = item["value"]?.ToObject<object>();
                    paramList.Add((name, val));
                }
            }

            if (paramList.Count == 0)
                throw new ArgumentException("At least one parameter entry is required.");

            _handler.SetParameters(elementId, paramList);

            if (RaiseAndWaitForCompletion(15000))
                return _handler.Result;

            throw new TimeoutException("set_element_parameter timed out.");
        }
    }
}
