using Floorplanner.Models.Components;
using Floorplanner.ProblemParser;
using System.IO;

namespace Floorplanner.Models
{
    public class Design
    {
        public string ID { get; set; }

        public Costs Costs { get; set; }

        public FPGA FPGA { get; set; }

        public Region[] Regions { get; set; }

        public int[,] RegionWires { get; set; }

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
    }
}
