using Floorplanner.Models;
using Floorplanner.Models.Components;
using Floorplanner.Models.Solver;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Floorplanner
{
    public static class FPHelper
    {
        public readonly static char _separator = char.Parse(" ");

        public static readonly long MagicLongSeed = -5315112344061764177;

        public static readonly int MagicIntSeed = 689465938;

        /// <summary>
        /// Shuffles the element order of the specified list.
        /// </summary>
        public static void Shuffle<T>(this IList<T> ts, int seed)
        {
            var count = ts.Count;
            var last = count - 1;

            Random rnd = new Random(seed);

            for (var i = 0; i < last; ++i)
            {
                var r = rnd.Next(i, count);
                var tmp = ts[i];
                ts[i] = ts[r];
                ts[r] = tmp;
            }
        }

        public static void Disrupt(this Area a, IList<Area> unconfirmed)
        {
            a.IsConfirmed = false;
            unconfirmed.Add(a);
        }

        public static int GetCost(this IDictionary<BlockType, int> resources, Costs costs) => 
            resources.Sum(kv => kv.Value * costs.ResourceWeight[kv.Key]);

        /// <summary>
        /// Return a new empty resources dictionary
        /// </summary>
        public static Dictionary<BlockType, int> EmptyResources
        {
            get => new Dictionary<BlockType, int>()
            {
                { BlockType.CLB, 0 },
                { BlockType.BRAM, 0 },
                { BlockType.DSP, 0 },
                { BlockType.Forbidden, 0 },
                { BlockType.Null, 0 }
            };
        }

        public static IDictionary<K, int> Sum<K>(this IDictionary<K, int> dict, IDictionary<K, int> other)
            => dict.ToDictionary(kv => kv.Key, kv => kv.Value + other[kv.Key]);

        public static IDictionary<K, int> Sub<K>(this IDictionary<K, int> dict, IDictionary<K, int> other)
            => dict.ToDictionary(kv => kv.Key, kv => kv.Value - other[kv.Key]);

        public static Direction Opposite(this Direction direction)
        {
            return (Direction)(((int)direction + 2) % 4);
        }
    }
}
