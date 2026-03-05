using MakaleGrafAnaliz;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DataStructuresProject
{
    public partial class MainWindow : Window
    {
        // --- VARIABLES ---
        Dictionary<string, Point> NodePositions = new Dictionary<string, Point>();
        List<Makale> AllArticles = new List<Makale>();
        GrafIslemleri GraphManager = new GrafIslemleri();

        // For Zoom/Pan
        private bool isDragging = false;
        private Point startPoint;


        HashSet<string> ActiveNodes = new HashSet<string>();


        private string ConstantFocusId = null;
        private string GeneralAnalysisRootId = null;
        HashSet<string> GlobalKCoreSet = new HashSet<string>();

        private List<Makale> BackupArticleList = null; // Articles from the previous screen
        private bool WasKCoreMode = false;            // Was the previous screen K-Core?
        private bool IsFocusMode = false;            // Are we currently in focus mode?
        private string FocusCenterId = null;             // Who is the Focus Center?

        public MainWindow()
        {
            InitializeComponent();

            // Setup Zoom and Pan Transforms
            var transformGroup = new TransformGroup();
            var st = new ScaleTransform();
            var tt = new TranslateTransform();
            transformGroup.Children.Add(st);
            transformGroup.Children.Add(tt);
            CizimAlani.RenderTransform = transformGroup;

            // Bind zoom events
            AnaPanel.MouseWheel += AnaPanel_MouseWheel;
            AnaPanel.MouseLeftButtonDown += AnaPanel_MouseLeftButtonDown;
            AnaPanel.MouseLeftButtonUp += AnaPanel_MouseLeftButtonUp;
            AnaPanel.MouseMove += AnaPanel_MouseMove;
        }

        // --- HELPER METHODS ---
        private string ExtractId(string rawId)
        {
            if (string.IsNullOrEmpty(rawId)) return "Unknown";
            return rawId.Replace("https://openalex.org/", "").Replace("W", "").Replace("/", "");
        }

        private string GetAuthors(List<string> authors)
        {
            if (authors == null || authors.Count == 0) return "Unknown";
            if (authors.Count > 3) return string.Join(", ", authors.Take(3)) + " et al.";
            return string.Join(", ", authors);
        }

        private void ResetZoom()
        {
            var transformGroup = (TransformGroup)CizimAlani.RenderTransform;
            var st = (ScaleTransform)transformGroup.Children[0];
            var tt = (TranslateTransform)transformGroup.Children[1];

            st.ScaleX = 1.0;
            st.ScaleY = 1.0;

            // Bring the graph center (2500, 2500) to the middle of the screen
            tt.X = (AnaPanel.ActualWidth / 2) - 2500;
            tt.Y = (AnaPanel.ActualHeight / 2) - 2500;
        }

        // --- FILE LOADING ---
        private void btnDosyaYukle_Click(object sender, RoutedEventArgs e)
        {
            // Create File Selection Window
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            // Only show json files
            openFileDialog.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
            openFileDialog.Title = "Select Article Data File";

            // Open window and check if user selected something
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string filePath = openFileDialog.FileName; // Full path of the selected file

                    // JSON handling logic
                    string jsonText = File.ReadAllText(filePath);
                    MakaleAyiklayici parser = new MakaleAyiklayici();
                    AllArticles = parser.AnalyzeJson(jsonText);

                    GraphManager.BuildGraph(AllArticles);
                    GraphManager.CalculateBetweennessCentrality();

                    // Interface updates...
                    lblDugumSayisi.Text = $"Node Count: {GraphManager.TotalNodeCount}";
                    lblKenarSayisi.Text = $"Edge Count: {GraphManager.TotalCitationCount}";

                    var mostCited = GraphManager.GetMostCitedArticle();
                    if (mostCited != null)
                        lblEnCokAtifAlan.Text = $"{mostCited.ShortId}\n({mostCited.InDegree} citations)";

                    var topOutDegree = GraphManager.GetTopOutDegreeArticle();
                    if (topOutDegree != null)
                        lblEnCokAtifVeren.Text = $"{topOutDegree.ShortId}\n({topOutDegree.OutDegree} citations)";

                    MessageBox.Show($"Data Loaded Successfully!\nTotal {AllArticles.Count} articles analyzed.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("An error occurred while reading the file:\n" + ex.Message);
                }
            }
            // Same as edge count but shown separately as requested by the professor
            lblToplamVerilen.Text = $"Total Given Ref: {GraphManager.TotalCitationCount}";
            lblToplamAlinan.Text = $"Total Received Ref: {GraphManager.TotalCitationCount}";
        }

        // --- DRAWING OPERATIONS ---
        private void btnCiz_Click(object sender, RoutedEventArgs e)
        {
            // Cleanup
            GlobalKCoreSet.Clear(); // Returned to normal mode, clear K-Core memory
            ResetInterface();
            CizimAlani.Children.Clear();
            NodePositions.Clear();
            ResetZoom();
            IsFocusMode = false;
            FocusCenterId = null;
            BackupArticleList = null;

            if (AllArticles.Count == 0) return;

            // Mode Control
            bool isSpiralMode = chkSpiralMod.IsChecked == true;

            double CenterX = 2500;
            double CenterY = 2500;

            if (isSpiralMode)
            {
                // --- SPIRAL MODE (ANALYSIS) ---
                // Sort by popularity (Most citations at the center)
                var sortedList = AllArticles.OrderByDescending(m => m.InDegree).ToList();
                double c = 150; // Expansion coefficient

                for (int i = 0; i < sortedList.Count; i++)
                {
                    var article = sortedList[i];
                    double x = CenterX, y = CenterY;
                    if (i > 0)
                    {
                        double angle = i * 137.5 * (Math.PI / 180.0);
                        double r = c * Math.Sqrt(i);
                        x = CenterX + r * Math.Cos(angle);
                        y = CenterY + r * Math.Sin(angle);
                    }
                    NodePositions[article.Id] = new Point(x, y);
                }
                // Green Arrows are not drawn in spiral mode (to avoid visual clutter)
            }
            else
            {
                // --- CIRCULAR MODE ---
                // Sort by ID 
                var sortedList = AllArticles.OrderBy(m => m.Id).ToList();

                double radius = 4000; // Wide circle
                double outerRadius = 4050; // Where GREEN ARROWS will sit (Further out)
                double angleStep = (2 * Math.PI) / sortedList.Count;

                for (int i = 0; i < sortedList.Count; i++)
                {
                    var article = sortedList[i];
                    double angle = i * angleStep;
                    double x = CenterX + radius * Math.Cos(angle);
                    double y = CenterY + radius * Math.Sin(angle);
                    NodePositions[article.Id] = new Point(x, y);
                }

                // GREEN ARROWS: Connect sequential articles (i -> i+1)
                for (int i = 0; i < sortedList.Count - 1; i++)
                {
                    string id1 = sortedList[i].Id;
                    string id2 = sortedList[i + 1].Id;

                    double angle1 = i * angleStep;
                    double angle2 = (i + 1) * angleStep;

                    // START and END points of the arrow (On the outer circle)
                    Point pOuter1 = new Point(CenterX + outerRadius * Math.Cos(angle1), CenterY + outerRadius * Math.Sin(angle1));
                    Point pOuter2 = new Point(CenterX + outerRadius * Math.Cos(angle2), CenterY + outerRadius * Math.Sin(angle2));

                    // PARAMETER: true (Green arrow, no offset, exact edge-to-edge)
                    DrawArrow(pOuter1, pOuter2, id1, id2, Brushes.Green, 3, true);
                }
            }

            // --- SHARED TASKS: BLACK ARROWS AND NODES ---

            // Draw Black Arrows (Citation Relationships)
            foreach (var article in AllArticles)
            {
                if (!NodePositions.ContainsKey(article.Id)) continue;
                Point p1 = NodePositions[article.Id];

                foreach (var refId in article.ReferencedWorks)
                {
                    if (NodePositions.ContainsKey(refId))
                    {
                        Point p2 = NodePositions[refId];
                        // PARAMETER: false (Normal arrow, clipped at tip to avoid overlap)
                        DrawArrow(p1, p2, article.Id, refId, Brushes.Black, 1, false);
                    }
                }
            }

            // Draw Nodes
            foreach (var article in AllArticles)
            {
                Point p = NodePositions[article.Id];
                var visual = CreateNode(article);

                // Since radius is 12.5 (25/2)
                Canvas.SetLeft(visual, p.X - 12.5);
                Canvas.SetTop(visual, p.Y - 12.5);

                Panel.SetZIndex(visual, 100);
                CizimAlani.Children.Add(visual);
            }
        }

        private void DrawFocusedGraph(Makale centerArticle, List<Makale> neighbors)
        {
            // Cleanup
            CizimAlani.Children.Clear();
            NodePositions.Clear();
            ResetZoom();

            // Place Center Node (Exactly in the Middle)
            double CenterX = 2500; // Half of Canvas width
            double CenterY = 2500;

            NodePositions[centerArticle.Id] = new Point(CenterX, CenterY);

            // Arrange Neighbors in a Semi-Circle on the Left Side


            int n = neighbors.Count;
            if (n > 0)
            {
                double radius = 400; // How far should they be from the center?

                // Start angle (90 degrees - Bottom)
                double startAngle = Math.PI / 2;
                // End angle (270 degrees - Top)
                double endAngle = 3 * Math.PI / 2;

                // Step interval
                double step = (endAngle - startAngle) / (n + 1);

                for (int i = 0; i < n; i++)
                {
                    var neighbor = neighbors[i];

                    // Calculate angle (Arranging from top to bottom)
                    double angle = endAngle - (step * (i + 1));

                    double x = CenterX + radius * Math.Cos(angle);
                    double y = CenterY + radius * Math.Sin(angle); // Y axis increases downwards

                    NodePositions[neighbor.Id] = new Point(x, y);
                }
            }



            // Draw Arrows (Only relationships between this small subset)
            // Combine list: Center + Neighbors
            List<Makale> toDraw = new List<Makale> { centerArticle };
            toDraw.AddRange(neighbors);

            // Set for fast access
            HashSet<string> drawIdSet = new HashSet<string>(toDraw.Select(m => m.Id));

            foreach (var m in toDraw)
            {
                Point p1 = NodePositions[m.Id];
                foreach (var refId in m.ReferencedWorks)
                {
                    if (drawIdSet.Contains(refId)) // Only draw arrows between those on screen
                    {
                        Point p2 = NodePositions[refId];

                        // If you want to preserve K-Core colors, we can add that check here
                        // For now we draw standard, the Highlight method will handle the colors.
                        bool isKCore = GlobalKCoreSet.Contains(m.Id) && GlobalKCoreSet.Contains(refId);

                        Brush color = isKCore ? Brushes.DarkBlue : Brushes.Black;
                        double thickness = isKCore ? 2 : 1;

                        // Call DrawArrow with 'false' (Not a green arrow)
                        DrawArrow(p1, p2, m.Id, refId, color, thickness, false);
                    }
                }
            }

            // 5. Display Nodes
            foreach (var m in toDraw)
            {
                Point p = NodePositions[m.Id];
                var visual = CreateNode(m); // Use existing method

                Canvas.SetLeft(visual, p.X - 12.5);
                Canvas.SetTop(visual, p.Y - 12.5);

                // Let the center's Z-Index be high
                int z = (m.Id == centerArticle.Id) ? 300 : 100;
                Panel.SetZIndex(visual, z);

                CizimAlani.Children.Add(visual);
            }
        }

        private void ExpandSubNodes(Makale clickedArticle)
        {
            // Find New Neighbors (H-Core)
            GraphManager.CalculateHIndex(clickedArticle.Id, out List<Makale> newNeighbors);

            // Filter out those already on screen (don't overlap)
            List<Makale> toAdd = new List<Makale>();
            foreach (var m in newNeighbors)
            {
                if (!NodePositions.ContainsKey(m.Id))
                {
                    toAdd.Add(m);
                    ActiveNodes.Add(m.Id); // Add to visible list
                }
            }

            if (toAdd.Count == 0) return; // Exit if no one to add

            // Find the angle of the clicked node relative to the center (2500,2500) and add in that direction.

            Point clickedPos = NodePositions[clickedArticle.Id];
            double middleX = 2500;
            double middleY = 2500;

            // Angle of the clicked node relative to center
            double mainAngle = Math.Atan2(clickedPos.Y - middleY, clickedPos.X - middleX);

            double distance = 300; // How far should the new layer be?
            double spreadAngle = Math.PI / 4; // Spread in a 45-degree fan

            // Start angle
            double startAngle = mainAngle - (spreadAngle / 2);
            double step = spreadAngle / (toAdd.Count + 1);

            for (int i = 0; i < toAdd.Count; i++)
            {
                var m = toAdd[i];

                // Position by spreading slightly left and right
                double angle = startAngle + (step * (i + 1));

                // Calculate starting from the position of the clicked node
                double x = clickedPos.X + distance * Math.Cos(angle);
                double y = clickedPos.Y + distance * Math.Sin(angle);

                NodePositions[m.Id] = new Point(x, y);
            }

            // Draw New Arrows and Nodes

            // Arrows (Between new nodes and clicked node only)
            foreach (var m in toAdd)
            {
                // Between Clicked and New Child / or between New Child and Clicked (If relationship exists)

                bool isKCore = GlobalKCoreSet.Contains(clickedArticle.Id) && GlobalKCoreSet.Contains(m.Id);
                Brush color = isKCore ? Brushes.DarkBlue : Brushes.Black;
                double thickness = isKCore ? 2 : 1;

                // Clicked -> Child (If exists)
                if (clickedArticle.ReferencedWorks.Contains(m.Id))
                    DrawArrow(clickedPos, NodePositions[m.Id], clickedArticle.Id, m.Id, color, thickness, false);

                // Child -> Clicked (If exists)
                if (m.ReferencedWorks.Contains(clickedArticle.Id))
                    DrawArrow(NodePositions[m.Id], clickedPos, m.Id, clickedArticle.Id, color, thickness, false);
            }

            // Nodes
            foreach (var m in toAdd)
            {
                Point p = NodePositions[m.Id];
                var visual = CreateNode(m);

                Canvas.SetLeft(visual, p.X - 12.5); // Radius offset
                Canvas.SetTop(visual, p.Y - 12.5);
                Panel.SetZIndex(visual, 150);

                CizimAlani.Children.Add(visual);
            }
        }


        private void DrawArrow(Point p1, Point p2, string sourceId, string targetId, Brush color, double thickness, bool isGreenArrow)
        {
            // Node radius 12.5
            double nodeRadius = 12.5;

            // Calculate angle
            double angle = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);

            double startX = p1.X;
            double startY = p1.Y;
            double endX = p2.X;
            double endY = p2.Y;

            // --- GREEN ARROW DIFFERENTIATION ---
            if (isGreenArrow)
            {
                // GREEN ARROW: Do not perform any offset (shifting).
                // Start from P1, end at P2. This way arrows connect.
                startX = p1.X;
                startY = p1.Y;
                endX = p2.X;
                endY = p2.Y;
            }
            else
            {
                // BLACK ARROW: Start/end at the edge of the node, not the center
                startX = p1.X + (nodeRadius * Math.Cos(angle));
                startY = p1.Y + (nodeRadius * Math.Sin(angle));
                endX = p2.X - (nodeRadius * Math.Cos(angle));
                endY = p2.Y - (nodeRadius * Math.Sin(angle));
            }

            // Create line
            Line line = new Line
            {
                X1 = startX,
                Y1 = startY,
                X2 = endX,
                Y2 = endY,
                Stroke = color,
                StrokeThickness = thickness,
                IsHitTestVisible = false,
                Tag = new string[] { sourceId, targetId }
            };

            // Push the line to the back (Z-Index 0)
            Panel.SetZIndex(line, 0);
            CizimAlani.Children.Add(line);

            // --- ARROWHEAD CALCULATION (Proper geometry) ---
            Polygon arrowHead = new Polygon();
            arrowHead.Fill = color;

            // Arrowhead size (should increase with line thickness)
            double arrowSize = 8 + thickness;

            // Tip of the arrow is exactly the target point
            Point k1 = new Point(endX, endY);

            // Wings (30 degree angle backwards)
            Point k2 = new Point(endX - arrowSize * Math.Cos(angle - Math.PI / 6), endY - arrowSize * Math.Sin(angle - Math.PI / 6));
            Point k3 = new Point(endX - arrowSize * Math.Cos(angle + Math.PI / 6), endY - arrowSize * Math.Sin(angle + Math.PI / 6));

            arrowHead.Points.Add(k1);
            arrowHead.Points.Add(k2);
            arrowHead.Points.Add(k3);

            arrowHead.IsHitTestVisible = false;
            arrowHead.Tag = new string[] { sourceId, targetId };

            Panel.SetZIndex(arrowHead, 0);
            CizimAlani.Children.Add(arrowHead);
        }

        private FrameworkElement CreateNode(Makale m)
        {
            Grid grid = new Grid();
            string cleanId = ExtractId(m.Id);
            string authorInfo = GetAuthors(m.Authors);

            // Tooltip 
            string tooltipText = $"id: {cleanId}\n" +
                        $"author: {authorInfo}\n" +
                        $"title: {m.Title}\n" +
                        $"year: {m.Year}\n" +
                        $"citation: {m.InDegree}\n" +
                        $"centrality: {m.BetweennessScore.ToString("F4")}";

            ToolTip tooltip = new ToolTip
            {
                Content = tooltipText,
                Background = Brushes.White,
                Foreground = Brushes.Black,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12
            };
            grid.ToolTip = tooltip;
            grid.Tag = m.Id;

            // --- Size 25 ---
            double size = 25;

            Ellipse e = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = Brushes.CadetBlue,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            grid.Children.Add(e);

            // Write only the Citation Count (Small) inside
            TextBlock txtCitation = new TextBlock
            {
                Text = m.InDegree.ToString(),
                FontWeight = FontWeights.Bold,
                FontSize = 11, // Reduced font size
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            };

            // Contain citation count inside
            grid.Children.Add(txtCitation);

            grid.MouseLeftButtonDown += Node_Click;

            return grid;
        }

        // --- CLICK AND HIGHLIGHT ---
        private void Node_Click(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;
            string clickedId = element.Tag.ToString();

            // Access article data
            if (!GraphManager.Nodes.ContainsKey(clickedId)) return;
            var clickedArticle = GraphManager.Nodes[clickedId];

            // GENERAL ANALYSIS MODE (IF CHECKED)
            if (chkGenelAnalizModu.IsChecked == true)
            {
                // Root Node Control (Is this the first click?)
                if (GeneralAnalysisRootId == null)
                {
                    GeneralAnalysisRootId = clickedId; // You are the boss
                }

                // EXIT LOGIC: Exit only if the ROOT node is clicked again
                if (GeneralAnalysisRootId == clickedId && ConstantFocusId == clickedId)
                {
                    ResetInterface();
                    // Restore K-Core if exists
                    if (GlobalKCoreSet.Count > 0) foreach (var id in GlobalKCoreSet) ActiveNodes.Add(id);
                    Highlight(null);
                    return;
                }

                // Normal Expansion Process
                GraphManager.CalculateHIndex(clickedId, out List<Makale> hList);
                FillPanel(clickedArticle, hList);

                ConstantFocusId = clickedId;

                // We don't clear the list, we add cumulatively
                if (!ActiveNodes.Contains(clickedId)) ActiveNodes.Add(clickedId);

                foreach (var h in hList)
                {
                    if (!ActiveNodes.Contains(h.Id)) ActiveNodes.Add(h.Id);
                }

                Highlight(clickedId);
            }

            // FOCUS MODE (IF UNCHECKED)

            else
            {
                // If we are not in Focus Mode, ENTER THE MODE (INITIAL OPENING)
                if (!IsFocusMode)
                {
                    // Backup
                    if (GlobalKCoreSet.Count > 0)
                    {
                        BackupArticleList = new List<Makale>();
                        foreach (var id in GlobalKCoreSet) BackupArticleList.Add(GraphManager.Nodes[id]);
                        WasKCoreMode = true;
                    }
                    else
                    {
                        BackupArticleList = new List<Makale>(AllArticles);
                        WasKCoreMode = false;
                    }

                    IsFocusMode = true;
                    FocusCenterId = clickedId; // SAVE THE CENTER
                    ConstantFocusId = clickedId;

                    GraphManager.CalculateHIndex(clickedId, out List<Makale> hCore);
                    FillPanel(clickedArticle, hCore);

                    ActiveNodes.Clear();
                    ActiveNodes.Add(clickedId);
                    foreach (var h in hCore) ActiveNodes.Add(h.Id);

                    DrawFocusedGraph(clickedArticle, hCore);
                    Highlight(clickedId);
                }
                // Already in Focus Mode
                else
                {
                    // CASE: Did we click the "Main Boss" node in the center? then EXIT
                    if (clickedId == FocusCenterId)
                    {
                        // CONTROL: Is the Boss currently selected on screen?
                        if (ConstantFocusId == FocusCenterId)
                        {
                            // Yes, we were already looking at the boss, second click EXIT
                            RestoreFromBackup();
                            FocusCenterId = null;
                            return;
                        }
                        else
                        {
                            // No, we were looking at a neighbor just now, returned to boss FETCH INFO
                            GraphManager.CalculateHIndex(clickedId, out List<Makale> hCore);
                            FillPanel(clickedArticle, hCore);

                            ConstantFocusId = clickedId; // Now Boss is selected
                            Highlight(clickedId);      // Fix colors
                            return; // Don't exit, just show info
                        }
                    }

                    // CASE: Did we click the secondary nodes? -> EXPAND
                    else
                    {
                        // Update Left Panel (View properties of the clicked node)
                        GraphManager.CalculateHIndex(clickedId, out List<Makale> hCore);
                        FillPanel(clickedArticle, hCore);

                        // Mark this as the selected node (so color becomes Purple)
                        ConstantFocusId = clickedId;

                        // DO NOT ERASE THE SCREEN! Just add new children.
                        ExpandSubNodes(clickedArticle);

                        // Update colors (Check if newcomers are K-Core etc.)
                        Highlight(clickedId);
                    }
                }
            }

            if (e != null) e.Handled = true;
            // e.Handled = true;
        }

        private void FillPanel(Makale m, List<Makale> hCore)
        {
            txtDetayId.Text = ExtractId(m.Id);
            lblDetayTitle.Text = m.Title;
            lblDetayAuthors.Text = GetAuthors(m.Authors);
            lblDetayYear.Text = m.Year.ToString();
            lblDetayCitation.Text = m.InDegree.ToString();
            lblCentrality.Text = m.BetweennessScore.ToString("F4");

            lblHIndex.Text = hCore.Count.ToString();
            lblHMedian.Text = GraphManager.CalculateHMedian(hCore).ToString();
        }

        private void RestoreFromBackup()
        {
            if (BackupArticleList == null) return;

            // Reset Variables
            IsFocusMode = false;
            ConstantFocusId = null;
            ActiveNodes.Clear();
            ResetInterface(); // Clear left panel

            // If previous state was K-Core, restore the list
            if (WasKCoreMode)
            {
                GlobalKCoreSet.Clear();
                foreach (var m in BackupArticleList)
                {
                    GlobalKCoreSet.Add(m.Id);
                    ActiveNodes.Add(m.Id); // All considered "Active" in K-Core mode
                }
            }
            else
            {
                GlobalKCoreSet.Clear(); // Clear if not K-Core
            }

            // 3. Redraw the old graph
            // We are doing something similar to the drawing code in "btnKCore_Click" or "btnCiz_Click" here.
            // However, since we have a ready list (BackupArticleList), we can draw it directly.

            // Cleanup
            CizimAlani.Children.Clear();
            NodePositions.Clear();
            ResetZoom();

            // Spiral or Circular? (Check the checkbox)
            bool spiral = chkSpiralMod.IsChecked == true;
            double centerX = 2500, centerY = 2500;

            // --- Drawing Algorithm (Short Version) ---

            var list = BackupArticleList;
            if (spiral) list = list.OrderByDescending(m => m.InDegree).ToList();
            else list = list.OrderBy(m => m.Id).ToList(); // ID order if circular

            if (!spiral) // CIRCULAR MODE
            {
                double radius = 4000;
                double angleStep = (2 * Math.PI) / list.Count;
                for (int i = 0; i < list.Count; i++)
                {
                    double angle = i * angleStep;
                    NodePositions[list[i].Id] = new Point(centerX + radius * Math.Cos(angle), centerY + radius * Math.Sin(angle));
                }
                // Green arrows... (Add if necessary)
            }
            else // SPIRAL MODE (Usually the default K-Core drawing)
            {
                double c = 150;
                for (int i = 0; i < list.Count; i++)
                {
                    double angle = i * 137.5 * (Math.PI / 180.0);
                    double r = c * Math.Sqrt(i);
                    if (i == 0) r = 0;
                    NodePositions[list[i].Id] = new Point(centerX + r * Math.Cos(angle), centerY + r * Math.Sin(angle));
                }
            }

            // Draw Arrows and Nodes (Standard)
            HashSet<string> currentIds = new HashSet<string>(list.Select(m => m.Id));
            foreach (var m in list)
            {
                Point p1 = NodePositions[m.Id];
                foreach (var refId in m.ReferencedWorks)
                {
                    if (currentIds.Contains(refId))
                    {
                        bool kCoreLine = WasKCoreMode; // Simple approach
                        Brush color = kCoreLine ? Brushes.DarkBlue : Brushes.Black;
                        DrawArrow(p1, NodePositions[refId], m.Id, refId, color, kCoreLine ? 2 : 1, false);
                    }
                }
            }

            foreach (var m in list)
            {
                var visual = CreateNode(m);
                Point p = NodePositions[m.Id];
                Canvas.SetLeft(visual, p.X - 12.5);
                Canvas.SetTop(visual, p.Y - 12.5);
                Panel.SetZIndex(visual, 100);
                CizimAlani.Children.Add(visual);
            }

            // Finally call Highlight so K-Core colors return
            Highlight(null);
        }

        private void ResetInterface()
        {
            // 1. Logical Reset
            ConstantFocusId = null;
            ActiveNodes.Clear();
            GeneralAnalysisRootId = null;


            txtDetayId.Text = "-";
            lblDetayTitle.Text = "-";
            lblDetayAuthors.Text = "-";
            lblDetayYear.Text = "-";
            lblDetayCitation.Text = "-";
            lblHIndex.Text = "-";
            lblHMedian.Text = "-";
            lblCentrality.Text = "-";
        }


        private void Highlight(string lastClickedId)
        {
            bool showAll = (ActiveNodes.Count == 0);
            bool kCoreMode = GlobalKCoreSet.Count > 0;

            foreach (var child in CizimAlani.Children)
            {
                // COLORING OF NODES
                if (child is Grid nodeGrid && nodeGrid.Tag != null)
                {
                    string id = nodeGrid.Tag.ToString();
                    var circle = nodeGrid.Children[0] as Shape;

                    if (showAll || ActiveNodes.Contains(id))
                    {
                        nodeGrid.Opacity = 1.0;

                        // --- COLORING HIERARCHY ---

                        // 1. MAIN FOCUS (Center Boss Node)
                        if (id == FocusCenterId) // This node should remain a different and fixed color no matter what.
                        {
                            circle.Fill = Brushes.LimeGreen;
                            circle.Stroke = Brushes.Black;
                            circle.StrokeThickness = 3;    // Thick border
                            Panel.SetZIndex(nodeGrid, 999); // Stay on top
                        }
                        // 2. CURRENTLY SELECTED (The node you clicked)
                        else if (id == ConstantFocusId || id == lastClickedId)
                        {
                            circle.Fill = Brushes.Purple;  // Purple for selected
                            circle.Stroke = Brushes.Black;
                            circle.StrokeThickness = 1;
                            Panel.SetZIndex(nodeGrid, 800);
                        }
                        // 3. K-CORE MEMBER (If in K-Core mode)
                        else if (kCoreMode && GlobalKCoreSet.Contains(id))
                        {
                            circle.Fill = Brushes.Red;     // Red for K-Core
                            circle.Stroke = Brushes.Black;
                            circle.StrokeThickness = 1;
                            Panel.SetZIndex(nodeGrid, 500);
                        }
                        // 4. OTHERS (Neighbors)
                        else
                        {
                            // Orange if in focus mode and this is a neighbor
                            // CadetBlue if in normal mode and not K-Core
                            if (IsFocusMode)
                                circle.Fill = Brushes.Orange;
                            else
                                circle.Fill = Brushes.CadetBlue;

                            circle.Stroke = Brushes.Black;
                            circle.StrokeThickness = 1;
                            Panel.SetZIndex(nodeGrid, 100);
                        }
                    }
                    else
                    {
                        // Passive (Faded) Nodes
                        nodeGrid.Opacity = 0.1;
                        circle.Fill = Brushes.Gray;
                        Panel.SetZIndex(nodeGrid, 1);
                    }
                }

                //  COLORING OF EDGES (ARROWS)
                else if ((child is Line || child is Polygon) && (child as FrameworkElement).Tag is string[] relation)
                {
                    var shape = child as Shape;
                    string source = relation[0];
                    string target = relation[1];

                    bool sourceActive = showAll || ActiveNodes.Contains(source);
                    bool targetActive = showAll || ActiveNodes.Contains(target);

                    if (sourceActive && targetActive)
                    {
                        shape.Opacity = 1.0;

                        // K-Core Edges -> DARK BLUE
                        if (kCoreMode && GlobalKCoreSet.Contains(source) && GlobalKCoreSet.Contains(target))
                        {
                            shape.Stroke = Brushes.DarkBlue;
                            shape.Fill = Brushes.DarkBlue;
                            if (shape is Line) shape.StrokeThickness = 2;
                        }
                        // Edges from/to Main Center in Focus Mode -> Bold Black
                        else if (IsFocusMode && (source == FocusCenterId || target == FocusCenterId))
                        {
                            shape.Stroke = Brushes.Black;
                            shape.Fill = Brushes.Black;
                            if (shape is Line) shape.StrokeThickness = 2;
                        }
                        else
                        {
                            // Normal Arrows
                            shape.Stroke = Brushes.Black;
                            shape.Fill = Brushes.Black;
                            if (shape is Line) shape.StrokeThickness = 1;
                        }
                    }
                    else
                    {
                        shape.Opacity = 0.05;
                        shape.Stroke = Brushes.LightGray;
                        shape.Fill = Brushes.LightGray;
                    }
                }
            }
        }

        // --- ZOOM AND PAN FUNCTIONS ---
        private void AnaPanel_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var tg = (TransformGroup)CizimAlani.RenderTransform;
            var st = (ScaleTransform)tg.Children[0];
            var tt = (TranslateTransform)tg.Children[1];

            Point mousePos = e.GetPosition(AnaPanel);

            // 15% zoom factor
            double zoomFactor = 1.15;

            double newScale;
            if (e.Delta > 0)
            {
                // Zoom in on scroll forward
                newScale = st.ScaleX * zoomFactor;
            }
            else
            {
                // Zoom out on scroll back
                newScale = st.ScaleX / zoomFactor;
            }

            // LIMITS (Don't get too close or too far)
            if (newScale < 0.01) newScale = 0.01;
            if (newScale > 10) newScale = 10;


            // Center adjustment to keep mouse position fixed
            double f = newScale / st.ScaleX;
            tt.X = mousePos.X - (mousePos.X - tt.X) * f;
            tt.Y = mousePos.Y - (mousePos.Y - tt.Y) * f;

            // Apply new value
            st.ScaleX = newScale;
            st.ScaleY = newScale;

            // Stop the event so the page doesn't scroll
            e.Handled = true;
        }
        private void AnaPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            AnaPanel.CaptureMouse();
            isDragging = true;
            startPoint = e.GetPosition(AnaPanel);
        }

        private void AnaPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                var tt = (TranslateTransform)((TransformGroup)CizimAlani.RenderTransform).Children[1];
                Point pos = e.GetPosition(AnaPanel);
                tt.X += pos.X - startPoint.X;
                tt.Y += pos.Y - startPoint.Y;
                startPoint = pos;
            }
        }

        private void AnaPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            AnaPanel.ReleaseMouseCapture();
            isDragging = false;
        }

        private void btnKCore_Click(object sender, RoutedEventArgs e)
        {
            ResetInterface();

            IsFocusMode = false;
            FocusCenterId = null;
            BackupArticleList = null;
            // INPUT CHECK
            if (!int.TryParse(txtKDegeri.Text, out int k))
            {
                MessageBox.Show("Please enter a valid number.");
                return;
            }

            // GET K-CORE LIST
            List<Makale> kCoreList = GraphManager.CalculateKCore(k);

            GlobalKCoreSet.Clear();
            foreach (var m in kCoreList)
            {
                GlobalKCoreSet.Add(m.Id);
            }

            if (kCoreList.Count == 0)
            {
                // If nothing found
                MessageBox.Show($"No core found for K={k}.");
                return;
            }

            // Put IDs in HashSet for fast lookup
            HashSet<string> kCoreIdSet = new HashSet<string>(kCoreList.Select(m => m.Id));

            MessageBox.Show($"K={k} Analysis Completed.\n{kCoreList.Count} articles highlighted.");

            ConstantFocusId = null;   // Break previous click lock
            ActiveNodes.Clear(); // Clear old memory

            // Only K-Core nodes remain clickable
            foreach (var article in kCoreList)
            {
                ActiveNodes.Add(article.Id);
            }

            // UPDATE VIEW WITHOUT CLEARING (PAINTING) 
            foreach (var child in CizimAlani.Children)
            {
                // CHECK NODES
                if (child is Grid nodeGrid && nodeGrid.Tag != null)
                {
                    string id = nodeGrid.Tag.ToString();
                    var circle = nodeGrid.Children[0] as Shape; // Ellipse

                    if (kCoreIdSet.Contains(id))
                    {
                        // If in K-Core: RED and BRIGHT
                        circle.Fill = Brushes.Red;
                        circle.Stroke = Brushes.DarkRed;
                        nodeGrid.Opacity = 1.0;
                        Panel.SetZIndex(nodeGrid, 999); // Bring to front
                    }
                    else
                    {
                        // Else: GRAY and FADED
                        circle.Fill = Brushes.LightGray;
                        circle.Stroke = Brushes.Gray;
                        nodeGrid.Opacity = 0.2; // Significant fade
                        Panel.SetZIndex(nodeGrid, 1); // Send to back
                    }
                }

                // CHECK LINKS (Line or Polygon)
                else if ((child is Line || child is Polygon) && (child as FrameworkElement).Tag is string[] relation)
                {
                    string source = relation[0];
                    string target = relation[1];
                    var shape = child as Shape;

                    // Highlight if both ends are in K-Core
                    if (kCoreIdSet.Contains(source) && kCoreIdSet.Contains(target))
                    {
                        shape.Stroke = Brushes.DarkBlue;
                        shape.Fill = Brushes.DarkBlue;
                        shape.Opacity = 1.0;

                        // Thicken if line
                        if (shape is Line) shape.StrokeThickness = 2;
                    }
                    else
                    {
                        // Near-hide otherwise
                        shape.Stroke = Brushes.LightGray;
                        shape.Fill = Brushes.LightGray;
                        shape.Opacity = 0.1;
                        if (shape is Line) shape.StrokeThickness = 1;
                    }
                }
            }
        }

        private void btnAra_Click(object sender, RoutedEventArgs e)
        {
            string searchText = txtSearchId.Text.Trim();

            if (string.IsNullOrEmpty(searchText))
            {
                MessageBox.Show("Please enter an ID to search.");
                return;
            }
            if (searchText.Length < 4)
            {
                MessageBox.Show("Please enter a more specific ID.");
                return;
            }

            if (AllArticles.Count == 0)
            {
                MessageBox.Show("Please load data and draw graph first.");
                return;
            }

            // ID Match Lookup
            // Use EndsWith for partial ID searches
            string targetId = null;

            // Check exact match first
            if (GraphManager.Nodes.ContainsKey(searchText))
            {
                targetId = searchText;
            }
            else
            {
                // Check suffix match
                targetId = GraphManager.Nodes.Keys.FirstOrDefault(k => k.EndsWith(searchText));
            }

            if (targetId == null)
            {
                MessageBox.Show("Search ID not found in database!");
                return;
            }

            // Locate the visual node
            // Look at grids in CizimAlani.Children
            FrameworkElement targetVisual = null;

            foreach (var child in CizimAlani.Children)
            {
                if (child is FrameworkElement fe && fe.Tag != null)
                {
                    if (fe.Tag.ToString() == targetId)
                    {
                        targetVisual = fe;
                        break;
                    }
                }
            }

            // Trigger Click if found
            if (targetVisual != null)
            {
                // Send null to handle with Node_Click
                Node_Click(targetVisual, null);
                MessageBox.Show("Article found and focused!");
            }
            else
            {
                // Article exists but not drawn (e.g. filtered by K-Core)
                MessageBox.Show($"Article exists ({targetId}) but is not currently drawn.\nPlease click 'Draw Graph' or clear K-Core filter.");
            }
        }

    }
}