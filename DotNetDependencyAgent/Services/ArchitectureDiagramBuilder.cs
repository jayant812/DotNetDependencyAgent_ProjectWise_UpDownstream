using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DotNetDependencyAgent;

public static class ArchitectureDiagramBuilder
{
    private sealed record Node(string Id, string Label, string Kind, string Subtitle, string Confidence, int Layer);
    private sealed record Edge(string From, string To, string Label, string Confidence);

    public static string BuildHtmlSvg(AiArchitectureReport ai, DependencyReport report)
    {
        BuildGraph(ai, report, out var nodes, out var edges);
        if (nodes.Count == 0)
            return "<div class='diagram-empty'>No architecture nodes were identified from the available evidence.</div>";

        var layers = nodes.GroupBy(n => n.Layer).OrderBy(g => g.Key).ToList();
        const int boxW = 220;
        const int boxH = 88;
        const int gapX = 34;
        const int gapY = 120;
        const int margin = 48;
        var maxPerLayer = Math.Max(1, layers.Max(g => g.Count()));
        var width = Math.Max(900, margin * 2 + maxPerLayer * boxW + (maxPerLayer - 1) * gapX);
        var height = margin * 2 + layers.Count * boxH + Math.Max(0, layers.Count - 1) * gapY + 70;

        var positions = new Dictionary<string, (double x, double y)>(StringComparer.OrdinalIgnoreCase);
        var layerIndex = 0;
        foreach (var group in layers)
        {
            var row = group.OrderBy(n => n.Label, StringComparer.OrdinalIgnoreCase).ToList();
            var rowWidth = row.Count * boxW + Math.Max(0, row.Count - 1) * gapX;
            var startX = (width - rowWidth) / 2.0;
            var y = margin + layerIndex * (boxH + gapY);
            for (var i = 0; i < row.Count; i++)
                positions[row[i].Id] = (startX + i * (boxW + gapX), y);
            layerIndex++;
        }

        var sb = new StringBuilder();
        sb.Append($"<div class='architecture-diagram-wrap'><svg class='architecture-diagram' viewBox='0 0 {width} {height}' role='img' aria-label='Repository architecture diagram'>");
        sb.Append("<defs><style>.node-title{font:600 13px 'Segoe UI',Arial,sans-serif;fill:#0f172a}.node-subtitle{font:11px 'Segoe UI',Arial,sans-serif;fill:#475569}.node-kind{font:10px 'Segoe UI',Arial,sans-serif;fill:#64748b}.edge-label{font:10px 'Segoe UI',Arial,sans-serif;fill:#475569}</style><marker id='arrow' markerWidth='10' markerHeight='10' refX='9' refY='3' orient='auto' markerUnits='strokeWidth'><path d='M0,0 L0,6 L9,3 z' fill='#64748b'/></marker><marker id='arrowPossible' markerWidth='10' markerHeight='10' refX='9' refY='3' orient='auto' markerUnits='strokeWidth'><path d='M0,0 L0,6 L9,3 z' fill='#94a3b8'/></marker><filter id='shadow' x='-20%' y='-20%' width='140%' height='140%'><feDropShadow dx='0' dy='3' stdDeviation='4' flood-color='#0f172a' flood-opacity='.14'/></filter></defs>");

        foreach (var edge in edges)
        {
            if (!positions.TryGetValue(edge.From, out var a) || !positions.TryGetValue(edge.To, out var b)) continue;
            var x1 = a.x + boxW / 2.0;
            var y1 = a.y + boxH;
            var x2 = b.x + boxW / 2.0;
            var y2 = b.y;
            var sameLayer = Math.Abs(y1 - y2) < boxH;
            if (sameLayer)
            {
                x1 = a.x + boxW;
                y1 = a.y + boxH / 2.0;
                x2 = b.x;
                y2 = b.y + boxH / 2.0;
            }
            var possible = IsPossible(edge.Confidence);
            var stroke = possible ? "#94a3b8" : "#64748b";
            var dash = possible ? " stroke-dasharray='7 6'" : "";
            var marker = possible ? "arrowPossible" : "arrow";
            var midX = (x1 + x2) / 2.0;
            var midY = (y1 + y2) / 2.0 - 7;
            sb.Append($"<line x1='{x1:0.#}' y1='{y1:0.#}' x2='{x2:0.#}' y2='{y2:0.#}' stroke='{stroke}' stroke-width='2'{dash} marker-end='url(#{marker})'/>");
            if (!string.IsNullOrWhiteSpace(edge.Label))
            {
                sb.Append($"<rect x='{midX - 54:0.#}' y='{midY - 11:0.#}' width='108' height='22' rx='11' fill='white' opacity='.96'/>");
                sb.Append($"<text x='{midX:0.#}' y='{midY + 4:0.#}' text-anchor='middle' class='edge-label'>{Xml(edge.Label, 22)}</text>");
            }
        }

        foreach (var node in nodes)
        {
            var p = positions[node.Id];
            var (fill, stroke, accent) = ColorsFor(node.Kind);
            sb.Append($"<g filter='url(#shadow)'><rect x='{p.x:0.#}' y='{p.y:0.#}' width='{boxW}' height='{boxH}' rx='14' fill='{fill}' stroke='{stroke}' stroke-width='1.5'/>");
            sb.Append($"<rect x='{p.x:0.#}' y='{p.y:0.#}' width='7' height='{boxH}' rx='4' fill='{accent}'/>");
            sb.Append($"<text x='{p.x + 20:0.#}' y='{p.y + 29:0.#}' class='node-title'>{Xml(node.Label, 28)}</text>");
            sb.Append($"<text x='{p.x + 20:0.#}' y='{p.y + 50:0.#}' class='node-subtitle'>{Xml(node.Subtitle, 34)}</text>");
            sb.Append($"<text x='{p.x + 20:0.#}' y='{p.y + 70:0.#}' class='node-kind'>{Xml(node.Kind, 28)}{(string.IsNullOrWhiteSpace(node.Confidence) ? "" : " · " + Xml(node.Confidence, 15))}</text></g>");
        }

        sb.Append("</svg></div>");
        sb.Append("<div class='diagram-legend'><span><i class='legend-dot project'></i>Project</span><span><i class='legend-dot entry'></i>Entry point</span><span><i class='legend-dot data'></i>Data/Storage</span><span><i class='legend-dot messaging'></i>Messaging</span><span><i class='legend-dot external'></i>External/AI</span><span><i class='legend-dot telemetry'></i>Telemetry</span><span class='legend-line'></span>Confirmed<span class='legend-line dashed'></span>Possible</div>");
        return sb.ToString();
    }

    public static string BuildStandaloneSvg(AiArchitectureReport ai, DependencyReport report)
    {
        var html = BuildHtmlSvg(ai, report);
        var start = html.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
        var end = html.IndexOf("</svg>", StringComparison.OrdinalIgnoreCase);
        return start >= 0 && end > start ? html.Substring(start, end - start + 6) : html;
    }

    public static string BuildMermaid(AiArchitectureReport ai, DependencyReport report)
    {
        BuildGraph(ai, report, out var nodes, out var edges);
        var sb = new StringBuilder("flowchart TB\n");
        foreach (var n in nodes)
            sb.AppendLine($"  {n.Id}[\"{Mermaid(n.Label)}\\n{Mermaid(n.Subtitle)}\"]");
        foreach (var e in edges)
        {
            var arrow = IsPossible(e.Confidence) ? "-.->" : "-->";
            var label = string.IsNullOrWhiteSpace(e.Label) ? "" : $"|{Mermaid(e.Label)}|";
            sb.AppendLine($"  {e.From} {arrow}{label} {e.To}");
        }
        return sb.ToString();
    }

    public static BlockUIContainer BuildWpfDiagram(AiArchitectureReport ai, DependencyReport report)
    {
        BuildGraph(ai, report, out var nodes, out var edges);
        var root = new StackPanel { Margin = new Thickness(0, 6, 0, 18) };
        root.Children.Add(new TextBlock
        {
            Text = "Repository Architecture Diagram",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(23, 54, 93)),
            Margin = new Thickness(0, 0, 0, 10)
        });

        foreach (var group in nodes.GroupBy(n => n.Layer).OrderBy(g => g.Key))
        {
            var row = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var node in group.OrderBy(n => n.Label, StringComparer.OrdinalIgnoreCase))
                row.Children.Add(CreateCard(node));
            root.Children.Add(row);
            if (group.Key != nodes.Max(n => n.Layer))
                root.Children.Add(new TextBlock { Text = "↓", FontSize = 26, HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brushes.SlateGray, Margin = new Thickness(0, 2, 0, 2) });
        }

        if (edges.Count > 0)
        {
            root.Children.Add(new TextBlock
            {
                Text = "Connections: " + string.Join("   •   ", edges.Take(10).Select(e => $"{LabelFor(nodes, e.From)} → {LabelFor(nodes, e.To)} ({e.Label})")) + (edges.Count > 10 ? " …" : ""),
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.DimGray,
                FontSize = 11,
                Margin = new Thickness(0, 8, 0, 0)
            });
        }

        return new BlockUIContainer(root);
    }

    private static Border CreateCard(Node node)
    {
        var (fillHex, strokeHex, accentHex) = ColorsFor(node.Kind);
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = node.Label, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, FontSize = 12.5 });
        panel.Children.Add(new TextBlock { Text = node.Subtitle, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 10.5, Margin = new Thickness(0, 3, 0, 0) });
        panel.Children.Add(new TextBlock { Text = node.Kind + (string.IsNullOrWhiteSpace(node.Confidence) ? "" : " · " + node.Confidence), Foreground = Brush(accentHex), FontSize = 9.5, Margin = new Thickness(0, 5, 0, 0) });
        return new Border
        {
            Width = 185,
            MinHeight = 82,
            Background = Brush(fillHex),
            BorderBrush = Brush(strokeHex),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(12),
            Margin = new Thickness(6),
            Child = panel
        };
    }

    private static void BuildGraph(AiArchitectureReport ai, DependencyReport report, out List<Node> nodes, out List<Edge> edges)
    {
        var nodeList = new List<Node>();
        var edgeList = new List<Edge>();
        var byLabel = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);

        Node AddNode(string label, string kind, string subtitle, string confidence, int layer)
        {
            label = Clean(label);
            if (byLabel.TryGetValue(label, out var existing)) return existing;
            var id = "n" + (byLabel.Count + 1);
            var node = new Node(id, label, NormalizeKind(kind, label), Clean(subtitle), Clean(confidence), layer);
            nodeList.Add(node);
            byLabel[label] = node;
            return node;
        }

        foreach (var p in report.Projects)
            AddNode(p.ProjectName, "Project", string.IsNullOrWhiteSpace(p.TargetFramework) ? ".NET project" : p.TargetFramework, "Confirmed", 1);

        foreach (var p in ai.Projects)
        {
            var project = AddNode(p.ProjectName, "Project", "Application project", "Confirmed", 1);
            foreach (var ep in p.EntryPoints)
            {
                var entry = AddNode(ep.Component, "Entry Point", ep.EntryType + (string.IsNullOrWhiteSpace(ep.Trigger) ? "" : " · " + ep.Trigger), ep.Confidence, 0);
                AddEdge(entry, project, ep.EntryType, ep.Confidence);
            }
            foreach (var d in p.UpstreamDependencies)
            {
                var up = AddNode(d.Name, d.Type, d.Explanation, d.Confidence, 0);
                AddEdge(up, project, d.Type, d.Confidence);
            }
            foreach (var d in p.DownstreamDependencies)
            {
                var down = AddNode(d.Name, d.Type, d.Explanation, d.Confidence, 2);
                AddEdge(project, down, d.Type, d.Confidence);
            }
        }

        foreach (var c in ai.ProjectArchitecture.Components)
        {
            var layer = GuessLayer(c.Type, c.Name);
            AddNode(c.Name, c.Type, c.Responsibility, c.Confidence, layer);
        }

        foreach (var c in ai.ProjectArchitecture.Connections)
        {
            var from = AddNode(c.From, GuessKind(c.From), c.Relationship, c.Confidence, GuessLayer(GuessKind(c.From), c.From));
            var to = AddNode(c.To, GuessKind(c.To), c.Relationship, c.Confidence, GuessLayer(GuessKind(c.To), c.To));
            AddEdge(from, to, c.Relationship, c.Confidence);
        }

        foreach (var d in report.ProjectDependencies.Where(x => x.Direction.Equals("Downstream", StringComparison.OrdinalIgnoreCase)))
        {
            var from = AddNode(d.Project, "Project", "Application project", d.Confidence, 1);
            var to = AddNode(d.RelatedProject, "Project", "Referenced project", d.Confidence, 1);
            AddEdge(from, to, d.Relationship, d.Confidence);
        }

        // If AI returned no external architecture at all, fall back to deterministic static dependency summary.
        foreach (var d in report.UniqueDependencySummary)
        {
            if (string.IsNullOrWhiteSpace(d.Name)) continue;
            var project = AddNode(d.Project, "Project", "Application project", "Confirmed", 1);
            var layer = d.Direction.Equals("Upstream", StringComparison.OrdinalIgnoreCase) ? 0 : 2;
            var dep = AddNode(d.Name, d.Type, d.Direction + " dependency", d.Confidence, layer);
            if (layer == 0) AddEdge(dep, project, d.Type, d.Confidence); else AddEdge(project, dep, d.Type, d.Confidence);
        }

        nodes = nodeList.GroupBy(n => n.Label, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).Take(24).ToList();
        var valid = nodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        edges = edgeList.Where(e => valid.Contains(e.From) && valid.Contains(e.To) && !e.From.Equals(e.To, StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => $"{e.From}|{e.To}|{e.Label}", StringComparer.OrdinalIgnoreCase).Select(g => g.First()).Take(40).ToList();

        void AddEdge(Node from, Node to, string label, string confidence)
        {
            if (from.Id == to.Id) return;
            edgeList.Add(new Edge(from.Id, to.Id, Clean(label), Clean(confidence)));
        }
    }

    private static int GuessLayer(string kind, string label)
    {
        var s = (kind + " " + label).ToLowerInvariant();
        if (s.Contains("entry") || s.Contains("trigger") || s.Contains("client") || s.Contains("consumer") || s.Contains("scheduler") || s.Contains("upstream")) return 0;
        if (s.Contains("project") || s.Contains("application") || s.Contains("service layer") || s.Contains("api layer")) return 1;
        return 2;
    }

    private static string GuessKind(string label)
    {
        var s = label.ToLowerInvariant();
        if (s.Contains("cosmos") || s.Contains("sql") || s.Contains("database") || s.Contains("db") || s.Contains("storage")) return "Database";
        if (s.Contains("service bus") || s.Contains("queue") || s.Contains("kafka") || s.Contains("rabbit")) return "Queue";
        if (s.Contains("insight") || s.Contains("telemetry") || s.Contains("monitor") || s.Contains("log")) return "Telemetry";
        if (s.Contains("openai") || s.Contains("gemini") || s.Contains("external") || s.Contains("rest")) return "External";
        if (s.Contains("trigger") || s.Contains("http") || s.Contains("client")) return "Entry Point";
        return "Other";
    }

    private static string NormalizeKind(string kind, string label)
    {
        var s = (kind + " " + label).ToLowerInvariant();
        if (s.Contains("project") || s.Contains("application")) return "Project";
        if (s.Contains("entry") || s.Contains("trigger") || s.Contains("http")) return "Entry Point";
        if (s.Contains("database") || s.Contains("data") || s.Contains("storage") || s.Contains("cosmos") || s.Contains("sql")) return "Data/Storage";
        if (s.Contains("queue") || s.Contains("message") || s.Contains("service bus") || s.Contains("kafka") || s.Contains("rabbit")) return "Messaging";
        if (s.Contains("telemetry") || s.Contains("insight") || s.Contains("monitor") || s.Contains("observ")) return "Telemetry";
        if (s.Contains("external") || s.Contains("openai") || s.Contains("gemini") || s.Contains("ai") || s.Contains("api")) return "External/AI";
        return string.IsNullOrWhiteSpace(kind) ? "Other" : kind;
    }

    private static (string fill, string stroke, string accent) ColorsFor(string kind)
    {
        var s = kind.ToLowerInvariant();
        if (s.Contains("project")) return ("#eff6ff", "#93c5fd", "#2563eb");
        if (s.Contains("entry")) return ("#ecfdf5", "#86efac", "#16a34a");
        if (s.Contains("data") || s.Contains("storage")) return ("#fff7ed", "#fdba74", "#ea580c");
        if (s.Contains("messag")) return ("#fff7ed", "#fdba74", "#d97706");
        if (s.Contains("external") || s.Contains("ai")) return ("#faf5ff", "#d8b4fe", "#9333ea");
        if (s.Contains("telemetry")) return ("#f8fafc", "#cbd5e1", "#64748b");
        return ("#f8fafc", "#cbd5e1", "#475569");
    }

    private static bool IsPossible(string confidence)
    {
        var s = confidence.ToLowerInvariant();
        return s.Contains("possible") || s.Contains("low") || s.Contains("unknown");
    }

    private static string Clean(string? text) => string.IsNullOrWhiteSpace(text) ? "-" : text.Trim().Replace("\r", " ").Replace("\n", " ");
    private static string Xml(string text, int max) => WebUtility.HtmlEncode(Trim(text, max));
    private static string Trim(string text, int max) => text.Length <= max ? text : text[..Math.Max(1, max - 1)] + "…";
    private static string Mermaid(string text) => text.Replace("\"", "'").Replace("[", "(").Replace("]", ")");
    private static Brush Brush(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    private static string LabelFor(List<Node> nodes, string id) => nodes.FirstOrDefault(n => n.Id == id)?.Label ?? id;
}
