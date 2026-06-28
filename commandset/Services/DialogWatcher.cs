using System;
using System.Collections.Generic;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Observes Revit's modal dialogs / pop-ups (TaskDialogs, message boxes) so the AI agent can READ
    /// them — e.g. "Elements have duplicate 'Mark' values", "can't keep elements joined". We only
    /// OBSERVE (never auto-dismiss): DialogBoxShowing fires synchronously just before the dialog is
    /// shown, so even a modal that then blocks Revit is recorded here; the agent reads the buffer on a
    /// later get_warnings call (once the dialog is dismissed and external events resume).
    ///
    /// Subscription is lazy (first get_warnings call) so the whole feature lives in the command-set
    /// assembly and needs no change to the plugin host.
    /// </summary>
    public static class DialogWatcher
    {
        private const int MaxRecent = 25;
        private static readonly object _lock = new object();
        private static readonly List<DialogRecord> _recent = new List<DialogRecord>();
        private static bool _subscribed;

        public class DialogRecord
        {
            public string dialog_id { get; set; }
            public string message { get; set; }
        }

        /// <summary>Subscribe once to the live UIApplication's dialog event (idempotent).</summary>
        public static void EnsureSubscribed(UIApplication app)
        {
            lock (_lock)
            {
                if (_subscribed || app == null) return;
                try
                {
                    app.DialogBoxShowing += OnDialogBoxShowing;
                    _subscribed = true;
                }
                catch { /* if the host blocks the subscription, get_warnings still returns warnings */ }
            }
        }

        private static void OnDialogBoxShowing(object sender, DialogBoxShowingEventArgs e)
        {
            try
            {
                var rec = new DialogRecord { dialog_id = e?.DialogId ?? "", message = "" };
                if (e is TaskDialogShowingEventArgs t)
                    rec.message = t.Message ?? "";
                lock (_lock)
                {
                    _recent.Add(rec);
                    while (_recent.Count > MaxRecent) _recent.RemoveAt(0);
                }
            }
            catch { /* never let observation break the user's dialog */ }
        }

        /// <summary>A snapshot of recently-shown dialogs (oldest first).</summary>
        public static List<DialogRecord> Recent()
        {
            lock (_lock) { return new List<DialogRecord>(_recent); }
        }
    }
}
