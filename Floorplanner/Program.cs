using Floorplanner.ArgParser;
using Floorplanner.Models;
using Floorplanner.Models.Solver;
using Floorplanner.Solver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Floorplanner
{
    class Program
    {
        private static string _inputFile = null;

        private static string _inputDir = null;

        private static string _outputDir = null;

        private static string _outputFile = null;

        private static SolverTuning _opt = new SolverTuning()
        {
            CaosFactor = -57
        };

        private static OptParser _optParser = new OptParser(
                "Floorplanner",
                "floorplanner.exe [arguments] ...\n\n" +
                "A floorplan will be computed for the given input and written to the console.\n" +
                "If an output path is specified, the floorplan will be written there.",
                new ArgOption("i|input", "Input file path", path => _inputFile = path, true),
                new ArgOption("|indir", "Input files directory", path => _inputDir = path, true),
                new ArgOption("|outdir", "Output files directory", path => _outputDir = path, true),
                new ArgOption("o|output", "Output file path", path => _outputFile = path, true),
                new ArgOption("|iterations", "Optimization iterations after a generic solution has been found.", it => {
                    int iterations;
                    if (int.TryParse(it, out iterations))
                        _opt.MaxOptIteration = iterations;
                }, true),
                new ArgOption("|disruptions", "Disruptions before failing generic plan search." +
                    "After generic plan has been found each optimization iteration will admit half disruptions " +
                    "before failing.", ds => {
                    int disruptuions;
                    if (int.TryParse(ds, out disruptuions))
                        _opt.MaxDisruption = disruptuions;
                }, true),
                new ArgOption("|dresthres", "Exceeding resource needed by two areas to be removed to place a new one" +
                    "during disruption process. (Can be a negative value)", drt => {
                    int dresthres;
                    if (int.TryParse(drt, out dresthres))
                        _opt.ResourceDisruptThreshold = dresthres;
                }, true),
                new ArgOption("|caosfactor", "How many areas to disrupt in one time", ds => {
                    int disruptuions;
                    if (int.TryParse(ds, out disruptuions))
                        _opt.CaosFactor = disruptuions;
                }, true),
                new ArgOption("|maxconcurrent", "How many concurrent optimizers can be instanciated.", mc => {
                    int maxConcurrent = 0;
                    if (int.TryParse(mc, out maxConcurrent) && maxConcurrent > 0)
                        _opt.MaxConcurrent = maxConcurrent;
                }, true),
                new ArgOption("|minDimension", "Minimum region thickness for every region to be considered during placement.", md =>
                {
                    int minDimension = 1;
                    if (int.TryParse(md, out minDimension) && minDimension >= 1)
                        _opt.MinDimension = minDimension;
                }, true));

        static void Main(string[] args)
        {
            if(!_optParser.Parse(args))
            {
                _optParser.PrintUsage();
                return;
            }

            if(_inputFile == null && _inputDir == null)
            {
                _optParser.PrintUsage();
                return;
            }

            IList<string> inputs = new List<string>();

            if (_inputFile != null)
            {
                if (!File.Exists(_inputFile))
                {
                    Console.WriteLine($"Input file not found at '{_inputFile}'");
                    return;
                }

                string result = Solve(_inputFile, _outputFile);

                Console.WriteLine(result);
            }

            if(_inputDir != null)
            {
                if (!Directory.Exists(_inputDir))
                {
                    Console.WriteLine($"Input directory not found at '{_inputDir}'");
                    return;
                }

                if (!Directory.Exists(_outputDir))
                {
                    Console.WriteLine($"Output directory not found at '{_outputDir}'");
                    return;
                }

                inputs = Directory.GetFiles(_inputDir).Select(Path.GetFileName).ToList();
                IList<string> solverResults = new List<string>();

                foreach (var f in inputs)
                {
                    Console.WriteLine();
                    string outFile = Path.Combine(_outputDir, f);
                    string inputFile = Path.Combine(_inputDir, f);

                    string result = Solve(inputFile, outFile);
                    Console.WriteLine(result);

                    solverResults.Add(result);
                }

                Console.WriteLine("\n\nAll problems have been computed.\n");
                foreach (string solveOut in solverResults)
                    Console.WriteLine(solveOut);
            }
            
            Console.WriteLine("Press a key to exit...");
            Console.ReadKey();
        }

        private static string Solve(string inputFile, string outputFile = null)
        {
            Console.WriteLine($"Solving problem {Path.GetFileNameWithoutExtension(inputFile)}");
            Console.WriteLine("Loading design requests and boundaries from file...");

            Design problem = Design.Parse(File.OpenText(inputFile));

            Console.WriteLine("Design constraints loaded succesfully.");

            Console.WriteLine(problem.Stats);

            if(!problem.IsFeasible)
            {
                Console.WriteLine($"Problem {problem.ID} is infeasible.\n" +
                    "Submitted regions require more resources than the FPGA can offer.");
                return $"Problem {problem.ID}: computation infeasible.\n";
            }
                        
            if(_opt.CaosFactor == -57)
                _opt.CaosFactor = (int)Math.Ceiling(Math.Max(3, problem.Regions.Length * 0.2));

            _opt.CaosVariance = (int)Math.Ceiling(_opt.CaosFactor * 0.25);

            FloorplanOptimizer s = new FloorplanOptimizer(problem, _opt);
            Console.WriteLine("Optimizator initialized with following parameters:");
            _opt.PrintValuesTo(Console.Out);

            Console.WriteLine("\nNow computing solution...");

            Floorplan optimiezdPlan;

            try
            {
                optimiezdPlan = s.Solve();
            }
            catch (OptimizationException e)
            {
                Console.WriteLine($"Error during optimization process...");
                Console.WriteLine("\t" + e.Message);
                return $"Problem {Path.GetFileNameWithoutExtension(inputFile)}: computation time exceeded.\n" +
                    $"\t{e.Message}\n";
            }

            Console.WriteLine("Solution was computed succesfully!\n");

            int planScore = optimiezdPlan.GetScore();

            Console.WriteLine($"Optimized plan has scored {planScore:N0}/{problem.Costs.MaxScore:N0} points.\n");
            if(planScore <= 0)
            {
                Console.WriteLine("Score is negative. Not writing solution.\n");
                optimiezdPlan.PrintDesignToConsole();
                return $"Problem {Path.GetFileNameWithoutExtension(inputFile)}: negative score.";
            }

            TextWriter outPipe = Console.Out;
            if (outputFile != null)
                outPipe = File.CreateText(outputFile);

            optimiezdPlan.PrintSolutionOn(outPipe);
            optimiezdPlan.PrintDesignToConsole();
            Console.WriteLine();

            if (outputFile != null)
            {
                outPipe.Close();
                Console.WriteLine($"Optimized floorplan written to '{Path.GetFullPath(outputFile)}'.\n");
            }

            return $"Problem {problem.ID}: {planScore:N0}\n" +
                $"\tMedium region height: {optimiezdPlan.Areas.Average(a => a.Height + 1):N4}\n" +
                $"\tMedium region width: {optimiezdPlan.Areas.Average(a => a.Width + 1):N4}\n";
        }
    }
}
