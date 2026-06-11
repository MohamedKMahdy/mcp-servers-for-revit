using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Elements;

public class GetParameterDefinitionsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private readonly ManualResetEvent _resetEvent = new(false);

    public int ElementId { get; set; }
    public bool IncludeTypeParameters { get; set; } = true;
    public AIResult<List<object>> Result { get; private set; }

    public void SetParameters(int elementId, bool includeTypeParameters)
    {
        ElementId = elementId;
        IncludeTypeParameters = includeTypeParameters;
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

            var defs = new List<object>();

            void CollectParams(Element el, bool isType)
            {
                foreach (Parameter param in el.Parameters)
                {
                    var def = param.Definition;
                    string guid = null;
                    if (def is ExternalDefinition extDef)
                        guid = extDef.GUID.ToString();

                    defs.Add(new
                    {
                        name = def.Name,
                        guid,
                        storageType = param.StorageType.ToString(),
                        group = def.GetGroupTypeId()?.TypeId ?? "",
                        isShared = def is ExternalDefinition,
                        isReadOnly = param.IsReadOnly,
                        isTypeParameter = isType
                    });
                }
            }

            CollectParams(element, false);

            if (IncludeTypeParameters)
            {
                var typeId = element.GetTypeId();
                if (typeId != null && typeId != ElementId.InvalidElementId)
                {
                    var typeElem = doc.GetElement(typeId);
                    if (typeElem != null)
                        CollectParams(typeElem, true);
                }
            }

            Result = new AIResult<List<object>>
            {
                Success = true,
                Message = $"Found {defs.Count} parameter definition(s).",
                Response = defs
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

    public string GetName() => "Get Parameter Definitions";
}
