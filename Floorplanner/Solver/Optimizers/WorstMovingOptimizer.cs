using Floorplanner.Models;
using Floorplanner.Models.Solver;
using Floorplanner.Solver.Placers;
using Floorplanner.Solver.Reducers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Floorplanner.Solver.Optimizers
{
    public class WorstMovingOptimizer : IFloorplanOptimizer
    {
        private readonly IAreaPlacer _nearestCenterPlacer;

        private readonly SolverTuning _st;

        private readonly IFloorplanOptimizer _afterDisruptOptimizer;

        private readonly bool _canPrint;

        /// <summary>
        /// Initialize a worst area optimizer instance.
        /// </summary>
        /// <param name="areaReducer">The reducer to be used for area placement. This object will be cloned and its cost function will be changed.</param>
        /// <param name="st">Solver settigs to be used.</param>
        /// <param name="afterDisruptOptimizer">Area optimizer to be used after worst area move to place back other moved areas.</param>
        /// <param name="verbose">True to print computating info, else false.</param>
        public WorstMovingOptimizer(
            IAreaReducer areaReducer,
            SolverTuning st,
            IFloorplanOptimizer afterDisruptOptimizer,
            bool verbose = false)
        {
            IAreaReducer bestAreaReducer = areaReducer.Clone();
            bestAreaReducer.CostFunction = (Area a, Floorplan f) => a.GetCost(f.Design.Costs.ToNonZero());
            _nearestCenterPlacer = new NearestCenterPlacer(bestAreaReducer);
            _st = st;
            _afterDisruptOptimizer = afterDisruptOptimizer;
            _canPrint = verbose;
        }

        public Floorplan OptimizePlacement(Floorplan startingPlan, CancellationToken ct)
        {
            int fpScore = startingPlan.GetScore();

            IList<Area> worstAreas = startingPlan.Areas
                .OrderByDescending(a => GetCostFor(a, startingPlan))
                .ToList();

            foreach(Area worstArea in worstAreas)
            {
                Floorplan fp = new Floorplan(startingPlan);

                if (_canPrint) Console.WriteLine($"\tReallocating region {worstArea.Region.ID}");

                // Calculate ideal center
                double bestX = 0;
                double bestY = 0;
                int wires = 0;

                // Areas interconnections wiring
                foreach (Area a in fp.Areas)
                {
                    int aWires = fp.Design.RegionWires[a.ID, worstArea.ID] + fp.Design.RegionWires[worstArea.ID, a.ID];
                    bestX += aWires * a.Center.X;
                    bestY += aWires * a.Center.Y;
                    wires += aWires;
                }

                // I/O connections wiring
                foreach(IOConn io in worstArea.Region.IOConns)
                {
                    bestX += io.Wires * io.Point.X;
                    bestY += io.Wires * io.Point.Y;
                    wires += io.Wires;
                }

                Point idealCenter = new Point(bestX / wires, bestY / wires);

                IList<Area> fixedAreas = new List<Area>();

                while(fixedAreas.Count < fp.Areas.Count - 1)
                {
                    // Remove each area from current floorplan
                    foreach (Area a in fp.Areas.Except(fixedAreas)) a.IsConfirmed = false;

                    // Place worst area in ideal position
                    _nearestCenterPlacer.PlaceArea(worstArea, fp, idealCenter);
                    worstArea.IsConfirmed = true;

                    // Confirm back every non overlapping area
                    foreach (Area a in fp.Areas)
                    {
                        if (a.ID != worstArea.ID)
                        {
                            if (a.IsOverlapping(worstArea))
                                fixedAreas.Add(a);
                            else
                                a.IsConfirmed = true;
                        }
                    }

                    // If all areas have been reconfirmed after new placement
                    // go ahead with new plan comparison
                    if (fp.IsConfirmed)
                        break;
                    else // Else if any area remains unconfirmed try place it somewhere else
                    {
                        try
                        {
                            fp = _afterDisruptOptimizer.OptimizePlacement(fp, ct);
                            break;
                        }
                        catch (OptimizationException) { }
                    }
                }

                // If cancellation requested return starting plan
                if (ct.IsCancellationRequested) return startingPlan;

                // If new placement for current worst region can't confirm all areas
                // no comparison can be made, so go on with next region
                if(fp.Areas.Any(a => !a.IsConfirmed))
                {
                    if (_canPrint)
                        Console.WriteLine($"\tCouldn't reallcoate region {worstArea.Region.ID}");

                    continue;
                }
                
                // If a plan is found, compare with current best
                // and return if it is better
                int newScore = fp.GetScore();
                if(newScore > fpScore)
                {
                    if (_canPrint) Console.WriteLine($"\tFloorplan improved at {newScore:N0}.");
                    return fp;
                } 
            }

            // If no better plan has been found after moving all areas return starting plan
            return startingPlan;
        }

        /// <summary>
        /// Return cost for specified area and related connections only on given floorplan.
        /// (All areas are considered as confirmed without overlapping/validity checks)
        /// </summary>
        /// <param name="a">Area to get cost for.</param>
        /// <param name="fp">Floorplan to consider for connection distances.</param>
        /// <returns>Given area score.</returns>
        private int GetCostFor(Area a, Floorplan fp)
        {
            // Area cost
            int cost = a.GetCost(fp.Design.Costs);

            // I/O connections cost
            cost += (int)a.Region.IOConns.Sum(io => io.Point.ManhattanFrom(a.Center) * io.Wires);

            // Interconnections cost
            for (int i = 0; i < fp.Design.RegionWires.GetLength(0); i++)
            {
                Area current = fp.Areas.Single(ar => ar.ID == i);

                cost += (fp.Design.RegionWires[a.ID, i] + fp.Design.RegionWires[i, a.ID])
                    * (int)current.Center.ManhattanFrom(a.Center);
            }

            return cost;
        }
    }
}
