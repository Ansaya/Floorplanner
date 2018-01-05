using Floorplanner.Models.Components;
using Floorplanner.Models.Solver;
using Floorplanner.Solver.Disruptors;
using Floorplanner.Solver.Placers;
using Floorplanner.Solver.Reducers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Floorplanner.Solver.Optimizers
{
    public class FirstPlacementOptimizer : IFloorplanOptimizer
    {
        private readonly IAreaPlacer _areaPlacer;

        private readonly IAreaReducer _areaReducer;

        private readonly IAreaDisruptor _areaDisruptor;

        private readonly SolverTuning _st;

        public FirstPlacementOptimizer(IAreaPlacer areaPlacer, IAreaReducer areaReducer, IAreaDisruptor areaDisruptor, SolverTuning st)
        {
            _areaPlacer = areaPlacer;
            _areaReducer = areaReducer;
            _areaDisruptor = areaDisruptor;
            _st = st;
        }

        public Floorplan OptimizePlacement(Floorplan startingPlan, CancellationToken ct)
        {
            // Only first call when no valid plan has been found
            // is a sequentail call and can print what's happening
            bool canPrint = ct == CancellationToken.None;

            Floorplan firstPlan = new Floorplan(startingPlan);
            int failDisrupt = _st.MaxDisruption;
            int caosFactor = _st.CaosFactor;

            int minLeftRegions = firstPlan.Areas.Count;
            Floorplan currentBest = firstPlan;

            if (canPrint)
                Console.WriteLine("Starting region placement...");

            IList<Area> unconfirmed = new List<Area>(firstPlan.Areas.Where(a => !a.IsConfirmed));

            // Place and disrupt areas until they are all placed in some position
            while (unconfirmed.Count > 0 && !ct.IsCancellationRequested)
            {
                unconfirmed = unconfirmed
                    .OrderByDescending(a => a.Region.Resources[BlockType.DSP])
                    .ThenByDescending(a => a.Region.Resources[BlockType.BRAM])
                    .ThenByDescending(a => a.Region.Resources[BlockType.CLB])
                    .ToList();

                // Try position each unconfirmed area
                for (int i = 0; i < unconfirmed.Count && !ct.IsCancellationRequested;)
                {
                    Area area = unconfirmed[i];

                    if (canPrint)
                    {
                        Console.Title = $"Floorplanner: optimizing problem {firstPlan.Design.ID}    " +
                        $"({unconfirmed.Count,-3} remaining regions)    " +
                        $"({_st.MaxDisruption - failDisrupt,-4} solution disruptions)";

                        area.Region.PrintInfoTo(Console.Out);
                    }

                    // Try to place current region exploring free areas next to previous
                    Point previousCenter = i > 0 ? unconfirmed[i - 1].Center
                        : area.Center;

                    try
                    {
                        // Place the area, confirm it in current floorplan
                        // and remove it from unconfirmed list
                        _areaPlacer.PlaceArea(area, firstPlan, previousCenter);
                        area.IsConfirmed = true;
                        unconfirmed.Remove(area);

                        if (canPrint)
                        {
                            Console.WriteLine($"\tPlaced at ({area.TopLeft.X,-3}, {area.TopLeft.Y,-3})   " +
                            $"Width: {area.Width}   Height: {area.Height}");

                            //firstPlan.PrintDesignToConsole();

                            //Console.WriteLine("Press a key to continue...");
                            //Console.ReadKey();
                        }
                    }
                    catch (OptimizationException)
                    {
                        // If current area couldn't be placed, go to next area
                        i++;

                        if (canPrint)
                            Console.WriteLine($"Can't place area {area.ID} with current state.");
                    }
                }

                // If some areas remained unplaced,
                // disrupt current state and try to place them again
                if (unconfirmed.Count > 0 && !ct.IsCancellationRequested)
                {
                    // If current partial result is better than last
                    // update current best
                    if (minLeftRegions > unconfirmed.Count)
                    {
                        minLeftRegions = unconfirmed.Count;
                        currentBest = new Floorplan(firstPlan);
                    }

                    // When maximum number of disruption has been reached stop computation
                    if (failDisrupt == 0)
                    {
                        // If on main instance print error information
                        if (canPrint) currentBest.PrintDesignToConsole();

                        throw new OptimizationException($"{minLeftRegions} out of {currentBest.Areas.Count} " +
                            $"regions couldn't be placed after {_st.MaxDisruption} disruption.\n");
                    }

                    int currentUnconf = unconfirmed.Count;
                    failDisrupt--;

                    _areaDisruptor.DisruptStateFor(unconfirmed.First().Region, unconfirmed, firstPlan);

                    if (canPrint)
                        Console.WriteLine($"\tDisrupted {unconfirmed.Count - currentUnconf} " +
                            "areas from current solution.");
                }
            }

            return firstPlan;
        }
    }
}
