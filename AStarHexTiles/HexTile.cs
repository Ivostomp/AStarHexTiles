using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AStarHexTiles
{

    /// <summary>
    /// Tile of type offset odd-r 
    /// </summary>
    /// <seealso cref="http://www.redblobgames.com/grids/hexagons/#coordinates"/>
    public class HexTile
    {
        [JsonIgnore]
        private static Dictionary<TileType, Brush> Colors = new Dictionary<TileType, Brush>() {
            { TileType.Normal, Brushes.White },
            { TileType.Start, Brushes.Blue },
            { TileType.Target, Brushes.Red },
            { TileType.Open, new SolidColorBrush(Color.FromArgb(64,173,255,47)) },
            { TileType.OpenWater, new SolidColorBrush(Color.FromArgb(172,173,255,47)) },
            { TileType.Closed, new SolidColorBrush(Color.FromArgb(64,255,69,00)) },
            { TileType.ClosedWater, new SolidColorBrush(Color.FromArgb(172,255,69,00)) },
            { TileType.NonWalkable, Brushes.DimGray },
            { TileType.Water, Brushes.LightBlue },
            { TileType.Path, Brushes.MediumPurple },
            { TileType.PathWater, Brushes.Purple }
        };

        [JsonIgnore]
        private static Dictionary<TileType, int> Penalty = new Dictionary<TileType, int>() {
            { TileType.Normal       , 4 },
            { TileType.Start        , 4 },
            { TileType.Target       , 4 },
            { TileType.Open         , 4 },
            { TileType.Closed       , 4 },
            { TileType.Path         , 4 },
            { TileType.NonWalkable  , 4 },
            { TileType.OpenWater    , 104 },
            { TileType.ClosedWater  , 104 },
            { TileType.Water        , 104 }
        };

        //public Canvas Canvas { get; set; }

        [JsonProperty(propertyName: "C")]
        public int Column { get; set; }
        [JsonProperty(propertyName: "R")]
        public int Row { get; set; }

        [JsonProperty(propertyName: "S")]
        public double SizeFactor { get; set; } = 25.0;

        [JsonIgnore]
        private TileType _Type = TileType.Normal;
        [JsonProperty(propertyName: "T")]
        public TileType Type { get { return _Type; }  set { _Type = value; SetColor(_Type); } }
        [JsonIgnore]
        public bool Selected { get; set; }
        [JsonIgnore]
        public bool IsStart { get { return Type == TileType.Start; } }
        [JsonIgnore]
        public bool IsEnd { get { return Type == TileType.Target; } }

        [JsonIgnore]
        public Polygon Poly { get; set; }
        [JsonIgnore]
        public Ellipse CenterPoint { get; set; }

        [JsonIgnore]
        public Label lblG { get; set; } = new Label();
        [JsonIgnore]
        public Label lblH { get;  set; } = new Label();
        [JsonIgnore]
        public Label lblF { get;  set; } = new Label();
        [JsonIgnore]
        public HexTile ParentTile { get; set; }

        [JsonIgnore]
        private int _GCost;
        [JsonIgnore]
        private int _HCost;

        /// <summary>
        /// Distance from start
        /// </summary>
        [JsonIgnore]
        public int GCost { get { return _GCost; } set { _GCost = value; lblG.Content = _GCost.ToString(); lblF.Content = FCost.ToString(); } }
        /// <summary>
        /// (Heuristic) Distance from end node
        /// </summary>
        [JsonIgnore]
        public int HCost { get { return _HCost; } set { _HCost = value; lblH.Content = _HCost.ToString(); lblF.Content = FCost.ToString(); } }
        /// <summary>
        /// full cost (Gcost + Hcost)
        /// </summary>
        [JsonIgnore]
        public int FCost { get { return GCost + HCost; } }

        //==================== CTOR ====================
        public HexTile() { }

        public HexTile(int x, int y, double factor = 25.0) : this() { Column = x; Row = y; SizeFactor = factor; }

        public HexTile(int x, int y, TileType type, double factor = 25.0) : this(x, y, factor) { Type = type; }

        //==================== Methods ====================
        public void InitGraphics() {
            Polygon p = new Polygon();
            p.Stroke = Brushes.Black;
            p.Fill = Colors[_Type];
            p.StrokeThickness = 1;
            p.HorizontalAlignment = HorizontalAlignment.Left;
            p.VerticalAlignment = VerticalAlignment.Center;
            p.Name = $"X{Column}Y{Row}";

            //var factor = 50;
            var hexWidth = SizeFactor * Math.Sqrt(3) * (3 / 2); ;
            var hexHeight = SizeFactor * 2;


            p.Points = new PointCollection() {
                new Point((0), (0.25*hexHeight)),
                new Point((0), (0.75*hexHeight)),
                new Point((0.5 * hexWidth), (hexHeight) ),
                new Point((hexWidth), (0.75*hexHeight)),
                new Point((hexWidth), (0.25*hexHeight)),
                new Point((0.5 * hexWidth), (0)),
            };

            var posY = Row > 0 ? ((Row * hexHeight) - (Row * hexHeight / 4)) : (Row * hexHeight);
            //var posX = x % 2 == 1 ? (x * hexWidth) + hexWidth / 2 : (x * hexWidth);
            var posX = Row % 2 == 1 ? (Column * hexWidth) + hexWidth / 2 : (Column * hexWidth);

            Canvas.SetLeft(p, posX);
            Canvas.SetTop(p, posY);

            Poly = p;

            var fontSize = SizeFactor/2;

            lblG.FontSize = fontSize;
            lblG.Width = lblF.Width = lblH.Width = hexWidth;

            lblG.IsHitTestVisible = lblH.IsHitTestVisible = lblF.IsHitTestVisible = false;
            //lblG.Height = lblF.Height = lblH.Height = 10;
            Canvas.SetLeft(lblG, posX);
            Canvas.SetTop(lblG, posY + (hexHeight / 2) - fontSize*2 - 3);
            lblH.FontSize = fontSize;
            Canvas.SetLeft(lblH, posX);
            Canvas.SetTop(lblH, posY + (hexHeight / 2) - fontSize - 3);
            lblF.FontSize = fontSize;
            Canvas.SetLeft(lblF, posX);
            Canvas.SetTop(lblF, posY + (hexHeight / 2) + fontSize - fontSize - 3);

            //Canvas.Children.Add(p);

            Ellipse el = new Ellipse();
            el.Fill = Brushes.Black;
            el.StrokeThickness = 2;
            el.Width = 4;
            el.Height = 4;

            el.SetValue(Canvas.LeftProperty, (posX - (el.Width / 2) + (hexWidth / 2)));
            el.SetValue(Canvas.TopProperty, (posY - (el.Height / 2) + (hexHeight / 2)));

            CenterPoint = el;

            //Canvas.Children.Add(el);
        }

        public void SetColor(Brush brush) { Poly.Fill = brush; }

        public void SetColor(TileType type) { if (Poly != null) { Poly.Fill = Colors[Type]; } }

        public int GetMovementPenalty() => Penalty[Type];

        public Vector3 ToCubicCoordinates() {

            //x = col - (row - (row & 1)) / 2
            //z = row
            //y = -x - z

            var vec = new Vector3();
            vec.X = Column - (Row - (Row&1)) / 2;
            vec.Y = Row;
            vec.Z = -vec.X - vec.Y;
            return vec;
        }

        public void SetAsStart() { Type = TileType.Start; }
        public void SetAsEnd() { Type = TileType.Target; }

        public void ResetStartEnd() { Type = TileType.Normal; }

        //==================== operators ====================
        public static HexTile operator +(HexTile a, HexTile b) => new HexTile((a.Column + b.Column), (a.Row + b.Row));

        public static HexTile operator -(HexTile a, HexTile b) => new HexTile((a.Column - b.Column), (a.Row - b.Row));

        public static bool operator ==(HexTile a, HexTile b) {
            if (ReferenceEquals(a, null)) { return ReferenceEquals(b, null); }
            if (ReferenceEquals(b, null)) { return false; }

            return (a.Column == b.Column && a.Row == b.Row);
        }

        public static bool operator !=(HexTile a, HexTile b) => !(a == b);

        //==================== overrides ====================
        public override string ToString() => $"Hex X:{Column} Y:{Row} || G:{GCost} H:{HCost} = F:{FCost}";

        public override bool Equals(object obj) {
            if(this == null && obj == null) { return true; }
            if(this == null && obj != null) { return true; }
            var tile = obj as HexTile;
            return this == tile;
        }

        public override int GetHashCode() { return base.GetHashCode(); }

        //==================== Statics ====================
        public static int GetMovementePenalty(TileType type) => Penalty[type];

    }

    public enum TileType { Normal, NonWalkable, Start, Target, Path, Closed, Open, Water, PathWater, OpenWater, ClosedWater }
}
