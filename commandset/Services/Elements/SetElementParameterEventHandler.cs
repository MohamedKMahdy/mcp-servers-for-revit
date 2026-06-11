using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Elements;

public class SetElementParameterEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private readonly ManualResetEvent _resetEvent = new(false);

    public int ElementId { get; set; }
    public List<(string Name, object Value)> Parameters { get; set; } = new();
    public AIResult<List<string>> Result { get; private set; }

    public void SetParameters(int elementId, List<(string Name, object Value)> parameters)
    {
        ElementId = elementId;
        Parameters = parameters;
        _resetEvent.Reset();
    }

    public bool WaitForCompletion(int timeoutMilliseconds = 10000)
    {
        _resetEvent.Reset();
        return _resetEvent.WaitOne(timeoutMilliseconds);
    }

    public void Execute(UIApplication app)
    {
        var doc = app.ActiveUIDocument.Document;
        var updated = new List<string>();
        var failed = new List<string>();

        try
        {
            var element = doc.GetElement(new ElementId(ElementId));
            if (element == null)
            {
                Result = new AIResult<List<string>> { Success = false, Message = $"Element {ElementId} not found." };
                return;
            }

            using var tx = new Transaction(doc, "Set Element Parameters");
            tx.Start();
            try
            {
                foreach (var (name, value) in Parameters)
                {
                    var param = element.LookupParameter(name);
                    if (param == null || param.IsReadOnly)
                    {
                        failed.Add(name);
                        continue;
                    }

                    bool ok = param.StorageType switch
                    {
                        StorageType.String => param.Set(value?.ToString() ?? ""),
                        StorageType.Integer => int.TryParse(value?.ToString(), out int iv) && param.Set(iv),
                        StorageType.Double => double.TryParse(value?.ToString(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out double dv) && param.Set(dv),
                        StorageType.ElementId => int.TryParse(value?.ToString(), out int eid) && param.Set(new ElementId(eid)),
                        _ => false
                    };

                    (ok ? updated : failed).Add(name);
                }
                tx.Commit();
            }
            catch
            {
                tx.RollBack();
                throw;
            }

            Result = new AIResult<List<string>>
            {
                Success = failed.Count == 0,
                Message = $"Updated {updated.Count} parameter(s). Failed: {string.Join(", ", failed)}",
                Response = updated
            };
        }
        catch (Exception ex)
        {
            Result = new AIResult<List<string>> { Success = false, Message = ex.Message };
        }
        finally
        {
            _resetEvent.Set();
        }
    }

    public string GetName() => "Set Element Parameter";
}
