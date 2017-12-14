﻿using Floorplanner.Models;
using Floorplanner.Models.Components;
using Floorplanner.Models.Solver;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Floorplanner.Test.Models.Components
{
    [TestClass]
    public class FPGATest
    {

        private Design _design = DesignFactory.Design;

        private FPGA fpga;

        public FPGATest()
        {
            fpga = _design.FPGA;
        }

        [TestMethod]
        public void IsValid()
        {
            Assert.AreEqual(9, fpga.Ymax);
            Assert.AreEqual(30, fpga.Xmax);
        }

        [TestMethod]
        public void GetPoints()
        {
            IEnumerable<Point> fpgaPoints = fpga.ValidPoints;

            // In test case there are no forbidden points
            Assert.AreEqual((fpga.Xmax + 1) * (fpga.Ymax + 1), fpgaPoints.Count());

            foreach (Point p in fpgaPoints)
                checkPointInside(p, 0, 0, 30, 9);
        }

        [TestMethod]
        public void ResourcesFor()
        {
            Area area = new Area(fpga, null, new Point(0, 0));
            area.Width = 4;
            area.Height = 4;

            IDictionary<BlockType, int> areaRes = fpga.ResourcesFor(area);

            Assert.AreEqual(area.Value, areaRes.Sum(resVal => resVal.Value));
            Assert.AreEqual(15, areaRes[BlockType.CLB]);
            Assert.AreEqual(5, areaRes[BlockType.BRAM]);
            Assert.AreEqual(0, areaRes[BlockType.DSP]);
            Assert.AreEqual(0, areaRes[BlockType.Forbidden]);
            Assert.AreEqual(5, areaRes[BlockType.Null]);

            area.MoveTo(new Point(3, 2));

            areaRes = fpga.ResourcesFor(area);

            Assert.AreEqual(area.Value, areaRes.Sum(resVal => resVal.Value));
            Assert.AreEqual(15, areaRes[BlockType.CLB]);
            Assert.AreEqual(5, areaRes[BlockType.BRAM]);
            Assert.AreEqual(5, areaRes[BlockType.DSP]);
            Assert.AreEqual(0, areaRes[BlockType.Forbidden]);
            Assert.AreEqual(0, areaRes[BlockType.Null]);
        }

        [TestMethod]
        public void ContainsArea()
        {
            Area area = new Area(fpga, _design.Regions[0], new Point(2, 0))
            {
                Width = 5,
                Height = 9
            };

            Assert.IsTrue(fpga.Contains(area));

            area.TopLeft.X = -1;

            Assert.IsFalse(fpga.Contains(area));

            area.TopLeft.X = 2;
            area.Width = 30;

            Assert.IsFalse(fpga.Contains(area));

            area.Width = 5;
            area.Height = 10;

            Assert.IsFalse(fpga.Contains(area));

            area.Height = 9;
            area.TopLeft.Y = -1;

            Assert.IsFalse(fpga.Contains(area));
        }

        [TestMethod]
        public void ContainsPoint()
        {
            Point point = new Point(2, 0);

            Assert.IsTrue(fpga.Contains(point));

            point.X = -1;

            Assert.IsFalse(fpga.Contains(point));

            point.X = 31;

            Assert.IsFalse(fpga.Contains(point));

            point.X = 2;
            point.Y = -1;

            Assert.IsFalse(fpga.Contains(point));

            point.Y = 10;

            Assert.IsFalse(fpga.Contains(point));
        }

        private void checkPointInside(Point p, double xMin, double yMin, double xMax, double yMax)
        {
            Assert.IsTrue(p.X >= xMin && p.X <= xMax);
            Assert.IsTrue(p.Y >= yMin && p.Y <= yMax);
        }
    }
}
