using Floorplanner.ProblemParser;
using System.IO;

namespace Floorplanner.Models.Components
{
    public class FPGA
    {
        public BlockType[,] Design { get; private set; }

        public bool[] LRecCol { get; private set; }

        public bool[] RRecCol { get; private set; }

        public int TileHeight { get; private set; }

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
            for (int r = 0; r < rows; r++)
            {
                string[] currentRow = atFPGADesign.ReadLine().Split(DesignParser._separator);
                for (int c = 0; c < cols; c++)
                    fpga.Design[r, c] = (BlockType)currentRow[c][0];                    
            }

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
    }
}
