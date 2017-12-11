using Floorplanner.Models;
using System.IO;

namespace Floorplanner.Test
{
    public static class DesignFactory
    {
        private static string inFile = File.Exists(@"..\..\..\Problems\10001.txt") ? @"..\..\..\Problems\10001.txt"
            : @"..\..\..\..\..\..\..\..\Problems\10001.txt";

        public static Design Design { get => Design.Parse(new StreamReader(File.OpenRead(inFile))); }
    }
}
