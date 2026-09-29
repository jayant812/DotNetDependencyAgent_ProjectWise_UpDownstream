using System.Text.Json.Serialization;

namespace DotNetDependencyAgent;

public sealed class AiArchitectureReport
{
    [JsonPropertyName("report_title")] public string ReportTitle { get; set; } = ".NET Dependency Impact Analysis";
    [JsonPropertyName("executive_summary")] public string ExecutiveSummary { get; set; } = "";
    [JsonPropertyName("overall_assessment")] public string OverallAssessment { get; set; } = "";
    [JsonPropertyName("project_architecture")] public AiProjectArchitecture ProjectArchitecture { get; set; } = new();
    [JsonPropertyName("projects")] public List<AiProjectReport> Projects { get; set; } = [];
    [JsonPropertyName("global_recommendations")] public List<AiRecommendationItem> GlobalRecommendations { get; set; } = [];
    [JsonPropertyName("analysis_notes")] public List<string> AnalysisNotes { get; set; } = [];
}

public sealed class AiProjectArchitecture
{
    [JsonPropertyName("architecture_style")] public string ArchitectureStyle { get; set; } = "";
    [JsonPropertyName("overview")] public string Overview { get; set; } = "";
    [JsonPropertyName("components")] public List<AiArchitectureComponent> Components { get; set; } = [];
    [JsonPropertyName("connections")] public List<AiArchitectureConnection> Connections { get; set; } = [];
    [JsonPropertyName("flow_summary")] public List<string> FlowSummary { get; set; } = [];
    [JsonPropertyName("notes")] public List<string> Notes { get; set; } = [];
}

public sealed class AiArchitectureComponent
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("responsibility")] public string Responsibility { get; set; } = "";
    [JsonPropertyName("evidence")] public string Evidence { get; set; } = "";
    [JsonPropertyName("confidence")] public string Confidence { get; set; } = "";
}

public sealed class AiArchitectureConnection
{
    [JsonPropertyName("from")] public string From { get; set; } = "";
    [JsonPropertyName("to")] public string To { get; set; } = "";
    [JsonPropertyName("relationship")] public string Relationship { get; set; } = "";
    [JsonPropertyName("confidence")] public string Confidence { get; set; } = "";
    [JsonPropertyName("evidence")] public string Evidence { get; set; } = "";
}

public sealed class AiProjectReport
{
    [JsonPropertyName("project_name")] public string ProjectName { get; set; } = "";
    [JsonPropertyName("project_summary")] public string ProjectSummary { get; set; } = "";
    [JsonPropertyName("upstream_dependencies")] public List<AiDependencyItem> UpstreamDependencies { get; set; } = [];
    [JsonPropertyName("downstream_dependencies")] public List<AiDependencyItem> DownstreamDependencies { get; set; } = [];
    [JsonPropertyName("bidirectional_or_possible_dependencies")] public List<AiDependencyItem> BidirectionalOrPossibleDependencies { get; set; } = [];
    [JsonPropertyName("entry_points")] public List<AiEntryPoint> EntryPoints { get; set; } = [];
    [JsonPropertyName("dependency_flow")] public List<AiFlowStep> DependencyFlow { get; set; } = [];
    [JsonPropertyName("risks")] public List<AiRiskItem> Risks { get; set; } = [];
    [JsonPropertyName("recommendations")] public List<AiRecommendationItem> Recommendations { get; set; } = [];
    [JsonPropertyName("analysis_notes")] public List<string> AnalysisNotes { get; set; } = [];
}

public sealed class AiDependencyItem
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("direction")] public string Direction { get; set; } = "";
    [JsonPropertyName("confidence")] public string Confidence { get; set; } = "";
    [JsonPropertyName("evidence")] public string Evidence { get; set; } = "";
    [JsonPropertyName("explanation")] public string Explanation { get; set; } = "";
}

public sealed class AiEntryPoint
{
    [JsonPropertyName("component")] public string Component { get; set; } = "";
    [JsonPropertyName("entry_type")] public string EntryType { get; set; } = "";
    [JsonPropertyName("trigger")] public string Trigger { get; set; } = "";
    [JsonPropertyName("evidence")] public string Evidence { get; set; } = "";
    [JsonPropertyName("confidence")] public string Confidence { get; set; } = "";
}

public sealed class AiFlowStep
{
    [JsonPropertyName("step")] public int Step { get; set; }
    [JsonPropertyName("component")] public string Component { get; set; } = "";
    [JsonPropertyName("action")] public string Action { get; set; } = "";
    [JsonPropertyName("direction")] public string Direction { get; set; } = "";
}

public sealed class AiRiskItem
{
    [JsonPropertyName("severity")] public string Severity { get; set; } = "";
    [JsonPropertyName("area")] public string Area { get; set; } = "";
    [JsonPropertyName("risk")] public string Risk { get; set; } = "";
    [JsonPropertyName("recommendation")] public string Recommendation { get; set; } = "";
}

public sealed class AiRecommendationItem
{
    [JsonPropertyName("priority")] public string Priority { get; set; } = "";
    [JsonPropertyName("recommendation")] public string Recommendation { get; set; } = "";
    [JsonPropertyName("benefit")] public string Benefit { get; set; } = "";
}
