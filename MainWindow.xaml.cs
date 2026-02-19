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

            // Nově: naplnit ComboBoxy pro přidávání hran
            if (cbEdgeSource != null) cbEdgeSource.ItemsSource = nodes;
            if (cbEdgeTarget != null) cbEdgeTarget.ItemsSource = nodes;
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
        // Nově: lze zvýraznit uzel nebo hranu pomocí highlightNode / highlightEdge
        private void DrawGraph(List<string> highlightPath = null, string highlightNode = null, (string, string)? highlightEdge = null)
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

                        bool isSearchHighlight = false;
                        if (highlightEdge.HasValue)
                        {
                            var (hs, ht) = highlightEdge.Value;
                            if ((hs == node.Key && ht == edge.TargetKey) || (hs == edge.TargetKey && ht == node.Key))
                                isSearchHighlight = true;
                        }

                        // priority: blocked -> výrazné; otherwise search highlight -> zelená; otherwise selected -> oranžová; otherwise path/normal
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

        // Odebrání hrany z grafu (volané tlačítkem)
        private void BtnRemoveEdge_Click(object sender, RoutedEventArgs e)
        {
            (string, string)? pair = null;

            // Preferujeme právě vybranou hranu kliknutím na canvas
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

            // Odebereme hranu z modelu grafu
            network.RemoveEdge(s, t);

            // Odebereme případné blokace spojené s touto hranou
            blockedEdges.Remove((s, t));
            blockedEdges.Remove((t, s));

            // Zrušíme výběr pokud byly vybrány
            if (selectedEdge.HasValue && (selectedEdge.Value == (s, t) || selectedEdge.Value == (t, s)))
                selectedEdge = null;

            // Aktualizovat GUI
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
            // Získat pozici kliknutí na canvas
            var pos = e.GetPosition(graphCanvas);

            // --- Nově: doplnit souřadnice do GUI pro přidání uzlu ---
            // Používáme invariantní formát, aby parsing v BtnAddNode_Click fungoval se stejným formátem
            tbNewX.Text = pos.X.ToString(CultureInfo.InvariantCulture);
            tbNewY.Text = pos.Y.ToString(CultureInfo.InvariantCulture);
            // (Volitelně) přesun focusu do klíče uzlu, aby uživatel mohl ihned zadat jméno
            tbNewKey.Focus();

            // Najdi nejbližší hranu k bodu kliknutí (prahová vzdálenost)
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

        // --- Nová metoda: přidání hrany z GUI ---
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

            // Přidání hrany do sítě (RoadNetwork implementuje neorientovaný přístup)
            try
            {
                network.AddEdge(source, target, edgeName, weight);
            }
            catch
            {
                MessageBox.Show("Při přidávání hrany došlo k chybě.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Aktualizovat GUI
            PopulateCombos();
            PopulateBlockedEdgesCombo();
            DrawGraph();

            // Vyčistit vstupy
            tbEdgeName.Text = "";
            tbEdgeWeight.Text = "";
            cbEdgeSource.SelectedItem = null;
            cbEdgeTarget.SelectedItem = null;
        }

        // --- Nové: vyhledávání uzlu ---
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

            // Vybereme v comboboxech a zvýrazníme na mapě
            if (cbStart.ItemsSource != null && cbStart.Items.Contains(key)) cbStart.SelectedItem = key;
            DrawGraph(null, key, null);
        }

        // --- Nové: vyhledávání hrany ---
        private void BtnSearchEdge_Click(object sender, RoutedEventArgs e)
        {
            string query = tbSearchEdge.Text?.Trim();
            if (string.IsNullOrEmpty(query))
            {
                MessageBox.Show("Zadejte název hrany nebo formát zdroj-cíl (např. a-b).", "Informace", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Pokud je zadáno ve formátu "s-t", zkusíme přímo podle konců
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

            // Jinak hledáme podle názvu hrany (edge.Data)
            foreach (var n in network.GetAllNodes())
            {
                foreach (var edge in n.Edges)
                {
                    if (edge.Data != null && edge.Data.ToString().Equals(query, System.StringComparison.InvariantCultureIgnoreCase))
                    {
                        DrawGraph(null, null, (n.Key, edge.TargetKey));
                        MessageBox.Show($"Hrana '{query}' nalezena mezi {n.Key} - {edge.TargetKey}. Hodnota je: {edge.Weight.ToString()}", "Výsledek vyhledávání", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
            }

            MessageBox.Show("Hrana nenalezena.", "Výsledek vyhledávání", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // --- Nové: načíst vybranou hranu do polí pro editaci ---
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

            // Naplnit editovací políčka
            tbEditSource.Text = s;
            tbEditTarget.Text = t;
            tbEditEdgeName.Text = edge.Data?.ToString() ?? "";
            tbEditEdgeWeight.Text = edge.Weight.ToString(CultureInfo.InvariantCulture);
            tbEditEdgeName.Focus();
        }

        // --- Nové: aplikovat úpravy na hranu ---
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

            // Najdeme a upravíme obě orientace hrany
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

            // Aktualizovat GUI a vyčistit editovací pole
            PopulateCombos();
            PopulateBlockedEdgesCombo();
            DrawGraph();

            tbEditSource.Text = "";
            tbEditTarget.Text = "";
            tbEditEdgeName.Text = "";
            tbEditEdgeWeight.Text = "";

            MessageBox.Show("Hrana byla upravena.", "Hotovo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}