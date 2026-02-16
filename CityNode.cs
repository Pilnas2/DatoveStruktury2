using System.Windows; // Pro Point
using System.IO;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;

namespace DopravniSit
{
    // Konkrétní třída uzlu pro silniční síť
    public class CityNode : Node<string, Point, string, double>
    {
        public CityNode(string id, Point pos) : base(id, pos) { }
    }

    // Konkrétní implementace AbstrGraph
    public class RoadNetwork : AbstrGraph<string, Point, string, double>
    {
        protected override double Zero => 0.0;
        protected override double MaxValue => double.MaxValue;

        protected override double AddWeights(double a, double b) => a + b;

        // Překrytí pro správné typování seznamu hran
        public new void AddNode(string key, Point data)
        {
            NodesBST.Insert(key, new CityNode(key, data));
        }

        public new void AddEdge(string source, string target, string roadName, double time)
        {
            var sNode = NodesBST.Find(source) as CityNode;
            var tNode = NodesBST.Find(target) as CityNode;

            if (sNode != null && tNode != null)
            {
                sNode.Edges.Add(new Edge<string, string, double>(target, roadName, time));
                tNode.Edges.Add(new Edge<string, string, double>(source, roadName, time)); // Obousměrné [cite: 8]
            }
        }

        public new CityNode GetNode(string key) => NodesBST.Find(key) as CityNode;
        public new List<CityNode> GetAllNodes() => NodesBST.InOrderTraversal().Cast<CityNode>().ToList();

        // Jednoduché textové ukládání:
        // Formát: sekce #NODES, #EDGES, #BLOCKED; oddělovač ';'
        public void SaveToTextFile(string path, HashSet<(string, string)> blockedEdges)
        {
            var lines = new List<string>();
            lines.Add("#NODES");
            foreach (var n in GetAllNodes())
            {
                lines.Add($"{n.Key};{n.Data.X.ToString(CultureInfo.InvariantCulture)};{n.Data.Y.ToString(CultureInfo.InvariantCulture)}");
            }

            lines.Add("#EDGES");
            foreach (var n in GetAllNodes())
            {
                foreach (var e in n.Edges)
                {
                    if (string.Compare(n.Key, e.TargetKey) < 0)
                    {
                        lines.Add($"{n.Key};{e.TargetKey};{e.Data};{e.Weight.ToString(CultureInfo.InvariantCulture)}");
                    }
                }
            }

            lines.Add("#BLOCKED");
            if (blockedEdges != null)
            {
                foreach (var p in blockedEdges.Where(p => string.Compare(p.Item1, p.Item2) < 0))
                {
                    lines.Add($"{p.Item1};{p.Item2}");
                }
            }

            File.WriteAllLines(path, lines);
        }

        public static RoadNetwork LoadFromTextFile(string path, out HashSet<(string, string)> blockedEdges)
        {
            blockedEdges = new HashSet<(string, string)>();
            var rn = new RoadNetwork();

            var allLines = File.ReadAllLines(path);
            string section = null;

            foreach (var raw in allLines)
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line.StartsWith("#"))
                {
                    section = line.ToUpperInvariant();
                    continue;
                }

                if (section == "#NODES")
                {
                    var parts = line.Split(';');
                    if (parts.Length >= 3)
                    {
                        var key = parts[0];
                        if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                            double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                        {
                            rn.AddNode(key, new Point(x, y));
                        }
                    }
                }
                else if (section == "#EDGES")
                {
                    var parts = line.Split(';');
                    if (parts.Length >= 4)
                    {
                        var s = parts[0];
                        var t = parts[1];
                        var data = parts[2];
                        if (double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double w))
                        {
                            rn.AddEdge(s, t, data, w);
                        }
                    }
                }
                else if (section == "#BLOCKED")
                {
                    var parts = line.Split(';');
                    if (parts.Length >= 2)
                    {
                        var a = parts[0];
                        var b = parts[1];
                        blockedEdges.Add((a, b));
                        blockedEdges.Add((b, a));
                    }
                }
            }

            return rn;
        }
    }
}