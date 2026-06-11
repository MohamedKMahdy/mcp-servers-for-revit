using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Elements;

public class GetElementParametersEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private readonly ManualResetEvent _resetEvent = new(false);

    public int ElementId { get; set; }
    public List<string> ParameterNames { get; set; } = new();
    public AIResult<List<object>> Result { get; private set; }

    public void SetParameters(int elementId, List<string> parameterNames)
    {
        ElementId = elementId;
        ParameterNames = parameterNames;
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

        try
        {
            var element = doc.GetElement(new ElementId(ElementId));
            if (element == null)
            {
                Result = new AIResult<List<object>> { Success = false, Message = $"Element {ElementId} not found." };
                return;
            }

            var results = new List<object>();
            var allParams = element.Parameters.Cast<Parameter>();

            if (ParameterNames != null && ParameterNames.Count > 0)
                allParams = allParams.Where(p => ParameterNames.Contains(p.Definition.Name));

            foreach (var param in allParams)
            {
                object val = param.StorageType switch
                {
                    StorageType.String => param.AsString(),
                    StorageType.Integer => param.AsInteger(),
                    StorageType.Double => param.AsDouble(),
                    StorageType.ElementId => param.AsElementId()?.GetIntValue(),
                    _ => null
                };

                results.Add(new
                {
                    name = param.Definition.Name,
                    value = val,
                    storageType = param.StorageType.ToString(),
                    isReadOnly = param.IsReadOnly,
                    group = param.Definition.GetGroupTypeId()?.TypeId ?? ""
                });
            }

            Result = new AIResult<List<object>>
            {
                Success = true,
                Message = $"Returned {results.Count} parameter(s).",
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

    public string GetName() => "Get Element Parameters";
}
