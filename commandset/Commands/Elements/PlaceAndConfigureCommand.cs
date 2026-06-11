using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Elements;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Elements;

public class PlaceAndConfigureCommand : ExternalEventCommandBase
{
    private static readonly object _lock = new();
    private PlaceAndConfigureEventHandler _handler => (PlaceAndConfigureEventHandler)Handler;

    public override string CommandName => "place_and_configure";

    public PlaceAndConfigureCommand(UIApplication uiApp)
        : base(new PlaceAndConfigureEventHandler(), uiApp) { }

    public override object Execute(JObject parameters, string requestId)
    {
        lock (_lock)
        {
            var placementsArray = parameters?["placements"] as JArray
                ?? throw new ArgumentException("'placements' array is required.");

            var placements = new List<PlacementItemData>();
            foreach (var item in placementsArray)
            {
                var paramPairs = new List<(string Name, object Value)>();
                if (item["parameters"] is JArray paramsArr)
                {
                    foreach (var p in paramsArr)
                        paramPairs.Add((p["name"]!.Value<string>(), p["value"]?.ToObject<object>()));
                }

                placements.Add(new PlacementItemData
                {
                    FamilyName = item["familyName"]?.Value<string>() ?? "",
                    TypeName = item["typeName"]?.Value<string>() ?? "",
                    X = item["x"]?.Value<double>() ?? 0,
                    Y = item["y"]?.Value<double>() ?? 0,
                    Z = item["z"]?.Value<double>() ?? 0,
                    LevelName = item["levelName"]?.Value<string>() ?? "",
                    Parameters = paramPairs
                });
            }

            _handler.SetParameters(placements);

            if (RaiseAndWaitForCompletion(60000))
                return _handler.Result;

            throw new TimeoutException("place_and_configure timed out.");
        }
    }
}
