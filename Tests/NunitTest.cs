using NUnit.Framework;
using System.Diagnostics;
using wgmulti;

namespace Tests
{
  [TestFixture]
  public class NunitTest
  {
    [Test]
    public void Run_With_XML_Config()
    {
      var te = new TestEnvironment(ppType: PostProcessType.NONE);
      te.RunWgmulti(useJsonConfig: false);
      ResultChecks.CheckElementsAfterNoPostProcess(te);
    }

    [Test]
    public void Run_With_XML_Config_REX_PostProcess()
    {
      var te = new TestEnvironment(ppType: PostProcessType.REX);
      te.RunWgmulti(useJsonConfig: false);
      ResultChecks.ChecksAfterPostProcess(te);
    }
  }
}
