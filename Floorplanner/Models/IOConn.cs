using Floorplanner.ProblemParser;
using System.IO;

namespace Floorplanner.Models
{
    public class IOConn
    {
        public int Column { get; private set; }

        public int Row { get; private set; }

        public int Wires { get; private set; }

        public static IOConn Parse(TextReader atConn)
        {
            string[] colRowWire = atConn.ReadLine().Split(DesignParser._separator);

            return new IOConn()
            {
                Column = int.Parse(colRowWire[0]) - 1,
                Row = int.Parse(colRowWire[1]) - 1,
                Wires = int.Parse(colRowWire[2])
            };
        }

    }
}
