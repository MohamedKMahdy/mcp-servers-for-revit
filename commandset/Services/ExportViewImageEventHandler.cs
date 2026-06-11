using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services;

public class ExportViewImageEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private readonly ManualResetEvent _resetEvent = new(false);

    public string OutputPath { get; set; }
    public string ViewName { get; set; }
    public int PixelWidth { get; set; } = 1920;
    public int PixelHeight { get; set; } = 1080;
    public AIResult<string> Result { get; private set; }

    public void SetParameters(string outputPath, string viewName, int pixelWidth, int pixelHeight)
    {
        OutputPath = outputPath;
        ViewName = viewName;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
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

        try
        {
            // Resolve view
            View view = null;
            if (!string.IsNullOrEmpty(ViewName))
            {
                view = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .FirstOrDefault(v => v.Name == ViewName && !v.IsTemplate);
            }
            view ??= doc.ActiveView;

            if (view == null)
            {
                Result = new AIResult<string> { Success = false, Message = "No view found." };
                return;
            }

            // Ensure output directory exists
            var dir = System.IO.Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var fileName = System.IO.Path.GetFileNameWithoutExtension(OutputPath);
            var outDir = System.IO.Path.GetDirectoryName(OutputPath) ?? System.IO.Path.GetTempPath();

            var options = new ImageExportOptions
            {
                ViewName = view.Name,
                ZoomType = ZoomFitType.FitPage,
                ImageResolution = ImageResolution.DPI_150,
                HLRandWFViewsFileType = ImageFileType.PNG,
                ShadowViewsFileType = ImageFileType.PNG,
                PixelSize = Math.Max(PixelWidth, PixelHeight),
                ExportRange = ExportRange.SetOfViews,
                FilePath = System.IO.Path.Combine(outDir, fileName),
            };

            options.SetViewsAndSheets(new List<ElementId> { view.Id });

            doc.ExportImage(options);

            Result = new AIResult<string>
            {
                Success = true,
                Message = $"Exported view '{view.Name}' to {OutputPath}",
                Response = OutputPath
            };
        }
        catch (Exception ex)
        {
            Result = new AIResult<string> { Success = false, Message = ex.Message };
        }
        finally
        {
            _resetEvent.Set();
        }
    }

    public string GetName() => "Export View Image";
}
