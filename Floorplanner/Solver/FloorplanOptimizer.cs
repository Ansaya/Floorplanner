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
using System.Threading;
using System.Threading.Tasks;

namespace Floorplanner.Solver
{
    public class FloorplanOptimizer
    {
        public Design Design { get; private set; }

        private readonly SolverTuning _st;

        public FloorplanOptimizer(Design toPlan, SolverTuning st = null)
        {
            if (!toPlan.IsFeasible)
                throw new OptimizationException("Given design is infeasible.");

            Design = toPlan;
            _st = st ?? new SolverTuning();
        }

        public Floorplan Solve()
        {
            // Setup placement tools to be used
            double PRRegionsRatio = Design.Regions.Count(r => r.Type == RegionType.Reconfigurable) / Design.Regions.Count();

            IAreaReducer ratioReducer = new RatioAreaReducer(1.4, 1.4, 100, 100);
            IAreaReducer prReducer = new PRAreaReducer(ratioReducer);

            IAreaReducer areaReducer = PRRegionsRatio > 0.75 ? prReducer : ratioReducer;
            IAreaPlacer areaPlacer = new MinCostPlacer(areaReducer);
            IAreaDisruptor areaDisruptor = new CommonResourcesDisruptor(_st);

            // Search for a first valid placement for all regions
            Floorplan firstValidPlan = FirstValidPlacement(
                new Floorplan(Design),
                areaReducer,
                areaPlacer,
                areaDisruptor,
                CancellationToken.None);

            // Try to optimize current placement as much as possible
            Floorplan optimizedPlan = OptimizeValid(
                firstValidPlan,
                new IAreaReducer[] { ratioReducer, prReducer },
                areaPlacer,
                areaDisruptor);

            Console.WriteLine("Region areas optimization completed successfully.");

            return optimizedPlan;
        }

        private Floorplan FirstValidPlacement(
            Floorplan startingPlan, 
            IAreaReducer areaReducer, 
            IAreaPlacer areaPlacer, 
            IAreaDisruptor areaDisruptor,
            CancellationToken ct)
        {
            // Only first call when no valid plan has been found
            // is a sequentail call and can print what's happening
            bool canPrint = ct == CancellationToken.None;

            Floorplan firstPlan = new Floorplan(startingPlan);
            int failDisrupt = _st.MaxDisruption;
            int caosFactor = _st.CaosFactor;

            int minLeftRegions = firstPlan.Areas.Count;
            Floorplan currentBest = firstPlan;

            if(canPrint)
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
                for (int i = 0; i < unconfirmed.Count && !ct.IsCancellationRequested; )
                {
                    Area area = unconfirmed[i];

                    if (canPrint)
                        Console.Title = $"Floorplanner: optimizing problem {Design.ID}    " +
                        $"({unconfirmed.Count, -3} remaining regions)    " +
                        $"({_st.MaxDisruption - failDisrupt, -4} solution disruptions)";

                    if (canPrint) PrintRegionTo(Console.Out, area.Region);

                    // Try to place current region exploring free areas next to previous
                    Point previousCenter = i > 0 ? unconfirmed[i - 1].Center
                        : area.Center;

                    try
                    {
                        // Place the area, confirm it in current floorplan
                        // and remove it from unconfirmed list
                        areaPlacer.PlaceArea(area, firstPlan, previousCenter);
                        area.IsConfirmed = true;
                        unconfirmed.Remove(area);

                        if (canPrint)
                            Console.WriteLine($"\tPlaced at ({area.TopLeft.X, -3}, {area.TopLeft.Y, -3})   " +
                            $"Width: {area.Height}   Height: {area.Width}");
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
                        if (canPrint) PrintIncompleteToConsole(currentBest);

                        throw new OptimizationException($"{minLeftRegions} out of {currentBest.Areas.Count} " + 
                            $"regions couldn't be placed after {_st.MaxDisruption} disruption.\n");
                    }

                    int currentUnconf = unconfirmed.Count;
                    failDisrupt--;

                    areaDisruptor.DisruptStateFor(unconfirmed.First().Region, unconfirmed, firstPlan);

                    if (canPrint)
                        Console.WriteLine($"\tDisrupted {unconfirmed.Count - currentUnconf} " +
                            "areas from current solution.");
                }
            }

            return firstPlan;
        }

        /// <summary>
        /// Optimize a valid floorplan as much as possible
        /// exploiting given tools
        /// </summary>
        /// <param name="bestFloorplan">Floorplan to start from.</param>
        /// <param name="areaReducers">Area reducers to be used.</param>
        /// <param name="areaPlacer">Area placer to be used.</param>
        /// <param name="areaDisruptor">Area disruptor to be used.</param>
        /// <returns>Optimized floorplan if any is found.</returns>
        private Floorplan OptimizeValid(
            Floorplan bestFloorplan, 
            IAreaReducer[] areaReducers, 
            IAreaPlacer areaPlacer, 
            IAreaDisruptor areaDisruptor)
        {
            int currentBestScore = bestFloorplan.GetScore();

            // Update max disruptions per iteration 
            // and set maximum concurrent operations number
            _st.MaxDisruption = (int)(_st.MaxDisruption * _st.DisruptPerIteration);
            int concOpt = Math.Min(Design.Regions.Length, _st.MaxConcurrent);

            // Try optimizing current floorplan running some parallel tasks
            // for specified number of iterations
            for (int i = 1; i <= _st.MaxOptIteration; i++)
            {
                Console.Title = $"Floorplanner: optimizing problem {Design.ID}    " +
                        $"({_st.MaxOptIteration - i,-3} remaining iterations)    " +
                        $"(current score {currentBestScore,-8:N0}/{Design.Costs.MaxScore:N0})";

                Console.WriteLine($"Optimization iteration {i}...");

                // Increment caos factor from standard value to 40% of total regions
                // during different iterations
                _st.CaosFactor = (int)Math.Max(_st.CaosFactor,
                    0.4 * Design.Regions.Length * Math.Min(i * 1.5 / _st.MaxOptIteration, 1));

                // Set up some optimization tasks disrupting different areas 
                // from current best floorplan to try getting a better result
                // on floorplan score
                Task<Floorplan>[] fpOptimizers = new Task<Floorplan>[concOpt];
                CancellationTokenSource cts = new CancellationTokenSource();

                IList<Area> areas = new List<Area>(bestFloorplan.Areas);
                areas.Shuffle();

                for (int j = 0; j < concOpt; j++)
                {
                    // Duplicate current floorplan and disrupt for an area
                    Floorplan disFP = new Floorplan(bestFloorplan);
                    areaDisruptor.DisruptStateFor(areas[j].Region, new List<Area>(), disFP);

                    // Clone area reducer object and associate cost function to duplicated
                    // floorplan partial cost function
                    IAreaReducer associatedReducer = i % 2 == 0 ? areaReducers[0] : areaReducers[1];
                    associatedReducer = associatedReducer.Clone();
                    associatedReducer.CostFunction = (Area a, Costs c) => disFP.GetPartialCostWith(a);

                    // Generate new task with duplicated floorplan and associated area reducer
                    fpOptimizers[j] = Task.Factory.StartNew(FirstValidPlacement, new OptTools()
                    {
                        Floorplan = disFP,
                        AreaReducer = associatedReducer,
                        AreaPlacer = areaPlacer,
                        AreaDisruptor = areaDisruptor,
                        CancellationToken = cts.Token
                    });
                }

                // Check for key press if user wants to abort iterations
                // and get current result
                Task userAbort = Task.Factory.StartNew(token =>
                {
                    CancellationToken ct = (CancellationToken)token;

                    while (true && !ct.IsCancellationRequested)
                    {
                        if (Console.KeyAvailable)
                        {
                            Console.ReadKey(true);
                            cts.Cancel();
                            break;
                        }

                        Thread.Sleep(500);
                    }
                }, cts.Token);

                // Wait for any of the launched optimization tasks and 
                // check if result is better of current floorplan
                while (fpOptimizers.Any(t => !t.IsCompleted))
                {
                    int completed = Task.WaitAny(fpOptimizers);

                    // Abort iteration sequence if user requested
                    if (cts.IsCancellationRequested)
                    {
                        Console.WriteLine("Iteration sequence aborted.");
                        Task.WaitAll(fpOptimizers);
                        userAbort.Wait();
                        return bestFloorplan;
                    }

                    // If new floorplan is better than current, update it,
                    // abort other optimization tasks and complete current iteration
                    Floorplan newFP = fpOptimizers[completed].Result;

                    if (newFP != null && newFP.GetScore() > currentBestScore)
                    {
                        cts.Cancel();
                        bestFloorplan = newFP;
                        currentBestScore = newFP.GetScore();

                        Console.WriteLine($"Better solution found!\n" +
                            $"\tNew score {currentBestScore:N0}/{Design.Costs.MaxScore:N0}");

                        Task.WaitAll(fpOptimizers);
                        break;
                    }

                    // Else wait for another task to complete
                }

                if (!cts.IsCancellationRequested)
                    cts.Cancel();

                userAbort.Wait();
            }

            return bestFloorplan;
        }

        private class OptTools
        {
            public Floorplan Floorplan { get; set; }

            public IAreaReducer AreaReducer { get; set; }

            public IAreaPlacer AreaPlacer { get; set; }

            public IAreaDisruptor AreaDisruptor { get; set; }

            public CancellationToken CancellationToken { get; set; }
        }

        private Floorplan FirstValidPlacement(object optTools)
        {
            OptTools tools = (OptTools)optTools;

            try
            {
                return FirstValidPlacement(
                    tools.Floorplan,
                    tools.AreaReducer,
                    tools.AreaPlacer,
                    tools.AreaDisruptor,
                    tools.CancellationToken);
            }
            catch (OptimizationException)
            {
                // If optimization process couldn't place all regions again return null
                return null;
            }
        }

        /// <summary>
        /// Print given floorplan information
        /// </summary>
        /// <param name="bestPlan">Incomplete floorplan.</param>
        private void PrintIncompleteToConsole(Floorplan bestPlan)
        {
            Console.WriteLine("\nMissing regions are:");
            foreach (Area a in bestPlan.Areas.Where(a => !a.IsConfirmed))
            {
                Console.Write("\t");
                PrintRegionTo(Console.Out, a.Region);
            }
            IEnumerable<Area> confirmed = bestPlan.Areas.Where(a => a.IsConfirmed);

            Console.WriteLine($"\nPlaced areas statistics:\n" +
                $"\tMedium region height: {confirmed.Average(a => a.Height + 1):N4}\n" +
                $"\tMedium region width: {confirmed.Average(a => a.Width + 1):N4}\n");
            
            bestPlan.PrintDesignToConsole();
            Console.WriteLine();
        }

        /// <summary>
        /// Print specified region information to given text writer.
        /// </summary>
        /// <param name="tw">Text writer to print to.</param>
        /// <param name="r">Region to print information.</param>
        private void PrintRegionTo(TextWriter tw, Region r)
        {
            tw.WriteLine($"Region {r.ID} - {r.Resources[BlockType.CLB]} CLB" +
                                    $"  {r.Resources[BlockType.BRAM]} BRAM" +
                                    $"  {r.Resources[BlockType.DSP]} DSP" +
                                    $"  ({r.Type.ToString()})");
        }
    }
}
