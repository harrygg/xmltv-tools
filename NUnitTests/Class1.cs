using NUnit.Framework;
using wgmulti;

namespace Tests
{
  [TestFixture]
  public class UnitTest1
  {
    [Test]
    public void Run_External_XML_Config_Nunit()
    {
      var te = new TestEnvironment(ppType: PostProcessType.NONE);
      te.RunWgmulti(useJsonConfig: false);

      ResultChecks.CheckElementsAfterNoPostProcess(te);
    }
  }
}
