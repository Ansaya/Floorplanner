using Floorplanner.Models.Components;
using System.IO;
using System.Linq;

namespace Floorplanner.Models
{
    public class Design
    {
        public string ID { get; private set; }

        public Costs Costs { get; private set; }

        public FPGA FPGA { get; private set; }

        public Region[] Regions { get; private set; }

        public int[,] RegionWires { get; private set; }

        private bool _isFeasible;

        public bool IsFeasible { get => _isFeasible; }

        private string _statString;

        public string Stats { get => _statString; }

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

            int clbs = design.FPGA.Resources[BlockType.CLB];
            int brams = design.FPGA.Resources[BlockType.BRAM];
            int dsps = design.FPGA.Resources[BlockType.DSP];

            int rclbs = design.Regions.Sum(r => r.Resources[BlockType.CLB]);
            int rbrams = design.Regions.Sum(r => r.Resources[BlockType.BRAM]);
            int rdsps = design.Regions.Sum(r => r.Resources[BlockType.DSP]);

            double resRatio = (double)(rclbs + rbrams + rdsps) / (clbs + brams + dsps);

            design._isFeasible = clbs >= rclbs && brams >= rbrams && dsps >= rdsps;

            design._statString = $"Requires to allocate {design.Regions.Length} regions.\n" +
                $"\t{design.Regions.Count(r => r.IOConns.Any())} regions have I/O connections\n" +
                $"\t{design.Regions.Count(r => r.Type == RegionType.Static)} static\t" +
                $"\t{design.Regions.Count(r => r.Type == RegionType.Reconfigurable)} reconfigurable\n" +
                $"\tAll regions requires {resRatio:P2} of total resources.\n" +
                $"\t{rclbs}/{clbs} CLB\t{rbrams}/{brams} BRAM\t{rdsps}/{dsps} DSP  ";

            return design;
        }
    }
}
