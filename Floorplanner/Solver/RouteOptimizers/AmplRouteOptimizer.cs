using Floorplanner.Models;
using Floorplanner.Models.Components;
using Floorplanner.Models.Solver;
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Floorplanner.Solver.RouteOptimizers
{
    public class AmplRouteOptimizer : IRouteOptimizer
    {
        private readonly Area[] _areas;

        private readonly int[,] _interConns;

        private readonly FPGA _fpga;

        private readonly bool _updateOnError;
        
        public AmplRouteOptimizer(Area[] areas, int[,] regionConns, bool updateOnError = false)
        {
            _areas = areas;
            _interConns = regionConns;
            _fpga = areas.First().FPGA;
            _updateOnError = updateOnError;
        }

        /// <summary>
        /// Update given array of centers with new values computed considering confirmed area
        /// effective center points.
        /// Center points in the array can be accessed by area id to bind them correctly.
        /// </summary>
        /// <param name="centers">Initialized centers array.</param>
        public void GetOptimizedCenters(ref Point[] centers)
        {
            Point[] newCenters = null;
                        
            RunAmplFor(DistanceAmplFiles(), out newCenters);

            // If points are all at origin it means that ampl failed, so update 
            // current point array with new confirmed area centers
            if(newCenters == null 
                || newCenters.Select(p1 => p1.X + p1.Y).Aggregate((p1, p2) => p1 + p2) == 0)
            {
                if (!_updateOnError) return;

                for(int i = 0; i < centers.Length; i++)
                {
                    Point current = centers[i];
                    Area related = _areas.Single(a => a.ID == i);

                    if (!related.IsConfirmed) continue;

                    for(int j = i + 1; j < centers.Length; j++)
                        if (centers[j] == current)
                            centers[j] = new Point(related.Center);

                    centers[i] = related.Center;
                }
            }
            else
            {
                centers = newCenters;
            }
        }

        private void RunAmplFor(string[] modRunOutPaths, out Point[] coords)
        {
            Process ampl = Process.Start(new ProcessStartInfo()
            {
                FileName = @"Ampl\ampl.exe",
                WorkingDirectory = @"Ampl",
                Arguments = modRunOutPaths[0],
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            ampl.WaitForExit();
            if (ampl.ExitCode != 0)
                throw new OptimizationException("Ampl process returned non zero.");

            string[] resultLines = File.ReadAllLines(modRunOutPaths[1]);

            coords = new Point[_areas.Length];

            int i = 0;
            foreach (Area a in _areas)
            {
                if(a.IsConfirmed)
                    coords[a.ID] = new Point(a.Center);
                else
                    coords[a.ID] = new Point(
                        double.Parse(resultLines[i++].Split('=')[1]),
                        double.Parse(resultLines[i++].Split('=')[1]));
            }

            foreach (var f in modRunOutPaths)
                File.Delete(f);
        }

        /// <summary>
        /// Generate all needed files to run ampl instance and get results.
        /// </summary>
        /// <returns>Array of run file path, results file path and model file path.</returns>
        private string[] DistanceAmplFiles()
        {
            string variables = String.Empty;
            string obFunc = "minimize Distance: ";
            string[] equations = new string[_areas.Length];
            string runText = "reset;" +
                "option solver couenne;" +
                "model '{0}';" +
                "solve;" +
                "display ";

            Task[] writers = new Task[_areas.Length];

            // Generate objective function for each region in separate threads
            for (int i = 0; i < writers.Length; i++)
                writers[i] = Task.Factory.StartNew(area =>
                {
                    Area a = (Area)area;

                    equations[a.ID] = WriteObjectiveFor(a, _interConns);
                }, _areas[i]);

            // While generating objective function define necessary constraints
            // and variables for the problem
            foreach(Area a in _areas)
            {
                if(!a.IsConfirmed)
                {
                    variables += $"var x{a.ID} integer, >= 0, <= {_fpga.Xmax};"
                        + $"var y{a.ID} integer, >= 0, <= {_fpga.Ymax};";
                    runText += $"x{a.ID}, y{a.ID}, ";
                }
            }

            // Generate needed files
            string runFile = Path.GetTempFileName();
            string result = Path.GetTempFileName();
            string modFile = Path.GetTempFileName();

            runText = String.Format(runText.TrimEnd(' ', ','), modFile) + $">'{result}';";
            
            File.AppendAllText(runFile, runText);

            // Wait for objective function generation to complete
            Task.WaitAll(writers.ToArray());

            // Put model file together and write it
            string modText = variables + obFunc + equations.Aggregate(String.Concat).TrimEnd('+') + ";";
            File.AppendAllText(modFile, modText);

            // Return all generated files
            return new string[] { runFile, result, modFile };
        }

        /// <summary>
        /// Generate objective function for a given area
        /// </summary>
        /// <param name="a">Area to generate the of for.</param>
        /// <param name="interConns">Regions interconnections.</param>
        /// <returns>Part of the objective function related to given area.</returns>
        private string WriteObjectiveFor(Area a, int[,] interConns)
        {
            string obFunc = String.Empty;

            string xVar = $"x{a.ID}";
            string yVar = $"y{a.ID}";

            // Sum distances for io connections to current region
            if (!a.IsConfirmed)
            {
                foreach (IOConn ioc in a.Region.IOConns)
                    obFunc += $"abs({ioc.Point.X}-{xVar})*{ioc.Wires}+"
                        + $"abs({ioc.Point.Y}-{yVar})*{ioc.Wires}+";
            }
            else
            {
                xVar = $"{(int)a.Center.X}";
                yVar = $"{(int)a.Center.Y}";
            }

            // Sum distances for region interconnections
            foreach (Area other in _areas)
            {
                int wires = _interConns[a.ID, other.ID];

                if (wires > 0 && !other.IsConfirmed)
                    obFunc += $"abs({xVar}-x{other.ID})*{wires}+"
                        + $"abs({yVar}-y{other.ID})*{wires}+";
            }

            return obFunc;
        }
    }
}
