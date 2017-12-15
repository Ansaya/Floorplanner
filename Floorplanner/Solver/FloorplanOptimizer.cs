using Floorplanner.Models;
using Floorplanner.Models.Components;
using Floorplanner.Models.Solver;
using Floorplanner.Solver.Disruptors;
using Floorplanner.Solver.Placers;
using Floorplanner.Solver.Reducers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Floorplanner.Solver
{
    public class FloorplanOptimizer
    {
        public Design Design { get; private set; }

        private IAreaReducer _areaReducer;

        private IAreaDisruptor _areaDisruptor;

        private IAreaPlacer _areaPlacer;

        private readonly SolverTuning _st;

        public FloorplanOptimizer(Design toPlan, SolverTuning st = null)
        {
            Design = toPlan;
            _st = st ?? new SolverTuning();
        }

        public Floorplan Solve()
        {
            Floorplan firstPlan = new Floorplan(Design);
            int failDisrupt = _st.MaxDisruption;
            int caosFactor = _st.CaosFactor;

            int minLeftRegions = firstPlan.Areas.Count;
            Floorplan currentBest = firstPlan;

            _areaReducer = new RatioAreaReducer(1.3, 1.3, 2, 2);
            _areaPlacer = new MinCostPlacer(_areaReducer);
            _areaDisruptor = new CommonResourcesDisruptor(_st);

            Console.WriteLine("Starting regions area optimization...");

            IList<Area> unconfirmed = new List<Area>(firstPlan.Areas);
            IList<Area> unplaceable = new List<Area>();

            // Place and disrupt areas until they are all placed in some position
            while(unconfirmed.Count > 0)
            {
                // Try position each unconfirmed area
                for(int i = 0; i < unconfirmed.Count; i++)
                {
                    Area area = unconfirmed[i];

                    Console.Title = $"Floorplanner: optimizing problem {Design.ID}    " +
                        $"({unconfirmed.Count} remaining regions)    " +
                        $"({_st.MaxDisruption - failDisrupt} solution disruptions)";

                    PrintRegionTo(Console.Out, area.Region);

                    Point previousCenter = i > 0 ? unconfirmed[i - 1].Center
                        : area.Center;

                    try
                    {
                        _areaPlacer.PlaceArea(area, firstPlan, previousCenter);
                        area.IsConfirmed = true;

                        Console.WriteLine($"\tPlaced at ({area.TopLeft.X}, {area.TopLeft.Y})   " +
                            $"Width: {area.Height}   Height: {area.Width}");
                    }
                    catch (OptimizationException)
                    {
                        // If current area couldn't be placed, store it away and go ahead with
                        // remaining areas
                        Console.WriteLine($"Can't place area {area.ID} with current state.");
                        unplaceable.Add(area);
                    }

                    // Remove current area from unconfirmed list despite placement result
                    unconfirmed.Remove(area);                    
                }

                // If some areas remaind unplaced put them back in unconfirmed list,
                // disrupt current state and try to place them again
                if(unplaceable.Count() > 0)
                {
                    if (minLeftRegions > unconfirmed.Count)
                    {
                        minLeftRegions = unconfirmed.Count;
                        currentBest = new Floorplan(firstPlan);
                    }

                    if (failDisrupt == 0)
                        FinalizeOnUnfeasible(currentBest, minLeftRegions);

                    foreach (Area a in unplaceable) unconfirmed.Add(a);

                    failDisrupt--;
                    _areaDisruptor.DisruptStateFor(unplaceable.First(), ref unconfirmed, firstPlan);
                }
            }

            int currentBestScore = firstPlan.GetScore();

            // TODO: add disruption only cycles on feasible solution from previous cycles

            Console.WriteLine("Region areas optimization completed successfully.");

            return firstPlan;
        }

        private void FinalizeOnUnfeasible(Floorplan lastPlan, int leftRegions)
        {
            Console.WriteLine("\nMissing regions are:");
            foreach (Area a in lastPlan.Areas.Where(a => !a.IsConfirmed))
            {
                Console.Write("\t");
                PrintRegionTo(Console.Out, a.Region);
            }
            Console.WriteLine();
            lastPlan.PrintDesignToConsole();
            Console.WriteLine();

            throw new OptimizationException($"{leftRegions} out of {lastPlan.Areas.Count} " +
                $"regions couldn't be placed after {_st.MaxDisruption} disruption.\n");
        }

        private void PrintRegionTo(TextWriter tw, Region r)
        {
            tw.WriteLine($"Region {r.ID} - {r.Resources[BlockType.CLB]} CLB" +
                                    $"  {r.Resources[BlockType.BRAM]} BRAM" +
                                    $"  {r.Resources[BlockType.DSP]} DSP" +
                                    $"  ({r.Type.ToString()})");
        }
    }
}
