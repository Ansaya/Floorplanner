using Floorplanner.Models;
using Floorplanner.Models.Components;
using Floorplanner.Models.Solver;
using Floorplanner.Solver.Disruptors;
using Floorplanner.Solver.Placers;
using Floorplanner.Solver.Reducers;
using System;
using System.Collections.Generic;

namespace Floorplanner.Solver
{
    public class FloorplanOptimizer
    {
        public Design Design { get; private set; }

        private readonly IAreaReducer _areaReducer;

        private readonly IAreaDisruptor _areaDisruptor;

        private readonly IAreaPlacer _areaPlacer;

        private readonly SolverTuning _st;

        public FloorplanOptimizer(Design toPlan, SolverTuning st = null)
        {
            Design = toPlan;
            _st = st ?? new SolverTuning();

            _areaReducer = new RatioAreaReducer(1.7, 1.7, 2, 2);
            _areaDisruptor = new CommonResourcesDisruptor(_st);
            _areaPlacer = new NearestCenterPlacer(_areaReducer);
        }

        public Floorplan Solve()
        {
            Floorplan currentBest = new Floorplan(Design);
            int failDisrupt = _st.MaxDisruption;
            int caosFactor = _st.CaosFactor;

            int minLeftRegions = currentBest.Areas.Count;
            Console.WriteLine("Starting regions area optimization...");

            IList<Area> notConfirmed = new List<Area>(currentBest.Areas);

            // Try place and expand each area nearest possible to computed center points
            while(notConfirmed.Count > 0)
            {
                for(int i = 0; i < notConfirmed.Count; i++)
                {
                    Area area = notConfirmed[i];

                    Console.Title = $"Floorplanner: optimizing problem {Design.ID}    " +
                        $"({notConfirmed.Count} remaining regions)    " +
                        $"({_st.MaxDisruption - failDisrupt} solution disruptions)";

                    Console.WriteLine($"Optimizing region {area.ID}\n" +
                        $"\tCLB: {area.Region.Resources[BlockType.CLB]}    " +
                        $"BRAM: {area.Region.Resources[BlockType.BRAM]}    " +
                        $"DSP: {area.Region.Resources[BlockType.DSP]}    " +
                        $"({area.Type.ToString()})");

                    Point previousCenter = i > 0 ? notConfirmed[i - 1].Center
                        : area.Center;

                    try
                    {
                        _areaPlacer.PlaceArea(area, currentBest, previousCenter);

                        Console.WriteLine($"\tPlaced at ({area.TopLeft.X}, {area.TopLeft.Y})   " +
                            $"Width: {area.Height}   Height: {area.Width}");
                    }
                    catch (OptimizationException)
                    {
                        if (minLeftRegions > notConfirmed.Count)
                            minLeftRegions = notConfirmed.Count;

                        if (failDisrupt == 0)
                        {
                            Console.WriteLine("Last computed plan:\n");
                            currentBest.PrintDesignToConsole();

                            throw new OptimizationException($"{minLeftRegions} out of {currentBest.Areas.Count} " +
                                $"regions couldn't be placed after {_st.MaxDisruption} disruption.\n");
                        }                            

                        Console.WriteLine($"Can't place area {area.ID} with current state.");

                        failDisrupt--;
                        _areaDisruptor.DisruptStateFor(area, ref notConfirmed, currentBest);
                        continue;
                    }

                    notConfirmed.Remove(area);                    
                }
            }

            int currentBestScore = currentBest.GetScore();

            // TODO: add disruption only cycles on feasible solution from previous cycles

            Console.WriteLine("Region areas optimization completed successfully.");

            return currentBest;
        }
    }
}
