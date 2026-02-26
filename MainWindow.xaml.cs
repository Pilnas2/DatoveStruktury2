using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace DopravniSit
{
    public partial class MainWindow : Window
    {
        private RoadNetwork network = new RoadNetwork();
        private HashSet<(string, string)> blockedEdges = new HashSet<(string, string)>();
        private (string, string)? selectedEdge = null;

        public MainWindow()
        {
            InitializeComponent();
            InitializeGraphData();
            DrawGraph();
            PopulateCombos();
            PopulateBlockedEdgesCombo();
        }

        private void InitializeGraphData()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.CurrentDirectory, "inputGraph.txt")
            };

            foreach (var path in candidates)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var loaded = RoadNetwork.LoadFromTextFile(path, out var loadedBlocked);
                        if (loaded != null)
                        {
                            network = loaded;
                            blockedEdges = loadedBlocked ?? new HashSet<(string, string)>();
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Chyba při načítání souboru '{path}': {ex.Message}");
                    break;
                }
            }
        }

        private void PopulateCombos()
        {
            var nodes = network.GetAllNodes().OrderBy(n => n.Key).Select(n => n.Key).ToList();
            cbStart.ItemsSource = nodes;
            cbEnd.ItemsSource = nodes;

            if (cbEdgeSource != null) cbEdgeSource.ItemsSource = nodes;
            if (cbEdgeTarget != null) cbEdgeTarget.ItemsSource = nodes;
        }

        private void PopulateBlockedEdgesCombo()
        {
            cbBlockedEdges.Items.Clear();

            var nodes = network.GetAllNodes();
            foreach (var node in nodes)
            {
                foreach (var edge in node.Edges)
                {
                    if (string.Compare(node.Key, edge.TargetKey) < 0)
                    {
                        bool isBlocked = blockedEdges.Contains((node.Key, edge.TargetKey)) ||
                                         blockedEdges.Contains((edge.TargetKey, node.Key));

                        string display = $"{node.Key} - {edge.TargetKey} ({edge.Data})";
                        if (isBlocked) display += " [BLOKOVÁNO]";

                        var item = new ComboBoxItem
                        {
                            Content = display,
                            Tag = (node.Key, edge.TargetKey)
                        };

                        if (selectedEdge.HasValue)
                        {
                            var (s, t) = selectedEdge.Value;
                            if ((s == node.Key && t == edge.TargetKey) || (s == edge.TargetKey && t == node.Key))
                                item.IsSelected = true;
                        }

                        cbBlockedEdges.Items.Add(item);
                    }
                }
            }
        }

        private void DrawGraph(List<string> highlightPath = null, string highlightNode = null, (string, string)? highlightEdge = null, List<List<string>> multiPaths = null)
        {
            graphCanvas.Children.Clear();
            var nodes = network.GetAllNodes();

            Dictionary<(string, string), int> edgeToPathIndex = new();
            if (multiPaths != null)
            {
                for (int pi = 0; pi < multiPaths.Count; pi++)
                {
                    var p = multiPaths[pi];
                    for (int i = 0; i < p.Count - 1; i++)
                    {
                        var a = p[i];
                        var b = p[i + 1];
                        var key = string.Compare(a, b) < 0 ? (a, b) : (b, a);
                        if (!edgeToPathIndex.ContainsKey(key))
                            edgeToPathIndex[key] = pi;
                    }
                }
            }

            Brush[] altBrushes = new Brush[] { Brushes.Blue, Brushes.Green, Brushes.Purple, Brushes.Orange, Brushes.Brown };

            foreach (var node in nodes)
            {
                foreach (var edge in node.Edges)
                {
                    if (string.Compare(node.Key, edge.TargetKey) < 0)
                    {
                        var targetNode = network.GetNode(edge.TargetKey);
                        if (targetNode == null) continue;

                        bool isBlocked = blockedEdges.Contains((node.Key, edge.TargetKey)) ||
                                         blockedEdges.Contains((edge.TargetKey, node.Key));

                        bool isPath = false;
                        if (highlightPath != null)
                        {
                            for (int i = 0; i < highlightPath.Count - 1; i++)
                            {
                                if ((highlightPath[i] == node.Key && highlightPath[i + 1] == edge.TargetKey) ||
                                    (highlightPath[i] == edge.TargetKey && highlightPath[i + 1] == node.Key))
                                {
                                    isPath = true; break;
                                }
                            }
                        }

                        bool isSelected = false;
                        if (selectedEdge.HasValue)
                        {
                            var (s, t) = selectedEdge.Value;
                            if ((s == node.Key && t == edge.TargetKey) || (s == edge.TargetKey && t == node.Key))
                                isSelected = true;
                        }

                        bool isSearchHighlight = false;
                        if (highlightEdge.HasValue)
                        {
                            var (hs, ht) = highlightEdge.Value;
                            if ((hs == node.Key && ht == edge.TargetKey) || (hs == edge.TargetKey && ht == node.Key))
                                isSearchHighlight = true;
                        }

                        Brush strokeBrush;
                        double thickness;
                        DoubleCollection dash = null;

                        if (isBlocked)
                        {
                            strokeBrush = Brushes.Red;
                            thickness = 2;
                            dash = new DoubleCollection() { 2 };
                        }
                        else if (isSearchHighlight)
                        {
                            strokeBrush = Brushes.Green;
                            thickness = 4;
                        }
                        else if (isSelected)
                        {
                            strokeBrush = Brushes.Orange;
                            thickness = 4;
                        }
                        else
                        {
                            var normKey = string.Compare(node.Key, edge.TargetKey) < 0 ? (node.Key, edge.TargetKey) : (edge.TargetKey, node.Key);
                            if (edgeToPathIndex.TryGetValue(normKey, out int pathIndex))
                            {
                                strokeBrush = altBrushes[pathIndex % altBrushes.Length];
                                thickness = 3;
                            }
                            else if (isPath)
                            {
                                strokeBrush = Brushes.Blue;
                                thickness = 3;
                            }
                            else
                            {
                                strokeBrush = Brushes.Gray;
                                thickness = 1;
                            }
                        }

                        Line line = new Line
                        {
                            X1 = node.Data.X,
                            Y1 = node.Data.Y,
                            X2 = targetNode.Data.X,
                            Y2 = targetNode.Data.Y,
                            Stroke = strokeBrush,
                            StrokeThickness = thickness,
                            StrokeDashArray = dash,
                            Tag = (node.Key, edge.TargetKey)
                        };
                        graphCanvas.Children.Add(line);

                        TextBlock txt = new TextBlock { Text = edge.Weight.ToString(), Foreground = Brushes.Black, FontSize = 10, Background = Brushes.White };
                        Canvas.SetLeft(txt, (node.Data.X + targetNode.Data.X) / 2);
                        Canvas.SetTop(txt, (node.Data.Y + targetNode.Data.Y) / 2);
                        graphCanvas.Children.Add(txt);
                    }
                }
            }

            foreach (var node in nodes)
            {
                bool isHighlighted = highlightNode != null && node.Key == highlightNode;

                Ellipse el = new Ellipse
                {
                    Width = isHighlighted ? 16 : 10,
                    Height = isHighlighted ? 16 : 10,
                    Stroke = Brushes.Black,
                    StrokeThickness = isHighlighted ? 2 : 1,
                    Fill = isHighlighted ? Brushes.Yellow : Brushes.Transparent
                };
                Canvas.SetLeft(el, node.Data.X - (el.Width / 2));
                Canvas.SetTop(el, node.Data.Y - (el.Height / 2));
                graphCanvas.Children.Add(el);

                TextBlock txt = new TextBlock { Text = node.Key, Foreground = Brushes.Black, FontWeight = FontWeights.Bold };
                Canvas.SetLeft(txt, node.Data.X - 10);
                Canvas.SetTop(txt, node.Data.Y - 20);
                graphCanvas.Children.Add(txt);
            }
        }

        private void BtnCalc_Click(object sender, RoutedEventArgs e)
        {
            if (cbStart.SelectedItem == null || cbEnd.SelectedItem == null) return;
            string start = cbStart.SelectedItem.ToString();
            string end = cbEnd.SelectedItem.ToString();

            var predecessors = network.Dijkstra(start, end, blockedEdges, out var dists);

            List<string> path = new List<string>();
            string curr = end;

            if (predecessors.ContainsKey(curr) || curr == start)
            {
                while (curr != null)
                {
                    path.Add(curr);
                    if (curr == start) break;
                    if (!predecessors.ContainsKey(curr)) { path.Clear(); break; }
                    curr = predecessors[curr];
                }
                path.Reverse();
            }

            if (path.Count > 0)
            {
                string length = dists != null && dists.ContainsKey(end) ? dists[end].ToString(CultureInfo.InvariantCulture) : "N/A";
                MessageBox.Show("Cesta: " + string.Join(" -> ", path) + "\nCelková váha: " + length, "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
                DrawGraph(path);
            }
            else
            {
                MessageBox.Show("Cesta neexistuje", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Warning);
                DrawGraph(null);
            }
        }

        private void BtnBlock_Click(object sender, RoutedEventArgs e)
        {
            if (cbBlockedEdges.SelectedItem == null) return;

            if (cbBlockedEdges.SelectedItem is ComboBoxItem item && item.Tag is ValueTuple<string, string> pair)
            {
                var (s, t) = pair;
                blockedEdges.Add((s, t));
                blockedEdges.Add((t, s));

                PopulateBlockedEdgesCombo();
                DrawGraph();
            }
        }

        private void BtnRemoveEdge_Click(object sender, RoutedEventArgs e)
        {
            (string, string)? pair = null;

            if (selectedEdge.HasValue)
                pair = selectedEdge.Value;
            else if (cbBlockedEdges.SelectedItem is ComboBoxItem item && item.Tag is ValueTuple<string, string> tag)
                pair = tag;
            else
            {
                MessageBox.Show("Vyberte hranu kliknutím do mapy nebo v seznamu hran.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (s, t) = pair.Value;

            network.RemoveEdge(s, t);

            blockedEdges.Remove((s, t));
            blockedEdges.Remove((t, s));

            if (selectedEdge.HasValue && (selectedEdge.Value == (s, t) || selectedEdge.Value == (t, s)))
                selectedEdge = null;

            PopulateCombos();
            PopulateBlockedEdgesCombo();
            DrawGraph();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            blockedEdges.Clear();
            selectedEdge = null;
            PopulateBlockedEdgesCombo();
            DrawGraph();
        }

        private void Canvas_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(graphCanvas);

            tbNewX.Text = pos.X.ToString(CultureInfo.InvariantCulture);
            tbNewY.Text = pos.Y.ToString(CultureInfo.InvariantCulture);
            tbNewKey.Focus();

            double bestDist = double.MaxValue;
            (string, string)? bestEdge = null;

            foreach (var child in graphCanvas.Children)
            {
                if (child is Line line && line.Tag is ValueTuple<string, string> tag)
                {
                    var p1 = new Point(line.X1, line.Y1);
                    var p2 = new Point(line.X2, line.Y2);
                    double dist = DistancePointToSegment(pos, p1, p2);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestEdge = tag;
                    }
                }
            }

            const double threshold = 8.0;
            if (bestEdge.HasValue && bestDist <= threshold)
            {
                if (selectedEdge.HasValue && selectedEdge.Value == bestEdge.Value)
                    selectedEdge = null;
                else
                    selectedEdge = bestEdge;

                PopulateBlockedEdgesCombo();
                DrawGraph();
            }
        }

        private static double DistancePointToSegment(Point p, Point a, Point b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            if (dx == 0 && dy == 0)
                return (p - a).Length;

            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy);
            if (t < 0) t = 0;
            else if (t > 1) t = 1;

            var proj = new Point(a.X + t * dx, a.Y + t * dy);
            return (p - proj).Length;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Textový soubor (*.txt)|*.txt|Všechny soubory (*.*)|*.*",
                FileName = "graph.txt"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                network.SaveToTextFile(dlg.FileName, blockedEdges);
            }
            catch (IOException ex)
            {
                MessageBox.Show("Chyba při ukládání souboru: " + ex.Message, "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Textový soubor (*.txt)|*.txt|Všechny soubory (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                network = RoadNetwork.LoadFromTextFile(dlg.FileName, out var loadedBlocked);
                blockedEdges = loadedBlocked ?? new HashSet<(string, string)>();
                PopulateCombos();
                PopulateBlockedEdgesCombo();
                DrawGraph();
            }
            catch (IOException ex)
            {
                MessageBox.Show("Chyba při načítání souboru: " + ex.Message, "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAddNode_Click(object sender, RoutedEventArgs e)
        {
            string key = tbNewKey.Text?.Trim();
            if (string.IsNullOrEmpty(key))
            {
                MessageBox.Show("Zadejte klíč uzlu.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (network.GetNode(key) != null)
            {
                MessageBox.Show("Uzel s tímto klíčem již existuje.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(tbNewX.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ||
                !double.TryParse(tbNewY.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
            {
                MessageBox.Show("Souřadnice X a Y musí být čísla (např. 123.45).", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            network.AddNode(key, new Point(x, y));
            PopulateCombos();
            PopulateBlockedEdgesCombo();
            DrawGraph();

            tbNewKey.Text = "";
            tbNewX.Text = "";
            tbNewY.Text = "";
        }

        private void BtnAddEdge_Click(object sender, RoutedEventArgs e)
        {
            if (cbEdgeSource.SelectedItem == null || cbEdgeTarget.SelectedItem == null)
            {
                MessageBox.Show("Vyberte zdroj a cílový uzel.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string source = cbEdgeSource.SelectedItem.ToString();
            string target = cbEdgeTarget.SelectedItem.ToString();

            if (source == target)
            {
                MessageBox.Show("Zdroj a cíl nesmí být stejné.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string edgeName = tbEdgeName.Text?.Trim();
            if (string.IsNullOrEmpty(edgeName))
            {
                MessageBox.Show("Zadejte název hrany.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(tbEdgeWeight.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double weight))
            {
                MessageBox.Show("Váha musí být číslo (např. 5 nebo 3.5).", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                network.AddEdge(source, target, edgeName, weight);
            }
            catch
            {
                MessageBox.Show("Při přidávání hrany došlo k chybě.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            PopulateCombos();
            PopulateBlockedEdgesCombo();
            DrawGraph();

            tbEdgeName.Text = "";
            tbEdgeWeight.Text = "";
            cbEdgeSource.SelectedItem = null;
            cbEdgeTarget.SelectedItem = null;
        }

        private void BtnSearchNode_Click(object sender, RoutedEventArgs e)
        {
            string key = tbSearchNode.Text?.Trim();
            if (string.IsNullOrEmpty(key))
            {
                MessageBox.Show("Zadejte klíč uzlu pro vyhledání.", "Informace", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var node = network.GetNode(key);
            if (node == null)
            {
                MessageBox.Show($"Uzel '{key}' nebyl nalezen.", "Výsledek vyhledávání", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (cbStart.ItemsSource != null && cbStart.Items.Contains(key)) cbStart.SelectedItem = key;
            DrawGraph(null, key, null);
        }

        private void BtnSearchEdge_Click(object sender, RoutedEventArgs e)
        {
            string query = tbSearchEdge.Text?.Trim();
            if (string.IsNullOrEmpty(query))
            {
                MessageBox.Show("Zadejte název hrany nebo formát zdroj-cíl (např. a-b).", "Informace", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (query.Contains("-"))
            {
                var parts = query.Split('-').Select(p => p.Trim()).ToArray();
                if (parts.Length == 2)
                {
                    var s = parts[0];
                    var t = parts[1];
                    var sNode = network.GetNode(s);
                    if (sNode != null && sNode.Edges.Any(e => e.TargetKey.Equals(t)))
                    {
                        DrawGraph(null, null, (s, t));
                        MessageBox.Show($"Hrana {s} - {t} nalezena a zvýrazněna.", "Výsledek vyhledávání", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
            }

            foreach (var n in network.GetAllNodes())
            {
                foreach (var edge in n.Edges)
                {
                    if (edge.Data != null && edge.Data.ToString().Equals(query, System.StringComparison.InvariantCultureIgnoreCase))
                    {
                        DrawGraph(null, null, (n.Key, edge.TargetKey));
                        return;
                    }
                }
            }

            MessageBox.Show("Hrana nenalezena.", "Výsledek vyhledávání", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnLoadSelectedEdge_Click(object sender, RoutedEventArgs e)
        {
            (string, string)? pair = null;

            if (selectedEdge.HasValue)
                pair = selectedEdge.Value;
            else if (cbBlockedEdges.SelectedItem is ComboBoxItem item && item.Tag is ValueTuple<string, string> tag)
                pair = tag;

            if (!pair.HasValue)
            {
                MessageBox.Show("Vyberte hranu kliknutím do mapy nebo v seznamu hran.", "Informace", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var (s, t) = pair.Value;
            var sNode = network.GetNode(s);
            if (sNode == null)
            {
                MessageBox.Show("Zdrojový uzel nenalezen.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var edge = sNode.Edges.FirstOrDefault(e => e.TargetKey.Equals(t));
            if (edge == null)
            {
                MessageBox.Show("Hrana nenalezena v modelu.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            tbEditSource.Text = s;
            tbEditTarget.Text = t;
            tbEditEdgeName.Text = edge.Data?.ToString() ?? "";
            tbEditEdgeWeight.Text = edge.Weight.ToString(CultureInfo.InvariantCulture);
            tbEditEdgeName.Focus();
        }

        private void BtnEditEdge_Click(object sender, RoutedEventArgs e)
        {
            string s = tbEditSource.Text?.Trim();
            string t = tbEditTarget.Text?.Trim();

            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(t))
            {
                MessageBox.Show("Nejdříve načtěte vybranou hranu pomocí 'Načíst vybranou'.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sNode = network.GetNode(s);
            var tNode = network.GetNode(t);
            if (sNode == null || tNode == null)
            {
                MessageBox.Show("Uzly hrany nebyly nalezeny v grafu.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string newName = tbEditEdgeName.Text?.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("Název hrany nemůže být prázdný.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(tbEditEdgeWeight.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double newWeight))
            {
                MessageBox.Show("Váha musí být číslo (např. 5 nebo 3.5).", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var e1 = sNode.Edges.FirstOrDefault(ed => ed.TargetKey.Equals(t));
            var e2 = tNode.Edges.FirstOrDefault(ed => ed.TargetKey.Equals(s));

            if (e1 == null || e2 == null)
            {
                MessageBox.Show("Hrana nebyla nalezena v obou směrech.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            e1.Data = newName;
            e1.Weight = newWeight;
            e2.Data = newName;
            e2.Weight = newWeight;

            PopulateCombos();
            PopulateBlockedEdgesCombo();
            DrawGraph();

            tbEditSource.Text = "";
            tbEditTarget.Text = "";
            tbEditEdgeName.Text = "";
            tbEditEdgeWeight.Text = "";

            MessageBox.Show("Hrana byla upravena.", "Hotovo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAlternatives_Click(object sender, RoutedEventArgs e)
        {
            if (cbStart.SelectedItem == null || cbEnd.SelectedItem == null) return;
            string start = cbStart.SelectedItem.ToString();
            string end = cbEnd.SelectedItem.ToString();

            List<double> lengths;
            var paths = network.GetAlternativePaths(start, end, blockedEdges, out lengths);

            lbAlternatives.Items.Clear();
            for (int i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                string pathText = string.Join(" -> ", path);
                string lengthStr = (lengths != null && i < lengths.Count) ? "Váha:" + lengths[i].ToString(CultureInfo.InvariantCulture) : "N/A";

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var tbPath = new TextBlock { Text = pathText, TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(tbPath, 0);
                grid.Children.Add(tbPath);

                var tbLen = new TextBlock { Text = lengthStr, Margin = new Thickness(8, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(tbLen, 1);
                grid.Children.Add(tbLen);

                var item = new ListBoxItem { Content = grid, Tag = (lengths != null && i < lengths.Count) ? (object)lengths[i] : null };
                lbAlternatives.Items.Add(item);
            }

            if (paths.Count > 0)
                MessageBox.Show($"Nalezeno {paths.Count} alternativních cest.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("Nebyla nalezena žádná alternativa.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LbAlternatives_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbAlternatives.SelectedItem == null) return;

            if (lbAlternatives.SelectedItem is ListBoxItem sel)
            {
                string selectedPathText = "";

                if (sel.Content is Grid g && g.Children.Count > 0 && g.Children[0] is TextBlock tb)
                    selectedPathText = tb.Text;
                else
                    selectedPathText = sel.Content?.ToString() ?? "";

                var nodes = selectedPathText.Split(new[] { " -> " }, StringSplitOptions.RemoveEmptyEntries);

                List<string> path = new List<string>();
                foreach (var node in nodes)
                    path.Add(node.Trim());

                DrawGraph(path);
            }
        }
    }
}