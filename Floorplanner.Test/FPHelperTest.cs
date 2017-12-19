using Floorplanner.Models.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Floorplanner.Test
{
    [TestClass]
    public class FPHelperTest
    {
        [TestMethod]
        public void Sum()
        {
            IDictionary<BlockType, int> a = new Dictionary<BlockType, int>()
            {
                { BlockType.CLB, 10 },
                { BlockType.BRAM, 10 },
                { BlockType.DSP, 10 },
                { BlockType.Forbidden, 10 },
                { BlockType.Null, 10 }
            };

            IDictionary<BlockType, int> b = new Dictionary<BlockType, int>()
            {
                { BlockType.CLB, 10 },
                { BlockType.BRAM, 5 },
                { BlockType.DSP, 7 },
                { BlockType.Forbidden, 0 },
                { BlockType.Null, 4 }
            };

            IDictionary<BlockType, int> c = a.Sum(b);

            Assert.AreEqual(10, a[BlockType.CLB]);
            Assert.AreEqual(10, b[BlockType.CLB]);
            Assert.AreEqual(20, c[BlockType.CLB]);

            Assert.AreEqual(10, a[BlockType.BRAM]);
            Assert.AreEqual(5, b[BlockType.BRAM]);
            Assert.AreEqual(15, c[BlockType.BRAM]);

            Assert.AreEqual(10, a[BlockType.DSP]);
            Assert.AreEqual(7, b[BlockType.DSP]);
            Assert.AreEqual(17, c[BlockType.DSP]);

            Assert.AreEqual(10, a[BlockType.Forbidden]);
            Assert.AreEqual(0, b[BlockType.Forbidden]);
            Assert.AreEqual(10, c[BlockType.Forbidden]);

            Assert.AreEqual(10, a[BlockType.Null]);
            Assert.AreEqual(4, b[BlockType.Null]);
            Assert.AreEqual(14, c[BlockType.Null]);
        }

        [TestMethod]
        public void Sub()
        {
            IDictionary<BlockType, int> a = new Dictionary<BlockType, int>()
            {
                { BlockType.CLB, 10 },
                { BlockType.BRAM, 10 },
                { BlockType.DSP, 10 },
                { BlockType.Forbidden, 10 },
                { BlockType.Null, 10 }
            };

            IDictionary<BlockType, int> b = new Dictionary<BlockType, int>()
            {
                { BlockType.CLB, 10 },
                { BlockType.BRAM, 5 },
                { BlockType.DSP, 7 },
                { BlockType.Forbidden, 0 },
                { BlockType.Null, 4 }
            };

            IDictionary<BlockType, int> c = a.Sub(b);

            Assert.AreEqual(10, a[BlockType.CLB]);
            Assert.AreEqual(10, b[BlockType.CLB]);
            Assert.AreEqual(0, c[BlockType.CLB]);

            Assert.AreEqual(10, a[BlockType.BRAM]);
            Assert.AreEqual(5, b[BlockType.BRAM]);
            Assert.AreEqual(5, c[BlockType.BRAM]);

            Assert.AreEqual(10, a[BlockType.DSP]);
            Assert.AreEqual(7, b[BlockType.DSP]);
            Assert.AreEqual(3, c[BlockType.DSP]);

            Assert.AreEqual(10, a[BlockType.Forbidden]);
            Assert.AreEqual(0, b[BlockType.Forbidden]);
            Assert.AreEqual(10, c[BlockType.Forbidden]);

            Assert.AreEqual(10, a[BlockType.Null]);
            Assert.AreEqual(4, b[BlockType.Null]);
            Assert.AreEqual(6, c[BlockType.Null]);
        }
    }
}
