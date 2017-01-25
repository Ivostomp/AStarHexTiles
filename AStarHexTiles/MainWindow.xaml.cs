using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AStarHexTiles
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int WATERTHRESHOLD = 20;
        private const int BLOCKADETHRESHOLD = 60;

        private List<ControlPoint> _CurveControlPoints = new List<ControlPoint>();

        private List<HexTile> _HexTiles = new List<HexTile>();

        private HexMap _Map = new HexMap();

        private List<Line> _CurveLines = new List<Line>();
        private List<Line> _ControlLines = new List<Line>();
        private bool _AllowNew = true;

        private bool _MouseDown;

        private TileType SelectedTileType { get; set; } = TileType.NonWalkable;

        public bool GenEmpty { get; set; } = false;
        public bool GenWater { get; set; } = true;
        public bool GenAllowLonely { get; set; } = true;
        public int GenSpeed = 30;
        public int Seed = 1337;

        private System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();

        public MainWindow() {
            InitializeComponent();
            Initialize();
        }

        private void Initialize() {
            _Map = new HexMap(35, 35);
            canDrawArea.MouseLeftButtonDown += CanDrawArea_MouseDown;
            btnReset.Click += BtnReset_Click;
            btnFindPath.Click += BtnFindPath_Click;
            cbMethod.ItemsSource = new CBOption[] {
                new CBOption { Id = (int)MethodType.FCostH, Name = "F -> H" },
                new CBOption { Id = (int)MethodType.FCostG, Name = "F -> G" },
                new CBOption { Id = (int)MethodType.GCostF, Name = "G -> F" },
                new CBOption { Id = (int)MethodType.GCostH, Name = "G -> H" },
                new CBOption { Id = (int)MethodType.HCostF, Name = "H -> F" },
                new CBOption { Id = (int)MethodType.HCostG, Name = "H -> G" },
            };
            cbClickTileType.ItemsSource = new CBOption[] {
                new CBOption { Id = (int)TileType.NonWalkable, Name = "Blockade" },
                new CBOption { Id = (int)TileType.Water, Name = "Water" },
                new CBOption { Id = (int)TileType.Normal, Name = "Normal" },
            };
            txtSeed.TextChanged += TxtSeed_TextChanged;
            chbGenEmpty.LostFocus += (s, e) => { GenEmpty = chbGenEmpty.IsChecked ?? false; };
            chbGenWater.LostFocus += (s, e) => { GenWater = chbGenWater.IsChecked ?? false; };
            chbGenAllowLonely.LostFocus += (s, e) => { GenAllowLonely = chbGenAllowLonely.IsChecked ?? false; };
            slSpeed.ValueChanged += (s, e) => { GenSpeed = (int)slSpeed.Value; lblGenSpeed.Content = GenSpeed; _timer.Interval = GenSpeed; };
            btnSave.Click += BtnSave_Click;
            btnLoad.Click += BtnLoad_Click;
        }

        

        private void TxtSeed_TextChanged(object sender, TextChangedEventArgs e) {
            var textbox = sender as TextBox;
            if (textbox == null) { return; }

            int seed;
            int.TryParse(textbox.Text, out seed);
            Seed = seed;
        }

        private void BtnFindPath_Click(object sender, RoutedEventArgs e) {
            bool instantGen = chbInstant.IsChecked ?? false;
            FindPath((MethodType)cbMethod.SelectedValue, instantGen);
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e) {
            ResetGrid();
            DrawGrid(35, 35);
        }

        private void ResetGrid() {
            _timer.Stop();
            canDrawArea.Children.Clear();
            _CurveControlPoints.Clear();
            _HexTiles.Clear();
        }

        private void CanDrawArea_MouseDown(object sender, MouseButtonEventArgs e) {
            if (true) {
                return;
            }

            Point mousePoint = e.MouseDevice.GetPosition((Canvas)sender);

            if (_CurveControlPoints.Count >= 15 || !_AllowNew) { return; }

            // Create a red Ellipse.
            Ellipse ellipse = new Ellipse();

            // Create a SolidColorBrush with a red color to fill the 
            // Ellipse with.
            SolidColorBrush mySolidColorBrush = new SolidColorBrush();

            // Describes the brush's color using RGB values. 
            // Each value has a range of 0-255.
            mySolidColorBrush.Color = Color.FromArgb(255, 255, 255, 0);
            ellipse.Name = $"Control{_CurveControlPoints.Count.ToString("d2")}";
            ellipse.Fill = mySolidColorBrush;
            ellipse.StrokeThickness = 2;
            ellipse.Stroke = Brushes.Black;

            ellipse.MouseMove += Ellipse_MouseMove;
            ellipse.MouseLeave += (s, eArg) => { _AllowNew = true; };

            // Set the width and height of the Ellipse.
            ellipse.Width = 10;
            ellipse.Height = 10;

            // Add the Ellipse to the StackPanel.
            canDrawArea.Children.Add(ellipse);

            ellipse.SetValue(Canvas.LeftProperty, ((double)mousePoint.X - (ellipse.Width / 2)));
            ellipse.SetValue(Canvas.TopProperty, ((double)mousePoint.Y - (ellipse.Height / 2)));

            if (_CurveControlPoints.Count > 0) {
                DrawLine(mousePoint, _CurveControlPoints.Last().Point, Brushes.DimGray);
            }

            _CurveControlPoints.Add(new ControlPoint($"Control{_CurveControlPoints.Count.ToString("d2")}", mousePoint, ellipse));

            if (_CurveControlPoints.Count > 1) { MakeBezierCurve(); }
        }

        private void Ellipse_MouseMove(object sender, MouseEventArgs e) {
            _AllowNew = false;
            if (e.LeftButton == MouseButtonState.Pressed) {
                var ellipse = sender as Ellipse;
                if (ellipse != null) {
                    var old = _CurveControlPoints.Find(q => q.Ellipse == ellipse);

                    Point mousePoint = e.MouseDevice.GetPosition(canDrawArea);
                    ellipse.SetValue(Canvas.LeftProperty, ((double)mousePoint.X - (ellipse.Width / 2)));
                    ellipse.SetValue(Canvas.TopProperty, ((double)mousePoint.Y - (ellipse.Height / 2)));

                    ClearControlLines();
                    DrawControlLines();
                }
            }
        }

        private void DrawControlLines() {
            var loopCount = _CurveControlPoints.Count;
            for (int i = 1; i < loopCount; i++) {
                var cur = _CurveControlPoints[i].Point;
                var prev = _CurveControlPoints[i - 1].Point;
                DrawLine(prev, cur, Brushes.Black, LineType.ControlLine);
            }
        }

        private void MakeBezierCurve() {
            ClearCurve();

            var curve = CalculateCurve();

            if (curve.Count() > 1) { DrawCurve(curve.ToArray()); }
        }

        private void ClearCurve() {
            foreach (var line in _CurveLines) {
                canDrawArea.Children.Remove(line);
            }
            _CurveLines.Clear();
        }

        private void ClearControlLines() {
            foreach (var line in _ControlLines) {
                canDrawArea.Children.Remove(line);
            }
            _ControlLines.Clear();
        }

        private IEnumerable<Point> CalculateCurve() {
            //var curve = GetBezierApproximation(_CurveControlPoints.ToArray(), (_CurveControlPoints.Count * 10));
            var curve = GetBezierApproximation(_CurveControlPoints.Select(s => s.Point).ToArray(), (256));

            return curve;
        }

        private void DrawCurve(params Point[] points) {
            var loopCount = points.Count();
            for (int i = 1; i < loopCount; i++) {
                var cur = points[i];
                var prev = points[i - 1];

                DrawLine(cur, prev, Brushes.OrangeRed, LineType.Curve);
            }
        }

        private void DrawLine(Point a, Point b, Brush color, LineType type = LineType.ControlLine) {
            var line = new Line();

            line.Stroke = color;
            line.X1 = a.X;
            line.X2 = b.X;
            line.Y1 = a.Y;
            line.Y2 = b.Y;

            switch (type) {
                case LineType.ControlLine:
                    line.Name = $"ConP_{Guid.NewGuid().ToString("N")}";
                    line.StrokeThickness = 2;
                    _ControlLines.Add(line);
                    break;
                case LineType.Curve:
                    line.Name = $"CurP_{Guid.NewGuid().ToString("N")}";
                    line.StrokeThickness = 3;
                    _CurveLines.Add(line);
                    break;
                case LineType.Grid:
                    line.Name = $"GriP_{Guid.NewGuid().ToString("N")}";
                    line.StrokeThickness = 1;
                    break;
                default:
                    line.Name = $"Line_{Guid.NewGuid().ToString("N")}";
                    line.StrokeThickness = 1;
                    break;
            }

            canDrawArea.Children.Add(line);
        }

        private Point[] GetBezierApproximation(Point[] controlPoints, int outputSegmentCount) {
            Point[] points = new Point[outputSegmentCount + 1];
            for (int i = 0; i <= outputSegmentCount; i++) {
                double t = (double)i / outputSegmentCount;
                points[i] = GetBezierPoint(t, controlPoints, 0, controlPoints.Length);
            }
            return points;
        }

        private Point GetBezierPoint(double t, Point[] controlPoints, int index, int count) {
            if (count == 1)
                return controlPoints[index];
            var P0 = GetBezierPoint(t, controlPoints, index, count - 1);
            var P1 = GetBezierPoint(t, controlPoints, index + 1, count - 1);
            return new Point((1 - t) * P0.X + t * P1.X, (1 - t) * P0.Y + t * P1.Y);
        }

        public void DrawGrid(int numCol, int numRow) {
            //var gridSize = 50;
            //var colCount = (int)Math.Floor(canDrawArea.Width / gridSize);
            //var rowCount = (int)Math.Floor(canDrawArea.Height / gridSize);

            //for (int x = 0; x < colCount; x++) {
            //    var a = new Point(((x + 1) * gridSize), 0);
            //    var b = new Point(((x + 1) * gridSize), canDrawArea.Height);
            //    DrawLine(a, b, Brushes.Gray, LineType.Grid);
            //}

            //for (int y = 0; y < rowCount; y++) {
            //    var a = new Point(0, ((y + 1) * gridSize));
            //    var b = new Point(canDrawArea.Width, ((y + 1) * gridSize));
            //    DrawLine(a, b, Brushes.Gray, LineType.Grid);
            //}

            // gen hexgrid
            var columns = numCol;
            var rows = numRow;
            var map = new HexMap(rows, columns);

            Random rand = new Random(Seed);

            for (int y = 0; y < rows; y++) {
                for (int x = 0; x < columns; x++) {


                    var randValue = rand.Next(0, 100);
                    var type = TileType.Normal;
                    if (!GenEmpty && randValue < BLOCKADETHRESHOLD && randValue > WATERTHRESHOLD) {
                        type = TileType.NonWalkable;
                    } else if (GenWater && !GenEmpty && randValue <= WATERTHRESHOLD) {
                        type = TileType.Water;
                    }


                    var tile = new HexTile(x, y, type, 15);
                    _HexTiles.Add(tile);
                    tile.InitGraphics();
                    canDrawArea.Children.Add(tile.Poly);
                    //canDrawArea.Children.Add(tile.lblG);
                    //canDrawArea.Children.Add(tile.lblH);
                    //canDrawArea.Children.Add(tile.lblF);
                    //canDrawArea.Children.Add(tile.CenterPoint);
                    tile.Poly.MouseUp += (s, e) => { _MouseDown = false; };
                    tile.Poly.MouseDown += (s, e) => {
                        var poly = (s as Polygon);
                        if (poly == null) { return; }

                        _MouseDown = true;
                        //if (_HexTiles.Where(q => q.Selected).Count() >= 3) return;
                        //tile.Selected = !tile.Selected;

                        //if (poly.Fill == Brushes.Blue || poly.Fill == Brushes.OrangeRed) {
                        //    poly.Fill = Brushes.WhiteSmoke;
                        //} else {
                        //    poly.Fill = Brushes.Blue;
                        //    //var n = GetNeighbours(tile);
                        //    //n.ToList().ForEach(f => f.SetColor(Brushes.OrangeRed));
                        //}
                        var sHex = _HexTiles.Find(q => q.IsStart);
                        var eHex = _HexTiles.Find(q => q.IsEnd);

                        if(sHex == null) { tile.SetAsStart(); }
                        else if(eHex == null) { tile.SetAsEnd(); }

                        sHex = _HexTiles.Find(q => q.IsStart);
                        eHex = _HexTiles.Find(q => q.IsEnd);
                        if (sHex != null && eHex != null) {
                            var first = sHex;
                            var last = eHex;

                            var distance = GetHexDistance(first, last);
                            lblHeuristicDistance.Content = $"Heuristic Distance: {distance}";
                            if (!tile.IsStart && !tile.IsEnd) {
                                if (tile.Type == TileType.NonWalkable || tile.Type == TileType.Water) { tile.Type = TileType.Normal; } 
                                else {
                                    tile.Type = (TileType)cbClickTileType.SelectedValue;
                                }
                            }
                        }

                    };
                    tile.Poly.MouseMove += (s, e) => {
                        if (!_MouseDown) return;
                        if (!tile.IsStart && !tile.IsEnd) {
                                tile.Type = (TileType)cbClickTileType.SelectedValue; 
                        }
                    };

                }
            }
            if (!GenAllowLonely) {
                var lonelyTiles = _HexTiles.Where(q => q.Type == TileType.NonWalkable && GetNeighbours(q, TileType.Normal, TileType.Water).Count() == 0).ToList();
                lonelyTiles.ForEach(n => {
                    n.Type = TileType.NonWalkable;
                    //n.Poly.Fill = Brushes.DarkViolet;
                    GetNeighbours(n).ToList().ForEach(m => {
                        var randVal = rand.Next(0, 100);
                        if (randVal >= 50) {
                            m.Type = TileType.NonWalkable;
                        }
                    });
                }); 
            }
            map.Tiles = _HexTiles;
            map.Seed = Seed;
            _Map = map;
            //p.LayoutTransform = new RotateTransform();
        }

        public void DrawGrid(HexMap map) {
            ResetGrid();
            map.Tiles.ForEach(tile => {
                _HexTiles.Add(tile);
                tile.InitGraphics();
                canDrawArea.Children.Add(tile.Poly);
                tile.Poly.MouseUp += (s, e) => {
                    _MouseDown = false;
                };
                tile.Poly.MouseDown += (s, e) => {
                    var poly = (s as Polygon);
                    if (poly == null) { return; }

                    _MouseDown = true;
                    //if (_HexTiles.Where(q => q.Selected).Count() >= 3) return;
                    //tile.Selected = !tile.Selected;

                    //if (poly.Fill == Brushes.Blue || poly.Fill == Brushes.OrangeRed) {
                    //    poly.Fill = Brushes.WhiteSmoke;
                    //} else {
                    //    poly.Fill = Brushes.Blue;
                    //    //var n = GetNeighbours(tile);
                    //    //n.ToList().ForEach(f => f.SetColor(Brushes.OrangeRed));
                    //}
                    var sHex = _HexTiles.Find(q => q.IsStart);
                    var eHex = _HexTiles.Find(q => q.IsEnd);

                    if (sHex == null) { tile.SetAsStart(); } else if (eHex == null) { tile.SetAsEnd(); }

                    sHex = _HexTiles.Find(q => q.IsStart);
                    eHex = _HexTiles.Find(q => q.IsEnd);
                    if (sHex != null && eHex != null) {
                        var first = sHex;
                        var last = eHex;

                        var distance = GetHexDistance(first, last);
                        lblHeuristicDistance.Content = $"Heuristic Distance: {distance}";
                        if (!tile.IsStart && !tile.IsEnd) {
                            if (tile.Type == TileType.NonWalkable || tile.Type == TileType.Water) { tile.Type = TileType.Normal; } else {
                                tile.Type = (TileType)cbClickTileType.SelectedValue;
                            }
                        }
                    }

                };
                tile.Poly.MouseMove += (s, e) => {
                    if (!_MouseDown) return;
                    if (!tile.IsStart && !tile.IsEnd) {
                        tile.Type = (TileType)cbClickTileType.SelectedValue;
                    }
                };
            });
        }

        public void FindPath(MethodType genMethod, bool instant) {
            _timer = new System.Windows.Forms.Timer();
            foreach (var q in _HexTiles) {
                if (q.Type == TileType.Start || q.Type == TileType.Target || q.Type == TileType.NonWalkable || q.Type == TileType.Water) { continue; }
                if (q.Type == TileType.OpenWater || q.Type == TileType.ClosedWater || q.Type == TileType.PathWater) { q.Type = TileType.Water; } 
                else { q.Type = TileType.Normal; }
            }

            var start = _HexTiles.Find(q => q.IsStart);
            var target = _HexTiles.Find(q => q.IsEnd);

            if (start == null || target == null) return;

            List<HexTile> openTiles = new List<HexTile>();
            List<HexTile> closedTiles = new List<HexTile>();
            openTiles.Add(start);

            if (instant) {
                while (openTiles.Count > 0) {
                    if (openTiles.Count == 0) return;
                    HexTile currentTile = null;
                    switch (genMethod) {
                        case MethodType.FCostG:
                            currentTile = openTiles.Where(q => q.FCost == openTiles.Min(t => t.FCost)).OrderBy(o => o.GCost).First();
                            break;
                        case MethodType.FCostH:
                            currentTile = openTiles.Where(q => q.FCost == openTiles.Min(t => t.FCost)).OrderBy(o => o.HCost).First();
                            break;
                        case MethodType.GCostF:
                            currentTile = openTiles.Where(q => q.GCost == openTiles.Min(t => t.GCost)).OrderBy(o => o.HCost).First();
                            break;
                        case MethodType.HCostF:
                            currentTile = openTiles.Where(q => q.HCost == openTiles.Min(t => t.HCost)).OrderBy(o => o.GCost).First();
                            break;
                        default:
                            currentTile = openTiles.Where(q => q.FCost == openTiles.Min(t => t.FCost)).OrderBy(o => o.GCost).First();
                            break;
                    }

                    openTiles.Remove(currentTile); closedTiles.Add(currentTile);

                    if (currentTile != start && currentTile != target) {
                        if (currentTile.Type == TileType.OpenWater) {
                            currentTile.Type = TileType.ClosedWater;
                        } else {
                            currentTile.Type = TileType.Closed;
                        }
                    }

                    //openTiles.Where(q => q.Type != TileType.Normal).ToList().ForEach(n => n.Type = TileType.Open);
                    //closedTiles.Where(q => q.Type == TileType.Normal).ToList().ForEach(n => n.Type = TileType.Closed);

                    if (currentTile == target) {
                        if (_timer != null) { _timer.Stop(); }
                        IEnumerable<HexTile> path = RetracePath(start, target);
                        path.ToList().ForEach(n => {
                            if (n.Type != TileType.Start && n.Type != TileType.Target) {

                                if (n.Type == TileType.Water || n.Type == TileType.ClosedWater || n.Type == TileType.OpenWater) {
                                    n.Type = TileType.PathWater;
                                } else {
                                    n.Type = TileType.Path;
                                }
                            }
                        });
                        lblDistance.Content = $"Path Distance: {path.Last().FCost}";
                        lblTileChecked.Content = $"Path Distance:{path.Count() + openTiles.Count + closedTiles.Count}";
                        return;
                    }

                    foreach (var adjecentTile in GetNeighbours(currentTile, TileType.NonWalkable)) {
                        if (closedTiles.Contains(adjecentTile)) continue;

                        var movPenalty = adjecentTile.GetMovementPenalty();
                        int newMovementCostToAdjecent = currentTile.GCost + GetHexDistance(currentTile, adjecentTile) + movPenalty;
                        if (newMovementCostToAdjecent < adjecentTile.GCost || !openTiles.Contains(adjecentTile)) {
                            adjecentTile.GCost = newMovementCostToAdjecent;
                            adjecentTile.HCost = GetHexDistance(adjecentTile, target) * (1 + HexTile.GetMovementePenalty(TileType.Normal));
                            adjecentTile.ParentTile = currentTile;

                            if (!openTiles.Contains(adjecentTile)) {
                                if (adjecentTile != start && adjecentTile != target) {
                                    if (adjecentTile.Type == TileType.Water) {
                                        adjecentTile.Type = TileType.OpenWater;
                                    } else {
                                        adjecentTile.Type = TileType.Open;
                                    }
                                }
                                openTiles.Add(adjecentTile);
                            }

                        }
                    }
                }
            } else {
                _timer.Interval = GenSpeed;
                _timer.Tick += (s, e) => {

                    if (openTiles.Count == 0) return;
                    HexTile currentTile = null;
                    switch (genMethod) {
                        case MethodType.FCostG:
                            currentTile = openTiles.Where(q => q.FCost == openTiles.Min(t => t.FCost)).OrderBy(o => o.GCost).First();
                            break;
                        case MethodType.FCostH:
                            currentTile = openTiles.Where(q => q.FCost == openTiles.Min(t => t.FCost)).OrderBy(o => o.HCost).First();
                            break;
                        case MethodType.GCostF:
                            currentTile = openTiles.Where(q => q.GCost == openTiles.Min(t => t.GCost)).OrderBy(o => o.HCost).First();
                            break;
                        case MethodType.HCostF:
                            currentTile = openTiles.Where(q => q.HCost == openTiles.Min(t => t.HCost)).OrderBy(o => o.GCost).First();
                            break;
                        default:
                            currentTile = openTiles.Where(q => q.FCost == openTiles.Min(t => t.FCost)).OrderBy(o => o.GCost).First();
                            break;
                    }

                    openTiles.Remove(currentTile); closedTiles.Add(currentTile);

                    if (currentTile != start && currentTile != target) {
                        if (currentTile.Type == TileType.OpenWater) {
                            currentTile.Type = TileType.ClosedWater;
                        } else {
                            currentTile.Type = TileType.Closed;
                        }
                    }

                    //openTiles.Where(q => q.Type != TileType.Normal).ToList().ForEach(n => n.Type = TileType.Open);
                    //closedTiles.Where(q => q.Type == TileType.Normal).ToList().ForEach(n => n.Type = TileType.Closed);

                    if (currentTile == target) {
                        if (_timer != null) { _timer.Stop(); }
                        IEnumerable<HexTile> path = RetracePath(start, target);
                        path.ToList().ForEach(n => {
                            if (n.Type != TileType.Start && n.Type != TileType.Target) {

                                if (n.Type == TileType.Water || n.Type == TileType.ClosedWater || n.Type == TileType.OpenWater) {
                                    n.Type = TileType.PathWater;
                                } else {
                                    n.Type = TileType.Path;
                                }
                            }
                        });
                        lblDistance.Content = $"Path Distance: {path.Max(q => q.FCost)}";
                        lblTileChecked.Content = $"Tiles Checked:{path.Count() + openTiles.Count + closedTiles.Count}";
                        return;
                    }

                    foreach (var adjecentTile in GetNeighbours(currentTile, TileType.NonWalkable)) {
                        if (closedTiles.Contains(adjecentTile)) continue;

                        var movPenalty = adjecentTile.GetMovementPenalty();
                        int newMovementCostToAdjecent = currentTile.GCost + GetHexDistance(currentTile, adjecentTile) + movPenalty;
                        if (newMovementCostToAdjecent < adjecentTile.GCost || !openTiles.Contains(adjecentTile)) {
                            adjecentTile.GCost = newMovementCostToAdjecent;
                            adjecentTile.HCost = GetHexDistance(adjecentTile, target) * (1 + HexTile.GetMovementePenalty(TileType.Normal));
                            adjecentTile.ParentTile = currentTile;

                            if (!openTiles.Contains(adjecentTile)) {
                                if (adjecentTile != start && adjecentTile != target) {
                                    if (adjecentTile.Type == TileType.Water) {
                                        adjecentTile.Type = TileType.OpenWater;
                                    } else {
                                        adjecentTile.Type = TileType.Open;
                                    }
                                }
                                openTiles.Add(adjecentTile);
                            }

                        }
                    }
                };
                _timer.Start();
            }
        }

        private IEnumerable<HexTile> RetracePath(HexTile start, HexTile target) {
            var path = new List<HexTile>();

            var current = target;

            while (current != start) {
                path.Add(current);
                current = current.ParentTile;
            }

            path.Reverse();

            return path;
        }

        private int GetHexDistance(HexTile first, HexTile last) {
            Vector3 f = first.ToCubicCoordinates();
            Vector3 l = last.ToCubicCoordinates();

            var diffX = Math.Abs(f.X - l.X);
            var diffY = Math.Abs(f.Y - l.Y);
            var diffZ = Math.Abs(f.Z - l.Z);

            var distance = (diffX + diffY + diffZ) / 2;
            return distance;
        }

        public IEnumerable<HexTile> GetNeighbours(HexTile tile, params TileType[] IgnoreTypes) {
            var offsets = new HexTile[][] {
                new HexTile[] {
                    new HexTile(1, 0), new HexTile(0, -1),
                    new HexTile(-1, -1), new HexTile(-1, 0),
                    new HexTile(-1, 1), new HexTile(0, 1)
                },
                new HexTile[] {
                    new HexTile(1, 0), new HexTile(1, -1),
                    new HexTile(0, -1), new HexTile(-1, 0),
                    new HexTile(0, 1), new HexTile(1, 1)
                }
            };
            var setType = tile.Row % 2;
            var set = offsets[setType];
            var posNeighbours = set.Select(s => {
                var res = s + tile;
                return res;
            });

            var neighbours = _HexTiles.Join(posNeighbours,
                s => new { s.Column, s.Row },
                h => new { h.Column, h.Row },
                (s, h) => s);

            var validNeighbours = neighbours.Where(q => !(IgnoreTypes.Contains(q.Type)));

            return validNeighbours;
        }

        public enum LineType { ControlLine, Curve, Grid }

        public enum MethodType { FCostG, FCostH, GCostF, GCostH, HCostF, HCostG }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) {

        }

        private void BtnSave_Click(object sender, RoutedEventArgs e) {
            _Map.Tiles.ForEach(q => {
                if (q.Type == TileType.Closed || q.Type == TileType.Open || q.Type == TileType.Path) { q.Type = TileType.Normal; }
                else if (q.Type == TileType.ClosedWater || q.Type == TileType.OpenWater || q.Type == TileType.PathWater) { q.Type = TileType.Water; }
            });


            var json = JsonConvert.SerializeObject(_Map);
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = "map"; // Default file name
            dlg.DefaultExt = ".hxm"; // Default file extension
            dlg.Filter = "Hex Map (.hxm)|*.hxm"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true) {
                // Save document
                string filename = dlg.FileName;
                using (StreamWriter file = new System.IO.StreamWriter(filename, false)) {
                    file.Write(json);
                }
            }
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e) {
            Stream myStream = null;
            System.Windows.Forms.OpenFileDialog openFileDialog1 = new System.Windows.Forms.OpenFileDialog();

            openFileDialog1.InitialDirectory = "c:\\";
            openFileDialog1.Filter = "Hex maps (*.hxm)|*.hxm";
            openFileDialog1.FilterIndex = 2;
            openFileDialog1.RestoreDirectory = true;
            if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                var fileText = File.ReadAllText(openFileDialog1.FileName);
                _Map = JsonConvert.DeserializeObject<HexMap>(fileText);
                DrawGrid(_Map);
            }
        }
    }
}
