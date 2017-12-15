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

        private readonly SolverTuning _st;

        public FloorplanOptimizer(Design toPlan, SolverTuning st = null)
        {
            Design = toPlan;
            _st = st ?? new SolverTuning();
        }

        public Floorplan Solve()
        {

            IAreaReducer areaReducer = new RatioAreaReducer(1.3, 1.3, 3, 3);
            IAreaPlacer areaPlacer = new MinCostPlacer(areaReducer);
            IAreaDisruptor areaDisruptor = new CommonResourcesDisruptor(_st);

            Floorplan firstValidPlan = FirstValidPlacement(
                new Floorplan(Design),
                areaReducer,
                areaPlacer,
                areaDisruptor);

            int currentBestScore = firstValidPlan.GetScore();

            // TODO: update areaReducer scoring function to effective floorplan
            //       objective function taking into account both area and wirelength

            _st.MaxDisruption = (int)(_st.MaxDisruption * 0.1);

            for(int i = 0; i < _st.MaxOptIteration; i++)
            {
                Console.Title = $"Floorplanner: optimizing problem {Design.ID}    " +
                        $"({_st.MaxOptIteration - i} remaining iterations)    " +
                        $"(current score {currentBestScore:N0}/{Design.Costs.MaxScore:N0})";

                // TODO: start some parallel calls of FirstValidPlacement and check if
                //       better scores are there

                // TODO: update current plan with better one if any and repeat the process
                //       until max iteration number
            }

            Console.WriteLine("Region areas optimization completed successfully.");

            return firstValidPlan;
        }

        private Floorplan FirstValidPlacement(Floorplan startingPlan, IAreaReducer areaReducer, IAreaPlacer areaPlacer, IAreaDisruptor areaDisruptor)
        {
            Floorplan firstPlan = new Floorplan(startingPlan);
            int failDisrupt = _st.MaxDisruption;
            int caosFactor = _st.CaosFactor;

            int minLeftRegions = firstPlan.Areas.Count;
            Floorplan currentBest = firstPlan;

            Console.WriteLine("Starting region placement...");

            IList<Area> unconfirmed = new List<Area>(firstPlan.Areas.Where(a => !a.IsConfirmed));
            IList<Area> unplaceable = new List<Area>();

            // Place and disrupt areas until they are all placed in some position
            while (unconfirmed.Count > 0)
            {
                // Try position each unconfirmed area
                for (int i = 0; i < unconfirmed.Count; i++)
                {
                    Area area = unconfirmed[i];

                    Console.Title = $"Floorplanner: optimizing problem {Design.ID}    " +
                        $"({unconfirmed.Count + unplaceable.Count} remaining regions)    " +
                        $"({_st.MaxDisruption - failDisrupt} solution disruptions)";

                    PrintRegionTo(Console.Out, area.Region);

                    Point previousCenter = i > 0 ? unconfirmed[i - 1].Center
                        : area.Center;

                    try
                    {
                        areaPlacer.PlaceArea(area, firstPlan, previousCenter);
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
                if (unplaceable.Count() > 0)
                {
                    if (minLeftRegions > unconfirmed.Count)
                    {
                        minLeftRegions = unconfirmed.Count;
                        currentBest = new Floorplan(firstPlan);
                    }

                    if (failDisrupt == 0)
                        FinalizeOnUnfeasible(currentBest, minLeftRegions);

                    foreach (Area a in unplaceable) unconfirmed.Add(a);

                    int currentUnconf = unconfirmed.Count;
                    failDisrupt--;

                    areaDisruptor.DisruptStateFor(unplaceable.First(), ref unconfirmed, firstPlan);

                    Console.WriteLine($"\tDisrupted {unconfirmed.Count - currentUnconf} areas from current solution.");
                }
            }

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
