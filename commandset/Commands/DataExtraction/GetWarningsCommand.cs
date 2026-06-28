using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    /// <summary>
    /// get_warnings — read-only introspection of Revit's live warnings/errors + recent dialog pop-ups.
    /// Lets the self-healing executor + outcome verifier SEE Revit's own failure messages (duplicate
    /// Mark, unhosted, overlap, …) rather than only the tool success flag.
    /// </summary>
    public class GetWarningsCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private GetWarningsEventHandler _handler => (GetWarningsEventHandler)Handler;

        public override string CommandName => "get_warnings";

        public GetWarningsCommand(UIApplication uiApp)
            : base(new GetWarningsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    if (RaiseAndWaitForCompletion(15000))
                        return _handler.Result;
                    throw new TimeoutException("get_warnings timed out");
                }
                catch (Exception ex)
                {
                    throw new Exception($"get_warnings failed: {ex.Message}");
                }
            }
        }
    }
}
