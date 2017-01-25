using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shapes;

namespace AStarHexTiles
{
    public class ControlPoint
    {
        public string Name { get; set; }

        public Point Point { get; set; }

        public Ellipse Ellipse { get; set; }

        public ControlPoint() {  }
        public ControlPoint(string name) { Name = name; }
        public ControlPoint(Point point, Ellipse ellipse) { Point = point; Ellipse = ellipse; }
        public ControlPoint(string name, Point point, Ellipse ellipse) : this(name) { Point = point; Ellipse = ellipse; }
    }
}
