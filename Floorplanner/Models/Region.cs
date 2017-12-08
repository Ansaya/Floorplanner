using Floorplanner.ProblemParser;
using System.IO;

namespace Floorplanner.Models
{
    public class Region
    {
        public RegionType Type { get; private set; }

        public int CLB { get; private set; }

        public int DLP { get; private set; }

        public int BRAM { get; private set; }

        public IOConn[] IOConns { get; private set; }

        public static Region Parse(TextReader atRegion)
        {
            Region region = new Region();

            string[] regionData = atRegion.ReadLine().Split(DesignParser._separator);
            
            region.Type = (RegionType)regionData[0][0];

            region.CLB = int.Parse(regionData[1]);
            region.BRAM = int.Parse(regionData[2]);
            region.DLP = int.Parse(regionData[3]);

            int ios = int.Parse(regionData[4]);

            region.IOConns = new IOConn[ios];

            for (int i = 0; i < ios; i++)
                region.IOConns[i] = IOConn.Parse(atRegion);

            return region;
        }
    }
}
