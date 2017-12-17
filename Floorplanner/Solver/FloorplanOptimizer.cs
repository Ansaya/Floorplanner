﻿using Floorplanner.Models;
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
            Design = toPlan;
            _st = st ?? new SolverTuning();
        }

        public Floorplan Solve()
        {
            double PRRegionsRatio = Design.Regions.Count(r => r.Type == RegionType.Reconfigurable) / Design.Regions.Count();

            IAreaReducer ratioReducer = new RatioAreaReducer(1.4, 1.4, 2, 2);
            IAreaReducer prReducer = new PRAreaReducer(ratioReducer);

            IAreaReducer areaReducer = PRRegionsRatio > 0.75 ? prReducer : ratioReducer;
            IAreaPlacer areaPlacer = new MinCostPlacer(areaReducer);
            IAreaDisruptor areaDisruptor = new CommonResourcesDisruptor(_st);

            Floorplan firstValidPlan = FirstValidPlacement(
                new Floorplan(Design),
                areaReducer,
                areaPlacer,
                areaDisruptor,
                CancellationToken.None);

            int currentBestScore = firstValidPlan.GetScore();

            // TODO: update areaReducers scoring function to effective floorplan
            //       objective function taking into account both area and wirelength

            // Update max disruptions per iteration 
            // and set maximum concurrent operations number
            _st.MaxDisruption = (int)(_st.MaxDisruption * _st.DisruptPerIteration);
            int concOpt = Math.Min(Design.Regions.Length, _st.MaxConcurrent);


            for (int i = 1; i <= _st.MaxOptIteration; i++)
            {
                Console.Title = $"Floorplanner: optimizing problem {Design.ID}    " +
                        $"({_st.MaxOptIteration - i} remaining iterations)    " +
                        $"(current score {currentBestScore:N0}/{Design.Costs.MaxScore:N0})";

                Console.WriteLine($"Optimization iteration {i}...");

                // Increment caos factor from standard value to 40% of total regions
                // during different iterations
                _st.CaosFactor = (int)Math.Max(_st.CaosFactor,
                    0.4 * Design.Regions.Length * Math.Min(i * 1.5 / _st.MaxOptIteration, 1));

                // Set up some optimization tasks disrupting different areas 
                // from current best floorplan
                Task<Floorplan>[] fpOptimizers = new Task<Floorplan>[concOpt];
                CancellationTokenSource cts = new CancellationTokenSource();

                IList<Area> areas = new List<Area>(firstValidPlan.Areas);
                areas.Shuffle();

                for (int j = 0; j < concOpt; j++)
                {
                    Floorplan disFP = new Floorplan(firstValidPlan);

                    areaDisruptor.DisruptStateFor(areas[j].Region, new List<Area>(), disFP);

                    fpOptimizers[j] = Task.Factory.StartNew(FirstValidPlacement, new OptTools()
                    {
                        Floorplan = disFP,
                        AreaReducer = i % 2 == 0 ? ratioReducer : prReducer,
                        AreaPlacer = areaPlacer,
                        AreaDisruptor = areaDisruptor,
                        CancellationToken = cts.Token
                    });
                }

                // Check for key press if user wants to abort iterations
                // and get current result
                bool userAbort = false;
                Task.Factory.StartNew(token =>
                {
                    CancellationToken t = (CancellationToken)token;

                    while(true && !t.IsCancellationRequested)
                    {
                        if (Console.KeyAvailable)
                        {
                            Console.ReadKey(true);
                            userAbort = true;
                            cts.Cancel();
                        }

                        Thread.Sleep(500);
                    }
                }, cts.Token, cts.Token);

                // Wait for any of launched optimization tasks and check if result
                // is better of current floorplan
                while(fpOptimizers.Any(t => !t.IsCompleted))
                {
                    int completed = Task.WaitAny(fpOptimizers.ToArray(), cts.Token);

                    // Abort iteration sequence if user requested
                    if(cts.IsCancellationRequested)
                    {
                        userAbort = true;
                        Console.WriteLine("Iteration sequence aborted.");
                        break;
                    }

                    Floorplan newFP = fpOptimizers[completed].Result;

                    // If new floorplan is better, update current one
                    // Abort other optimization workers and complete current iteration
                    if(newFP != null && newFP.GetScore() > currentBestScore)
                    {
                        cts.Cancel();
                        firstValidPlan = newFP;
                        currentBestScore = newFP.GetScore();

                        Console.WriteLine($"Better solution found!\n" +
                            $"\tNew score {currentBestScore:N0}/{Design.Costs.MaxScore:N0}");

                        break;
                    }
                }

                Task.WaitAll(fpOptimizers);

                // Stop user key press wait task
                if(!cts.IsCancellationRequested)
                    cts.Cancel();

                // Stop computation if user requested
                if (userAbort) break;
            }

            Console.WriteLine("Region areas optimization completed successfully.");

            return firstValidPlan;
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

        private Floorplan FirstValidPlacement(
            Floorplan startingPlan, 
            IAreaReducer areaReducer, 
            IAreaPlacer areaPlacer, 
            IAreaDisruptor areaDisruptor,
            CancellationToken ct)
        {
            bool canPrint = ct == CancellationToken.None;

            Floorplan firstPlan = new Floorplan(startingPlan);
            int failDisrupt = _st.MaxDisruption;
            int caosFactor = _st.CaosFactor;

            int minLeftRegions = firstPlan.Areas.Count;
            Floorplan currentBest = new Floorplan(firstPlan);

            if(canPrint)
                Console.WriteLine("Starting region placement...");

            IList<Area> unconfirmed = new List<Area>(firstPlan.Areas.Where(a => !a.IsConfirmed));

            // Place and disrupt areas until they are all placed in some position
            while (unconfirmed.Count > 0 && !ct.IsCancellationRequested)
            {
                // Try position each unconfirmed area
                for (int i = 0; i < unconfirmed.Count && !ct.IsCancellationRequested; )
                {
                    Area area = unconfirmed[i];

                    if (canPrint)
                        Console.Title = $"Floorplanner: optimizing problem {Design.ID}    " +
                        $"({unconfirmed.Count} remaining regions)    " +
                        $"({_st.MaxDisruption - failDisrupt} solution disruptions)";

                    if (canPrint)
                        PrintRegionTo(Console.Out, area.Region);

                    Point previousCenter = i > 0 ? unconfirmed[i - 1].Center
                        : area.Center;

                    try
                    {
                        areaPlacer.PlaceArea(area, firstPlan, previousCenter);
                        area.IsConfirmed = true;

                        if (canPrint)
                            Console.WriteLine($"\tPlaced at ({area.TopLeft.X}, {area.TopLeft.Y})   " +
                            $"Width: {area.Height}   Height: {area.Width}");
                    }
                    catch (OptimizationException)
                    {
                        // If current area couldn't be placed, store it away and go ahead with
                        // remaining areas
                        if (canPrint)
                            Console.WriteLine($"Can't place area {area.ID} with current state.");

                        i++;
                        continue;
                    }

                    // Remove current area from unconfirmed list despite placement result
                    unconfirmed.Remove(area);
                }

                // If some areas remaind unplaced,
                // disrupt current state and try to place them again
                if (unconfirmed.Count > 0 && !ct.IsCancellationRequested)
                {
                    if (minLeftRegions > unconfirmed.Count)
                    {
                        minLeftRegions = unconfirmed.Count;
                        currentBest = new Floorplan(firstPlan);
                    }

                    // When maximum nber of disruption has been reached stop computation
                    if (failDisrupt == 0)
                    {
                        // If on main instance print error information
                        if (canPrint)
                            FinalizeOnUnfeasible(currentBest, minLeftRegions);

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

        private void FinalizeOnUnfeasible(Floorplan bestPlan, int leftRegions)
        {
            Console.WriteLine("\nMissing regions are:");
            foreach (Area a in bestPlan.Areas.Where(a => !a.IsConfirmed))
            {
                Console.Write("\t");
                PrintRegionTo(Console.Out, a.Region);
            }
            Console.WriteLine();
            bestPlan.PrintDesignToConsole();
            Console.WriteLine();
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
