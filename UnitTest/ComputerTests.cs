using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RemoteMsiManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RemoteMsiManager.Tests
{
    [TestClass()]
    public class ComputerTests
    {
        [TestMethod()]
        public void GetFormattedIdentifyingNumberTest()
        {
            string identifyingNumber = "{26A24AE4-039D-4CA4-87B4-2F86418065F0}";

            Assert.AreEqual(Computer.GetFormattedIdentifyingNumber(identifyingNumber), "26A24AE4-039D-4CA4-87B4-2F86418065F0");
        }
    }
}
