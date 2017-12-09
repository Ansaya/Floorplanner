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
                design.Regions[i] = Region.Parse(designFileContent);

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
            int xScore = GetScore(x);
            int yScore = GetScore(y);

            return xScore == yScore ? 0
                : xScore > yScore ? 1
                : -1;
        }

        public int GetScore(Region x)
        {
            int rIndex = 0;

            while (Regions[rIndex] != x) rIndex++;

            int interConnScore = 0;

            for (int i = 0; i < RegionWires.GetLength(1); i++)
                interConnScore += RegionWires[rIndex, i];

            return x.Resources.Sum(pari => pari.Value * Costs.ResourceWeight[pari.Key]) * Costs.Area
                + (x.IOConns.Sum(conn => conn.Wires) + interConnScore) * Costs.WireLength;
        }
    }
}
