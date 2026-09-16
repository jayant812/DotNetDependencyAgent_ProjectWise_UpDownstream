using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DotNetDependencyAgent;

public static class AiReportRenderer
{
    private static readonly Brush HeadingBrush = new SolidColorBrush(Color.FromRgb(23, 54, 93));
    private static readonly Brush SubHeadingBrush = new SolidColorBrush(Color.FromRgb(47, 85, 151));
    private static readonly Brush ProjectBrush = new SolidColorBrush(Color.FromRgb(31, 78, 121));
    private static readonly Brush HeaderBrush = new SolidColorBrush(Color.FromRgb(217, 234, 247));
    private static readonly Brush BorderBrush = new SolidColorBrush(Color.FromRgb(207, 215, 227));
    private static readonly Brush SummaryBrush = new SolidColorBrush(Color.FromRgb(245, 248, 252));

    public static FlowDocument CreateDocument(AiArchitectureReport ai, DependencyReport report)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            PagePadding = new Thickness(24),
            LineHeight = 20
        };

        doc.Blocks.Add(new Paragraph(new Run(ai.ReportTitle))
        {
            FontSize = 26,
            FontWeight = FontWeights.Bold,
            Foreground = HeadingBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        doc.Blocks.Add(new Paragraph(new Run($"Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}"))
        {
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 0, 0, 18)
        });

        AddHeading(doc, "Executive Summary");
        var summary = new Section { Background = SummaryBrush, Padding = new Thickness(14), Margin = new Thickness(0, 4, 0, 14) };
        summary.Blocks.Add(Text(ai.ExecutiveSummary));
        doc.Blocks.Add(summary);

        AddHeading(doc, "Repository Metrics");
        AddTable(doc,
            ["Metric", "Count"],
            [
                ["Projects (.csproj)", report.Projects.Count.ToString()],
                ["NuGet packages", report.Packages.Count.ToString()],
                ["C# files", report.CsFiles.Count.ToString()],
                ["Confirmed project relationships", report.ProjectDependencies.Count.ToString()],
                ["Unique dependencies", report.UniqueDependencySummary.Count.ToString()]
            ],
            [3.5, 1.0]);

        AddHeading(doc, "Project Dependency Overview");
        var overviewRows = report.Projects.Select(p =>
        {
            var upstream = report.ProjectDependencies.Count(x => x.Project.Equals(p.ProjectName, StringComparison.OrdinalIgnoreCase) && x.Direction.Equals("Upstream", StringComparison.OrdinalIgnoreCase));
            var downstream = report.ProjectDependencies.Count(x => x.Project.Equals(p.ProjectName, StringComparison.OrdinalIgnoreCase) && x.Direction.Equals("Downstream", StringComparison.OrdinalIgnoreCase));
            var ext = report.UniqueDependencySummary.Count(x => x.Project.Equals(p.ProjectName, StringComparison.OrdinalIgnoreCase));
            return new[] { p.ProjectName, p.TargetFramework, upstream.ToString(), downstream.ToString(), ext.ToString() };
        }).ToList();
        AddTable(doc, ["Project", "Target Framework", "Confirmed Upstream", "Confirmed Downstream", "Other Detected"], overviewRows,
            [2.2, 1.5, 1.2, 1.2, 1.2]);

        var index = 1;
        foreach (var project in ai.Projects)
        {
            AddProjectHeading(doc, $"Project {index}: {project.ProjectName}");
            var staticProject = report.Projects.FirstOrDefault(x => x.ProjectName.Equals(project.ProjectName, StringComparison.OrdinalIgnoreCase));
            if (staticProject is not null && !string.IsNullOrWhiteSpace(staticProject.TargetFramework))
                doc.Blocks.Add(new Paragraph(new Run($"Target framework: {staticProject.TargetFramework}")) { Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 8) });

            var projectSummary = new Section { Background = SummaryBrush, Padding = new Thickness(12), Margin = new Thickness(0, 4, 0, 12) };
            projectSummary.Blocks.Add(Text(string.IsNullOrWhiteSpace(project.ProjectSummary)
                ? $"Dependency view for {project.ProjectName}."
                : project.ProjectSummary));
            doc.Blocks.Add(projectSummary);

            AddDependencies(doc, "Upstream Dependencies", project.UpstreamDependencies);
            AddDependencies(doc, "Downstream Dependencies", project.DownstreamDependencies);
            AddDependencies(doc, "Bidirectional / Possible Dependencies", project.BidirectionalOrPossibleDependencies);

            AddSubHeading(doc, "Entry Points");
            if (project.EntryPoints.Count == 0) AddEmpty(doc, "No entry points were confidently identified for this project.");
            else AddTable(doc,
                ["Component", "Entry Type", "Trigger", "Confidence", "Evidence"],
                project.EntryPoints.Select(x => new[] { x.Component, x.EntryType, x.Trigger, x.Confidence, x.Evidence }).ToList(),
                [1.8, 1.3, 2.2, 1.0, 2.8]);

            AddSubHeading(doc, "Project Dependency Flow");
            if (project.DependencyFlow.Count == 0) AddEmpty(doc, "Insufficient evidence to build a reliable flow for this project.");
            else AddTable(doc,
                ["Step", "Component", "Direction", "Action"],
                project.DependencyFlow.OrderBy(x => x.Step).Select(x => new[] { x.Step.ToString(), x.Component, x.Direction, x.Action }).ToList(),
                [0.6, 2.0, 1.0, 4.4]);

            AddSubHeading(doc, "Risks");
            if (project.Risks.Count == 0) AddEmpty(doc, "No project-specific risks were identified from the available evidence.");
            else AddTable(doc,
                ["Severity", "Area", "Risk", "Recommendation"],
                project.Risks.Select(x => new[] { x.Severity, x.Area, x.Risk, x.Recommendation }).ToList(),
                [0.9, 1.5, 3.0, 3.0]);

            AddSubHeading(doc, "Recommendations");
            if (project.Recommendations.Count == 0) AddEmpty(doc, "No additional project-specific recommendations were returned.");
            else AddTable(doc,
                ["Priority", "Recommendation", "Benefit"],
                project.Recommendations.Select(x => new[] { x.Priority, x.Recommendation, x.Benefit }).ToList(),
                [1.0, 4.0, 3.0]);

            AddSubHeading(doc, "Project Analysis Notes");
            AddNotes(doc, project.AnalysisNotes, "No additional project-specific notes.");
            index++;
        }

        AddHeading(doc, "Overall Assessment");
        doc.Blocks.Add(Text(ai.OverallAssessment));

        AddHeading(doc, "Global Recommendations");
        if (ai.GlobalRecommendations.Count == 0) AddEmpty(doc, "No repository-wide recommendations were returned.");
        else AddTable(doc,
            ["Priority", "Recommendation", "Benefit"],
            ai.GlobalRecommendations.Select(x => new[] { x.Priority, x.Recommendation, x.Benefit }).ToList(),
            [1.0, 4.0, 3.0]);

        AddHeading(doc, "Repository Analysis Notes");
        AddNotes(doc, ai.AnalysisNotes, "No additional repository-level notes.");

        doc.Blocks.Add(new Paragraph(new Run(
            "Note: Every discovered .csproj is shown in its own section. Confirmed project relationships come from static ProjectReference analysis. " +
            "Items marked Possible or Low confidence should be validated in source/configuration before making change-impact decisions."))
        {
            FontSize = 11,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 20, 0, 0)
        });

        return doc;
    }

    private static void AddDependencies(FlowDocument doc, string title, List<AiDependencyItem> items)
    {
        AddSubHeading(doc, title);
        if (items.Count == 0)
        {
            AddEmpty(doc, "None identified for this project from the available evidence.");
            return;
        }

        AddTable(doc,
            ["Dependency", "Type", "Direction", "Confidence", "Explanation", "Evidence"],
            items.Select(x => new[] { x.Name, x.Type, x.Direction, x.Confidence, x.Explanation, x.Evidence }).ToList(),
            [1.6, 1.2, 0.9, 1.0, 2.6, 2.7]);
    }

    private static void AddHeading(FlowDocument doc, string text) => doc.Blocks.Add(new Paragraph(new Run(text))
    {
        FontSize = 18,
        FontWeight = FontWeights.SemiBold,
        Foreground = SubHeadingBrush,
        Margin = new Thickness(0, 22, 0, 8)
    });

    private static void AddProjectHeading(FlowDocument doc, string text) => doc.Blocks.Add(new Paragraph(new Run(text))
    {
        FontSize = 22,
        FontWeight = FontWeights.Bold,
        Foreground = ProjectBrush,
        Margin = new Thickness(0, 30, 0, 10),
        Padding = new Thickness(0, 0, 0, 6),
        BorderBrush = BorderBrush,
        BorderThickness = new Thickness(0, 0, 0, 2)
    });

    private static void AddSubHeading(FlowDocument doc, string text) => doc.Blocks.Add(new Paragraph(new Run(text))
    {
        FontSize = 15,
        FontWeight = FontWeights.SemiBold,
        Foreground = HeadingBrush,
        Margin = new Thickness(0, 16, 0, 6)
    });

    private static Paragraph Text(string? value) => new(new Run(string.IsNullOrWhiteSpace(value) ? "-" : value.Trim()))
    {
        Margin = new Thickness(0, 2, 0, 10)
    };

    private static void AddEmpty(FlowDocument doc, string message) => doc.Blocks.Add(new Paragraph(new Run(message))
    {
        FontStyle = FontStyles.Italic,
        Foreground = Brushes.DimGray,
        Margin = new Thickness(0, 2, 0, 12)
    });

    private static void AddNotes(FlowDocument doc, List<string> notes, string emptyMessage)
    {
        if (notes.Count == 0) { AddEmpty(doc, emptyMessage); return; }
        var list = new List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(18, 0, 0, 14) };
        foreach (var note in notes) list.ListItems.Add(new ListItem(Text(note)));
        doc.Blocks.Add(list);
    }

    private static void AddTable(FlowDocument doc, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, IReadOnlyList<double> widths)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 14) };
        foreach (var width in widths) table.Columns.Add(new TableColumn { Width = new GridLength(width, GridUnitType.Star) });

        var group = new TableRowGroup();
        table.RowGroups.Add(group);
        var header = new TableRow { Background = HeaderBrush, FontWeight = FontWeights.SemiBold };
        foreach (var h in headers) header.Cells.Add(Cell(h, true));
        group.Rows.Add(header);

        foreach (var values in rows)
        {
            var row = new TableRow();
            foreach (var value in values) row.Cells.Add(Cell(value, false));
            group.Rows.Add(row);
        }
        doc.Blocks.Add(table);
    }

    private static TableCell Cell(string? value, bool isHeader) => new(new Paragraph(new Run(string.IsNullOrWhiteSpace(value) ? "-" : value.Trim()))
    {
        Margin = new Thickness(0)
    })
    {
        Padding = new Thickness(7),
        BorderBrush = BorderBrush,
        BorderThickness = new Thickness(0.5),
        Background = isHeader ? HeaderBrush : Brushes.White
    };
}
