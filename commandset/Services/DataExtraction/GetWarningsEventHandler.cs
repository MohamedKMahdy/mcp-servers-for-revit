using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    /// <summary>
    /// Reads Revit's CURRENT warning list (Document.GetWarnings) + recently-shown dialog pop-ups, so
    /// the AI agent can react to "duplicate Mark", "unhosted element", overlap, etc. instead of being
    /// blind to them. Read-only (no transaction).
    /// </summary>
    public class GetWarningsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public object Result { get; private set; }

        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                DialogWatcher.EnsureSubscribed(app);          // start observing pop-ups from now on
                var doc = app.ActiveUIDocument.Document;

                var warnings = doc.GetWarnings().Select(w => new
                {
                    description = w.GetDescriptionText(),
                    severity = w.GetSeverity().ToString(),
                    has_resolution = w.HasResolutions(),
                    failing_ids = w.GetFailingElements().Select(IdValue).ToList(),
                    additional_ids = w.GetAdditionalElements().Select(IdValue).ToList()
                }).ToList();

                Result = new { warnings = warnings, dialogs = DialogWatcher.Recent() };
            }
            catch (Exception ex)
            {
                Result = new { warnings = new List<object>(), dialogs = new List<object>(),
                               error = ex.Message };
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        private static long IdValue(ElementId id)
        {
#if REVIT2024_OR_GREATER
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }

        public string GetName() => "Get Revit warnings";
    }
}
