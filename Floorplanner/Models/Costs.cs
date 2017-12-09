using Floorplanner.Models.Components;
using Floorplanner.ProblemParser;
using System.Collections.Generic;
using System.IO;

namespace Floorplanner.Models
{
    public class Costs
    {
        public int MaxScore { get; private set; }

        public int Area { get; private set; }

        public int WireLength { get; private set; }

        public IReadOnlyDictionary<BlockType, int> ResourceWeight { get; private set; }

        public static Costs Parse(TextReader atCosts)
        {
            string[] scoreAreaWirelength = atCosts.ReadLine().Split(DesignParser._separator);
            string[] clbBRAMdlpCosts = atCosts.ReadLine().Split(DesignParser._separator);

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
                Area = int.Parse(scoreAreaWirelength[1]),
                WireLength = int.Parse(scoreAreaWirelength[2]),
                ResourceWeight = resW
            };
        }
    }
}
