using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Elements;

public class PlacementItemData
{
    public string FamilyName { get; set; }
    public string TypeName { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public string LevelName { get; set; }
    public List<(string Name, object Value)> Parameters { get; set; } = new();
}

public class PlaceAndConfigureEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private const double MM_TO_FEET = 1.0 / 304.8;
    private readonly ManualResetEvent _resetEvent = new(false);

    public List<PlacementItemData> Placements { get; set; } = new();
    public AIResult<List<int>> Result { get; private set; }

    public void SetParameters(List<PlacementItemData> placements)
    {
        Placements = placements;
        _resetEvent.Reset();
    }

    public bool WaitForCompletion(int timeoutMilliseconds = 30000)
    {
        _resetEvent.Reset();
        return _resetEvent.WaitOne(timeoutMilliseconds);
    }

    public void Execute(UIApplication app)
    {
        var doc = app.ActiveUIDocument.Document;
        var createdIds = new List<int>();

        try
        {
            using var tg = new TransactionGroup(doc, "Place and Configure");
            tg.Start();

            foreach (var item in Placements)
            {
                // Find family symbol
                var symbol = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(fs =>
                        (string.IsNullOrEmpty(item.FamilyName) || fs.Family.Name == item.FamilyName) &&
                        (string.IsNullOrEmpty(item.TypeName) || fs.Name == item.TypeName));

                if (symbol == null)
                    throw new InvalidOperationException($"Family type '{item.FamilyName} : {item.TypeName}' not found.");

                if (!symbol.IsActive)
                {
                    using var txActivate = new Transaction(doc, "Activate");
                    txActivate.Start();
                    symbol.Activate();
                    txActivate.Commit();
                }

                // Resolve level
                var point = new XYZ(item.X * MM_TO_FEET, item.Y * MM_TO_FEET, item.Z * MM_TO_FEET);
                Level level = string.IsNullOrEmpty(item.LevelName)
                    ? doc.FindNearestLevel(point.Z)
                    : new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                        .FirstOrDefault(l => l.Name == item.LevelName)
                      ?? doc.FindNearestLevel(point.Z);

                FamilyInstance instance;
                using (var tx = new Transaction(doc, "Place Element"))
                {
                    tx.Start();
                    instance = doc.CreateInstance(symbol, locationPoint: point, baseLevel: level);
                    tx.Commit();
                }

                if (instance == null)
                    throw new InvalidOperationException($"Failed to place '{item.FamilyName} : {item.TypeName}'.");

                // Set parameters
                if (item.Parameters?.Count > 0)
                {
                    using var txParam = new Transaction(doc, "Set Parameters");
                    txParam.Start();
                    foreach (var (name, value) in item.Parameters)
                    {
                        var param = instance.LookupParameter(name);
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
                            case StorageType.ElementId:
                                if (int.TryParse(value?.ToString(), out int eid)) param.Set(new ElementId(eid)); break;
                        }
                    }
                    txParam.Commit();
                }

                createdIds.Add(instance.Id.GetIntValue());
            }

            tg.Assimilate();

            Result = new AIResult<List<int>>
            {
                Success = true,
                Message = $"Placed and configured {createdIds.Count} element(s).",
                Response = createdIds
            };
        }
        catch (Exception ex)
        {
            Result = new AIResult<List<int>> { Success = false, Message = ex.Message };
        }
        finally
        {
            _resetEvent.Set();
        }
    }

    public string GetName() => "Place and Configure";
}
