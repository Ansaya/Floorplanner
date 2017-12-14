using System;
using System.Collections.Generic;
using System.Linq;

namespace Floorplanner.ArgParser
{
    public class OptParser
    {
        public string ProgramName { get; private set; }

        public string ProgramDescription { get; private set; }

        private SortedSet<ArgOption> _options;

        /// <summary>
        /// Initialize an argument parser instance for the given set of parameters options
        /// </summary>
        /// <param name="progName">Name of the program</param>
        /// <param name="progDescription">Short usage description for the program</param>
        /// <param name="options">Parameters to search arguments for</param>
        public OptParser(string progName, string progDescription, params ArgOption[] options)
        {
            ProgramName = progName;
            ProgramDescription = progDescription;
            _options = new SortedSet<ArgOption>(options);
        }

        /// <summary>
        /// Parse given args serching for registered parameters
        /// </summary>
        /// <param name="args">Arguments to be parsed</param>
        /// <returns>True if all non-optional parameters have been found</returns>
        public bool Parse(string[] args)
        {
            // Arguments has always to be couples param-paramVal
            if (args.Length % 2 != 0)
                args = args.Concat(new string[] { "-" }).ToArray();

            List<ArgOption> remainingOpt = new List<ArgOption>(_options);

            for(int i = 0; i < args.Length; i = i + 2)
            {
                // If current arg isn't an option identifier abort
                if (args[i][0] != '-')
                    return false;

                // Get effective parameter name
                string argName = args[i].Replace("-", String.Empty);

                // Search for matching arg option
                ArgOption match = _options
                    .FirstOrDefault(opt => opt.Name == argName || opt.FullName == argName);

                // If match is found try reading value and execute assignement
                if (match != null)
                {
                    string paramVal = args[i + 1];

                    // If param value is missing set as empty and fix index for next param
                    if (paramVal[0] == '-')
                    {
                        if(args.Length - 1 > i + 1)
                            i--;
                    }
                    else
                    {
                        match.SetValue(paramVal);
                        remainingOpt.Remove(match);
                    }
                }
            }

            // Check if all left options are optional and return result
            return remainingOpt.Count == 0 || remainingOpt.Select(opt => opt.Optional).Aggregate((o1, o2) => o1 && o2);
        }

        /// <summary>
        /// Prints out a full description for registered parameters
        /// </summary>
        public void PrintUsage()
        {
            Console.WriteLine($"Usage of {ProgramName}:\n");
            Console.WriteLine($"\t{ProgramDescription}\n");
            Console.WriteLine("Required parameters:");
            IEnumerator<ArgOption> opts = _options.GetEnumerator();

            while(opts.MoveNext() && !opts.Current.Optional)
                Console.WriteLine($"\t-{opts.Current.Name}" +
                    $"{(opts.Current.FullName == String.Empty ? "\t" : $" | --{opts.Current.FullName}")}" +
                    $"\t {opts.Current.Description}");

            if (opts.Current == null)
                return;

            Console.WriteLine("\nOptional parameters:");

            do
            {
                Console.WriteLine($"\t-{opts.Current.Name}" +
                    $"{(opts.Current.FullName == String.Empty ? "\t" : $" | --{opts.Current.FullName}")}" +
                    $"\t {opts.Current.Description}");
            } while (opts.MoveNext());
            Console.WriteLine();
        }
    }
}
