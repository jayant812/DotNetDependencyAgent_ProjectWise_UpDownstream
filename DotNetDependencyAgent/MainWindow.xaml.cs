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
    private CancellationTokenSource? _cts;
    private AnalysisResult? _lastResult;

    public MainWindow()
    {
        InitializeComponent();
        InputTextBox.Text = "";
        ResetAiReport("Run an analysis to generate the structured AI impact report.");


        _analyzer.Progress += (p, m) => Dispatcher.Invoke(() =>
        {
            ProgressBar.Value = p;
            ProgressText.Text = m;
        });
        _analyzer.Log += m => Dispatcher.Invoke(() => AppendLog(m));
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
        string key = "";
        string model = "gemini-3.5-flash";
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
                ProgressBar.Value = 85;
                ProgressText.Text = "Generating structured Gemini AI impact report";

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
                    var structuredJson = JsonSerializer.Serialize(aiReport, jsonOptions);

                    _lastResult.AiAnalysis = markdown;
                    _lastResult.AiReportPath = Path.Combine(_lastResult.OutputDirectory, "ai_dependency_analysis.md");
                    var htmlPath = Path.Combine(_lastResult.OutputDirectory, "ai_dependency_analysis.html");
                    var jsonPath = Path.Combine(_lastResult.OutputDirectory, "ai_dependency_analysis.json");

                    await File.WriteAllTextAsync(_lastResult.AiReportPath, markdown, _cts.Token);
                    await File.WriteAllTextAsync(htmlPath, html, _cts.Token);
                    await File.WriteAllTextAsync(jsonPath, structuredJson, _cts.Token);

                    AppendLog("Structured AI analysis saved: " + _lastResult.AiReportPath);
                    AppendLog("Readable HTML report saved: " + htmlPath);
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
