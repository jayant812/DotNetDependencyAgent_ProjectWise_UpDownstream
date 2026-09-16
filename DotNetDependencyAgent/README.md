# .NET Dependency Agent - Upstream/Downstream Edition

.NET 8 WPF desktop application for analyzing GitHub repositories or local .NET projects.

## What this version adds

- Confirmed project-to-project dependency direction from `<ProjectReference>`.
  - A project it references is **Downstream**.
  - A project that references it is **Upstream**.
- Local `.csproj` mode scans the nearby `.sln`/Git repository when available so reverse references can be found.
- Expanded package heuristics for databases, caches, queues/brokers, REST/gRPC, Azure/AWS, email, identity, search, telemetry, scheduling, storage, payments, testing, and more.
- Source-level producer/consumer refinements for Service Bus, RabbitMQ, Kafka, MassTransit and other common patterns.
- Confidence labels distinguish **Confirmed**, **Detected in source**, and **Possible (package heuristic)** results.
- Dedicated **Project Up/Downstream** UI tab.
- Git long-path protection using a short clone path (`C:\DDA\r\<id>`) and `git -c core.longpaths=true clone --depth 1`.

## Important interpretation

Package names are evidence of a possible integration, but often cannot prove direction. For example, a Kafka package could mean producer, consumer, or both. Source scanning is used to refine this when recognizable APIs are found. ProjectReference direction is considered confirmed.

## Run

1. Open `DotNetDependencyAgent.sln` in Visual Studio 2022.
2. Ensure the .NET 8 SDK and **.NET desktop development** workload are installed.
3. Press F5 or Ctrl+F5.
4. Select GitHub Repository URL or Local `.csproj`.
5. Click **Analyze Project**.

Gemini analysis is optional. Provide the API key in the application or via `GEMINI_API_KEY`.

## Structured AI Impact Report
The AI report no longer relies on raw Markdown rendering inside a TextBox. Gemini returns a strict JSON structure and the WPF application renders it as a native FlowDocument with headings and real tables. The output directory also contains:
- `ai_dependency_analysis.html` - clean browser-readable report
- `ai_dependency_analysis.md` - portable Markdown report
- `ai_dependency_analysis.json` - structured AI response for automation/integration

Report order: Executive Summary, Key Metrics, Confirmed Project Relationships, Upstream, Downstream, Bidirectional/Possible, Entry Points, Dependency Flow, Overall Assessment, Risks, Recommendations, Analysis Notes.

## Project-wise AI dependency report

The AI Impact Report now renders every discovered `.csproj` in a separate section. Each project has its own Upstream, Downstream, Bidirectional/Possible dependencies, Entry Points, Dependency Flow, Risks, Recommendations, and Notes. Confirmed `ProjectReference` and reverse-reference relationships are injected from static analysis so a project section is still present even if the AI response omits it. Package/source heuristics are grouped only under their owning project.
