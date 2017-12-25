using NUnit.Framework;

namespace NunitTests
{
  [TestFixture]
  public class RunTests
  {
    [Test]
    public void Run_With_XML_Config()
    {
      var te = new TestEnvironment(PostProcessType.NONE, false, "xml_config_no_pp");
      te.CreateWGRunTimeConfig(useJsonConfig: false);
      te.RunWgmulti();
      Verify.AllElementsExistAfterNoPostProcess(te);
      Verify.ChannelHasStartHour(te.outputEpg, "Канал 1 ID", "0000");
    }

    [Test]
    public void Run_With_XML_Config_REX_PostProcess()
    {
      var te = new TestEnvironment(PostProcessType.REX, false, "xml_config_rex_pp");
      te.CreateWGRunTimeConfig(useJsonConfig: false);
      te.RunWgmulti();
      Verify.AllElementsExistAfterPostProcess(te);
      Verify.ChannelHasStartHour(te.outputEpg, "Канал 1 ID", "0000");
    }

    [Test]
    public void Run_With_JSON_Config()
    {
      var te = new TestEnvironment(PostProcessType.NONE, false, "json_config_no_pp");
      te.CreateWGRunTimeConfig();
      te.RunWgmulti();
      Verify.AllElementsExistAfterNoPostProcess(te);
      Verify.ChannelHasStartHour(te.outputEpg, "Канал 1 ID", "0000");
    }

    [Test]
    public void Run_With_JSON_Config_REX_PostProcess()
    {
      var te = new TestEnvironment(PostProcessType.REX, false, "json_config_rex_pp");
      te.CreateWGRunTimeConfig();
      te.RunWgmulti();
      Verify.AllElementsExistAfterPostProcess(te);
      Verify.ChannelHasStartHour(te.outputEpg, "Канал 1 ID", "0000");
    }

    [Test]
    public void Run_With_JSON_Config_Add_Global_Offset()
    {
      var te = new TestEnvironment(PostProcessType.NONE, false, "json_config_global_offset");
      te.CreateWGRunTimeConfig();
      te.RunWgmulti();
      Verify.AllElementsExistAfterNoPostProcess(te);
      Verify.ChannelHasStartHour(te.outputEpg, "Канал 1 ID", "0100");
    }
  }
}
