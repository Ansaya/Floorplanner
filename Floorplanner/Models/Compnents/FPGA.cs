using Floorplanner.Models.Solver;
using System;
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

        public IDictionary<BlockType, int> Resources { get => _resourcesFromOrigin[Xmax + 1, Ymax + 1]; }
        
        private IEnumerable<Point> _validPoints;

        public IEnumerable<Point> ValidPoints { get => _validPoints; }

        private IDictionary<BlockType, int>[,] _resourcesFromOrigin;
        
        public static FPGA Parse(TextReader atFPGADesign)
        {
            FPGA fpga = new FPGA();

            // Read rows and columns number
            string[] rowCOL = atFPGADesign.ReadLine().Split(FPHelper._separator);
            int rows = int.Parse(rowCOL[0]);
            int cols = int.Parse(rowCOL[1]);

            // Read tile height
            fpga.TileHeight = int.Parse(atFPGADesign.ReadLine());

            // Initialize all FPGA blocks
            fpga.Design = new BlockType[rows, cols];

            IDictionary<BlockType, int> res = FPHelper.EmptyResources;
            IList<Point> validPoints = new List<Point>();
            IDictionary<BlockType, int>[,] resFromOrigin = new Dictionary<BlockType, int>[cols + 1,rows + 1];
            for(int i = 0; i < Math.Max(rows, cols) + 1; i++)
            {
                if (i <= rows)
                    resFromOrigin[0, i] = FPHelper.EmptyResources;

                if(i <= cols)
                    resFromOrigin[i, 0] = FPHelper.EmptyResources;
            }

            for (int r = 0; r < rows; r++)
            {
                string[] currentRow = atFPGADesign.ReadLine().Split(FPHelper._separator);
                for (int c = 0; c < cols; c++)
                {
                    BlockType bt = (BlockType)currentRow[c][0];

                    fpga.Design[r, c] = bt;
                    res[bt]++;

                    // Area valid points pre-enumeration
                    if (bt != BlockType.Forbidden && bt != BlockType.Null)
                        validPoints.Add(new Point(c, r));

                    // Area resources pre-calculation
                    if(r == 0)
                    {
                        if(c == 0)
                            resFromOrigin[1, 1] = FPHelper.EmptyResources;
                        else
                            resFromOrigin[c + 1, 1] = new Dictionary<BlockType, int>(resFromOrigin[c, 1]);
                    }
                    else
                    {
                        if (c == 0)
                            resFromOrigin[1, r + 1] = new Dictionary<BlockType, int>(resFromOrigin[1, r]);
                        else
                            resFromOrigin[c + 1, r + 1] = resFromOrigin[c, r + 1]
                                .Sum(resFromOrigin[c + 1, r])
                                .Sub(resFromOrigin[c, r]);
                    }

                    resFromOrigin[c + 1, r + 1][bt]++;
                }                  
            }

            fpga._validPoints = validPoints;
            fpga._resourcesFromOrigin = resFromOrigin;

            fpga.CLBratioBRAM = (double)res[BlockType.CLB] / res[BlockType.BRAM];
            fpga.CLBratioDSP = (double)res[BlockType.CLB] / res[BlockType.DSP];

            // Read reconfigurable regions boundaries
            string[] lrecCol = atFPGADesign.ReadLine().Split(FPHelper._separator);
            string[] rrecCol = atFPGADesign.ReadLine().Split(FPHelper._separator);

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
        
        /// <summary>
        /// Return resource quantity for the specified area.
        /// </summary>
        /// <param name="a">Area to get resources for.</param>
        /// <returns>Area available resources.</returns>
        public IDictionary<BlockType, int> ResourcesFor(Area a)
        {
            int startX = (int)a.TopLeft.X;
            int startY = (int)a.TopLeft.Y;
            int endX = startX + a.Width + 1;
            int endY = startY + a.Height + 1;

            return _resourcesFromOrigin[startX, startY]
                .Sum(_resourcesFromOrigin[endX, endY])
                .Sub(_resourcesFromOrigin[endX, startY])
                .Sub(_resourcesFromOrigin[startX, endY]);
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
