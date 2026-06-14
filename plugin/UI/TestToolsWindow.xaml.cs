using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace revit_mcp_plugin.UI
{
    public partial class TestToolsWindow : Window
    {
        private const int PORT = 8080;
        public ObservableCollection<TestCase> Tests { get; } = new();

        public TestToolsWindow()
        {
            InitializeComponent();
            BuildTestCases();
            TestListView.ItemsSource = Tests;
            _ = CheckServerAsync();
        }

        // ─── Test case definitions ────────────────────────────────────────────

        private void BuildTestCases()
        {
            // No element ID required
            Add("send_code_to_revit",       "Returns the current document title",               false,
                _ => Params("code", "return doc.Title;"));

            Add("get_current_view_info",    "Returns active view name and type",                false,
                _ => "{}");

            Add("analyze_model_statistics", "Counts elements by category",                      false,
                _ => "{}");

            Add("resolve_category",         "Maps 'door' → OST_Doors (no Revit needed)",        false,
                _ => Params("categoryName", "door"));

            Add("get_available_family_types", "Lists door family types",                        false,
                _ => Params("category", "OST_Doors"));

            Add("export_view_image",        "Exports active view to C:\\Users\\Public\\mcp_test.png", false,
                _ => ParamsObj(new { outputPath = @"C:\Users\Public\mcp_test.png", pixelWidth = 800, pixelHeight = 600 }));

            Add("get_current_view_elements", "Returns first 5 walls in view",                  false,
                _ => ParamsObj(new { category = "OST_Walls", limit = 5 }));

            // Placement — door on wall (draw a wall through point (2500,0) on Level 1 first)
            Add("create_point_based_element", "Place M_Single-Flush door at (2500,0) on nearest wall", false,
                _ => ParamsObj(new
                {
                    data = new[]
                    {
                        new
                        {
                            name = "M_Single-Flush",
                            locationPoint = new { x = 2500.0, y = 0.0, z = 0.0 },
                            width = 900.0, height = 2100.0, baseLevel = 0.0, baseOffset = 0.0
                        }
                    }
                }));

            Add("place_and_configure", "Atomic: place door at (2500,0) + set Mark=D-TEST", false,
                _ => ParamsObj(new
                {
                    placements = new[]
                    {
                        new
                        {
                            familyName = "M_Single-Flush",
                            typeName = "",
                            x = 2500.0, y = 0.0, z = 0.0,
                            parameters = new[] { new { name = "Mark", value = "D-TEST" } }
                        }
                    }
                }));

            // Requires element ID
            Add("get_element_info",         "Family/type/level/bbox for element",               true,
                id => Params("elementId", id));

            Add("get_element_parameters",   "All parameters on element",                       true,
                id => ParamsObj(new { elementId = ParseId(id), parameterNames = new string[0] }));

            Add("get_parameter_definitions","Full parameter schema (name/GUID/group)",          true,
                id => ParamsObj(new { elementId = ParseId(id), includeTypeParameters = true }));

            Add("set_element_parameter",    "Sets Mark = 'MCP-TEST' on element",               true,
                id => ParamsObj(new
                {
                    elementId = ParseId(id),
                    parameters = new[] { new { name = "Mark", value = "MCP-TEST" } }
                }));

            Add("tag_element",              "Auto-tags element in active view",                 true,
                id => ParamsObj(new { elementId = ParseId(id), useLeader = false, offsetX = 0, offsetY = 500 }));

            Add("duplicate_element",        "Copies element 1000 mm along X",                  true,
                id => ParamsObj(new { elementId = ParseId(id), dx = 1000.0, dy = 0.0, dz = 0.0 }));

            Add("operate_element",          "Selects & isolates element",                       true,
                id => ParamsObj(new { elementIds = new[] { id }, action = "Isolate" }));

            Add("ai_element_filter",        "Query: walls thicker than 200mm",                  false,
                _ => Params("query", "walls with width greater than 200mm"));
        }

        private void Add(string name, string desc, bool needsId, Func<string, string> getParams)
            => Tests.Add(new TestCase { Name = name, Description = desc, RequiresElementId = needsId, GetParams = getParams });

        // ─── TCP helper ───────────────────────────────────────────────────────

        private static async Task<string> SendCommandAsync(string method, string paramsJson)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var client = new TcpClient();
                    client.Connect("localhost", PORT);
                    client.ReceiveTimeout = 20000;
                    client.SendTimeout = 5000;

                    using var stream = client.GetStream();

                    var request = $"{{\"jsonrpc\":\"2.0\",\"id\":\"test\",\"method\":\"{method}\",\"params\":{paramsJson}}}";
                    var bytes = Encoding.UTF8.GetBytes(request);
                    stream.Write(bytes, 0, bytes.Length);

                    // Read response (may come in chunks)
                    var sb = new StringBuilder();
                    var buf = new byte[65536];
                    int read;
                    do
                    {
                        read = stream.Read(buf, 0, buf.Length);
                        sb.Append(Encoding.UTF8.GetString(buf, 0, read));
                    } while (read == buf.Length);

                    return sb.ToString();
                }
                catch (Exception ex)
                {
                    return $"{{\"error\": \"{ex.Message}\"}}";
                }
            });
        }

        private static async Task<bool> IsServerRunningAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var c = new TcpClient();
                    c.Connect("localhost", PORT);
                    return true;
                }
                catch { return false; }
            });
        }

        // ─── Event handlers ───────────────────────────────────────────────────

        private async void BtnCheckServer_Click(object sender, RoutedEventArgs e)
            => await CheckServerAsync();

        private async Task CheckServerAsync()
        {
            ServerStatusText.Text = "●";
            ServerStatusLabel.Text = "Checking…";
            ServerStatusText.Foreground = System.Windows.Media.Brushes.Orange;

            bool running = await IsServerRunningAsync();
            ServerStatusText.Foreground = running
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.Red;
            ServerStatusLabel.Text = running
                ? "Running on port 8080"
                : "Not running — click 'Revit MCP Switch' first";
        }

        private async void BtnGetSelected_Click(object sender, RoutedEventArgs e)
        {
            var raw = await SendCommandAsync("get_selected_elements", "{}");
            try
            {
                var obj = JObject.Parse(raw);
                var ids = obj["result"]?["elementIds"] as JArray
                       ?? obj["result"]?["Response"] as JArray;
                if (ids != null && ids.Count > 0)
                {
                    ElementIdBox.Text = ids[0].ToString();
                    ResultBox.Text = $"Picked element ID: {ids[0]}\n\nFull response:\n{FormatJson(raw)}";
                }
                else
                {
                    ResultBox.Text = "No elements selected in Revit. Select something first.\n\n" + FormatJson(raw);
                }
            }
            catch
            {
                ResultBox.Text = raw;
            }
        }

        private async void BtnRunSingle_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is not TestCase tc) return;
            await RunTest(tc);
            ResultBox.Text = tc.Result;
            TestListView.ScrollIntoView(tc);
        }

        private async void BtnRunAll_Click(object sender, RoutedEventArgs e)
        {
            BtnRunAll.IsEnabled = false;
            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.Value = 0;

            int total = Tests.Count;
            int done = 0;

            foreach (var tc in Tests)
            {
                await RunTest(tc);
                done++;
                ProgressBar.Value = (double)done / total * 100;
            }

            ProgressBar.Visibility = Visibility.Collapsed;
            BtnRunAll.IsEnabled = true;

            int passed = 0, failed = 0;
            foreach (var tc in Tests)
                if (tc.Status == "✓ Pass") passed++; else if (tc.Status == "✗ Fail") failed++;

            ResultBox.Text = $"Run complete — {passed} passed, {failed} failed out of {total} tests.";
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            foreach (var tc in Tests)
            {
                tc.Status = "Pending";
                tc.StatusColor = "Gray";
                tc.Result = "";
            }
            ResultBox.Text = "";
            ProgressBar.Value = 0;
        }

        // ─── Core test runner ─────────────────────────────────────────────────

        private async Task RunTest(TestCase tc)
        {
            tc.Status = "Running…";
            tc.StatusColor = "Orange";

            string elementId = ElementIdBox.Text.Trim();

            if (tc.RequiresElementId && string.IsNullOrEmpty(elementId))
            {
                tc.Status = "⚠ No ID";
                tc.StatusColor = "DarkOrange";
                tc.Result = "Skipped: provide an element ID in the box above, or use 'Get Selected'.";
                return;
            }

            string paramsJson;
            try { paramsJson = tc.GetParams(elementId); }
            catch (Exception ex) { paramsJson = $"{{\"error\":\"{ex.Message}\"}}"; }

            string raw = await SendCommandAsync(tc.Name, paramsJson);

            bool success = IsSuccess(raw);
            tc.Status = success ? "✓ Pass" : "✗ Fail";
            tc.StatusColor = success ? "Green" : "Red";
            tc.Result = $"=== {tc.Name} ===\nParams: {paramsJson}\n\nResponse:\n{FormatJson(raw)}";
        }

        // ─── Utilities ────────────────────────────────────────────────────────

        private static bool IsSuccess(string raw)
        {
            try
            {
                var obj = JObject.Parse(raw);
                if (obj["error"] != null) return false;
                var result = obj["result"];
                if (result == null) return false;
                // AIResult<T> returns { Success: true, ... }
                if (result["Success"] is JToken s) return s.Value<bool>();
                // Some commands return raw objects (delete, etc.)
                return true;
            }
            catch { return false; }
        }

        private static string FormatJson(string raw)
        {
            try { return JToken.Parse(raw).ToString(Formatting.Indented); }
            catch { return raw; }
        }

        private static string Params(string key, string value)
            => $"{{\"{key}\": \"{value}\"}}";

        private static string ParamsObj(object obj)
            => JsonConvert.SerializeObject(obj);

        private static int ParseId(string id)
            => int.TryParse(id, out int v) ? v : 0;
    }

    // ─── TestCase model ───────────────────────────────────────────────────────

    public class TestCase : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool RequiresElementId { get; set; }
        public Func<string, string> GetParams { get; set; }

        private string _status = "Pending";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        private string _statusColor = "Gray";
        public string StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(nameof(StatusColor)); }
        }

        private string _result = "";
        public string Result
        {
            get => _result;
            set { _result = value; OnPropertyChanged(nameof(Result)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
