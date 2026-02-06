using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Labyrinth.DomainServices
{
    public sealed class KnownCell
    {
        public int X { get; }
        public int Y { get; }

        public KnownCellType Type { get; set; } = KnownCellType.Unknown;

        public bool Visited { get; set; }

        public KnownCell(int x, int y)
        {
            X = x;
            Y = y;
        }
    }    
}

