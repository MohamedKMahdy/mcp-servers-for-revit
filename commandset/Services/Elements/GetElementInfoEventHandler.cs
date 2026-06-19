using Autodesk.Revit.DB;
using RevitMCPCommandSet.Utils;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Elements;

public class GetElementInfoEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private const double FEET_TO_MM = 304.8;
    private readonly ManualResetEvent _resetEvent = new(false);

    public int ElementId { get; set; }
    public AIResult<object> Result { get; private set; }

    public void SetParameters(int elementId)
    {
        ElementId = elementId;
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
                Result = new AIResult<object> { Success = false, Message = $"Element {ElementId} not found." };
                return;
            }

            string familyName = null, typeName = null;
            if (element is FamilyInstance fi)
            {
                familyName = fi.Symbol?.Family?.Name;
                typeName = fi.Symbol?.Name;
            }
            else if (element.GetTypeId() is ElementId typeId && typeId != ElementId.InvalidElementId)
            {
                var etype = doc.GetElement(typeId);
                typeName = etype?.Name;
            }

            // Level
            string levelName = null;
            var levelParam = element.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
                             ?? element.get_Parameter(BuiltInParameter.LEVEL_PARAM);
            if (levelParam != null && levelParam.AsElementId() is ElementId lid && lid != ElementId.InvalidElementId)
                levelName = (doc.GetElement(lid) as Level)?.Name;

            // Host
            string hostId = null;
            if (element is FamilyInstance fi2 && fi2.Host != null)
                hostId = fi2.Host.Id.GetIntValue().ToString();

            // Location
            object locationData = null;
            if (element.Location is LocationPoint lp)
                locationData = new { type = "point", x = lp.Point.X * FEET_TO_MM, y = lp.Point.Y * FEET_TO_MM, z = lp.Point.Z * FEET_TO_MM };
            else if (element.Location is LocationCurve lc)
            {
                var s = lc.Curve.GetEndPoint(0);
                var e = lc.Curve.GetEndPoint(1);
                locationData = new { type = "curve", startX = s.X * FEET_TO_MM, startY = s.Y * FEET_TO_MM, startZ = s.Z * FEET_TO_MM, endX = e.X * FEET_TO_MM, endY = e.Y * FEET_TO_MM, endZ = e.Z * FEET_TO_MM };
            }

            // Bounding box
            object bbox = null;
            var bb = element.get_BoundingBox(null);
            if (bb != null)
                bbox = new
                {
                    minX = bb.Min.X * FEET_TO_MM, minY = bb.Min.Y * FEET_TO_MM, minZ = bb.Min.Z * FEET_TO_MM,
                    maxX = bb.Max.X * FEET_TO_MM, maxY = bb.Max.Y * FEET_TO_MM, maxZ = bb.Max.Z * FEET_TO_MM
                };

            Result = new AIResult<object>
            {
                Success = true,
                Message = "Element info retrieved.",
                Response = new
                {
                    elementId = ElementId,
                    familyName,
                    typeName,
                    category = element.Category?.Name,
                    level = levelName,
                    hostId,
                    location = locationData,
                    boundingBox = bbox
                }
            };
        }
        catch (Exception ex)
        {
            Result = new AIResult<object> { Success = false, Message = ex.Message };
        }
        finally
        {
            _resetEvent.Set();
        }
    }

    public string GetName() => "Get Element Info";
}
