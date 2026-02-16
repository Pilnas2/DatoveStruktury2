using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace DopravniSit
{
    public partial class MainWindow : Window
    {
        private RoadNetwork network = new RoadNetwork();
        private HashSet<(string, string)> blockedEdges = new HashSet<(string, string)>(); // Množina neprůjezdných hran [cite: 67, 72]
        private (string, string)? selectedEdge = null; // Aktuálně vybraná hrana kliknutím

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
            // Příklad dat dle Obr. 1 v PDF [cite: 25] + doplnění do 20 uzlů
            network.AddNode("z", new Point(50, 50));
            network.AddNode("k", new Point(100, 80));
            network.AddNode("s", new Point(250, 80));
            network.AddNode("a", new Point(150, 180));
            network.AddNode("x", new Point(250, 220));
            network.AddNode("i", new Point(350, 150));
            network.AddNode("m", new Point(450, 250));
            network.AddNode("g", new Point(400, 300));
            network.AddNode("u", new Point(100, 350));
            network.AddNode("t", new Point(450, 500));
            network.AddNode("n", new Point(350, 480));
            network.AddNode("p", new Point(250, 450));
            network.AddNode("w", new Point(200, 500));
            network.AddNode("r", new Point(300, 400));
            network.AddNode("f", new Point(400, 420));

            // Hrany a váhy (čas) [cite: 64]
            network.AddEdge("z", "k", "E1", 5);
            network.AddEdge("k", "s", "E2", 10);
            network.AddEdge("k", "a", "E3", 8);
            network.AddEdge("s", "i", "E4", 6);
            network.AddEdge("s", "a", "E5", 12); // Příklad
            network.AddEdge("a", "x", "E6", 4);
            network.AddEdge("i", "x", "E7", 7);
            network.AddEdge("x", "m", "E8", 9);
            network.AddEdge("x", "g", "E9", 5);
            network.AddEdge("x", "u", "E10", 11);
            network.AddEdge("u", "g", "E11", 15);
            network.AddEdge("m", "g", "E12", 3);
            network.AddEdge("g", "t", "E13", 10);
            network.AddEdge("t", "n", "E14", 4);
            network.AddEdge("n", "p", "E15", 6);
            network.AddEdge("p", "w", "E16", 5);
            // ... další hrany pro splnění 30 hran[cite: 76]...
        }

        private void PopulateCombos()
        {
            var nodes = network.GetAllNodes().OrderBy(n => n.Key).Select(n => n.Key).ToList();
            cbStart.ItemsSource = nodes;
            cbEnd.ItemsSource = nodes;
        }

        // Naplní cbBlockedEdges seznamem všech (neorientovaných) hran
        private void PopulateBlockedEdgesCombo()
        {
            cbBlockedEdges.Items.Clear();

            var nodes = network.GetAllNodes();
            foreach (var node in nodes)
            {
                foreach (var edge in node.Edges)
                {
                    // kreslíme / uvádíme každou hranu jen jednou (neorientovaný)
                    if (string.Compare(node.Key, edge.TargetKey) < 0)
                    {
                        bool isBlocked = blockedEdges.Contains((node.Key, edge.TargetKey)) ||
                                         blockedEdges.Contains((edge.TargetKey, node.Key));

                        string display = $"{node.Key} - {edge.TargetKey} ({edge.Data})";
                        if (isBlocked) display += " [BLOKOVÁNO]";

                        var item = new ComboBoxItem
                        {
                            Content = display,
                            Tag = (node.Key, edge.TargetKey) // uložíme pár do Tagu
                        };

                        // Pokud je tato hrana aktuálně vybraná kliknutím, nastavíme selekci v ComboBoxu
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

        // Vykreslení grafu na Canvas
        private void DrawGraph(List<string> highlightPath = null)
        {
            graphCanvas.Children.Clear();
            var nodes = network.GetAllNodes();

            // 1. Vykreslit hrany
            foreach (var node in nodes)
            {
                foreach (var edge in node.Edges)
                {
                    // Abychom nekreslili hranu dvakrát (neorientovaný), kreslíme jen když Key < TargetKey
                    if (string.Compare(node.Key, edge.TargetKey) < 0)
                    {
                        var targetNode = network.GetNode(edge.TargetKey);
                        if (targetNode == null) continue;

                        bool isBlocked = blockedEdges.Contains((node.Key, edge.TargetKey)) ||
                                         blockedEdges.Contains((edge.TargetKey, node.Key));

                        bool isPath = false;
                        if (highlightPath != null)
                        {
                            // Zjištění, zda je hrana součástí cesty
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

                        // priority: blocked -> vydatně označeno; otherwise selected -> oranžová; otherwise path/normal
                        Brush strokeBrush;
                        double thickness;
                        DoubleCollection dash = null;

                        if (isBlocked)
                        {
                            strokeBrush = Brushes.Red;
                            thickness = 2;
                            dash = new DoubleCollection() { 2 };
                        }
                        else if (isSelected)
                        {
                            strokeBrush = Brushes.Orange;
                            thickness = 4;
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

                        Line line = new Line
                        {
                            X1 = node.Data.X,
                            Y1 = node.Data.Y,
                            X2 = targetNode.Data.X,
                            Y2 = targetNode.Data.Y,
                            Stroke = strokeBrush,
                            StrokeThickness = thickness,
                            StrokeDashArray = dash,
                            Tag = (node.Key, edge.TargetKey) // uložíme identitu hrany pro kliknutí
                        };
                        graphCanvas.Children.Add(line);

                        // Label váhy
                        TextBlock txt = new TextBlock { Text = edge.Weight.ToString(), Foreground = Brushes.Black, FontSize = 10, Background = Brushes.White };
                        Canvas.SetLeft(txt, (node.Data.X + targetNode.Data.X) / 2);
                        Canvas.SetTop(txt, (node.Data.Y + targetNode.Data.Y) / 2);
                        graphCanvas.Children.Add(txt);
                    }
                }
            }

            // 2. Vykreslit uzly
            foreach (var node in nodes)
            {
                Ellipse el = new Ellipse { Width = 10, Height = 10, Stroke = Brushes.Black, StrokeThickness = 1 };
                Canvas.SetLeft(el, node.Data.X - 5);
                Canvas.SetTop(el, node.Data.Y - 5);
                graphCanvas.Children.Add(el);

                TextBlock txt = new TextBlock { Text = node.Key, Foreground = Brushes.Black, FontWeight = FontWeights.Bold };
                Canvas.SetLeft(txt, node.Data.X - 15);
                Canvas.SetTop(txt, node.Data.Y - 10);
                graphCanvas.Children.Add(txt);
            }
        }

        // Tlačítko výpočtu
        private void BtnCalc_Click(object sender, RoutedEventArgs e)
        {
            if (cbStart.SelectedItem == null || cbEnd.SelectedItem == null) return;
            string start = cbStart.SelectedItem.ToString();
            string end = cbEnd.SelectedItem.ToString();

            // Výpočet Dijkstry s ohledem na blokované hrany [cite: 65, 67]
            var predecessors = network.Dijkstra(start, end, blockedEdges, out var dists);

            // Rekonstrukce cesty
            List<string> path = new List<string>();
            string curr = end;

            // Pokud cesta existuje
            if (predecessors.ContainsKey(curr) || curr == start)
            {
                while (curr != null)
                {
                    path.Add(curr);
                    if (curr == start) break;
                    if (!predecessors.ContainsKey(curr)) { path.Clear(); break; } // Nedosažitelné
                    curr = predecessors[curr];
                }
                path.Reverse();
            }

            // Zobrazíme výsledek v dialogu (ListBox byl odstraněn)
            if (path.Count > 0)
            {
                string length = dists != null && dists.ContainsKey(end) ? dists[end].ToString(CultureInfo.InvariantCulture) : "N/A";
                MessageBox.Show("Cesta: " + string.Join(" -> ", path) + "\nCelková váha: " + length, "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
                DrawGraph(path); // Zvýraznění cesty
            }
            else
            {
                MessageBox.Show("Cesta neexistuje", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Warning);
                DrawGraph(null);
            }
        }

        // Přidání "problematické" hrany (alternativní trasy) [cite: 7, 72]
        private void BtnBlock_Click(object sender, RoutedEventArgs e)
        {
            if (cbBlockedEdges.SelectedItem == null) return;

            if (cbBlockedEdges.SelectedItem is ComboBoxItem item && item.Tag is ValueTuple<string, string> pair)
            {
                var (s, t) = pair;
                // Přidáme obě orientace, aby bylo hledání/označení jednoduché
                blockedEdges.Add((s, t));
                blockedEdges.Add((t, s));

                // Aktualizovat seznam a překreslit graf
                PopulateBlockedEdgesCombo();
                DrawGraph();
            }
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
            // Najdi nejbližší hranu k bodu kliknutí (prahová vzdálenost)
            var pos = e.GetPosition(graphCanvas);
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

            // Pokud je nejbližší hrana dost blízko (např. do 8 px), označíme ji
            const double threshold = 8.0;
            if (bestEdge.HasValue && bestDist <= threshold)
            {
                // přepnutí výběru: pokud klikneme na již vybranou hranu -> zrušíme výběr
                if (selectedEdge.HasValue && selectedEdge.Value == bestEdge.Value)
                    selectedEdge = null;
                else
                    selectedEdge = bestEdge;

                // Zaktualizujeme ComboBox (označí vybranou položku) a překreslíme
                PopulateBlockedEdgesCombo();
                DrawGraph();
            }
        }

        // Výpočet vzdálenosti bodu od úsečky (v pixelech)
        private static double DistancePointToSegment(Point p, Point a, Point b)
        {
            // pokud a == b -> vzdálenost k bodu
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            if (dx == 0 && dy == 0)
                return (p - a).Length;

            // projekce bodu p na přímku ab, parametr t v [0,1]
            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy);
            if (t < 0) t = 0;
            else if (t > 1) t = 1;

            var proj = new Point(a.X + t * dx, a.Y + t * dy);
            return (p - proj).Length;
        }

        // --- Uložit / Načíst do textového souboru (volá RoadNetwork) ---

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

        // --- Nová metoda: přidání uzlu z GUI ---
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

            // Vyčistit pole
            tbNewKey.Text = "";
            tbNewX.Text = "";
            tbNewY.Text = "";
        }
    }
}