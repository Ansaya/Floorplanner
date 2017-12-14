using Floorplanner.Models.Solver;
using System.IO;

namespace Floorplanner.Models
{
    public class IOConn
    {
        public Point Point
        {
            get
            {
                return new Point(_column, _row);
            }
        }

        private int _column;

        private int _row;

        public int Wires { get; private set; }

        public static IOConn Parse(TextReader atConn)
        {
            string[] colRowWire = atConn.ReadLine().Split(FPHelper._separator);

            return new IOConn()
            {
                _column = int.Parse(colRowWire[0]) - 1,
                _row = int.Parse(colRowWire[1]) - 1,
                Wires = int.Parse(colRowWire[2])
            };
        }

    }
}
