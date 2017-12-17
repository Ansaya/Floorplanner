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

        /// <summary>
        /// Shuffles the element order of the specified list.
        /// </summary>
        public static void Shuffle<T>(this IList<T> ts)
        {
            var count = ts.Count;
            var last = count - 1;

            Random rnd = new Random();

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

        public static readonly Func<int, int, int> add = (a, b) => a + b;

        public static readonly Func<int, int, int> sub = (a, b) => a - b;

        public static IDictionary<K, V> Merge<K, V>(
            this IDictionary<K, V> dict, 
            IDictionary<K, V> other, 
            Func<V, V, V> valueMerger) =>
            dict.Concat(other)
                .GroupBy(kv => kv.Key)
                .ToDictionary(
                    kvGroup => kvGroup.Key, 
                    kvGroup => kvGroup
                        .Select(kv => kv.Value)
                        .Aggregate(valueMerger));

        public static bool IsAdjacent(this Area x, Area y)
        {
            double xDiff = Math.Abs(x.Center.X - y.Center.X);
            double yDiff = Math.Abs(x.Center.Y - y.Center.Y);

            bool leftrightAdj = (x.Width + y.Width) / 2d == xDiff;
            bool updownAdj = (x.Height + y.Height) / 2d == yDiff;

            return leftrightAdj && yDiff <= 1
                || updownAdj && xDiff <= 1;
        }

        public static Direction Opposite(this Direction direction)
        {
            return (Direction)(((int)direction + 2) % 4);
        }
    }
}
