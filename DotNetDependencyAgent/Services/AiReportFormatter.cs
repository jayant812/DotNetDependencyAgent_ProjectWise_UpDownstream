using System.Net;
using System.Text;

namespace DotNetDependencyAgent;

public static class AiReportFormatter
{
    public static string ToMarkdown(AiArchitectureReport ai, DependencyReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {Safe(ai.ReportTitle)}");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("## Executive Summary");
        sb.AppendLine(Safe(ai.ExecutiveSummary));
        sb.AppendLine();

        sb.AppendLine("## Repository Metrics");
        sb.AppendLine("| Metric | Count |");
        sb.AppendLine("|---|---:|");
        sb.AppendLine($"| Projects (.csproj) | {report.Projects.Count} |");
        sb.AppendLine($"| NuGet packages | {report.Packages.Count} |");
        sb.AppendLine($"| C# files | {report.CsFiles.Count} |");
        sb.AppendLine($"| Confirmed project relationships | {report.ProjectDependencies.Count} |");
        sb.AppendLine($"| Unique dependencies | {report.UniqueDependencySummary.Count} |");
        sb.AppendLine();

        sb.AppendLine("## Project Dependency Overview");
        sb.AppendLine("| Project | Target Framework | Confirmed Upstream | Confirmed Downstream | Other Detected |");
        sb.AppendLine("|---|---|---:|---:|---:|");
        foreach (var p in report.Projects)
        {
            var up = report.ProjectDependencies.Count(x => x.Project.Equals(p.ProjectName, StringComparison.OrdinalIgnoreCase) && x.Direction.Equals("Upstream", StringComparison.OrdinalIgnoreCase));
            var down = report.ProjectDependencies.Count(x => x.Project.Equals(p.ProjectName, StringComparison.OrdinalIgnoreCase) && x.Direction.Equals("Downstream", StringComparison.OrdinalIgnoreCase));
            var other = report.UniqueDependencySummary.Count(x => x.Project.Equals(p.ProjectName, StringComparison.OrdinalIgnoreCase));
            sb.AppendLine($"| {Cell(p.ProjectName)} | {Cell(p.TargetFramework)} | {up} | {down} | {other} |");
        }
        sb.AppendLine();

        sb.AppendLine("# Project Architecture");
        sb.AppendLine($"**Architecture Style:** {Safe(ai.ProjectArchitecture.ArchitectureStyle)}");
        sb.AppendLine();
        sb.AppendLine(Safe(ai.ProjectArchitecture.Overview));
        sb.AppendLine();
        sb.AppendLine("## Architecture Diagram");
        sb.AppendLine("```mermaid");
        sb.Append(ArchitectureDiagramBuilder.BuildMermaid(ai, report));
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Architecture Components");
        if (ai.ProjectArchitecture.Components.Count == 0) sb.AppendLine("No architecture components were identified from the available evidence.");
        else
        {
            sb.AppendLine("| Component | Type | Responsibility | Confidence | Evidence |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var x in ai.ProjectArchitecture.Components)
                sb.AppendLine($"| {Cell(x.Name)} | {Cell(x.Type)} | {Cell(x.Responsibility)} | {Cell(x.Confidence)} | {Cell(x.Evidence)} |");
        }
        sb.AppendLine();
        sb.AppendLine("## Architecture Connections");
        if (ai.ProjectArchitecture.Connections.Count == 0) sb.AppendLine("No architecture connections were identified from the available evidence.");
        else
        {
            sb.AppendLine("| From | To | Relationship | Confidence | Evidence |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var x in ai.ProjectArchitecture.Connections)
                sb.AppendLine($"| {Cell(x.From)} | {Cell(x.To)} | {Cell(x.Relationship)} | {Cell(x.Confidence)} | {Cell(x.Evidence)} |");
        }
        sb.AppendLine();
        sb.AppendLine("## Architecture Flow");
        if (ai.ProjectArchitecture.FlowSummary.Count == 0) sb.AppendLine("- Insufficient evidence to build a reliable end-to-end architecture flow.");
        else foreach (var x in ai.ProjectArchitecture.FlowSummary) sb.AppendLine("- " + Safe(x));
        sb.AppendLine();
        sb.AppendLine("## Architecture Notes");
        if (ai.ProjectArchitecture.Notes.Count == 0) sb.AppendLine("- No additional architecture notes.");
        else foreach (var x in ai.ProjectArchitecture.Notes) sb.AppendLine("- " + Safe(x));
        sb.AppendLine();

        var i = 1;
        foreach (var p in ai.Projects)
        {
            sb.AppendLine($"# Project {i}: {Safe(p.ProjectName)}");
            sb.AppendLine();
            sb.AppendLine("## Project Summary");
            sb.AppendLine(Safe(p.ProjectSummary));
            sb.AppendLine();
            AppendDependencies(sb, "Upstream Dependencies", p.UpstreamDependencies);
            AppendDependencies(sb, "Downstream Dependencies", p.DownstreamDependencies);
            AppendDependencies(sb, "Bidirectional / Possible Dependencies", p.BidirectionalOrPossibleDependencies);

            sb.AppendLine("## Entry Points");
            if (p.EntryPoints.Count == 0) sb.AppendLine("No entry points were confidently identified for this project.");
            else
            {
                sb.AppendLine("| Component | Entry Type | Trigger | Confidence | Evidence |");
                sb.AppendLine("|---|---|---|---|---|");
                foreach (var x in p.EntryPoints)
                    sb.AppendLine($"| {Cell(x.Component)} | {Cell(x.EntryType)} | {Cell(x.Trigger)} | {Cell(x.Confidence)} | {Cell(x.Evidence)} |");
            }
            sb.AppendLine();

            sb.AppendLine("## Project Dependency Flow");
            if (p.DependencyFlow.Count == 0) sb.AppendLine("Insufficient evidence to build a reliable flow for this project.");
            else
            {
                sb.AppendLine("| Step | Component | Direction | Action |");
                sb.AppendLine("|---:|---|---|---|");
                foreach (var x in p.DependencyFlow.OrderBy(x => x.Step))
                    sb.AppendLine($"| {x.Step} | {Cell(x.Component)} | {Cell(x.Direction)} | {Cell(x.Action)} |");
            }
            sb.AppendLine();

            sb.AppendLine("## Risks");
            if (p.Risks.Count == 0) sb.AppendLine("No project-specific risks were identified.");
            else
            {
                sb.AppendLine("| Severity | Area | Risk | Recommendation |");
                sb.AppendLine("|---|---|---|---|");
                foreach (var x in p.Risks)
                    sb.AppendLine($"| {Cell(x.Severity)} | {Cell(x.Area)} | {Cell(x.Risk)} | {Cell(x.Recommendation)} |");
            }
            sb.AppendLine();

            sb.AppendLine("## Recommendations");
            if (p.Recommendations.Count == 0) sb.AppendLine("No project-specific recommendations were returned.");
            else
            {
                sb.AppendLine("| Priority | Recommendation | Benefit |");
                sb.AppendLine("|---|---|---|");
                foreach (var x in p.Recommendations)
                    sb.AppendLine($"| {Cell(x.Priority)} | {Cell(x.Recommendation)} | {Cell(x.Benefit)} |");
            }
            sb.AppendLine();

            sb.AppendLine("## Project Analysis Notes");
            if (p.AnalysisNotes.Count == 0) sb.AppendLine("- No additional project-specific notes.");
            else foreach (var note in p.AnalysisNotes) sb.AppendLine("- " + Safe(note));
            sb.AppendLine();
            i++;
        }

        sb.AppendLine("# Overall Assessment");
        sb.AppendLine(Safe(ai.OverallAssessment));
        sb.AppendLine();
        sb.AppendLine("## Global Recommendations");
        if (ai.GlobalRecommendations.Count == 0) sb.AppendLine("No repository-wide recommendations were returned.");
        else
        {
            sb.AppendLine("| Priority | Recommendation | Benefit |");
            sb.AppendLine("|---|---|---|");
            foreach (var x in ai.GlobalRecommendations)
                sb.AppendLine($"| {Cell(x.Priority)} | {Cell(x.Recommendation)} | {Cell(x.Benefit)} |");
        }
        sb.AppendLine();
        sb.AppendLine("## Repository Analysis Notes");
        if (ai.AnalysisNotes.Count == 0) sb.AppendLine("- No additional repository-level notes.");
        else foreach (var note in ai.AnalysisNotes) sb.AppendLine("- " + Safe(note));

        return sb.ToString();
    }

    public static string ToHtml(AiArchitectureReport ai, DependencyReport report)
    {
        var sb = new StringBuilder();
        sb.Append("""
<!DOCTYPE html><html><head><meta charset="utf-8"><title>.NET Dependency Impact Analysis</title>
<style>
body{font-family:Segoe UI,Arial,sans-serif;margin:32px;color:#202124;line-height:1.45;max-width:1500px}
h1{color:#17365d;margin-bottom:4px}h2{color:#2f5597;border-bottom:1px solid #d9e2f3;padding-bottom:6px;margin-top:26px}h3{color:#17365d;margin-top:22px}
.summary{background:#f5f8fc;border-left:5px solid #2f5597;padding:14px 18px;border-radius:5px}.project{margin-top:34px;padding-top:8px;border-top:3px solid #2f5597}
table{border-collapse:collapse;width:100%;margin:10px 0 18px 0;font-size:14px}th{background:#d9eaf7;text-align:left}th,td{border:1px solid #cfd7e3;padding:8px;vertical-align:top}tr:nth-child(even){background:#fafbfd}
.muted{color:#667085}.footer{margin-top:32px;color:#777;font-size:12px}.architecture-diagram-wrap{margin:18px 0 10px;padding:18px;background:linear-gradient(180deg,#f8fbff,#fff);border:1px solid #dbe7f4;border-radius:14px;overflow:auto}.architecture-diagram{width:100%;min-width:820px;height:auto}.node-title{font:600 13px 'Segoe UI',Arial,sans-serif;fill:#0f172a}.node-subtitle{font:11px 'Segoe UI',Arial,sans-serif;fill:#475569}.node-kind{font:10px 'Segoe UI',Arial,sans-serif;fill:#64748b}.edge-label{font:10px 'Segoe UI',Arial,sans-serif;fill:#475569}.diagram-legend{display:flex;flex-wrap:wrap;gap:14px;align-items:center;font-size:12px;color:#475569;margin:8px 0 20px}.legend-dot{display:inline-block;width:10px;height:10px;border-radius:3px;margin-right:5px;vertical-align:-1px}.legend-dot.project{background:#2563eb}.legend-dot.entry{background:#16a34a}.legend-dot.data{background:#ea580c}.legend-dot.messaging{background:#d97706}.legend-dot.external{background:#9333ea}.legend-dot.telemetry{background:#64748b}.legend-line{display:inline-block;width:28px;height:0;border-top:2px solid #64748b;margin-right:4px}.legend-line.dashed{border-top-style:dashed}.diagram-empty{padding:16px;background:#f8fafc;border:1px dashed #cbd5e1;border-radius:10px;color:#64748b}
</style></head><body>
""");
        sb.Append($"<h1>{H(ai.ReportTitle)}</h1><div class='muted'>Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}</div>");
        sb.Append($"<h2>Executive Summary</h2><div class='summary'>{H(ai.ExecutiveSummary)}</div>");
        sb.Append("<h2>Repository Metrics</h2><table><tr><th>Metric</th><th>Count</th></tr>");
        Metric("Projects (.csproj)", report.Projects.Count); Metric("NuGet packages", report.Packages.Count); Metric("C# files", report.CsFiles.Count);
        Metric("Confirmed project relationships", report.ProjectDependencies.Count); Metric("Unique dependencies", report.UniqueDependencySummary.Count); sb.Append("</table>");

        sb.Append("<h2>Project Dependency Overview</h2><table><tr><th>Project</th><th>Target Framework</th><th>Confirmed Upstream</th><th>Confirmed Downstream</th><th>Other Detected</th></tr>");
        foreach (var p in report.Projects)
        {
            var up = report.ProjectDependencies.Count(x => x.Project.Equals(p.ProjectName, StringComparison.OrdinalIgnoreCase) && x.Direction.Equals("Upstream", StringComparison.OrdinalIgnoreCase));
            var down = report.ProjectDependencies.Count(x => x.Project.Equals(p.ProjectName, StringComparison.OrdinalIgnoreCase) && x.Direction.Equals("Downstream", StringComparison.OrdinalIgnoreCase));
            var other = report.UniqueDependencySummary.Count(x => x.Project.Equals(p.ProjectName, StringComparison.OrdinalIgnoreCase));
            sb.Append($"<tr><td>{H(p.ProjectName)}</td><td>{H(p.TargetFramework)}</td><td>{up}</td><td>{down}</td><td>{other}</td></tr>");
        }
        sb.Append("</table>");

        sb.Append("<h1>Project Architecture</h1>");
        sb.Append($"<p><strong>Architecture Style:</strong> {H(ai.ProjectArchitecture.ArchitectureStyle)}</p>");
        sb.Append($"<div class='summary'>{H(ai.ProjectArchitecture.Overview)}</div>");
        sb.Append("<h2>Architecture Diagram</h2>");
        sb.Append(ArchitectureDiagramBuilder.BuildHtmlSvg(ai, report));
        sb.Append("<h2>Architecture Components</h2>");
        if (ai.ProjectArchitecture.Components.Count == 0) sb.Append("<p>No architecture components were identified from the available evidence.</p>");
        else { sb.Append("<table><tr><th>Component</th><th>Type</th><th>Responsibility</th><th>Confidence</th><th>Evidence</th></tr>"); foreach(var x in ai.ProjectArchitecture.Components) sb.Append($"<tr><td>{H(x.Name)}</td><td>{H(x.Type)}</td><td>{H(x.Responsibility)}</td><td>{H(x.Confidence)}</td><td>{H(x.Evidence)}</td></tr>"); sb.Append("</table>"); }
        sb.Append("<h2>Architecture Connections</h2>");
        if (ai.ProjectArchitecture.Connections.Count == 0) sb.Append("<p>No architecture connections were identified from the available evidence.</p>");
        else { sb.Append("<table><tr><th>From</th><th>To</th><th>Relationship</th><th>Confidence</th><th>Evidence</th></tr>"); foreach(var x in ai.ProjectArchitecture.Connections) sb.Append($"<tr><td>{H(x.From)}</td><td>{H(x.To)}</td><td>{H(x.Relationship)}</td><td>{H(x.Confidence)}</td><td>{H(x.Evidence)}</td></tr>"); sb.Append("</table>"); }
        sb.Append("<h2>Architecture Flow</h2><ul>");
        if (ai.ProjectArchitecture.FlowSummary.Count == 0) sb.Append("<li>Insufficient evidence to build a reliable end-to-end architecture flow.</li>"); else foreach(var x in ai.ProjectArchitecture.FlowSummary) sb.Append($"<li>{H(x)}</li>"); sb.Append("</ul>");
        sb.Append("<h2>Architecture Notes</h2><ul>");
        if (ai.ProjectArchitecture.Notes.Count == 0) sb.Append("<li>No additional architecture notes.</li>"); else foreach(var x in ai.ProjectArchitecture.Notes) sb.Append($"<li>{H(x)}</li>"); sb.Append("</ul>");

        var index = 1;
        foreach (var p in ai.Projects)
        {
            sb.Append($"<div class='project'><h1>Project {index}: {H(p.ProjectName)}</h1>");
            sb.Append($"<div class='summary'>{H(p.ProjectSummary)}</div>");
            HtmlDependencies("Upstream Dependencies", p.UpstreamDependencies);
            HtmlDependencies("Downstream Dependencies", p.DownstreamDependencies);
            HtmlDependencies("Bidirectional / Possible Dependencies", p.BidirectionalOrPossibleDependencies);

            sb.Append("<h2>Entry Points</h2>");
            if (p.EntryPoints.Count == 0) sb.Append("<p>No entry points were confidently identified for this project.</p>");
            else { sb.Append("<table><tr><th>Component</th><th>Entry Type</th><th>Trigger</th><th>Confidence</th><th>Evidence</th></tr>"); foreach(var x in p.EntryPoints) sb.Append($"<tr><td>{H(x.Component)}</td><td>{H(x.EntryType)}</td><td>{H(x.Trigger)}</td><td>{H(x.Confidence)}</td><td>{H(x.Evidence)}</td></tr>"); sb.Append("</table>"); }

            sb.Append("<h2>Project Dependency Flow</h2>");
            if (p.DependencyFlow.Count == 0) sb.Append("<p>Insufficient evidence to build a reliable flow for this project.</p>");
            else { sb.Append("<table><tr><th>Step</th><th>Component</th><th>Direction</th><th>Action</th></tr>"); foreach(var x in p.DependencyFlow.OrderBy(x=>x.Step)) sb.Append($"<tr><td>{x.Step}</td><td>{H(x.Component)}</td><td>{H(x.Direction)}</td><td>{H(x.Action)}</td></tr>"); sb.Append("</table>"); }

            sb.Append("<h2>Risks</h2>");
            if (p.Risks.Count == 0) sb.Append("<p>No project-specific risks were identified.</p>");
            else { sb.Append("<table><tr><th>Severity</th><th>Area</th><th>Risk</th><th>Recommendation</th></tr>"); foreach(var x in p.Risks) sb.Append($"<tr><td>{H(x.Severity)}</td><td>{H(x.Area)}</td><td>{H(x.Risk)}</td><td>{H(x.Recommendation)}</td></tr>"); sb.Append("</table>"); }

            sb.Append("<h2>Recommendations</h2>");
            if (p.Recommendations.Count == 0) sb.Append("<p>No project-specific recommendations were returned.</p>");
            else { sb.Append("<table><tr><th>Priority</th><th>Recommendation</th><th>Benefit</th></tr>"); foreach(var x in p.Recommendations) sb.Append($"<tr><td>{H(x.Priority)}</td><td>{H(x.Recommendation)}</td><td>{H(x.Benefit)}</td></tr>"); sb.Append("</table>"); }

            sb.Append("<h2>Project Analysis Notes</h2><ul>");
            if (p.AnalysisNotes.Count == 0) sb.Append("<li>No additional project-specific notes.</li>"); else foreach(var x in p.AnalysisNotes) sb.Append($"<li>{H(x)}</li>");
            sb.Append("</ul></div>");
            index++;
        }

        sb.Append($"<h1>Overall Assessment</h1><p>{H(ai.OverallAssessment)}</p>");
        sb.Append("<h2>Global Recommendations</h2>");
        if (ai.GlobalRecommendations.Count == 0) sb.Append("<p>No repository-wide recommendations were returned.</p>");
        else { sb.Append("<table><tr><th>Priority</th><th>Recommendation</th><th>Benefit</th></tr>"); foreach(var x in ai.GlobalRecommendations) sb.Append($"<tr><td>{H(x.Priority)}</td><td>{H(x.Recommendation)}</td><td>{H(x.Benefit)}</td></tr>"); sb.Append("</table>"); }

        sb.Append("<h2>Repository Analysis Notes</h2><ul>"); if (ai.AnalysisNotes.Count == 0) sb.Append("<li>No additional repository-level notes.</li>"); else foreach(var x in ai.AnalysisNotes) sb.Append($"<li>{H(x)}</li>"); sb.Append("</ul>");
        sb.Append("<div class='footer'>Every discovered .csproj is rendered separately. Confirmed ProjectReference relationships come from static analysis; Possible/Low items require source/configuration validation.</div></body></html>");
        return sb.ToString();

        void Metric(string name, int count) => sb.Append($"<tr><td>{H(name)}</td><td>{count}</td></tr>");
        void HtmlDependencies(string title, List<AiDependencyItem> items)
        {
            sb.Append($"<h2>{H(title)}</h2>");
            if (items.Count == 0) { sb.Append("<p>None identified for this project from the available evidence.</p>"); return; }
            sb.Append("<table><tr><th>Dependency</th><th>Type</th><th>Direction</th><th>Confidence</th><th>Explanation</th><th>Evidence</th></tr>");
            foreach(var x in items) sb.Append($"<tr><td>{H(x.Name)}</td><td>{H(x.Type)}</td><td>{H(x.Direction)}</td><td>{H(x.Confidence)}</td><td>{H(x.Explanation)}</td><td>{H(x.Evidence)}</td></tr>");
            sb.Append("</table>");
        }
    }

    private static void AppendDependencies(StringBuilder sb, string title, List<AiDependencyItem> items)
    {
        sb.AppendLine($"## {title}");
        if (items.Count == 0) sb.AppendLine("None identified for this project from the available evidence.");
        else
        {
            sb.AppendLine("| Dependency | Type | Direction | Confidence | Explanation | Evidence |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var x in items)
                sb.AppendLine($"| {Cell(x.Name)} | {Cell(x.Type)} | {Cell(x.Direction)} | {Cell(x.Confidence)} | {Cell(x.Explanation)} | {Cell(x.Evidence)} |");
        }
        sb.AppendLine();
    }

    private static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    private static string Cell(string? value) => Safe(value).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    private static string H(string? value) => WebUtility.HtmlEncode(Safe(value)).Replace("\n", "<br>");
}
