using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Elements;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Elements;

/// <summary>
/// pick_point — let the user click the placement location in Revit.
/// Params: { "mode": "point" | "line", "prompt": "optional status-bar text" }
/// Returns mm coordinates: point → {x,y,z}; line → {p0:{x,y,z}, p1:{x,y,z}}.
/// </summary>
public class PickPointCommand : ExternalEventCommandBase
{
    private static readonly object _lock = new();
    private PickPointEventHandler _handler => (PickPointEventHandler)Handler;

    public override string CommandName => "pick_point";

    public PickPointCommand(UIApplication uiApp)
        : base(new PickPointEventHandler(), uiApp) { }

    public override object Execute(JObject parameters, string requestId)
    {
        lock (_lock)
        {
            var mode = parameters?["mode"]?.Value<string>() ?? "point";
            var prompt = parameters?["prompt"]?.Value<string>();

            _handler.SetParameters(mode, prompt);

            // Long timeout: this blocks waiting for the USER to click in Revit, which can
            // take far longer than a normal tool. 3 minutes before giving up — kept just
            // under the Python bridge's socket timeout so the plugin reports the timeout
            // (cleanly) before the socket does.
            if (RaiseAndWaitForCompletion(180000))
                return _handler.Result;

            throw new TimeoutException("pick_point timed out (no point picked within 3 minutes).");
        }
    }
}
