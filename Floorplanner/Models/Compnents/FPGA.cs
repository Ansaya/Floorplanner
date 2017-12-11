using Floorplanner.Models.Solver;
using Floorplanner.ProblemParser;
using System.Collections.Generic;
using System.IO;

namespace Floorplanner.Models.Components
{
    public class FPGA
    {
        /// <summary>
        /// FPGA block types for each position.
        /// </summary>
        public BlockType[,] Design { get; private set; }

        /// <summary>
        /// Maximum valid x value
        /// </summary>
        public int Xmax { get => Design.GetLength(1) - 1; }

        /// <summary>
        /// Maximum valid y value
        /// </summary>
        public int Ymax { get => Design.GetLength(0) - 1; }

        /// <summary>
        /// True where a reconfigurable region can start
        /// </summary>
        public bool[] LRecCol { get; private set; }
        
        /// <summary>
        /// True where a reconfigurable region can end
        /// </summary>
        public bool[] RRecCol { get; private set; }

        /// <summary>
        /// Height of tiles for this FPGA
        /// </summary>
        public int TileHeight { get; private set; }

        public double CLBratioBRAM { get; private set; }

        public double CLBratioDSP { get; private set; }

        public IEnumerable<Point> Points
        {
            get
            {
                IList<Point> covered = new List<Point>();

                for (int y = 0; y < Design.GetLength(0); y++)
                    for (int x = 0; x < Design.GetLength(1); x++)
                        covered.Add(new Point(x, y));

                return covered;
            }
        }

        public static FPGA Parse(TextReader atFPGADesign)
        {
            FPGA fpga = new FPGA();

            // Read rows and columns number
            string[] rowCOL = atFPGADesign.ReadLine().Split(DesignParser._separator);
            int rows = int.Parse(rowCOL[0]);
            int cols = int.Parse(rowCOL[1]);

            // Read tile height
            fpga.TileHeight = int.Parse(atFPGADesign.ReadLine());

            // Initialize all FPGA blocks
            fpga.Design = new BlockType[rows, cols];

            IDictionary<BlockType, int> res = DesignParser.EmptyResources();

            for (int r = 0; r < rows; r++)
            {
                string[] currentRow = atFPGADesign.ReadLine().Split(DesignParser._separator);
                for (int c = 0; c < cols; c++)
                {
                    BlockType bt = (BlockType)currentRow[c][0];

                    fpga.Design[r, c] = bt;
                    res[bt]++;
                }                  
            }

            fpga.CLBratioBRAM = (double)res[BlockType.CLB] / res[BlockType.BRAM];
            fpga.CLBratioDSP = (double)res[BlockType.CLB] / res[BlockType.DSP];

            // Read reconfigurable regions boundaries
            string[] lrecCol = atFPGADesign.ReadLine().Split(DesignParser._separator);
            string[] rrecCol = atFPGADesign.ReadLine().Split(DesignParser._separator);

            // Initialize all reconfigurable regions boundaries
            fpga.LRecCol = new bool[cols];
            fpga.RRecCol = new bool[cols];
            for (int c = 0; c < cols; c++)
            {
                fpga.LRecCol[c] = lrecCol[c] != "0";
                fpga.RRecCol[c] = rrecCol[c] != "0";
            }

            return fpga;
        }

        public bool Contains(Area a)
        {
            return 0 <= a.TopLeft.X && a.TopLeft.X + a.Width <= Xmax
                && 0 <= a.TopLeft.Y && a.TopLeft.Y + a.Height <= Ymax;
        }

        public bool Contains(Point p)
        {
            return 0 <= p.X && p.X <= Xmax
                && 0 <= p.Y && p.Y <= Ymax;
        }
    }
}
