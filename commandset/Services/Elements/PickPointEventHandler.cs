using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Elements;

/// <summary>
/// Lets the user PICK the placement location interactively in Revit instead of typing
/// coordinates. Runs on the Revit UI thread via the external-event pattern, so
/// Selection.PickPoint() is in a valid API context.
///   mode "point" → one PickPoint (point-based / hosted elements like doors, furniture)
///   mode "line"  → two PickPoints (line-based elements like walls, beams)
/// Coordinates are returned in MILLIMETRES (Revit internal feet × 304.8), matching the
/// units the create_* tools expect.
/// </summary>
public class PickPointEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private const double FEET_TO_MM = 304.8;
    private readonly ManualResetEvent _resetEvent = new(false);

    public string Mode { get; set; } = "point";
    public string Prompt { get; set; }
    public AIResult<object> Result { get; private set; }

    public void SetParameters(string mode, string prompt)
    {
        Mode = string.IsNullOrWhiteSpace(mode) ? "point" : mode.Trim().ToLowerInvariant();
        Prompt = prompt;
        _resetEvent.Reset();
    }

    public bool WaitForCompletion(int timeoutMilliseconds = 120000)
    {
        _resetEvent.Reset();
        return _resetEvent.WaitOne(timeoutMilliseconds);
    }

    public void Execute(UIApplication app)
    {
        try
        {
            var uidoc = app.ActiveUIDocument;
            if (uidoc == null)
            {
                Result = new AIResult<object> { Success = false, Message = "No active document." };
                return;
            }
            Selection sel = uidoc.Selection;

            if (Mode == "line")
            {
                string lead = string.IsNullOrWhiteSpace(Prompt) ? "Pick the" : Prompt;
                XYZ a = sel.PickPoint($"{lead} START point (Esc to cancel)");
                XYZ b = sel.PickPoint($"{lead} END point (Esc to cancel)");
                Result = new AIResult<object>
                {
                    Success = true,
                    Message = "Picked 2 points.",
                    Response = new
                    {
                        mode = "line",
                        p0 = new { x = a.X * FEET_TO_MM, y = a.Y * FEET_TO_MM, z = a.Z * FEET_TO_MM },
                        p1 = new { x = b.X * FEET_TO_MM, y = b.Y * FEET_TO_MM, z = b.Z * FEET_TO_MM }
                    }
                };
            }
            else
            {
                string prompt = string.IsNullOrWhiteSpace(Prompt) ? "Pick the location (Esc to cancel)" : Prompt;
                XYZ p = sel.PickPoint(prompt);
                Result = new AIResult<object>
                {
                    Success = true,
                    Message = "Picked 1 point.",
                    Response = new
                    {
                        mode = "point",
                        x = p.X * FEET_TO_MM,
                        y = p.Y * FEET_TO_MM,
                        z = p.Z * FEET_TO_MM
                    }
                };
            }
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // User pressed Esc — not an error, just nothing picked.
            Result = new AIResult<object> { Success = false, Message = "Pick cancelled." };
        }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException)
        {
            // PickPoint isn't allowed in the active view (schedule, sheet, legend, ...).
            Result = new AIResult<object>
            {
                Success = false,
                Message = "Can't pick here — switch to a plan, section, elevation or 3D view first."
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

    public string GetName() => "Pick Point";
}
