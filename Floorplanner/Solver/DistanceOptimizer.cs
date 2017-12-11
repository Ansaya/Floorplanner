using Floorplanner.Models;
using Floorplanner.Models.Components;
using Floorplanner.Models.Solver;
using Floorplanner.ProblemParser;
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Floorplanner.Solver
{
    public class DistanceOptimizer
    {
        private readonly IEnumerable<Region> _regions;

        private readonly int[,] _interConns;

        private readonly FPGA _fpga;
        
        public DistanceOptimizer(IEnumerable<Region> regions, int[,] regionConns, FPGA fpga)
        {
            _regions = regions;
            _interConns = regionConns;
            _fpga = fpga;
        }

        public Point[] GetOptimizedCenters()
        {
            int[] xCoord = null;
            int[] yCoord = null;
                        
            Task xCompute = new Task(() => RunAmplFor(
                DistanceAmplFiles(io => (int)io.Point.X, _fpga.Design.GetLength(1)), 
                out xCoord));

            Task yCompute = new Task(() => RunAmplFor(
                DistanceAmplFiles(io => (int)io.Point.Y, _fpga.Design.GetLength(0)), 
                out yCoord));

            xCompute.Start();
            yCompute.Start();
            Task.WaitAll(xCompute, yCompute);

            if (xCoord == null || yCoord == null)
                throw new Exception("There was an error parsing AMPL results.");

            Point[] centers = new Point[xCoord.Length];

            for (int i = 0; i < xCoord.Length; i++)
                centers[i] = new Point(xCoord[i], yCoord[i]);

            return centers;
        }

        private void RunAmplFor(string[] modRunOutPaths, out int[] coords)
        {
            string yCoordResultPath = modRunOutPaths[2];

            Process ampl = Process.Start(new ProcessStartInfo()
            {
                FileName = @"Ampl\ampl.exe",
                WorkingDirectory = @"Ampl",
                Arguments = modRunOutPaths[1],
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            ampl.WaitForExit();
            if (ampl.ExitCode != 0)
                throw new Exception("Ampl process returned non zero.");

            string[] resultLines = File.ReadAllLines(modRunOutPaths[2]);

            coords = new int[_regions.Count()];

            for (int i = 0; i < _regions.Count(); i++)
                coords[i] = int.Parse(resultLines[i].Split('=')[1]);

            foreach (var f in modRunOutPaths)
                File.Delete(f);
        }

        private string[] DistanceAmplFiles(Func<IOConn, int> getCoord, int fpgaMaxCoord)
        {
            string equation = "minimize Distance: ";
            string constraints = String.Empty;
            string displays = DesignParser.RunIncipit + "display ";
            
            for (int r = 0; r < _regions.Count(); r++)
            {
                Region currentReg = _regions.ElementAt(r);
                string var = $"c{r}";

                constraints += $"var {var} integer, >= 0, <= {fpgaMaxCoord};";
                displays += $"{var}, ";

                // Sum distances for io connections to current region
                foreach (var io in currentReg.IOConns)
                    equation += $"abs({getCoord(io)}-{var})*{io.Wires}+";

                // Sum distances for region interconnections
                for (int i = 0; i < _interConns.GetLength(0); i++)
                {
                    string connRegVar = $"c{i}";
                    int wires = _interConns[r, i];

                    if (wires > 0)
                        equation += $"abs({var}-{connRegVar})*{wires}+";
                }
            }

            string modFile = Path.GetTempFileName();
            string runFile = Path.GetTempFileName();
            string result = Path.GetTempFileName();

            equation = equation.TrimEnd('+') + ";";
            displays = String.Format(displays.TrimEnd(' ', ','), modFile) + $">'{result}';";

            File.AppendAllText(modFile, constraints + equation);
            File.AppendAllText(runFile, displays);

            return new string[] { modFile, runFile, result };
        }
    }
}
