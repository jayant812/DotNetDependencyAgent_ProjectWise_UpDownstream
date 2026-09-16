using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DotNetDependencyAgent;

public sealed class GeminiService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public async Task<AiArchitectureReport> AnalyzeAsync(
        DependencyReport report,
        string apiKey,
        string model,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Gemini API key is required for AI analysis.");

        model = string.IsNullOrWhiteSpace(model) ? "gemini-3.5-flash" : model.Trim();

        var compact = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = false });
        if (compact.Length > 120000) compact = compact[..120000];

        var projectNames = report.Projects.Select(x => x.ProjectName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var projectList = string.Join(", ", projectNames);

        var prompt = $$"""
You are a senior .NET software architect performing dependency and change-impact analysis.

IMPORTANT RULES:
1. Use ONLY evidence contained in the supplied dependency report.
2. Do not invent systems, projects, classes, APIs, databases, queues, callers, or relationships.
3. ProjectReference relationships marked Confirmed are authoritative.
4. Package/source heuristics marked Possible are not facts. Keep their confidence as Possible/Low unless stronger source evidence exists.
5. The repository contains these projects: {{projectList}}
6. YOU MUST RETURN ONE SEPARATE PROJECT OBJECT FOR EVERY PROJECT ABOVE. Do not combine projects into one dependency list.
7. For each project, list its own upstream, downstream, possible/bidirectional dependencies, entry points, flow, risks and recommendations.
8. If Project A references Project B, then B is Downstream for A and A is Upstream for B.
9. Remove duplicates inside each project.
10. Keep explanations short, precise, and useful to a .NET developer or engineering manager.
11. If evidence is insufficient for a project, return empty arrays and explain the limitation in that project's analysis_notes.
12. Build dependency_flow only when there is enough evidence. Do not guess missing steps.
13. Return JSON ONLY. No Markdown, no code fence, no introductory text.

Return exactly this JSON structure:
{
  "report_title": ".NET Dependency Impact Analysis",
  "executive_summary": "2-5 concise sentences for the repository as a whole.",
  "overall_assessment": "One short repository-level assessment.",
  "projects": [
    {
      "project_name": "EXACT project name from the dependency report",
      "project_summary": "Short summary for this project only.",
      "upstream_dependencies": [
        {
          "name": "dependency/system/project",
          "type": "Project / API / Queue / Scheduler / Authentication / Other",
          "direction": "Upstream",
          "confidence": "Confirmed / High / Possible / Low",
          "evidence": "short evidence from report",
          "explanation": "why this is upstream for THIS project"
        }
      ],
      "downstream_dependencies": [
        {
          "name": "dependency/system/project",
          "type": "Project / Database / API / Cache / Queue / Storage / Telemetry / Other",
          "direction": "Downstream",
          "confidence": "Confirmed / High / Possible / Low",
          "evidence": "short evidence from report",
          "explanation": "why this is downstream for THIS project"
        }
      ],
      "bidirectional_or_possible_dependencies": [
        {
          "name": "dependency/system/project",
          "type": "dependency type",
          "direction": "Both / Unknown / Possible",
          "confidence": "Possible / Low",
          "evidence": "short evidence from report",
          "explanation": "why direction cannot be confirmed for THIS project"
        }
      ],
      "entry_points": [
        {
          "component": "component",
          "entry_type": "HTTP / gRPC / Message Consumer / Scheduler / Function / Other",
          "trigger": "what initiates execution",
          "evidence": "short evidence",
          "confidence": "Confirmed / High / Possible / Low"
        }
      ],
      "dependency_flow": [
        {
          "step": 1,
          "component": "component/project/system",
          "action": "short action",
          "direction": "Inbound / Internal / Outbound"
        }
      ],
      "risks": [
        {
          "severity": "High / Medium / Low",
          "area": "area/component",
          "risk": "concise risk description",
          "recommendation": "specific mitigation"
        }
      ],
      "recommendations": [
        {
          "priority": "High / Medium / Low",
          "recommendation": "specific next action",
          "benefit": "expected benefit"
        }
      ],
      "analysis_notes": ["project-specific uncertainty or evidence note"]
    }
  ],
  "global_recommendations": [
    {
      "priority": "High / Medium / Low",
      "recommendation": "repository-wide next action",
      "benefit": "expected benefit"
    }
  ],
  "analysis_notes": ["repository-level uncertainty or limitation"]
}

DEPENDENCY REPORT:
{{compact}}
""";

        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new
            {
                temperature = 0.1,
                responseMimeType = "application/json"
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(apiKey)}";
        using var response = await _http.PostAsJsonAsync(url, body, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gemini request failed ({(int)response.StatusCode}): {raw}");

        var node = JsonNode.Parse(raw);
        var text = node?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Gemini returned no analysis content.");

        text = CleanJson(text);

        try
        {
            var result = JsonSerializer.Deserialize<AiArchitectureReport>(text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result is null)
                throw new JsonException("The response deserialized to null.");

            Normalize(result);
            EnsureStaticProjectCoverage(result, report);
            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Gemini returned an invalid structured report. Please run the AI analysis again. " + ex.Message,
                ex);
        }
    }

    private static string CleanJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = text.IndexOf('\n');
            if (firstNewLine >= 0) text = text[(firstNewLine + 1)..];
            var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0) text = text[..lastFence];
        }
        return text.Trim();
    }

    private static void Normalize(AiArchitectureReport report)
    {
        report.ReportTitle = EmptyTo(report.ReportTitle, ".NET Dependency Impact Analysis");
        report.ExecutiveSummary = EmptyTo(report.ExecutiveSummary, "No executive summary was returned.");
        report.OverallAssessment = EmptyTo(report.OverallAssessment, "No overall assessment was returned.");
        report.Projects ??= [];
        report.GlobalRecommendations ??= [];
        report.AnalysisNotes ??= [];

        foreach (var project in report.Projects)
        {
            project.ProjectName = project.ProjectName?.Trim() ?? "";
            project.ProjectSummary = project.ProjectSummary?.Trim() ?? "";
            project.UpstreamDependencies ??= [];
            project.DownstreamDependencies ??= [];
            project.BidirectionalOrPossibleDependencies ??= [];
            project.EntryPoints ??= [];
            project.DependencyFlow ??= [];
            project.Risks ??= [];
            project.Recommendations ??= [];
            project.AnalysisNotes ??= [];
        }
    }

    // Guarantees that every discovered .csproj gets a separate section, even if AI misses it.
    // Confirmed ProjectReference relationships are injected from deterministic static analysis.
    private static void EnsureStaticProjectCoverage(AiArchitectureReport ai, DependencyReport report)
    {
        foreach (var p in report.Projects)
        {
            var section = ai.Projects.FirstOrDefault(x =>
                x.ProjectName.Equals(p.ProjectName, StringComparison.OrdinalIgnoreCase));

            if (section is null)
            {
                section = new AiProjectReport
                {
                    ProjectName = p.ProjectName,
                    ProjectSummary = $"Static dependency summary for {p.ProjectName}."
                };
                ai.Projects.Add(section);
            }

            var confirmed = report.ProjectDependencies
                .Where(x => x.Project.Equals(p.ProjectName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var dep in confirmed.Where(x => x.Direction.Equals("Upstream", StringComparison.OrdinalIgnoreCase)))
                AddStaticDependency(section.UpstreamDependencies, dep.RelatedProject, "Project", "Upstream", dep.Confidence,
                    dep.Relationship, $"{dep.RelatedProject} depends on {p.ProjectName} through a project reference.");

            foreach (var dep in confirmed.Where(x => x.Direction.Equals("Downstream", StringComparison.OrdinalIgnoreCase)))
                AddStaticDependency(section.DownstreamDependencies, dep.RelatedProject, "Project", "Downstream", dep.Confidence,
                    dep.Relationship, $"{p.ProjectName} directly references {dep.RelatedProject}.");

            // Package/source heuristics are grouped by their owning project, never mixed across csproj files.
            var external = report.UniqueDependencySummary
                .Where(x => x.Project.Equals(p.ProjectName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var dep in external)
            {
                var evidence = dep.SourceFiles.Count > 0
                    ? $"{dep.Occurrences} occurrence(s); source: {string.Join(", ", dep.SourceFiles.Take(3))}"
                    : $"{dep.Occurrences} occurrence(s) from package/source heuristic";

                if (dep.Direction.Equals("Upstream", StringComparison.OrdinalIgnoreCase))
                    AddStaticDependency(section.UpstreamDependencies, dep.Name, dep.Type, "Upstream", dep.Confidence, evidence,
                        "Detected as a possible upstream dependency for this project.");
                else if (dep.Direction.Equals("Downstream", StringComparison.OrdinalIgnoreCase))
                    AddStaticDependency(section.DownstreamDependencies, dep.Name, dep.Type, "Downstream", dep.Confidence, evidence,
                        "Detected as a possible downstream dependency for this project.");
                else
                    AddStaticDependency(section.BidirectionalOrPossibleDependencies, dep.Name, dep.Type, dep.Direction, dep.Confidence, evidence,
                        "Direction is not confirmed from the available static evidence.");
            }

            section.UpstreamDependencies = Deduplicate(section.UpstreamDependencies);
            section.DownstreamDependencies = Deduplicate(section.DownstreamDependencies);
            section.BidirectionalOrPossibleDependencies = Deduplicate(section.BidirectionalOrPossibleDependencies);
        }

        // Keep project order identical to the .csproj discovery order.
        ai.Projects = report.Projects
            .Select(p => ai.Projects.First(x => x.ProjectName.Equals(p.ProjectName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static void AddStaticDependency(List<AiDependencyItem> items, string name, string type, string direction,
        string confidence, string evidence, string explanation)
    {
        if (items.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                           x.Direction.Equals(direction, StringComparison.OrdinalIgnoreCase))) return;

        items.Add(new AiDependencyItem
        {
            Name = name,
            Type = type,
            Direction = direction,
            Confidence = confidence,
            Evidence = evidence,
            Explanation = explanation
        });
    }

    private static List<AiDependencyItem> Deduplicate(IEnumerable<AiDependencyItem> items) =>
        items.GroupBy(x => $"{x.Name}|{x.Direction}|{x.Type}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(x => ConfidenceOrder(x.Confidence)).First())
            .OrderBy(x => x.Type).ThenBy(x => x.Name).ToList();

    private static int ConfidenceOrder(string? confidence) => confidence?.ToLowerInvariant() switch
    {
        "confirmed" => 0,
        "high" => 1,
        "possible" => 2,
        "low" => 3,
        _ => 4
    };

    private static string EmptyTo(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
