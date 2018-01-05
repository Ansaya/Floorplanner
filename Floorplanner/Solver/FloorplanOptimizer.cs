using Floorplanner.Models;
using Floorplanner.Models.Solver;
using Floorplanner.Solver.Disruptors;
using Floorplanner.Solver.Optimizers;
using Floorplanner.Solver.Placers;
using Floorplanner.Solver.Reducers;
using System;
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
            IAreaDisruptor areaDisruptor = new CommonResourcesDisruptor(_st, FPHelper.MagicIntSeed);
                        
            Floorplan firstValidPlan = new Floorplan(Design);

            // Set cost function for areas
            areaReducer.CostFunction = (Area a, Floorplan f) => 
                // Better with less resources and more adjacency points with FPGA borders or othe regions
                a.GetCost(f.Design.Costs.ToNonZero()) - firstValidPlan.GetAdjacentScore(a, 10);

            IFloorplanOptimizer firstOpt = new FirstPlacementOptimizer(
                areaPlacer,
                areaReducer,
                areaDisruptor, 
                _st);

            // Search for a first valid placement for all regions
            firstValidPlan = firstOpt.OptimizePlacement(firstValidPlan, CancellationToken.None);

            // Update optimization criteria
            areaReducer.CostFunction = (Area a, Floorplan f) => f.GetPartialCostWith(a);

            // Setup valid plan optimization process
            Floorplan optimizedPlan = firstValidPlan;
            int currentBestScore = optimizedPlan.GetScore();
            CancellationTokenSource cts = new CancellationTokenSource();

            // Check for key press if user wants to abort iterations
            // and get current result
            Task userAbort = Task.Factory.StartNew(token =>
            {
                CancellationToken _ct = (CancellationToken)token;

                while (true && !_ct.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        Console.ReadKey(true);
                        cts.Cancel();
                        Console.WriteLine("Iteration sequence aborted.");
                        break;
                    }

                    Thread.Sleep(500);
                }
            }, cts.Token);

            // Update max disruptions per iteration
            _st.MaxDisruption = (int)(_st.MaxDisruption * _st.DisruptPerIteration);

            int incrementalSeed = FPHelper.MagicIntSeed;

            // Try optimizing current floorplan running some parallel tasks
            // for specified number of iterations
            for (int i = 1; i <= _st.MaxOptIteration && !cts.IsCancellationRequested; i++)
            {
                Console.Title = $"Floorplanner: optimizing problem {Design.ID}    " +
                        $"({_st.MaxOptIteration - i,-3} remaining iterations)    " +
                        $"(current score {currentBestScore,-8:N0}/{Design.Costs.MaxScore:N0})";

                Console.WriteLine($"Optimization iteration {i}...");

                // Increment caos factor from standard value to 40% of total regions
                // during different iterations
                _st.CaosFactor = (int)Math.Max(_st.CaosFactor,
                    0.4 * Design.Regions.Length * Math.Min(i * 1.5 / _st.MaxOptIteration, 1));

                IFloorplanOptimizer fpOpt = new ShuffleDisruptionOptimizer(
                    areaDisruptor,
                    _st,
                    firstOpt,
                    incrementalSeed++,
                    true);

                //IFloorplanOptimizer fpOpt = new WorstMovingOptimizer(
                //    areaReducer,
                //    _st,
                //    firstOpt,
                //    true);

                // Try to optimize current placement as much as possible
                optimizedPlan = fpOpt.OptimizePlacement(optimizedPlan, cts.Token);
                currentBestScore = optimizedPlan.GetScore();
            }

            // Cancel token and wait for keypress task to complete
            if (!cts.IsCancellationRequested) cts.Cancel();
            userAbort.Wait();

            Console.WriteLine("Region areas optimization completed.");

            return optimizedPlan;
        }
    }
}
