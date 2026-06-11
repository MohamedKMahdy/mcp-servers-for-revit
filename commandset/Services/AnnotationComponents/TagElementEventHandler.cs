using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.AnnotationComponents;

public class TagElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private const double MM_TO_FEET = 1.0 / 304.8;
    private readonly ManualResetEvent _resetEvent = new(false);

    public int ElementId { get; set; }
    public int TagTypeId { get; set; } = -1;
    public bool UseLeader { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public AIResult<int> Result { get; private set; }

    public void SetParameters(int elementId, int tagTypeId, bool useLeader, double offsetX, double offsetY)
    {
        ElementId = elementId;
        TagTypeId = tagTypeId;
        UseLeader = useLeader;
        OffsetX = offsetX;
        OffsetY = offsetY;
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
                Result = new AIResult<int> { Success = false, Message = $"Element {ElementId} not found." };
                return;
            }

            var view = doc.ActiveView;

            // Resolve tag type: use provided ID or auto-select by element category
            FamilySymbol tagSymbol = null;
            if (TagTypeId > 0)
            {
                tagSymbol = doc.GetElement(new ElementId(TagTypeId)) as FamilySymbol;
            }

            if (tagSymbol == null)
            {
                // Auto-select: find a tag family matching the element's category
                var categoryId = element.Category?.Id;
                tagSymbol = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(fs =>
                    {
                        var cat = fs.Category;
                        return cat != null &&
                               cat.Name.Contains("Tag") &&
                               fs.Family.FamilyCategoryId == categoryId;
                    });
            }

            if (tagSymbol == null)
            {
                Result = new AIResult<int> { Success = false, Message = $"No tag family found for element category '{element.Category?.Name}'." };
                return;
            }

            if (!tagSymbol.IsActive)
                tagSymbol.Activate();

            // Compute tag placement point
            XYZ elementPoint = XYZ.Zero;
            if (element.Location is LocationPoint lp)
                elementPoint = lp.Point;
            else if (element.Location is LocationCurve lc)
                elementPoint = lc.Curve.Evaluate(0.5, true);
            else if (element.get_BoundingBox(view) is BoundingBoxXYZ bb)
                elementPoint = (bb.Min + bb.Max) * 0.5;

            var tagPoint = new XYZ(
                elementPoint.X + OffsetX * MM_TO_FEET,
                elementPoint.Y + OffsetY * MM_TO_FEET,
                elementPoint.Z);

            using var tx = new Transaction(doc, "Tag Element");
            tx.Start();
            try
            {
                var tagRef = new Reference(element);
                var tag = IndependentTag.Create(
                    doc,
                    tagSymbol.Id,
                    view.Id,
                    tagRef,
                    UseLeader,
                    TagOrientation.Horizontal,
                    tagPoint);

                tx.Commit();
                Result = new AIResult<int>
                {
                    Success = true,
                    Message = $"Tagged element {ElementId} with tag {tag.Id.GetIntValue()}.",
                    Response = tag.Id.GetIntValue()
                };
            }
            catch
            {
                tx.RollBack();
                throw;
            }
        }
        catch (Exception ex)
        {
            Result = new AIResult<int> { Success = false, Message = ex.Message };
        }
        finally
        {
            _resetEvent.Set();
        }
    }

    public string GetName() => "Tag Element";
}
