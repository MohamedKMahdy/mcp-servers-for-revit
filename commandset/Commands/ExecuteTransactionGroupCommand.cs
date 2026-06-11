using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands;

public class ExecuteTransactionGroupCommand : ExternalEventCommandBase
{
    private static readonly object _lock = new();
    private ExecuteTransactionGroupEventHandler _handler => (ExecuteTransactionGroupEventHandler)Handler;

    public override string CommandName => "execute_transaction_group";

    public ExecuteTransactionGroupCommand(UIApplication uiApp)
        : base(new ExecuteTransactionGroupEventHandler(), uiApp) { }

    public override object Execute(JObject parameters, string requestId)
    {
        lock (_lock)
        {
            var callsArray = parameters?["calls"] as JArray
                ?? throw new ArgumentException("'calls' array is required.");

            var calls = callsArray.Select(c => new TransactionSubCall
            {
                Tool = c["tool"]?.Value<string>() ?? throw new ArgumentException("Each call must have a 'tool'."),
                Params = c["params"] as JObject ?? new JObject()
            }).ToList();

            string groupName = parameters?["groupName"]?.Value<string>() ?? "MCP Transaction Group";
            bool dryRun = parameters?["dryRun"]?.Value<bool>() ?? false;

            _handler.SetParameters(calls, groupName, dryRun);

            if (RaiseAndWaitForCompletion(120000))
                return _handler.Result;

            throw new TimeoutException("execute_transaction_group timed out.");
        }
    }
}
