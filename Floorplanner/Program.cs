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

        private static OptParser _optParser = new OptParser(
                "Floorplanner",
                "floorplanner.exe [arguments] ...\n\n" +
                "A floorplan will be computed for the given input and written to the console.\n" +
                "If an output path is specified, the floorplan will be written there.",
                new ArgOption("i|input", "Input file path", path => _inputFile = path, true),
                new ArgOption("a|indir", "Input files directory", path => _inputDir = path, true),
                new ArgOption("t|outdir", "Output files directory", path => _outputDir = path, true),
                new ArgOption("o", "Output file path", path => _outputFile = path, true));

        static void Main(string[] args)
        {
            if(!_optParser.Parse(args))
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

                inputs.Add(_inputFile);
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
            }
            
            foreach(var f in inputs)
            {
                string outFile = Path.Combine(_outputDir, f);
                string inputFile = Path.Combine(_inputDir, f);

                Solve(inputFile, outFile);
            }

            Console.WriteLine("Press a key to exit...");
            Console.ReadKey();
        }

        private static void Solve(string inputFile, string outputFile)
        {
            Console.WriteLine("Loading design requests and boundaries from file...");

            Design problem = Design.Parse(File.OpenText(inputFile));

            Console.WriteLine("Design constraints loaded succesfully.\nNow computing solution...");

            FloorplanOptimizer s = new FloorplanOptimizer(problem);

            Floorplan optimiezdPlan;

            try
            {
                optimiezdPlan = s.Solve();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error during optimization process...");
                Console.WriteLine(e.Message);
                return;
            }

            Console.WriteLine("Solution was computed succesfully!");

            int planScore = optimiezdPlan.GetScore();

            Console.WriteLine($"Optimized plan has scored {planScore} points.");
            if(planScore <= 0)
            {
                Console.WriteLine("Score is negative. Not writing solution.");
                return;
            }

            TextWriter outPipe = Console.Out;
            if (outputFile != null)
                outPipe = File.CreateText(outputFile);

            optimiezdPlan.PrintOn(outPipe);

            if (outputFile != null)
            {
                outPipe.Close();
                Console.WriteLine($"Optimized floorplan written to '{Path.GetFullPath(outputFile)}'.");
            }
        }
    }
}
