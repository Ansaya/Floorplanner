using Floorplanner.Models;
using Floorplanner.Models.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Floorplanner.ProblemParser
{
    public static class DesignParser
    {
        public static readonly string WolframAPIKey = "7YPPW7-Q635EGLP9L";

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
    }
}
