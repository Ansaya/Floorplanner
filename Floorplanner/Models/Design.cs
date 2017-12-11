using Floorplanner.Models.Components;
using Floorplanner.ProblemParser;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Floorplanner.Models
{
    public class Design : IComparer<Region>
    {
        public string ID { get; private set; }

        public Costs Costs { get; private set; }

        public FPGA FPGA { get; private set; }

        public Region[] Regions { get; private set; }

        public int[,] RegionWires { get; private set; }

        public static Design Parse(TextReader designFileContent)
        {
            Design design = new Design();

            design.ID = designFileContent.ReadLine().Trim(DesignParser._separator);

            design.Costs = Costs.Parse(designFileContent);

            design.FPGA = FPGA.Parse(designFileContent);

            int regions = int.Parse(designFileContent.ReadLine());

            design.Regions = new Region[regions];

            for (int i = 0; i < regions; i++)
                design.Regions[i] = Region.Parse(designFileContent, i);

            design.RegionWires = new int[regions, regions];

            for(int i = 0; i < regions; i++)
            {
                string[] currentRow = designFileContent.ReadLine().Split(DesignParser._separator);
                for (int j = 0; j < regions; j++)
                    design.RegionWires[i, j] = int.Parse(currentRow[j]);
            }

            return design;
        }

        public int Compare(Region x, Region y)
        {
            double xScore = GetScore(x);
            double yScore = GetScore(y);

            return xScore >= yScore ? -1
                : 1;
        }

        /// <summary>
        /// Calculate an internal score for the given region, useful to create a hierarchy among regions.
        /// </summary>
        /// <param name="x">Region to calculate score for.</param>
        /// <returns>Internal region score.</returns>
        public double GetScore(Region x)
        {
            int rIndex = 0;

            while (Regions[rIndex] != x) rIndex++;

            double interConnScore = x.IOConns.Length == 0 ? 0
                : x.IOConns.Sum(conn => conn.Wires);

            for (int i = 0; i < RegionWires.GetLength(1); i++)
                interConnScore += RegionWires[rIndex, i];

            double resourcesScore = x.Resources[BlockType.CLB] * Costs.ResourceWeight[BlockType.CLB]
                + x.Resources[BlockType.BRAM] * FPGA.CLBratioBRAM * Costs.ResourceWeight[BlockType.BRAM]
                + x.Resources[BlockType.DSP] * FPGA.CLBratioDSP * Costs.ResourceWeight[BlockType.DSP];

            return resourcesScore * Costs.Area + interConnScore * Costs.WireLength;
        }
    }
}
