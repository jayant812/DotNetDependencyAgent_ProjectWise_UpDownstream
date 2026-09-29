using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Documents;

namespace DotNetDependencyAgent;

public partial class MainWindow : Window
{
    private readonly DependencyAnalyzer _analyzer = new();
    private readonly GeminiService _gemini = new();
    private readonly AppSettings _settings;
    private CancellationTokenSource? _cts;
    private AnalysisResult? _lastResult;

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();
        InputTextBox.Text = "";
        InitializeGeminiModels();
        ResetAiReport("Run an analysis to generate the structured AI impact report.");


        _analyzer.Progress += (p, m) => Dispatcher.Invoke(() =>
        {
            ProgressBar.Value = p;
            ProgressText.Text = m;
        });
        _analyzer.Log += m => Dispatcher.Invoke(() => AppendLog(m));
    }

    private void InitializeGeminiModels()
    {
        // Stable text-generation models suitable for this structured dependency/architecture report.
        var models = new[]
        {
            "gemini-3.8-flash",
            "gemini-3.7-flash",
            "gemini-3.6-flash",
            "gemini-3.5-flash",
            "gemini-3.5-flash-lite",
            "gemini-3.1-flash-lite",
            "gemini-2.5-flash",
            "gemini-2.5-flash-lite",
            "gemini-2.5-pro"
        };

        GeminiModelComboBox.ItemsSource = models;

        var configuredModel = string.IsNullOrWhiteSpace(_settings.Gemini.Model)
            ? "gemini-3.5-flash"
            : _settings.Gemini.Model.Trim();

        GeminiModelComboBox.SelectedItem = models.FirstOrDefault(x =>
            string.Equals(x, configuredModel, StringComparison.OrdinalIgnoreCase));

        if (GeminiModelComboBox.SelectedItem is null)
        {
            GeminiModelComboBox.ItemsSource = models.Append(configuredModel).ToArray();
            GeminiModelComboBox.SelectedItem = configuredModel;
        }
    }

    private void InputModeChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var local = LocalRadio.IsChecked == true;
        BrowseButton.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        InputTextBox.Text = local ? "" : "";
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = ".NET Project (*.csproj)|*.csproj",
            Title = "Select .NET project"
        };
        if (dlg.ShowDialog(this) == true) InputTextBox.Text = dlg.FileName;
    }

    private async void Analyze_Click(object sender, RoutedEventArgs e)
    {
        string key = _settings.Gemini.ApiKey?.Trim() ?? string.Empty;
        string model = GeminiModelComboBox.SelectedItem?.ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(model))
            model = string.IsNullOrWhiteSpace(_settings.Gemini.Model) ? "gemini-3.5-flash" : _settings.Gemini.Model.Trim();
        var input = InputTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            MessageBox.Show(this, "Enter a GitHub repository URL or select a .csproj file.", "Input required");
            return;
        }

        AnalyzeButton.IsEnabled = false;
        OpenOutputButton.IsEnabled = false;
        ProgressBar.Value = 0;
        ReportTextBox.Clear();
        CallGraphTextBox.Clear();
        LogTextBox.Clear();
        ResetAiReport("AI report is waiting for static dependency analysis to complete...");
        DependenciesGrid.ItemsSource = null;
        ProjectDependenciesGrid.ItemsSource = null;
        _cts = new CancellationTokenSource();

        try
        {
            AppendLog("Starting analysis...");
            _lastResult = await _analyzer.AnalyzeAsync(input, _cts.Token);

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            ReportTextBox.Text = JsonSerializer.Serialize(_lastResult.Report, jsonOptions);
            CallGraphTextBox.Text = JsonSerializer.Serialize(_lastResult.Report.CallGraph, jsonOptions);
            BindSummary(_lastResult.Report);

            if (RunAiCheckBox.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(key) || key == "PASTE_YOUR_GEMINI_API_KEY_HERE")
                {
                    throw new InvalidOperationException(
                        "Gemini API key is missing. Add it under Gemini:ApiKey in appsettings.json " +
                        "or set the GEMINI_API_KEY environment variable.");
                }
                ProgressBar.Value = 85;
                ProgressText.Text = $"Generating AI report with {model}";
                AppendLog("Selected Gemini model: " + model);

                try
                {
                    var aiReport = await _gemini.AnalyzeAsync(
                        _lastResult.Report,
                        key,
                        model,
                        _cts.Token);

                    // Render a native WPF report. We do not display raw Markdown pipes in a TextBox.
                    AiReportBox.Document = AiReportRenderer.CreateDocument(aiReport, _lastResult.Report);

                    // Also save portable report formats to the output folder.
                    var markdown = AiReportFormatter.ToMarkdown(aiReport, _lastResult.Report);
                    var html = AiReportFormatter.ToHtml(aiReport, _lastResult.Report);
                    var architectureSvg = ArchitectureDiagramBuilder.BuildStandaloneSvg(aiReport, _lastResult.Report);
                    var structuredJson = JsonSerializer.Serialize(aiReport, jsonOptions);

                    _lastResult.AiAnalysis = markdown;
                    _lastResult.AiReportPath = Path.Combine(_lastResult.OutputDirectory, "ai_dependency_analysis.md");
                    var htmlPath = Path.Combine(_lastResult.OutputDirectory, "ai_dependency_analysis.html");
                    var svgPath = Path.Combine(_lastResult.OutputDirectory, "project_architecture_diagram.svg");
                    var jsonPath = Path.Combine(_lastResult.OutputDirectory, "ai_dependency_analysis.json");

                    await File.WriteAllTextAsync(_lastResult.AiReportPath, markdown, _cts.Token);
                    await File.WriteAllTextAsync(htmlPath, html, _cts.Token);
                    await File.WriteAllTextAsync(svgPath, architectureSvg, _cts.Token);
                    await File.WriteAllTextAsync(jsonPath, structuredJson, _cts.Token);

                    AppendLog("Structured AI analysis saved: " + _lastResult.AiReportPath);
                    AppendLog("Readable HTML report saved: " + htmlPath);
                    AppendLog("Architecture diagram SVG saved: " + svgPath);
                    AppendLog("Structured AI JSON saved: " + jsonPath);
                }
                catch (Exception aiEx)
                {
                    ResetAiReport(
                        "AI analysis was not completed.\n\n" + aiEx.Message +
                        "\n\nStatic project/dependency analysis is still available in the other tabs.");
                    AppendLog("AI warning: " + aiEx.Message);
                }
            }
            else
            {
                ResetAiReport("AI analysis was disabled for this run. Static dependency results are available in the Summary and Project Up/Downstream tabs.");
            }

            ProgressBar.Value = 100;
            ProgressText.Text = "Analysis completed successfully";
            OpenOutputButton.IsEnabled = true;
            AppendLog("Dependency report: " + _lastResult.ReportPath);
            AppendLog("Call graph: " + _lastResult.CallGraphPath);
        }
        catch (Exception ex)
        {
            ProgressText.Text = "Analysis failed";
            AppendLog("ERROR: " + ex);
            MessageBox.Show(this, ex.Message, "Analysis failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            AnalyzeButton.IsEnabled = true;
        }
    }

    private void BindSummary(DependencyReport report)
    {
        PackagesCount.Text = report.Packages.Count.ToString();
        FilesCount.Text = report.CsFiles.Count.ToString();
        UsagesCount.Text = report.Dependencies.Count.ToString();
        UniqueCount.Text = report.UniqueDependencySummary.Count.ToString();
        RefsCount.Text = report.ProjectReferences.Count.ToString();
        ProjectDepsCount.Text = report.ProjectDependencies.Count.ToString();
        DependenciesGrid.ItemsSource = report.UniqueDependencySummary;
        ProjectDependenciesGrid.ItemsSource = report.ProjectDependencies;
    }

    private void ResetAiReport(string message)
    {
        var document = new FlowDocument
        {
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 13,
            PagePadding = new Thickness(22)
        };
        document.Blocks.Add(new Paragraph(new Run(message))
        {
            Foreground = System.Windows.Media.Brushes.DimGray
        });
        AiReportBox.Document = document;
    }

    private void AppendLog(string message)
    {
        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
        LogTextBox.ScrollToEnd();
    }

    private void OpenOutput_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null || !Directory.Exists(_lastResult.OutputDirectory)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_lastResult.OutputDirectory}\"")
        {
            UseShellExecute = true
        });
    }
}
