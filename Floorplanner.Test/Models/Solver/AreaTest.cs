using Floorplanner.Models;
using Floorplanner.Models.Components;
using Floorplanner.Models.Solver;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Floorplanner.Test.Models.Solver
{
    [TestClass]
    public class AreaTest
    {
        private Design _design = DesignFactory.Design;

        private Area area1;
        
        public AreaTest()
        {
            area1 = new Area(_design.FPGA, _design.Regions[0], new Point(2, 0))
            {
                Width = 5,
                Height = 9
            };
        }

        [TestMethod]
        public void GetCenter()
        {
            Point center = new Point(4.5, 4.5);

            checkCorners(area1, 2, 0, 7, 9);

            Assert.AreEqual(center.X, area1.Center.X);
            Assert.AreEqual(center.Y, area1.Center.Y);
        }

        [TestMethod]
        public void GetTileRows()
        {
            checkCorners(area1, 2, 0, 7, 9);

            IEnumerable<int> tileRows = area1.TileRows;

            Assert.AreEqual(2, tileRows.Count());
            Assert.IsTrue(tileRows.Contains(0) && tileRows.Contains(1));

            area1.TryShape(Floorplanner.Models.Solver.Action.Shrink, Direction.Up, 5);

            checkCorners(area1, 2, 5, 7, 9);

            tileRows = area1.TileRows;

            Assert.AreEqual(1, tileRows.Count());
            Assert.IsTrue(tileRows.Contains(1));

            area1.TryShape(Floorplanner.Models.Solver.Action.Expand, Direction.Up, 5);

            checkCorners(area1, 2, 0, 7, 9);
        }

        [TestMethod]
        public void GetValue()
        {
            checkCorners(area1, 2, 0, 7, 9);
            Assert.AreEqual(6 * 10, area1.Value);
        }

        [TestMethod]
        public void GetResources()
        {
            checkCorners(area1, 2, 0, 7, 9);

            IDictionary<BlockType, int> areaRes = area1.Resources;

            Assert.AreEqual(40, areaRes[BlockType.CLB]);
            Assert.AreEqual(10, areaRes[BlockType.DSP]);
            Assert.AreEqual(10, areaRes[BlockType.BRAM]);
            Assert.AreEqual(0, areaRes[BlockType.Null]);
            Assert.AreEqual(0, areaRes[BlockType.Forbidden]);
            Assert.AreEqual(area1.Value, areaRes.Sum(pair => pair.Value));
        }

        [TestMethod]
        public void IsSufficient()
        {
            checkCorners(area1, 2, 0, 7, 9);

            Assert.IsTrue(area1.IsSufficient);

            area1.TryShape(Floorplanner.Models.Solver.Action.Shrink, Direction.Up);

            checkCorners(area1, 2, 1, 7, 9);

            Assert.IsFalse(area1.IsSufficient);

            area1.TryShape(Floorplanner.Models.Solver.Action.Expand, Direction.Up);
        }

        [TestMethod]
        public void GetPoints()
        {
            IEnumerable<Point> areaPoints = area1.Points;

            checkCorners(area1, 2, 0, 7, 9);
            Assert.AreEqual(6 * 10, areaPoints.Count());

            foreach (Point p in areaPoints)
                checkPointInside(p, 2, 0, 7, 9);
        }

        [TestMethod]
        public void IsValid()
        {
            Assert.AreEqual(RegionType.Reconfigurable, area1.Type);
            Assert.IsTrue(_design.FPGA.LRecCol[(int)area1.TopLeft.X]);
            Assert.IsTrue(_design.FPGA.RRecCol[(int)area1.TopLeft.X + area1.Width]);

            Assert.IsTrue(area1.IsValid);

            area1.MoveTo(new Point(3, 0));

            Assert.IsFalse(area1.IsValid);
        }

        [TestMethod]
        public void TryMove()
        {
            checkCorners(area1, 2, 0, 7, 9);
            Assert.IsTrue(9 < _design.FPGA.Xmax);
            Assert.IsTrue(9 == _design.FPGA.Ymax);

            area1.MoveTo(new Point(3, 0));

            checkCorners(area1, 3, 0, 8, 9);

            area1.MoveTo(new Point(2, 0));

            checkCorners(area1, 2, 0, 7, 9);

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => area1.MoveTo(new Point(2, 1)));

            checkCorners(area1, 2, 0, 7, 9);

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => area1.MoveTo(new Point(2, -1)));

            checkCorners(area1, 2, 0, 7, 9);
        }

        [TestMethod]
        public void TryMoveTo()
        {
            Point newPoint = new Point(18, 0);
            Point oldPoint = area1.TopLeft;

            checkCorners(area1, 2, 0, 7, 9);
            Assert.IsTrue(23 < _design.FPGA.Xmax);
            Assert.IsTrue(9 == _design.FPGA.Ymax);

            Assert.IsTrue(area1.TryMoveTo(newPoint));

            checkCorners(area1, 18, 0, 23, 9);
            Assert.AreNotSame(newPoint, area1.TopLeft);

            Assert.IsTrue(area1.TryMoveTo(oldPoint));

            checkCorners(area1, 2, 0, 7, 9);
        }

        [TestMethod]
        public void TryShape()
        {
            checkCorners(area1, 2, 0, 7, 9);
            Assert.IsTrue(11 < _design.FPGA.Xmax);
            Assert.IsTrue(9 == _design.FPGA.Ymax);

            Assert.IsTrue(area1
                .TryShape(Floorplanner.Models.Solver.Action.Expand, Direction.Right));

            checkCorners(area1, 2, 0, 8, 9);

            Assert.IsTrue(area1
                .TryShape(Floorplanner.Models.Solver.Action.Expand, Direction.Right, 3));

            checkCorners(area1, 2, 0, 11, 9);

            Assert.IsTrue(area1
                .TryShape(Floorplanner.Models.Solver.Action.Shrink, Direction.Right, 4));

            checkCorners(area1, 2, 0, 7, 9);

            Assert.IsFalse(area1.TryShape(Floorplanner.Models.Solver.Action.Expand, Direction.Up));

            checkCorners(area1, 2, 0, 7, 9);

            Assert.IsFalse(area1.TryShape(Floorplanner.Models.Solver.Action.Expand, Direction.Down));

            checkCorners(area1, 2, 0, 7, 9);

            Assert.IsTrue(area1.TryShape(Floorplanner.Models.Solver.Action.Shrink, Direction.Up, 3));

            checkCorners(area1, 2, 3, 7, 9);

            Assert.IsTrue(area1.TryShape(Floorplanner.Models.Solver.Action.Expand, Direction.Up, 3));

            checkCorners(area1, 2, 0, 7, 9);
        }

        [TestMethod]
        public void IsOverlapping()
        {
            Area area2 = new Area(_design.FPGA, _design.Regions[1], new Point(8, 0))
            {
                Width = 3,
                Height = 3
            };

            checkCorners(area1, 2, 0, 7, 9);
            checkCorners(area2, 8, 0, 11, 3);
            Assert.AreEqual(RegionType.Reconfigurable, area1.Type);
            Assert.AreEqual(RegionType.Reconfigurable, area2.Type);

            Assert.IsFalse(area1.IsOverlapping(area2));
            Assert.IsFalse(area2.IsOverlapping(area1));

            area2.MoveTo(new Point(6, 0));
            checkCorners(area2, 6, 0, 9, 3);

            Assert.IsTrue(area1.IsOverlapping(area2));
            Assert.IsTrue(area2.IsOverlapping(area1));

            area1.TryShape(Floorplanner.Models.Solver.Action.Shrink, Direction.Up, 4);
            checkCorners(area1, 2, 4, 7, 9);

            Assert.IsTrue(area1.IsOverlapping(area2));
            Assert.IsTrue(area2.IsOverlapping(area1));

            area1.Region.GetType().GetProperty("Type").SetValue(area1.Region, RegionType.Static);

            Assert.IsFalse(area1.IsOverlapping(area2));
            Assert.IsFalse(area2.IsOverlapping(area1));

            area1.TryShape(Floorplanner.Models.Solver.Action.Expand, Direction.Up, 4);
            checkCorners(area1, 2, 0, 7, 9);
            area1.Region.GetType().GetProperty("Type").SetValue(area1.Region, RegionType.Reconfigurable);
        }

        private void checkCorners(Area a, double xMin, double yMin, double xMax, double yMax)
        {
            Assert.AreEqual(xMin, a.TopLeft.X);
            Assert.AreEqual(yMin, a.TopLeft.Y);
            Assert.AreEqual(xMax, a.TopLeft.X + a.Width);
            Assert.AreEqual(yMax, a.TopLeft.Y + a.Height);
        }

        private void checkPointInside(Point p, double xMin, double yMin, double xMax, double yMax)
        {
            Assert.IsTrue(p.X >= xMin && p.X <= xMax);
            Assert.IsTrue(p.Y >= yMin && p.Y <= yMax);
        }
    }
}
