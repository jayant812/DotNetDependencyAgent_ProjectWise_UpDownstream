using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DotNetDependencyAgent;

public sealed class DependencyAnalyzer
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    { "bin", "obj", ".git", ".vs", "node_modules", "packages" };

    private static readonly Regex MethodRegex = new(
        @"^\s*(?:public|private|protected|internal|static|virtual|override|async|sealed|new|partial|extern|unsafe|\s)+\s*[\w<>,\[\]\?\.]+\s+(?<name>[A-Za-z_]\w*)\s*\([^;]*\)\s*(?:\{|=>)?\s*$",
        RegexOptions.Compiled);

    private static readonly Regex CallRegex = new(@"\b(?<name>[A-Za-z_]\w*)\s*\(", RegexOptions.Compiled);

    private static readonly HashSet<string> IgnoredCalls = new(StringComparer.OrdinalIgnoreCase)
    {
        "if","for","foreach","while","switch","catch","using","lock","return","new","nameof","typeof","sizeof","when",
        "get","set","add","remove","base","this","Task","ValueTask","ToString","GetType"
    };

    private sealed record ExternalRule(string Name, string Type, string Direction, string[] Terms);

    private static readonly ExternalRule[] ExternalPackageRules =
    [
        // Database / data access
        new("SQL Server", "Database", "Downstream", ["microsoft.data.sqlclient", "system.data.sqlclient", "entityframeworkcore.sqlserver"]),
        new("PostgreSQL", "Database", "Downstream", ["npgsql", "entityframeworkcore.postgresql"]),
        new("MySQL / MariaDB", "Database", "Downstream", ["mysqlconnector", "mysql.data", "pomelo.entityframeworkcore.mysql"]),
        new("Oracle Database", "Database", "Downstream", ["oracle.manageddataaccess", "oracle.entityframeworkcore"]),
        new("SQLite", "Database", "Downstream", ["microsoft.data.sqlite", "entityframeworkcore.sqlite"]),
        new("MongoDB", "NoSQL Database", "Downstream", ["mongodb.driver"]),
        new("Azure Cosmos DB", "NoSQL Database", "Downstream", ["microsoft.azure.cosmos", "microsoft.azure.documentdb"]),
        new("Dapper", "Data Access", "Downstream", ["dapper"]),
        new("Entity Framework Core", "ORM / Data Access", "Downstream", ["microsoft.entityframeworkcore"]),

        // Cache
        new("Redis", "Distributed Cache", "Downstream", ["stackexchange.redis", "microsoft.extensions.caching.stackexchange"]),
        new("Memory Cache", "Cache", "Downstream", ["microsoft.extensions.caching.memory"]),
        new("NCache", "Distributed Cache", "Downstream", ["alachisoft.ncache"]),

        // Messaging / eventing - package alone cannot prove producer vs consumer
        new("RabbitMQ", "Message Queue / Broker", "Possible Both", ["rabbitmq.client"]),
        new("Apache Kafka", "Message Queue / Broker", "Possible Both", ["confluent.kafka", "kafka"]),
        new("MassTransit", "Messaging Framework", "Possible Both", ["masstransit"]),
        new("Azure Service Bus", "Message Queue / Broker", "Possible Both", ["azure.messaging.servicebus", "microsoft.azure.servicebus"]),
        new("Amazon SQS", "Message Queue", "Possible Both", ["awssdk.sqs"]),
        new("Amazon SNS", "Event / Notification", "Possible Both", ["awssdk.simplenotificationservice"]),
        new("NServiceBus", "Messaging Framework", "Possible Both", ["nservicebus"]),
        new("CAP", "Distributed Messaging", "Possible Both", ["dotnetcore.cap"]),
        new("Rebus", "Messaging Framework", "Possible Both", ["rebus"]),

        // HTTP / APIs
        new("HTTP API Client", "External REST API", "Downstream", ["microsoft.extensions.http", "system.net.http"]),
        new("Refit API Client", "External REST API", "Downstream", ["refit"]),
        new("RestSharp", "External REST API", "Downstream", ["restsharp"]),
        new("Flurl", "External REST API", "Downstream", ["flurl.http"]),
        new("ASP.NET Core Web API", "Inbound HTTP API", "Upstream", ["microsoft.aspnetcore", "microsoft.net.sdk.web"]),
        new("OData", "Inbound HTTP API", "Upstream", ["microsoft.aspnetcore.odata", "microsoft.data.odata"]),
        new("GraphQL Server", "Inbound API", "Upstream", ["hotchocolate.aspnetcore", "graphql.server"]),
        new("GraphQL Client", "External API", "Downstream", ["graphql.client"]),

        // gRPC
        new("gRPC Client", "RPC Client", "Downstream", ["grpc.net.client"]),
        new("gRPC Server", "Inbound RPC", "Upstream", ["grpc.aspnetcore"]),
        new("gRPC", "RPC", "Possible Both", ["grpc.core"]),

        // Azure
        new("Azure Blob Storage", "Cloud Storage", "Downstream", ["azure.storage.blobs", "windowsazure.storage"]),
        new("Azure Queue Storage", "Message Queue", "Possible Both", ["azure.storage.queues"]),
        new("Azure Event Hubs", "Event Streaming", "Possible Both", ["azure.messaging.eventhubs"]),
        new("Azure Event Grid", "Event Integration", "Possible Both", ["azure.messaging.eventgrid"]),
        new("Azure Key Vault", "Secrets Management", "Downstream", ["azure.security.keyvault"]),
        new("Azure App Configuration", "Configuration", "Downstream", ["microsoft.extensions.configuration.azureappconfiguration"]),
        new("Azure Search", "Search Service", "Downstream", ["azure.search.documents"]),
        new("Azure Functions", "Serverless / Trigger", "Upstream", ["microsoft.azure.functions", "microsoft.net.sdk.functions"]),

        // AWS
        new("Amazon S3", "Cloud Storage", "Downstream", ["awssdk.s3"]),
        new("Amazon DynamoDB", "NoSQL Database", "Downstream", ["awssdk.dynamodbv2"]),
        new("AWS Lambda", "Serverless / Trigger", "Upstream", ["amazon.lambda"]),
        new("AWS Secrets Manager", "Secrets Management", "Downstream", ["awssdk.secretsmanager"]),

        // Email / identity / search
        new("MailKit / SMTP", "Email Service", "Downstream", ["mailkit", "mimekit"]),
        new("SendGrid", "Email Service", "Downstream", ["sendgrid"]),
        new("Amazon SES", "Email Service", "Downstream", ["awssdk.simpleemail"]),
        new("Microsoft Identity / Entra ID", "Authentication", "Downstream", ["microsoft.identity"]),
        new("IdentityServer", "Identity Provider", "Possible Both", ["identityserver", "duende.identityserver"]),
        new("JWT Authentication", "Authentication", "Upstream", ["microsoft.aspnetcore.authentication.jwtbearer"]),
        new("Auth0", "Authentication Provider", "Downstream", ["auth0"]),
        new("Elasticsearch", "Search Engine", "Downstream", ["elasticsearch.net", "elastic.clients.elasticsearch", "nest"]),
        new("Lucene", "Search Engine", "Downstream", ["lucene.net"]),

        // Observability
        new("Application Insights", "Monitoring / Telemetry", "Downstream", ["applicationinsights"]),
        new("Serilog", "Logging", "Downstream", ["serilog"]),
        new("NLog", "Logging", "Downstream", ["nlog"]),
        new("OpenTelemetry", "Observability", "Downstream", ["opentelemetry"]),
        new("Sentry", "Error Monitoring", "Downstream", ["sentry"]),
        new("Datadog", "Monitoring", "Downstream", ["datadog"]),
        new("New Relic", "Monitoring", "Downstream", ["newrelic"]),

        // Scheduling / real-time / file transfer
        new("Hangfire", "Background Job / Scheduler", "Upstream", ["hangfire"]),
        new("Quartz.NET", "Scheduler", "Upstream", ["quartz"]),
        new("Coravel", "Scheduler / Background Job", "Upstream", ["coravel"]),
        new("SignalR", "Real-Time Communication", "Possible Both", ["signalr"]),
        new("SFTP", "File Transfer", "Downstream", ["ssh.net", "renci.sshnet"]),
        new("FTP", "File Transfer", "Downstream", ["fluentftp"]),
        new("WCF Client", "SOAP / Service Client", "Downstream", ["system.servicemodel"]),
        new("CoreWCF", "SOAP Service", "Upstream", ["corewcf"]),

        // Internal / event store / payments / feature flags
        new("MediatR", "Internal Messaging / CQRS", "Internal", ["mediatr"]),
        new("EventStoreDB", "Event Store", "Possible Both", ["eventstore.client"]),
        new("Stripe", "Payment Gateway", "Downstream", ["stripe.net"]),
        new("PayPal", "Payment Gateway", "Downstream", ["paypal"]),
        new("Microsoft Feature Management", "Feature Flags", "Downstream", ["microsoft.featuremanagement"]),
        new("LaunchDarkly", "Feature Flags", "Downstream", ["launchdarkly"]),

        // Test-only packages
        new("xUnit", "Testing", "Test Only", ["xunit"]),
        new("NUnit", "Testing", "Test Only", ["nunit"]),
        new("MSTest", "Testing", "Test Only", ["mstest"]),
        new("Moq", "Testing / Mocking", "Test Only", ["moq"]),
        new("NSubstitute", "Testing / Mocking", "Test Only", ["nsubstitute"]),
        new("FluentAssertions", "Testing", "Test Only", ["fluentassertions"])
    ];

    public event Action<int, string>? Progress;
    public event Action<string>? Log;

    public async Task<AnalysisResult> AnalyzeAsync(string input, CancellationToken ct = default)
    {
        Progress?.Invoke(2, "Resolving project input");
        var resolved = await ResolveProjectsAsync(input, ct);
        var projects = resolved.Projects;
        if (projects.Count == 0) throw new InvalidOperationException("No .csproj files were found.");

        var analysisRoot = CreateOutputDirectory(resolved.SelectedProject);
        Log?.Invoke($"Output: {analysisRoot}");

        var report = new DependencyReport { SelectedProject = resolved.SelectedProject };
        var allFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Progress?.Invoke(10, "Reading project files");
        foreach (var project in projects)
        {
            ct.ThrowIfCancellationRequested();
            AnalyzeProjectFile(project, report);
            foreach (var file in EnumerateCsFiles(Path.GetDirectoryName(project)!)) allFiles.Add(file);
        }

        BuildProjectDependencyDirections(report);
        AddPackageHeuristicDependencies(report);
        report.CsFiles = allFiles.OrderBy(x => x).ToList();

        Progress?.Invoke(30, $"Scanning {report.CsFiles.Count} C# files");
        var projectDirectories = report.Projects
            .Select(p => (Project: p.ProjectPath, Directory: Path.GetDirectoryName(p.ProjectPath)!))
            .OrderByDescending(x => x.Directory.Length)
            .ToList();

        var index = 0;
        foreach (var file in report.CsFiles)
        {
            ct.ThrowIfCancellationRequested();
            var ownerProject = projectDirectories.FirstOrDefault(x => IsUnderDirectory(file, x.Directory)).Project ?? "";
            AnalyzeSourceFile(file, ownerProject, report.Dependencies);
            index++;
            if (index % Math.Max(1, report.CsFiles.Count / 10) == 0)
                Progress?.Invoke(30 + (int)(30.0 * index / Math.Max(1, report.CsFiles.Count)), $"Scanning source files ({index}/{report.CsFiles.Count})");
        }

        Progress?.Invoke(63, "Creating dependency summary");
        report.UniqueDependencySummary = report.Dependencies
            .GroupBy(d => $"{d.Project}\u001f{d.Name}\u001f{d.Type}\u001f{d.Direction}\u001f{d.Confidence}", StringComparer.OrdinalIgnoreCase)
            .Select(g => new UniqueDependency
            {
                Project = ShortProjectName(g.First().Project),
                Name = g.First().Name,
                Type = g.First().Type,
                Direction = g.First().Direction,
                Confidence = g.First().Confidence,
                Occurrences = g.Count(),
                SourceFiles = g.Select(x => x.SourceFile).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList()
            })
            .OrderBy(x => DirectionOrder(x.Direction)).ThenBy(x => x.Project).ThenBy(x => x.Name).ToList();

        Progress?.Invoke(70, "Building function call graph");
        report.CallGraph = BuildCallGraph(report.CsFiles);

        var options = new JsonSerializerOptions { WriteIndented = true };
        var reportPath = Path.Combine(analysisRoot, "dependency_report.json");
        var callGraphPath = Path.Combine(analysisRoot, "function_call_graph.json");
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, options), ct);
        await File.WriteAllTextAsync(callGraphPath, JsonSerializer.Serialize(report.CallGraph, options), ct);
        Progress?.Invoke(80, "Local dependency analysis complete");

        return new AnalysisResult { Report = report, OutputDirectory = analysisRoot, ReportPath = reportPath, CallGraphPath = callGraphPath };
    }

    private sealed record ResolvedInput(List<string> Projects, string SelectedProject);

    private async Task<ResolvedInput> ResolveProjectsAsync(string input, CancellationToken ct)
    {
        input = input.Trim();

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            var cloneRoot = GetShortCloneRoot();
            Directory.CreateDirectory(cloneRoot);
            var clonePath = Path.Combine(cloneRoot, Guid.NewGuid().ToString("N")[..8]);

            Progress?.Invoke(5, $"Cloning GitHub repository to {clonePath}");
            Log?.Invoke($"Clone root: {clonePath}");

            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-c core.longpaths=true clone --depth 1 \"{input}\" \"{clonePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start Git. Make sure Git for Windows is installed and git.exe is available in PATH.");
            var stdoutTask = p.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = p.StandardError.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (p.ExitCode != 0)
            {
                TryDeleteDirectory(clonePath);
                throw new InvalidOperationException("Git clone failed.\r\n\r\n" + stderr.Trim() + (string.IsNullOrWhiteSpace(stdout) ? "" : "\r\n\r\n" + stdout.Trim()));
            }

            var projects = Directory.EnumerateFiles(clonePath, "*.csproj", SearchOption.AllDirectories)
                .Where(pth => !HasIgnoredDirectory(pth)).Select(Path.GetFullPath).OrderBy(pth => pth).ToList();

            if (projects.Count == 0)
                throw new InvalidOperationException($"Repository cloned successfully to '{clonePath}', but no .csproj files were found.");

            Log?.Invoke($"Repository cloned successfully. Found {projects.Count} project(s).");
            return new ResolvedInput(projects, projects[0]);
        }

        if (!File.Exists(input) || !input.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException("The .csproj file was not found.", input);

        var selected = Path.GetFullPath(input);
        var scanRoot = FindSolutionOrRepoRoot(Path.GetDirectoryName(selected)!);
        var localProjects = scanRoot is null
            ? [selected]
            : Directory.EnumerateFiles(scanRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(pth => !HasIgnoredDirectory(pth)).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(pth => pth).ToList();

        if (!localProjects.Contains(selected, StringComparer.OrdinalIgnoreCase)) localProjects.Insert(0, selected);
        Log?.Invoke(scanRoot is null
            ? "Local mode: analyzing selected project only (no nearby .sln/.git root found)."
            : $"Local mode: scanning {localProjects.Count} project(s) under {scanRoot} so reverse/upstream ProjectReference relationships can be detected.");
        return new ResolvedInput(localProjects, selected);
    }

    private static string? FindSolutionOrRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        for (var i = 0; i < 7 && dir is not null; i++, dir = dir.Parent)
        {
            try
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")) || Directory.EnumerateFiles(dir.FullName, "*.sln", SearchOption.TopDirectoryOnly).Any())
                    return dir.FullName;
            }
            catch { }
        }
        return null;
    }

    private static string GetShortCloneRoot()
    {
        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        var preferred = Path.Combine(systemDrive, "DDA", "r");
        try
        {
            Directory.CreateDirectory(preferred);
            var testFile = Path.Combine(preferred, ".write_test");
            File.WriteAllText(testFile, "ok");
            File.Delete(testFile);
            return preferred;
        }
        catch
        {
            var fallback = Path.Combine(Path.GetTempPath(), "DDA", "r");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }

    private static string CreateOutputDirectory(string firstProject)
    {
        var root = Path.Combine(Path.GetDirectoryName(firstProject)!, "dependency_analysis", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static bool HasIgnoredDirectory(string path) => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(IgnoredDirectories.Contains);
    private static IEnumerable<string> EnumerateCsFiles(string projectDirectory) => Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories).Where(p => !HasIgnoredDirectory(p));

    private void AnalyzeProjectFile(string csprojPath, DependencyReport report)
    {
        Log?.Invoke($"Project: {csprojPath}");
        var doc = XDocument.Load(csprojPath);
        var framework = doc.Descendants().FirstOrDefault(x => x.Name.LocalName is "TargetFramework" or "TargetFrameworks")?.Value ?? "";
        report.Projects.Add(new ProjectResult { ProjectPath = csprojPath, ProjectName = ShortProjectName(csprojPath), TargetFramework = framework });

        foreach (var p in doc.Descendants().Where(x => x.Name.LocalName == "PackageReference"))
        {
            var name = p.Attribute("Include")?.Value ?? p.Attribute("Update")?.Value ?? "";
            if (string.IsNullOrWhiteSpace(name)) continue;
            var version = p.Attribute("Version")?.Value ?? p.Elements().FirstOrDefault(x => x.Name.LocalName == "Version")?.Value ?? "";
            report.Packages.Add(new PackageInfo { Name = name, Version = version, Classification = ClassifyPackage(name), Project = csprojPath });
        }

        foreach (var r in doc.Descendants().Where(x => x.Name.LocalName == "ProjectReference"))
        {
            var include = r.Attribute("Include")?.Value;
            if (string.IsNullOrWhiteSpace(include)) continue;
            var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(csprojPath)!, include));
            report.ProjectReferences.Add(new ProjectReferenceInfo { Project = csprojPath, Reference = include, ResolvedReference = resolved });
        }
    }

    private static void BuildProjectDependencyDirections(DependencyReport report)
    {
        var known = report.Projects.Select(p => Path.GetFullPath(p.ProjectPath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var r in report.ProjectReferences)
        {
            var source = Path.GetFullPath(r.Project);
            var target = Path.GetFullPath(r.ResolvedReference);

            report.ProjectDependencies.Add(new ProjectDependencyInfo
            {
                Project = ShortProjectName(source), RelatedProject = ShortProjectName(target), Direction = "Downstream",
                Relationship = "ProjectReference", Confidence = "Confirmed"
            });

            if (known.Contains(target))
            {
                report.ProjectDependencies.Add(new ProjectDependencyInfo
                {
                    Project = ShortProjectName(target), RelatedProject = ShortProjectName(source), Direction = "Upstream",
                    Relationship = "Reverse ProjectReference", Confidence = "Confirmed"
                });
            }
        }

        report.ProjectDependencies = report.ProjectDependencies
            .GroupBy(x => $"{x.Project}|{x.RelatedProject}|{x.Direction}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.Project).ThenBy(x => DirectionOrder(x.Direction)).ThenBy(x => x.RelatedProject).ToList();
    }

    private static void AddPackageHeuristicDependencies(DependencyReport report)
    {
        foreach (var package in report.Packages)
        {
            var low = package.Name.ToLowerInvariant();
            foreach (var rule in ExternalPackageRules)
            {
                if (!rule.Terms.Any(low.Contains)) continue;
                report.Dependencies.Add(new DependencyUsage
                {
                    Project = package.Project,
                    Name = rule.Name,
                    Type = rule.Type,
                    Direction = rule.Direction,
                    Confidence = "Possible (package heuristic)",
                    SourceFile = package.Project,
                    Evidence = $"PackageReference: {package.Name} {package.Version}".Trim()
                });
            }
        }
    }

    private static string ClassifyPackage(string n)
    {
        var s = n.ToLowerInvariant();
        if (s.Contains("azure") || s.Contains("servicebus") || s.Contains("storage") || s.Contains("awssdk")) return "Cloud/Integration";
        if (s.Contains("entityframework") || s.Contains("sql") || s.Contains("mongodb") || s.Contains("cosmos") || s.Contains("npgsql")) return "Database";
        if (s.Contains("http") || s.Contains("restsharp") || s.Contains("refit") || s.Contains("grpc")) return "HTTP/RPC";
        if (s.Contains("rabbit") || s.Contains("kafka") || s.Contains("masstransit") || s.Contains("nservicebus")) return "Messaging";
        if (s.Contains("openai") || s.Contains("semantic") || s.Contains("google") || s.Contains("gemini")) return "AI";
        if (s.Contains("serilog") || s.Contains("nlog") || s.Contains("opentelemetry")) return "Observability";
        if (s.Contains("xunit") || s.Contains("nunit") || s.Contains("mstest") || s.Contains("moq")) return "Testing";
        return "NuGet Package";
    }

    private static void AnalyzeSourceFile(string file, string ownerProject, List<DependencyUsage> output)
    {
        string[] lines;
        try { lines = File.ReadAllLines(file); } catch { return; }
        string currentFunction = "";

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var method = MethodRegex.Match(line);
            if (method.Success) currentFunction = method.Groups["name"].Value;
            var low = line.ToLowerInvariant();

            AddIf("HTTP Endpoint", "HTTP/API", "Upstream", low.Contains("[httpget") || low.Contains("[httppost") || low.Contains("[httpput") || low.Contains("[httpdelete") || low.Contains("mapget(") || low.Contains("mappost("));
            AddIf("Azure Function Trigger", "Trigger", "Upstream", low.Contains("trigger") && (low.Contains("servicebus") || low.Contains("queue") || low.Contains("timer") || low.Contains("eventgrid") || low.Contains("httptrigger")));
            AddIf("HttpClient / External REST API", "HTTP/API", "Downstream", low.Contains("httpclient") || low.Contains("getasync(") || low.Contains("postasync(") || low.Contains("sendasync("));
            AddIf("SQL / Relational Database", "Database", "Downstream", low.Contains("sqlconnection") || low.Contains("dbcontext") || low.Contains("usesqlserver") || low.Contains("queryasync(") || low.Contains("executereader"));
            AddIf("Azure Cosmos DB", "Database", "Downstream", low.Contains("cosmosclient") || low.Contains("createitemasync") || low.Contains("upsertitemasync"));
            AddIf("Azure Service Bus Consumer", "Messaging", "Upstream", low.Contains("servicebustrigger") || low.Contains("servicebusprocessor") || low.Contains("processmessageasync"));
            AddIf("Azure Service Bus Producer", "Messaging", "Downstream", low.Contains("servicebussender") || low.Contains("sendmessageasync") || low.Contains("sendmessagesasync"));
            AddIf("RabbitMQ Consumer", "Messaging", "Upstream", low.Contains("basicconsume") || low.Contains("eventingbasicconsumer") || low.Contains("asyncEventingBasicConsumer".ToLowerInvariant()));
            AddIf("RabbitMQ Producer", "Messaging", "Downstream", low.Contains("basicpublish"));
            AddIf("Kafka Consumer", "Messaging", "Upstream", low.Contains("consumerbuilder") || low.Contains(".consume("));
            AddIf("Kafka Producer", "Messaging", "Downstream", low.Contains("producerbuilder") || low.Contains("produceasync(") || low.Contains(".produce("));
            AddIf("MassTransit Consumer", "Messaging", "Upstream", low.Contains("iconsumer<") || low.Contains("consumerdefinition<") || low.Contains("consumer<"));
            AddIf("MassTransit Producer", "Messaging", "Downstream", low.Contains("ipublishendpoint") || low.Contains("isendendpoint") || low.Contains("publish<") || low.Contains("send<"));
            AddIf("Redis", "Distributed Cache", "Downstream", low.Contains("connectionmultiplexer") || low.Contains("idatabase") && low.Contains("redis"));
            AddIf("Azure Storage / Blob", "Storage", "Downstream", low.Contains("blobclient") || low.Contains("blobcontainerclient"));
            AddIf("OpenAI / Azure OpenAI", "AI", "Downstream", low.Contains("openai") || low.Contains("chatcomplet") || low.Contains("azureopenai"));
            AddIf("Google Gemini", "AI", "Downstream", low.Contains("gemini") || (low.Contains("generative") && low.Contains("google")));
            AddIf("Hangfire Job", "Background Job / Scheduler", "Upstream", low.Contains("backgroundjob.enqueue") || low.Contains("recurringjob.addorupdate") || low.Contains("[automaticretry"));
            AddIf("Quartz Job", "Scheduler", "Upstream", low.Contains("ijob") || low.Contains("itrigger") && low.Contains("quartz"));
            AddIf("gRPC Service", "Inbound RPC", "Upstream", low.Contains("bindservice") || low.Contains("servercallcontext"));
            AddIf("gRPC Client", "RPC Client", "Downstream", low.Contains("grpcchannel") || low.Contains("grpc.net.client"));

            void AddIf(string name, string type, string direction, bool condition)
            {
                if (!condition) return;
                output.Add(new DependencyUsage
                {
                    Project = ownerProject,
                    Name = name,
                    Type = type,
                    Direction = direction,
                    Confidence = "Detected in source",
                    SourceFile = file,
                    LineNumber = i + 1,
                    FunctionName = currentFunction,
                    Evidence = line.Trim()
                });
            }
        }
    }

    private static bool IsUnderDirectory(string file, string directory)
    {
        var fullFile = Path.GetFullPath(file).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullDir = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullFile.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase);
    }

    private static int DirectionOrder(string d) => d switch
    {
        "Upstream" => 0,
        "Downstream" => 1,
        "Possible Both" => 2,
        "Internal" => 3,
        "Test Only" => 4,
        _ => 5
    };

    private static string ShortProjectName(string path) => string.IsNullOrWhiteSpace(path) ? "" : Path.GetFileNameWithoutExtension(path);

    private static CallGraph BuildCallGraph(List<string> files)
    {
        var graph = new CallGraph();
        var bodies = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            string[] lines; try { lines = File.ReadAllLines(file); } catch { continue; }
            string? current = null; var braceDepth = 0; var body = new List<string>();
            for (var i = 0; i < lines.Length; i++)
            {
                var m = MethodRegex.Match(lines[i]);
                if (m.Success)
                {
                    current = m.Groups["name"].Value;
                    graph.Functions.Add(new FunctionNode { Name = current, File = file, Line = i + 1 });
                    body = []; bodies[$"{file}|{current}|{i + 1}"] = body; braceDepth = 0;
                }
                if (current is null) continue;
                body.Add(lines[i]);
                braceDepth += lines[i].Count(c => c == '{') - lines[i].Count(c => c == '}');
                if (braceDepth < 0 || (braceDepth == 0 && i > 0 && lines[i].Contains('}'))) current = null;
            }
        }

        var functionNames = graph.Functions.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in bodies)
        {
            var caller = kv.Key.Split('|')[1];
            foreach (var line in kv.Value)
            foreach (Match m in CallRegex.Matches(line))
            {
                var callee = m.Groups["name"].Value;
                if (IgnoredCalls.Contains(callee) || callee.Equals(caller, StringComparison.OrdinalIgnoreCase) || !functionNames.Contains(callee)) continue;
                if (!graph.Edges.Any(e => e.Caller.Equals(caller, StringComparison.OrdinalIgnoreCase) && e.Callee.Equals(callee, StringComparison.OrdinalIgnoreCase)))
                    graph.Edges.Add(new CallEdge { Caller = caller, Callee = callee });
            }
        }

        graph.Statistics["total_functions"] = graph.Functions.Count;
        graph.Statistics["total_nodes"] = graph.Functions.Select(f => f.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        graph.Statistics["total_edges"] = graph.Edges.Count;
        return graph;
    }
}
