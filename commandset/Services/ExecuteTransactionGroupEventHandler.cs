using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Services.Elements;
using RevitMCPCommandSet.Services.AnnotationComponents;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services;

public class TransactionSubCall
{
    public string Tool { get; set; }
    public JObject Params { get; set; }
}

public class ExecuteTransactionGroupEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private const double MM_TO_FEET = 1.0 / 304.8;
    private readonly ManualResetEvent _resetEvent = new(false);

    public List<TransactionSubCall> Calls { get; set; } = new();
    public string GroupName { get; set; } = "MCP Transaction Group";
    public bool DryRun { get; set; }
    public AIResult<List<object>> Result { get; private set; }

    public void SetParameters(List<TransactionSubCall> calls, string groupName, bool dryRun)
    {
        Calls = calls;
        GroupName = groupName;
        DryRun = dryRun;
        _resetEvent.Reset();
    }

    public bool WaitForCompletion(int timeoutMilliseconds = 60000)
    {
        _resetEvent.Reset();
        return _resetEvent.WaitOne(timeoutMilliseconds);
    }

    public void Execute(UIApplication app)
    {
        var doc = app.ActiveUIDocument.Document;
        var results = new List<object>();

        try
        {
            using var tg = new TransactionGroup(doc, GroupName);
            tg.Start();

            foreach (var call in Calls)
            {
                object stepResult = null;
                using var tx = new Transaction(doc, call.Tool);
                tx.Start();
                try
                {
                    stepResult = ExecuteSubCall(app, doc, call);
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    results.Add(new { tool = call.Tool, success = false, error = ex.Message });
                    tg.RollBack();
                    Result = new AIResult<List<object>>
                    {
                        Success = false,
                        Message = $"Sub-call '{call.Tool}' failed: {ex.Message}",
                        Response = results
                    };
                    return;
                }
                results.Add(new { tool = call.Tool, success = true, result = stepResult });
            }

            if (DryRun)
                tg.RollBack();
            else
                tg.Assimilate();

            Result = new AIResult<List<object>>
            {
                Success = true,
                Message = DryRun
                    ? $"Dry run: {Calls.Count} sub-call(s) validated, no changes committed."
                    : $"Committed {Calls.Count} sub-call(s).",
                Response = results
            };
        }
        catch (Exception ex)
        {
            Result = new AIResult<List<object>> { Success = false, Message = ex.Message };
        }
        finally
        {
            _resetEvent.Set();
        }
    }

    private object ExecuteSubCall(UIApplication app, Document doc, TransactionSubCall call)
    {
        switch (call.Tool)
        {
            case "set_element_parameter":
            {
                int elementId = call.Params["elementId"]!.Value<int>();
                var element = doc.GetElement(new ElementId(elementId))
                    ?? throw new InvalidOperationException($"Element {elementId} not found.");
                var parameters = call.Params["parameters"]?.ToObject<List<JObject>>() ?? new();
                var updated = new List<string>();
                foreach (var p in parameters)
                {
                    var name = p["name"]!.Value<string>();
                    var value = p["value"]?.ToObject<object>();
                    var param = element.LookupParameter(name);
                    if (param == null || param.IsReadOnly) continue;
                    switch (param.StorageType)
                    {
                        case StorageType.String: param.Set(value?.ToString() ?? ""); break;
                        case StorageType.Integer:
                            if (int.TryParse(value?.ToString(), out int iv)) param.Set(iv); break;
                        case StorageType.Double:
                            if (double.TryParse(value?.ToString(),
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out double dv)) param.Set(dv); break;
                    }
                    updated.Add(name);
                }
                return new { updated };
            }

            case "delete_element":
            {
                var ids = call.Params["elementIds"]?.ToObject<string[]>() ?? Array.Empty<string>();
                var toDelete = ids
                    .Select(s => int.TryParse(s, out int i) ? new ElementId(i) : null)
                    .Where(id => id != null && doc.GetElement(id) != null)
                    .ToList();
                doc.Delete(toDelete);
                return new { deleted = toDelete.Count };
            }

            case "tag_element":
            {
                // Delegating to inline logic; avoid reentrant ExternalEvent
                int elementId = call.Params["elementId"]!.Value<int>();
                var element = doc.GetElement(new ElementId(elementId))
                    ?? throw new InvalidOperationException($"Element {elementId} not found.");
                bool useLeader = call.Params["useLeader"]?.Value<bool>() ?? false;
                double offsetX = call.Params["offsetX"]?.Value<double>() ?? 0;
                double offsetY = call.Params["offsetY"]?.Value<double>() ?? 500;

                var view = doc.ActiveView;
                FamilySymbol tagSymbol = null;
                if (call.Params["tagTypeId"] is JToken tagTok && int.TryParse(tagTok.Value<string>(), out int tagTypeId) && tagTypeId > 0)
                    tagSymbol = doc.GetElement(new ElementId(tagTypeId)) as FamilySymbol;

                tagSymbol ??= new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                    .FirstOrDefault(fs => fs.Category?.Name.Contains("Tag") == true &&
                                          fs.Family.FamilyCategoryId == element.Category?.Id);

                if (tagSymbol == null)
                    throw new InvalidOperationException("No tag family found.");

                if (!tagSymbol.IsActive) tagSymbol.Activate();

                XYZ elemPt = XYZ.Zero;
                if (element.Location is LocationPoint lp) elemPt = lp.Point;
                else if (element.Location is LocationCurve lc) elemPt = lc.Curve.Evaluate(0.5, true);

                var tagPt = new XYZ(elemPt.X + offsetX * MM_TO_FEET, elemPt.Y + offsetY * MM_TO_FEET, elemPt.Z);
                var tag = IndependentTag.Create(doc, tagSymbol.Id, view.Id, new Reference(element),
                    useLeader, TagOrientation.Horizontal, tagPt);
                return new { tagId = tag.Id.GetIntValue() };
            }

            default:
                throw new NotSupportedException($"Sub-call tool '{call.Tool}' is not supported in execute_transaction_group. Supported: set_element_parameter, delete_element, tag_element.");
        }
    }

    public string GetName() => "Execute Transaction Group";
}
