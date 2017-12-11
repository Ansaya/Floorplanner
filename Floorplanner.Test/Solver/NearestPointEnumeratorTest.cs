using Floorplanner.Models.Solver;
using Floorplanner.Solver;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Floorplanner.Test.Solver
{
    [TestClass]
    public class NearestPointEnumeratorTest
    {
        [TestMethod]
        public void Current()
        {
            Point one = new Point(1, 0);
            Point two = new Point(2, 0);
            Point three = new Point(3, 0);
            Point four = new Point(4, 0);
            Point five = new Point(5, 0);

            IEnumerable<Point> points = new Point[] { one, five, two, four, three };

            IEnumerator<Point> dpe = new NearestPointEnumerator(new Point(0, 0), points);

            Assert.IsNull(dpe.Current);

            Assert.IsTrue(dpe.MoveNext());

            Assert.AreEqual(one, dpe.Current);

            Assert.IsTrue(dpe.MoveNext());

            Assert.AreEqual(two, dpe.Current);

            Assert.IsTrue(dpe.MoveNext());

            Assert.AreEqual(three, dpe.Current);

            Assert.IsTrue(dpe.MoveNext());

            Assert.AreEqual(four, dpe.Current);

            Assert.IsTrue(dpe.MoveNext());

            Assert.AreEqual(five, dpe.Current);

            Assert.IsFalse(dpe.MoveNext());
        }

        [TestMethod]
        public void Skip()
        {
            Point one = new Point(1, 0);
            Point two = new Point(2, 0);
            Point three = new Point(3, 0);
            Point four = new Point(4, 0);
            Point five = new Point(5, 0);

            IEnumerable<Point> points = new Point[] { one, five, two, four, three };

            NearestPointEnumerator dpe = new NearestPointEnumerator(new Point(0, 0), points);

            Assert.IsNull(dpe.Current);

            Assert.IsTrue(dpe.MoveNext());

            Assert.AreEqual(one, dpe.Current);

            Assert.IsTrue(dpe.MoveNext());

            Assert.AreEqual(two, dpe.Current);

            dpe.Skip(new Point[] { two, three, four });

            Assert.AreEqual(two, dpe.Current);

            Assert.IsTrue(dpe.MoveNext());

            Assert.AreEqual(five, dpe.Current);

            Assert.IsFalse(dpe.MoveNext());
        }
    }
}
