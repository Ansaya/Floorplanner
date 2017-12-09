using Floorplanner.ArgParser;
using Floorplanner.Models;
using Floorplanner.Models.Solver;
using System;
using System.IO;

namespace Floorplanner
{
    class Program
    {
        private static string _inputFile = null;

        private static string _outputFile = null;

        private static OptParser _optParser = new OptParser(
                "Floorplanner",
                "floorplanner.exe [arguments] ...\n\n" +
                "A floorplan will be computed for the given input and written to the console.\n" +
                "If an output path is specified, the floorplan will be written there.",
                new ArgOption("i|input", "Input file path", path => _inputFile = path),
                new ArgOption("o", "Output file path", path => _outputFile = path, true));

        static void Main(string[] args)
        {
            if(!_optParser.Parse(args))
            {
                _optParser.PrintUsage();
                return;
            }

            if(!File.Exists(_inputFile))
            {
                Console.WriteLine($"Input file not found at '{_inputFile}'");
                return;
            }

            if (!Path.IsPathRooted(_inputFile))
                _inputFile = Path.Combine(Directory.GetCurrentDirectory(), _inputFile);

            Console.WriteLine("Loading design requests and boundaries from file...");

            Design problem = Design.Parse(File.OpenText(_inputFile));

            Console.WriteLine("Design constraints loaded succesfully.\nNow computing solution...");

            Solver.Solver s = new Solver.Solver(problem);

            Floorplan optimiezdPlan = s.Solve();

            TextWriter outPipe = Console.Out;
            if (_outputFile != null)
                outPipe = File.CreateText(_outputFile);

            optimiezdPlan.PrintOn(Console.Out);            
        }
    }
}
