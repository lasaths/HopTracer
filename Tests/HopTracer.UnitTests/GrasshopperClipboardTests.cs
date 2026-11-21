using HopTracer.Core.Services;
using Xunit;

namespace HopTracer.UnitTests;

public class GrasshopperClipboardTests
{
    [Fact]
    public void Read_ReturnsComponentIds_AndSkipsParamsAndGroups()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Archive name="Root">
              <chunks>
                <chunk name="Clipboard">
                  <chunks>
                    <chunk name="DefinitionObjects">
                      <chunks>
                        <chunk name="Object">
                          <items>
                            <item name="Name">List Item</item>
                          </items>
                          <chunks>
                            <chunk name="Container">
                              <items>
                                <item name="InstanceGuid">aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee</item>
                                <item name="Name">List Item</item>
                              </items>
                              <chunks>
                                <chunk name="InputParam" index="0">
                                  <items>
                                    <item name="InstanceGuid">bbbbbbbb-bbbb-cccc-dddd-eeeeeeeeeeee</item>
                                  </items>
                                </chunk>
                              </chunks>
                            </chunk>
                          </chunks>
                        </chunk>
                        <chunk name="Object">
                          <items>
                            <item name="Name">Group</item>
                          </items>
                          <chunks>
                            <chunk name="Container">
                              <items>
                                <item name="Description">A group of Grasshopper objects</item>
                                <item name="InstanceGuid">cccccccc-bbbb-cccc-dddd-eeeeeeeeeeee</item>
                                <item name="Name">Group</item>
                                <item name="ID" index="0">dddddddd-bbbb-cccc-dddd-eeeeeeeeeeee</item>
                              </items>
                            </chunk>
                          </chunks>
                        </chunk>
                        <chunk name="Object">
                          <items>
                            <item name="Name">Number Slider</item>
                          </items>
                          <chunks>
                            <chunk name="Container">
                              <items>
                                <item name="InstanceGuid">eeeeeeee-bbbb-cccc-dddd-eeeeeeeeeeee</item>
                                <item name="Name">Number Slider</item>
                              </items>
                            </chunk>
                          </chunks>
                        </chunk>
                      </chunks>
                    </chunk>
                  </chunks>
                </chunk>
                <chunk name="Definition">
                  <chunks>
                    <chunk name="DefinitionObjects">
                      <chunks>
                        <chunk name="Object">
                          <chunks>
                            <chunk name="Container">
                              <items>
                                <item name="InstanceGuid">ffffffff-bbbb-cccc-dddd-eeeeeeeeeeee</item>
                                <item name="Name">Panel</item>
                              </items>
                            </chunk>
                          </chunks>
                        </chunk>
                      </chunks>
                    </chunk>
                  </chunks>
                </chunk>
              </chunks>
            </Archive>
            """;

        var read = GrasshopperClipboard.Read(xml);

        Assert.Null(read.Error);
        Assert.Equal(
            new[]
            {
                "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "eeeeeeee-bbbb-cccc-dddd-eeeeeeeeeeee"
            },
            read.Ids);
    }

    [Fact]
    public void Read_RejectsTextThatIsNotAGrasshopperSelection()
    {
        var read = GrasshopperClipboard.Read("not xml");

        Assert.Equal("Clipboard is not a Grasshopper selection.", read.Error);
        Assert.Empty(read.Ids);
    }
}
