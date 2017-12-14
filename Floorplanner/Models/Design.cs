using Floorplanner.Models.Components;
using System.Collections.Generic;
using System.IO;

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

            design.ID = designFileContent.ReadLine().Trim(FPHelper._separator);

            design.Costs = Costs.Parse(designFileContent);

            design.FPGA = FPGA.Parse(designFileContent);

            int regions = int.Parse(designFileContent.ReadLine());

            design.Regions = new Region[regions];

            for (int i = 0; i < regions; i++)
                design.Regions[i] = Region.Parse(designFileContent, i);

            design.RegionWires = new int[regions, regions];

            for(int i = 0; i < regions; i++)
            {
                string[] currentRow = designFileContent.ReadLine().Split(FPHelper._separator);
                for (int j = 0; j < regions; j++)
                    design.RegionWires[i, j] = int.Parse(currentRow[j]);
            }

            return design;
        }

        public int Compare(Region x, Region y)
        {
            if (x.ID == y.ID) return 0;

            double xCost = GetRegionWeight(x);
            double yCost = GetRegionWeight(y);

            if(xCost == yCost)
            {
                xCost -= x.ID;
                yCost -= y.ID;
            }

            return xCost > yCost ? -1
                : 1;
        }

        /// <summary>
        /// Calculate an internal weight for the given region, useful to create a hierarchy among regions.
        /// The more weight, the more a region is resource demanding
        /// </summary>
        /// <param name="x">Region to calculate score for.</param>
        /// <returns>Internal region weight.</returns>
        public double GetRegionWeight(Region x)
        {
            if (x.IOConns.Length > 0)
                return double.MaxValue - x.ID;

            double interConnScore = 0;

            for (int i = 0; i < RegionWires.GetLength(1); i++)
                interConnScore += RegionWires[x.ID, i];

            double resourcesScore = x.Resources[BlockType.CLB] * Costs.ResourceWeight[BlockType.CLB]
                + x.Resources[BlockType.BRAM] * FPGA.CLBratioBRAM * Costs.ResourceWeight[BlockType.BRAM]
                + x.Resources[BlockType.DSP] * FPGA.CLBratioDSP * Costs.ResourceWeight[BlockType.DSP];

            double recMult = x.Type == RegionType.Reconfigurable ? 1.2 : 1;

            return recMult * resourcesScore * Costs.Area;// + interConnScore * Costs.WireLength * 0.5;
        }
    }
}
