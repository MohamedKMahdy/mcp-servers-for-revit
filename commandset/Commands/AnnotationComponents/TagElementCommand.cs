using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.AnnotationComponents;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.AnnotationComponents;

public class TagElementCommand : ExternalEventCommandBase
{
    private static readonly object _lock = new();
    private TagElementEventHandler _handler => (TagElementEventHandler)Handler;

    public override string CommandName => "tag_element";

    public TagElementCommand(UIApplication uiApp)
        : base(new TagElementEventHandler(), uiApp) { }

    public override object Execute(JObject parameters, string requestId)
    {
        lock (_lock)
        {
            var elementId = parameters?["elementId"]?.Value<int>()
                ?? throw new ArgumentException("elementId is required.");

            int tagTypeId = -1;
            if (parameters?["tagTypeId"] is JToken tagTok && !string.IsNullOrEmpty(tagTok.Value<string>()))
                int.TryParse(tagTok.Value<string>(), out tagTypeId);

            bool useLeader = parameters?["useLeader"]?.Value<bool>() ?? false;
            double offsetX = parameters?["offsetX"]?.Value<double>() ?? 0;
            double offsetY = parameters?["offsetY"]?.Value<double>() ?? 500;

            _handler.SetParameters(elementId, tagTypeId, useLeader, offsetX, offsetY);

            if (RaiseAndWaitForCompletion(15000))
                return _handler.Result;

            throw new TimeoutException("tag_element timed out.");
        }
    }
}
