using Microsoft.VisualStudio.TestTools.UnitTesting;
using wgmulti;

namespace Tests
{
  [TestClass]
  public class UtilsTest
  {
    [TestMethod]
    public void Test_GetIniFilesInDir()
    {
      var files = Utils.GetIniFilesInDir(@"..\..\Test files\");
      if (files.Count != 4)
        Assert.Fail("Incorrect siteini files number!");
    }

    [TestMethod]
    public void Test_AddOffset_AddInt()
    {
      var expected = "20170109010000 +0200";
      var actual = Utils.AddOffset("20170109000000 +0200", 1);

      if (expected != actual)
        Assert.Fail("1 hour offset not added correctly!");
    }


    [TestMethod]
    public void Test_AddOffset_AddDouble()
    {
      var expected = "20170109013000 +0200";
      var actual = Utils.AddOffset("20170109000000 +0200", 1.5);

      if (expected != actual)
        Assert.Fail("1.5 hours offset not added correctly!");
    }


    [TestMethod]
    public void Test_AddOffset_ConvertToLocal()
    {
      var expected = "20170109010000 +0200";
      var actual = Utils.AddOffset("20170109000000 +0100");

      if (expected != actual)
        Assert.Fail("Time not converted to local!");
    }
  }
}
