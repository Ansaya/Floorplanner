using Floorplanner.Models;
using Floorplanner.Models.Components;
using Floorplanner.Models.Solver;
using Floorplanner.ProblemParser;
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;

namespace Floorplanner.Solver
{
    public class DistanceOptimizer
    {
        public Region[] Regions { get => _design.Regions; }

        public int[,] InterConn { get => _design.RegionWires; }

        public FPGA FPGA { get => _design.FPGA; }

        private readonly Design _design;
        
        public DistanceOptimizer(Design design)
        {
            _design = design;
        }

        public Point[] GetOptimizedCenters()
        {
            int[] xCoord = null;
            int[] yCoord = null;
            
            RunAmplFor(
                DistanceAmplFiles(io => (int)io.Point.X, FPGA.Design.GetLength(1)), 
                out xCoord);

            RunAmplFor(
                DistanceAmplFiles(io => (int)io.Point.Y, FPGA.Design.GetLength(0)), 
                out yCoord);

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
                UseShellExecute = false
            });

            ampl.WaitForExit();
            if (ampl.ExitCode != 0)
                throw new Exception("Ampl process returned non zero.");

            string[] resultLines = File.ReadAllLines(modRunOutPaths[2]);

            coords = new int[Regions.Count()];

            for (int i = 0; i < Regions.Count(); i++)
                coords[i] = int.Parse(resultLines[i].Split('=')[1]);

            foreach (var f in modRunOutPaths)
                File.Delete(f);
        }

        private string[] DistanceAmplFiles(Func<IOConn, int> getCoord, int fpgaMaxCoord)
        {
            int startVar = 'a';
            string equation = "minimize Distance: ";
            string constraints = String.Empty;
            string displays = DesignParser.RunIncipit + "display ";
            
            for (int r = 0; r < Regions.Length; r++)
            {
                Region currentReg = Regions[r];
                char var = (char)(startVar + r);

                constraints += $"var {var} integer, >= 0, <= {fpgaMaxCoord};";
                displays += $"{var}, ";

                // Sum distances for io connections to current region
                foreach (var io in currentReg.IOConns)
                    equation += $"abs({getCoord(io)}-{var})*{io.Wires}+";

                // Sum distances for region interconnections
                for (int i = 0; i < InterConn.GetLength(0); i++)
                {
                    char connRegVar = (char)(startVar + i);
                    int wires = InterConn[r, i];

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
