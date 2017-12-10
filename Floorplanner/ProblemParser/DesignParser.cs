using Floorplanner.Models.Components;
using Floorplanner.Models.Solver;
using System.Collections.Generic;

namespace Floorplanner.ProblemParser
{
    public static class DesignParser
    {
        public static readonly string AmplPath = @".\Ampl\ampl.exe";

        public static readonly string AmplDir = @".\Ampl";

        public static readonly string RunIncipit = "reset;option solver couenne;model '{0}';solve;";

        public readonly static char _separator = char.Parse(" ");

        public static Dictionary<BlockType, int> EmptyResources()
        {
            return new Dictionary<BlockType, int>()
            {
                { BlockType.CLB, 0 },
                { BlockType.BRAM, 0 },
                { BlockType.DSP, 0 },
                { BlockType.Forbidden, 0 },
                { BlockType.Null, 0 }
            };
        }

        public static Direction Opposite(this Direction direction)
        {
            return (Direction)(((int)direction + 2) % 4);
        }
    }
}
