using Floorplanner.Models;
using Floorplanner.Models.Components;
using Floorplanner.Models.Solver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Floorplanner.Solver.Reducers
{
    public class RatioAreaReducer : IAreaReducer
    {
        private readonly double _arBRAMratio;

        private readonly double _arDSPRatio;

        private readonly int _rBRAMThres;

        private readonly int _rDSPThres;

        /// <summary>
        /// Scoring function for areas
        /// </summary>
        /// <param name="a">Area to score.</param>
        /// <param name="c">Costs values container.</param>
        /// <returns>Area total score.</returns>
        public Func<Area, Costs, int> CostFunction { get; set; } = 
            (Area a, Costs c) => c.AreaWeight != 0 ? a.GetCost(c)
                : a.GetCost(c.ToNonZero());

        public RatioAreaReducer(double arBRAMratio, double arDSPratio, int rBRAMThres = 3, int rDSPThres = 3)
        {
            _arBRAMratio = arBRAMratio;
            _arDSPRatio = arDSPratio;
            _rBRAMThres = rBRAMThres;
            _rDSPThres = rDSPThres;
        }

        /// <summary>
        /// Reduce given area cost as much as possible also trying to near area center to ideal one.
        /// </summary>
        /// <param name="area">Area to reduce.</param>
        /// <param name="idealCenter">Ideal center point for given area.</param>
        /// <param name="floorPlan">Floorplan whom given area belongs to.</param>
        /// <returns>Reduced area cost.</returns>
        public void Reduce(Area area, Point idealCenter, Floorplan floorPlan)
        {
            // If the area requires mostly homogeneous resources
            // try reducing with two different approaches and
            // return best result
            if(area.Region.Resources[BlockType.BRAM] <= _rBRAMThres 
                && area.Region.Resources[BlockType.DSP] <= _rDSPThres)
            {
                Area hetReduced = new Area(area);
                Area homReduced = new Area(area);

                Task<double> hetCost = Task.Factory
                    .StartNew(() => ReduceWithPolicy(hetReduced, idealCenter, floorPlan, HeterogeneousShrinkArbiter));

                Task<double> homCost = Task.Factory
                    .StartNew(() => ReduceWithPolicy(homReduced, idealCenter, floorPlan, HomogeneousShrinkArbiter));

                Task.WaitAll(hetCost, homCost);

                bool homHetReduce = hetCost.Result < homCost.Result;

                Area best = homHetReduce ? hetReduced : homReduced;

                area.Width = best.Width;
                area.Height = best.Height;
                area.MoveTo(best.TopLeft);

                //Console.WriteLine($"\t{(homHetReduce ? "Heterogeneous" : "Homogeneous")} reduce branch taken.");
            }

            //Console.WriteLine("\tHeterogeneous reduce branch taken.");

            // Else go with standard approach only
            ReduceWithPolicy(area, idealCenter, floorPlan, HeterogeneousShrinkArbiter);
        }

        private double ReduceWithPolicy(Area area, Point idealCenter, Floorplan floorPlan, Func<Area, Point, bool> shrinkArbiter)
        {
            // Initialize explored shrinking direction vector
            IDictionary<Direction, bool> exploredShrinkDir = new Dictionary<Direction, bool>();
            foreach (Direction d in Enum.GetValues(typeof(Direction)))
                exploredShrinkDir.Add(d, false);

            do
            {
                // Store current area position and dimensions
                Point oldTopLeft = new Point(area.TopLeft);
                int oldHeight = area.Height;
                int oldWidth = area.Width;

                // Chose a shrinking axis looking at area/region BRAM's and DSP's ratios
                // and check if chosen axis hasn't been completely explored yet
                bool widthHeightShrink = shrinkArbiter(area, idealCenter);

                widthHeightShrink = widthHeightShrink
                    && (!exploredShrinkDir[Direction.Up] || !exploredShrinkDir[Direction.Down]);

                Direction shrinkDir;

                // Chose a shrinking direction on chosen axis if possible
                if (!widthHeightShrink
                    && (!exploredShrinkDir[Direction.Left] || !exploredShrinkDir[Direction.Right]))
                    shrinkDir = idealCenter.X > area.Center.X ? Direction.Left : Direction.Right;
                else
                    shrinkDir = idealCenter.Y > area.Center.Y ? Direction.Down : Direction.Up;

                // If wanted direction has already been explored chose opposite one
                if (exploredShrinkDir[shrinkDir]) shrinkDir = shrinkDir.Opposite();

                // NOTE: at this point a valid direction is there for shure, else the loop
                //       would have exited

                do
                {
                    // Try reducing on chosen direction
                    // If reduction isn't possible or leads to an area with insufficient resources
                    if (!area.TryShape(Models.Solver.Action.Shrink, shrinkDir) || !area.IsSufficient)
                    {
                        // Set current direction as explored
                        exploredShrinkDir[shrinkDir] = true;

                        // Restore area placement and dimensions before wrong shrinking
                        area.MoveTo(oldTopLeft);
                        area.Height = oldHeight;
                        area.Width = oldWidth;

                        // And break
                        break;
                    }

                } while (!area.IsValid);

                // I'm not sure code up to here is correct, so better throw an exception
                // if something wrong after shrink loop
                if (!area.IsValid || !area.IsSufficient)
                    throw new Exception("Reduce function behaviour unexpected.");

                // TODO: check distance from ideal center to improve area if it is too long in width
                // TODO: check distance from ideal center to improve area if it is too high in height

            } while (exploredShrinkDir.Values.Any(explored => !explored));

            return CostFunction(area, floorPlan.Design.Costs);
        }

        /// <summary>
        /// Define on which axis to reduce the area
        /// True: reduce on Y axis
        /// False: reduce on X axis
        /// </summary>
        /// <param name="area"></param>
        /// <param name="idealCenter"></param>
        /// <returns>True if area should de reduced in height, false if in width.</returns>
        private bool HeterogeneousShrinkArbiter(Area area, Point idealCenter)
        {
            return (area.ResourceRatio[BlockType.DSP] <= _arDSPRatio
                    || area.ResourceRatio[BlockType.BRAM] <= _arBRAMratio);
        }

        /// <summary>
        /// Define on which axis to reduce the area
        /// True: reduce on Y axis
        /// False: reduce on X axis
        /// </summary>
        /// <param name="area"></param>
        /// <param name="idealCenter"></param>
        /// <returns>True if area should de reduced in height, false if in width.</returns>
        private bool HomogeneousShrinkArbiter(Area area, Point idealCenter)
        {
            double xDistance = idealCenter.X - area.Center.X;
            double yDistance = idealCenter.Y - area.Center.Y;

            return Math.Abs(yDistance) >= Math.Abs(xDistance);
        }
    }
}
