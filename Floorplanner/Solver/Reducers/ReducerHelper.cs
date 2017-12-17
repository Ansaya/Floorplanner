using Floorplanner.Models;
using Floorplanner.Models.Components;

namespace Floorplanner.Solver.Reducers
{
    public static class ReducerHelper
    {

        public static Costs ToNonZero(this Costs costs)
        {
            int wW = costs.WireWeight, aW = costs.AreaWeight;
            int clb = costs.ResourceWeight[BlockType.CLB];
            int bram = costs.ResourceWeight[BlockType.BRAM];
            int dsp = costs.ResourceWeight[BlockType.DSP];

            if(aW == 0)
            {
                aW = wW;
                clb = 10;
                bram = 20;
                dsp = 25;
            }

            if (clb == 0)
                clb = (int)(bram > dsp ? bram / 2.5 : dsp / 2.5);

            return new Costs(costs.MaxScore, aW, wW, clb, bram, dsp);
        }

    }
}
