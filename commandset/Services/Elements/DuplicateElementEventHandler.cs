using Autodesk.Revit.DB;
using RevitMCPCommandSet.Utils;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Elements;

public class DuplicateElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private const double MM_TO_FEET = 1.0 / 304.8;
    private readonly ManualResetEvent _resetEvent = new(false);

    public int ElementId { get; set; }
    public double Dx { get; set; }
    public double Dy { get; set; }
    public double Dz { get; set; }
    public AIResult<int> Result { get; private set; }

    public void SetParameters(int elementId, double dx, double dy, double dz)
    {
        ElementId = elementId;
        Dx = dx; Dy = dy; Dz = dz;
        _resetEvent.Reset();
    }

    public bool WaitForCompletion(int timeoutMilliseconds = 15000)
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

            var translation = new XYZ(Dx * MM_TO_FEET, Dy * MM_TO_FEET, Dz * MM_TO_FEET);

            using var tx = new Transaction(doc, "Duplicate Element");
            tx.Start();
            try
            {
                var copied = ElementTransformUtils.CopyElement(doc, new ElementId(ElementId), translation);
                tx.Commit();

                var newId = copied.FirstOrDefault()?.GetIntValue() ?? -1;
                Result = new AIResult<int>
                {
                    Success = newId > 0,
                    Message = newId > 0 ? $"Duplicated element {ElementId} → {newId}." : "Copy returned no element.",
                    Response = newId
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

    public string GetName() => "Duplicate Element";
}
