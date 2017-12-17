using Floorplanner.Models.Components;
using System.Collections.Generic;
using System.IO;

namespace Floorplanner.Models
{
    public class Costs
    {
        public int MaxScore { get; private set; }

        public int AreaWeight { get; private set; }

        public int WireWeight { get; private set; }

        public IReadOnlyDictionary<BlockType, int> ResourceWeight { get; private set; }

        public Costs(int maxScore, int areaWeight, int wireWeight, int clb, int bram, int dsp)
        {
            MaxScore = maxScore;
            AreaWeight = areaWeight;
            WireWeight = wireWeight;
            ResourceWeight = new Dictionary<BlockType, int>()
            {
                { BlockType.CLB, clb },
                { BlockType.BRAM, bram },
                { BlockType.DSP, dsp },
                { BlockType.Forbidden, 0 },
                { BlockType.Null, 0 }
            };
        }

        private Costs() { }

        public static Costs Parse(TextReader atCosts)
        {
            string[] scoreAreaWirelength = atCosts.ReadLine().Split(FPHelper._separator);
            string[] clbBRAMdlpCosts = atCosts.ReadLine().Split(FPHelper._separator);

            var resW = new Dictionary<BlockType, int>()
            {
                { BlockType.CLB, int.Parse(clbBRAMdlpCosts[0]) },
                { BlockType.BRAM, int.Parse(clbBRAMdlpCosts[1]) },
                { BlockType.DSP, int.Parse(clbBRAMdlpCosts[2]) },
                { BlockType.Forbidden, 0 },
                { BlockType.Null, 0 }
            };

            return new Costs()
            {
                MaxScore = int.Parse(scoreAreaWirelength[0]),
                AreaWeight = int.Parse(scoreAreaWirelength[1]),
                WireWeight = int.Parse(scoreAreaWirelength[2]),
                ResourceWeight = resW
            };
        }
    }
}
