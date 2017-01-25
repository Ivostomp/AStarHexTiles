using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AStarHexTiles
{
    public class HexMap
    {
        public int Rows { get; set; }
        public int Columns { get; set; }
        public int Seed { get; set; }
        public List<HexTile> Tiles { get; set; }

        public HexMap() {  }

        public HexMap(int rows, int columns) : this() { Rows = rows; Columns = columns; }

        public HexMap(int rows, int columns, IEnumerable<HexTile> tiles) : this(rows, columns) { Tiles = tiles.ToList(); }

    }
}
