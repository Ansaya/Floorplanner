using Floorplanner.Models.Solver;
using Floorplanner.Solver.Disruptors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Floorplanner.Solver.Optimizers
{
    public class ShuffleDisruptionOptimizer : IFloorplanOptimizer
    {
        private readonly IAreaDisruptor _areaDisruptor;

        private readonly SolverTuning _st;

        private readonly IFloorplanOptimizer _afterDisruptOptimizer;

        private readonly int _seed;

        private readonly bool _canPrint;

        public ShuffleDisruptionOptimizer(
            IAreaDisruptor areaDisruptor, 
            SolverTuning st, 
            IFloorplanOptimizer afterDisruptOptimizer,
            int seed,
            bool verbose = false)
        {
            _areaDisruptor = areaDisruptor;
            _st = st;
            _afterDisruptOptimizer = afterDisruptOptimizer;
            _seed = seed;
            _canPrint = verbose;
        }

        public Floorplan OptimizePlacement(Floorplan startingPlan, CancellationToken ct)
        {
            int currentBestScore = startingPlan.GetScore();
            int concOpt = Math.Min(startingPlan.Design.Regions.Length, _st.MaxConcurrent);

            // Set up some optimization tasks disrupting different areas 
            // from current best floorplan to try getting a better result
            // on floorplan score
            Task<Floorplan>[] fpOptimizers = new Task<Floorplan>[concOpt];
            CancellationTokenSource cts = new CancellationTokenSource();

            IList<Area> areas = new List<Area>(startingPlan.Areas);
            areas.Shuffle(_seed);

            for (int j = 0; j < concOpt; j++)
            {
                // Duplicate current floorplan and disrupt for a region
                Floorplan disFP = new Floorplan(startingPlan);
                _areaDisruptor.DisruptStateFor(areas[j].Region, new List<Area>(), disFP);

                if (_canPrint) Console.WriteLine($"\tWorker {j}: disrupting region {areas[j].Region.ID}");

                // Unconfirm area relative to the region
                disFP.Areas.Single(a => a.ID == areas[j].ID).IsConfirmed = false;

                // Generate new task with duplicated floorplan and associated area reducer
                fpOptimizers[j] = Task.Factory.StartNew(AfterDisruptOptimize, new FPOptArgs()
                {
                    StartingPoint = disFP,
                    CT = cts.Token
                });
            }

            // Wait for any of the launched optimization tasks and 
            // check if result is better of current floorplan
            while (fpOptimizers.Length > 0)
            {
                int completed = -1;
                try
                {
                    completed = Task.WaitAny(fpOptimizers, ct);
                }
                catch (OperationCanceledException)
                {
                    // If cancellation is requested from caller, 
                    // abort all and return
                    cts.Cancel();

                    Task.WaitAll(fpOptimizers);

                    return startingPlan;
                }

                // Store completed result and remove related task from queue
                Floorplan newFP = fpOptimizers.ElementAt(completed).Result;
                fpOptimizers = fpOptimizers.Where((t, i) => i != completed).ToArray();

                // If task wasn't completed notify and continue
                if (newFP == null)
                {
                    if (_canPrint) Console.WriteLine($"\tWorker {completed}: Reached max disruptions.");
                    continue;
                }

                // Else compute plan score and compare with current best
                int newFPScore = newFP.GetScore();

                // If new floorplan is better than current, update it
                if (newFPScore > currentBestScore)
                {
                    startingPlan = newFP;
                    currentBestScore = newFPScore;

                    if(_canPrint) Console.WriteLine($"\tWorker {completed}: Better solution found!\n" +
                        $"\t\tNew score {newFPScore:N0}/{startingPlan.Design.Costs.MaxScore:N0}");
                }
                else // Else notify and wait for next task to complete
                {
                    if (_canPrint) Console.WriteLine($"\tWorker {completed}: Generated worst plan.");
                }
            }

            return startingPlan;
        }

        public Floorplan AfterDisruptOptimize(object args)
        {
            FPOptArgs _args = (FPOptArgs)args;

            try
            {
                return _afterDisruptOptimizer.OptimizePlacement(_args.StartingPoint, _args.CT);
            }
            catch (OptimizationException)
            {
                // If optimization process couldn't place all regions again return null
                return null;
            }
        }

        private class FPOptArgs
        {
            public Floorplan StartingPoint { get; set; }

            public CancellationToken CT { get; set; }
        }
    }
}
