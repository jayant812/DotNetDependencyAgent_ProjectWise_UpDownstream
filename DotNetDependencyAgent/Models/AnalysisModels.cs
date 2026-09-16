using System.Text.Json.Serialization;

namespace DotNetDependencyAgent;

public sealed class PackageInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("classification")] public string Classification { get; set; } = "";
    [JsonPropertyName("project")] public string Project { get; set; } = "";
}

public sealed class ProjectReferenceInfo
{
    [JsonPropertyName("project")] public string Project { get; set; } = "";
    [JsonPropertyName("reference")] public string Reference { get; set; } = "";
    [JsonPropertyName("resolved_reference")] public string ResolvedReference { get; set; } = "";
}

public sealed class ProjectDependencyInfo
{
    [JsonPropertyName("project")] public string Project { get; set; } = "";
    [JsonPropertyName("related_project")] public string RelatedProject { get; set; } = "";
    [JsonPropertyName("direction")] public string Direction { get; set; } = "Unknown";
    [JsonPropertyName("relationship")] public string Relationship { get; set; } = "ProjectReference";
    [JsonPropertyName("confidence")] public string Confidence { get; set; } = "Confirmed";
}

public sealed class DependencyUsage
{
    [JsonPropertyName("project")] public string Project { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("direction")] public string Direction { get; set; } = "Unknown";
    [JsonPropertyName("confidence")] public string Confidence { get; set; } = "Possible";
    [JsonPropertyName("source_file")] public string SourceFile { get; set; } = "";
    [JsonPropertyName("line_number")] public int LineNumber { get; set; }
    [JsonPropertyName("function_name")] public string FunctionName { get; set; } = "";
    [JsonPropertyName("evidence")] public string Evidence { get; set; } = "";
}

public sealed class UniqueDependency
{
    [JsonPropertyName("project")] public string Project { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("direction")] public string Direction { get; set; } = "Unknown";
    [JsonPropertyName("confidence")] public string Confidence { get; set; } = "Possible";
    [JsonPropertyName("occurrences")] public int Occurrences { get; set; }
    [JsonPropertyName("source_files")] public List<string> SourceFiles { get; set; } = [];
}

public sealed class FunctionNode
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("file")] public string File { get; set; } = "";
    [JsonPropertyName("line")] public int Line { get; set; }
}

public sealed class CallEdge
{
    [JsonPropertyName("caller")] public string Caller { get; set; } = "";
    [JsonPropertyName("callee")] public string Callee { get; set; } = "";
}

public sealed class CallGraph
{
    [JsonPropertyName("functions")] public List<FunctionNode> Functions { get; set; } = [];
    [JsonPropertyName("edges")] public List<CallEdge> Edges { get; set; } = [];
    [JsonPropertyName("statistics")] public Dictionary<string, int> Statistics { get; set; } = [];
}

public sealed class ProjectResult
{
    [JsonPropertyName("project_path")] public string ProjectPath { get; set; } = "";
    [JsonPropertyName("project_name")] public string ProjectName { get; set; } = "";
    [JsonPropertyName("target_framework")] public string TargetFramework { get; set; } = "";
}

public sealed class DependencyReport
{
    [JsonPropertyName("generated_at")] public DateTime GeneratedAt { get; set; } = DateTime.Now;
    [JsonPropertyName("selected_project")] public string SelectedProject { get; set; } = "";
    [JsonPropertyName("projects")] public List<ProjectResult> Projects { get; set; } = [];
    [JsonPropertyName("packages")] public List<PackageInfo> Packages { get; set; } = [];
    [JsonPropertyName("project_references")] public List<ProjectReferenceInfo> ProjectReferences { get; set; } = [];
    [JsonPropertyName("project_dependencies")] public List<ProjectDependencyInfo> ProjectDependencies { get; set; } = [];
    [JsonPropertyName("cs_files")] public List<string> CsFiles { get; set; } = [];
    [JsonPropertyName("dependencies")] public List<DependencyUsage> Dependencies { get; set; } = [];
    [JsonPropertyName("unique_dependency_summary")] public List<UniqueDependency> UniqueDependencySummary { get; set; } = [];
    [JsonPropertyName("call_graph")] public CallGraph CallGraph { get; set; } = new();
}

public sealed class AnalysisResult
{
    public required DependencyReport Report { get; init; }
    public required string OutputDirectory { get; init; }
    public required string ReportPath { get; init; }
    public required string CallGraphPath { get; init; }
    public string? AiReportPath { get; set; }
    public string? AiAnalysis { get; set; }
}
