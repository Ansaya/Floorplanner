using Floorplanner.ProblemParser;
using System.IO;

namespace Floorplanner.Models
{
    public class Costs
    {
        public int MaxScore { get; private set; }

        public int Area { get; private set; }

        public int WireLength { get; private set; }

        public int CLB { get; private set; }

        public int DLP { get; private set; }

        public int BRAM { get; private set; }

        public static Costs Parse(TextReader atCosts)
        {
            string[] scoreAreaWirelength = atCosts.ReadLine().Split(DesignParser._separator);
            string[] clbBRAMdlpCosts = atCosts.ReadLine().Split(DesignParser._separator);

            return new Costs()
            {
                MaxScore = int.Parse(scoreAreaWirelength[0]),
                Area = int.Parse(scoreAreaWirelength[1]),
                WireLength = int.Parse(scoreAreaWirelength[2]),
                CLB = int.Parse(clbBRAMdlpCosts[0]),
                BRAM = int.Parse(clbBRAMdlpCosts[1]),
                DLP = int.Parse(clbBRAMdlpCosts[2])
            };
        }
    }
}
