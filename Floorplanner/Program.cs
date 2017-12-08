using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Floorplanner.ArgParser;
using Floorplanner.Models;

namespace Floorplanner
{
    class Program
    {
        private static string _inputFile;

        private static string _outputFile;

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
            
            // TODO: compute solution

            // TODO: save solution to output

            // TODO: clap hands
            
        }
    }
}
