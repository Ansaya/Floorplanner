using Floorplanner.Models.Components;
using System.Collections.Generic;
using System.IO;

namespace Floorplanner.Models
{
    public class Region
    {
        public int ID { get; private set; }

        public RegionType Type { get; private set; }

        public IDictionary<BlockType, int> Resources { get; private set; }

        public double CLBratioBRAM { get; private set; }

        public double CLBratioDSP { get; private set; }

        public IOConn[] IOConns { get; private set; }

        public static Region Parse(TextReader atRegion, int regionID)
        {
            Region region = new Region();
            region.ID = regionID;

            string[] regionData = atRegion.ReadLine().Split(FPHelper._separator);
            
            region.Type = (RegionType)regionData[0][0];

            Dictionary<BlockType, int> res = FPHelper.EmptyResources;
            res[BlockType.CLB] = int.Parse(regionData[1]);
            res[BlockType.BRAM] = int.Parse(regionData[2]);
            res[BlockType.DSP] = int.Parse(regionData[3]);

            region.Resources = res;
            region.CLBratioBRAM = (double)res[BlockType.CLB] / res[BlockType.BRAM];
            region.CLBratioDSP = (double)res[BlockType.CLB] / res[BlockType.DSP];

            int ios = int.Parse(regionData[4]);

            region.IOConns = new IOConn[ios];

            for (int i = 0; i < ios; i++)
                region.IOConns[i] = IOConn.Parse(atRegion);

            return region;
        }
    }
}
