using HopTracer.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using System.Text.Json;
using Xunit;

namespace HopTracer.UnitTests;

public class GhxParserTests
{
    [Fact]
    public void Parse_UsesBoundsForNodeSizeAndPosition_WhenBoundsExist()
    {
        var parser = new GhxParser(NullLogger<GhxParser>.Instance);
        var path = GetFixturePath("SampleDefinition.ghx");
        var graph = parser.Parse(path);

        Assert.True(graph.Nodes.Count > 0);

        var withBounds = graph.Nodes.Values.Where(n => n.W > 0 && n.H > 0).ToList();
        Assert.True(withBounds.Count > 0);
        Assert.All(withBounds, n =>
        {
            Assert.True(n.W > 0);
            Assert.True(n.H > 0);
        });
    }

    [Fact]
    public void Parse_TracksClusterPreviewDiagnostics_WhenClusterDocumentExists()
    {
        var parser = new GhxParser(NullLogger<GhxParser>.Instance);
        var path = GetFixturePath("SampleDefinition.ghx");
        var graph = parser.Parse(path);

        var clusterNodes = graph.Nodes.Values
            .Where(n => n.Properties.TryGetValue("IsCluster", out var isCluster) &&
                        string.Equals(isCluster, "true", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(clusterNodes);
        Assert.All(clusterNodes, node =>
        {
            Assert.True(node.Properties.ContainsKey("ClusterHash"));
            Assert.True(node.Properties.ContainsKey("ClusterSize"));
            Assert.True(node.Properties.ContainsKey("ClusterPreviewStatus"));
        });
        Assert.True(graph.Metadata.ClusterPreviewParsed + graph.Metadata.ClusterPreviewFailed + graph.Metadata.ClusterPreviewDepthLimitHits >= 1);
    }

    [Fact]
    public void Parse_ParsesLegacyParamInputOutputConnections()
    {
        var parser = new GhxParser(NullLogger<GhxParser>.Instance);
        var path = GetFixturePath("SampleDefinition.ghx");
        var graph = parser.Parse(path);

        Assert.True(graph.Nodes.Count > 0);
        Assert.True(graph.Edges.Count > 0);
        Assert.All(graph.Edges, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Source));
            Assert.False(string.IsNullOrWhiteSpace(e.Target));
        });
    }

    [Fact]
    public void Parse_ExtractsGroupMembers_ForGrasshopperGroups()
    {
        var parser = new GhxParser(NullLogger<GhxParser>.Instance);
        var path = GetFixturePath("SampleDefinition_Modified.ghx");
        var graph = parser.Parse(path);

        var groups = graph.Nodes.Values
            .Where(n => n.Properties.TryGetValue("IsGroup", out var isGroup) &&
                        string.Equals(isGroup, "true", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(groups);

        var groupWithMembers = groups.FirstOrDefault(n =>
            n.Properties.TryGetValue("GroupMemberIds", out var raw) &&
            !string.IsNullOrWhiteSpace(raw));

        Assert.NotNull(groupWithMembers);

        var memberJson = groupWithMembers!.Properties["GroupMemberIds"];
        var memberIds = JsonSerializer.Deserialize<List<string>>(memberJson);

        Assert.NotNull(memberIds);
        Assert.NotEmpty(memberIds!);
    }

    [Fact]
    public void Parse_ClusterPreviewGraph_ContainsPortsAndPanelContent()
    {
        var innerClusterXml = """
            <Archive>
              <chunks>
                <chunk name="Object">
                  <chunks>
                    <chunk name="Container">
                      <items>
                        <item name="InstanceGuid">slider-node</item>
                        <item name="Name">Number Slider</item>
                        <item name="NickName">Slider</item>
                      </items>
                      <chunks>
                        <chunk name="ParameterData">
                          <chunks>
                            <chunk name="param_output">
                              <items>
                                <item name="InstanceGuid">slider-out-0</item>
                                <item name="Name">Value</item>
                                <item name="NickName">V</item>
                              </items>
                            </chunk>
                          </chunks>
                        </chunk>
                      </chunks>
                    </chunk>
                  </chunks>
                </chunk>
                <chunk name="Object">
                  <chunks>
                    <chunk name="Container">
                      <items>
                        <item name="InstanceGuid">panel-node</item>
                        <item name="Name">Panel</item>
                        <item name="NickName">Panel</item>
                      </items>
                      <chunks>
                        <chunk name="PanelProperties">
                          <items>
                            <item name="UserText">changed panel text</item>
                          </items>
                        </chunk>
                        <chunk name="ParameterData">
                          <chunks>
                            <chunk name="param_input">
                              <items>
                                <item name="InstanceGuid">panel-in-0</item>
                                <item name="Name">Input</item>
                                <item name="NickName">I</item>
                                <item name="Source">slider-out-0</item>
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

        var clusterBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(innerClusterXml));
        var outerXml = $$"""
            <Archive>
              <chunks>
                <chunk name="Object">
                  <items>
                    <item name="ClusterDocument">
                      <stream>{{clusterBase64}}</stream>
                    </item>
                  </items>
                  <chunks>
                    <chunk name="Container">
                      <items>
                        <item name="InstanceGuid">cluster-node-1</item>
                        <item name="Name">Cluster</item>
                        <item name="NickName">Cluster</item>
                      </items>
                    </chunk>
                  </chunks>
                </chunk>
              </chunks>
            </Archive>
            """;

        var parser = new GhxParser(NullLogger<GhxParser>.Instance);
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ghx");

        try
        {
            File.WriteAllText(tempPath, outerXml);
            var graph = parser.Parse(tempPath);
            var clusterNode = graph.Nodes.Values.Single();

            Assert.True(clusterNode.Properties.TryGetValue("ClusterPreviewStatus", out var status));
            Assert.Equal("parsed", status);
            Assert.True(clusterNode.Properties.TryGetValue("ClusterPreviewGraph", out var rawPreview));

            using var previewDoc = JsonDocument.Parse(rawPreview);
            var root = previewDoc.RootElement;
            var nodes = root.GetProperty("Nodes");

            var slider = nodes.EnumerateArray()
                .First(n => n.TryGetProperty("Id", out var id) && id.GetString() == "slider-node");
            Assert.True(slider.TryGetProperty("Outputs", out var sliderOutputs));
            Assert.Equal(1, sliderOutputs.GetArrayLength());
            Assert.Equal("slider-out-0", sliderOutputs[0].GetProperty("Id").GetString());

            var panel = nodes.EnumerateArray()
                .First(n => n.TryGetProperty("Id", out var id) && id.GetString() == "panel-node");
            Assert.True(panel.TryGetProperty("Inputs", out var panelInputs));
            Assert.Equal(1, panelInputs.GetArrayLength());

            var panelProps = panel.GetProperty("Properties");
            Assert.Equal("changed panel text", panelProps.GetProperty("PanelContent").GetString());

            var edges = root.GetProperty("Edges");
            Assert.Equal(1, edges.GetArrayLength());
            Assert.Equal("slider-out-0", edges[0].GetProperty("SourcePort").GetString());
            Assert.Equal("panel-in-0", edges[0].GetProperty("TargetPort").GetString());
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void Parse_ExtractsScriptSource_FromScriptChunkText()
    {
        var parser = new GhxParser(NullLogger<GhxParser>.Instance);
        var path = GetFixturePath("SampleDefinition.ghx");
        var graph = parser.Parse(path);

        var scriptNodes = graph.Nodes.Values
            .Where(n => n.Properties.TryGetValue("ScriptSource", out var src) &&
                        !string.IsNullOrWhiteSpace(src))
            .ToList();

        Assert.NotEmpty(scriptNodes);
        Assert.Contains(scriptNodes, n => n.Properties["ScriptSource"].Contains("RunScript(", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ExtractsShortScriptSource_FromBase64ScriptChunkText()
    {
        const string code = "a = x + 1;";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(code));
        var xml = $$"""
            <Archive>
              <chunks>
                <chunk name="Object">
                  <chunks>
                    <chunk name="Container">
                      <items>
                        <item name="InstanceGuid">script-node-1</item>
                        <item name="Name">C# Script</item>
                        <item name="NickName">C#</item>
                      </items>
                      <chunks>
                        <chunk name="Script">
                          <items>
                            <item name="Text">{{encoded}}</item>
                          </items>
                        </chunk>
                      </chunks>
                    </chunk>
                  </chunks>
                </chunk>
              </chunks>
            </Archive>
            """;

        var parser = new GhxParser(NullLogger<GhxParser>.Instance);
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ghx");

        try
        {
            File.WriteAllText(tempPath, xml);
            var graph = parser.Parse(tempPath);
            var node = Assert.Single(graph.Nodes.Values);

            Assert.True(node.Properties.TryGetValue("ScriptSource", out var scriptSource));
            Assert.Equal(code, scriptSource);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void Parse_ExtractsScriptSource_FromTopLevelText_WhenScriptSignalsExist()
    {
        const string code = "a = x * 2;";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(code));
        var xml = $$"""
            <Archive>
              <chunks>
                <chunk name="Object">
                  <items>
                    <item name="Text">{{encoded}}</item>
                    <item name="ScriptComponentVersion">3</item>
                    <item name="Title">C#</item>
                  </items>
                  <chunks>
                    <chunk name="Container">
                      <items>
                        <item name="InstanceGuid">script-node-2</item>
                        <item name="Name">C# Script</item>
                        <item name="NickName">C#</item>
                      </items>
                    </chunk>
                  </chunks>
                </chunk>
              </chunks>
            </Archive>
            """;

        var parser = new GhxParser(NullLogger<GhxParser>.Instance);
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ghx");

        try
        {
            File.WriteAllText(tempPath, xml);
            var graph = parser.Parse(tempPath);
            var node = Assert.Single(graph.Nodes.Values);

            Assert.True(node.Properties.TryGetValue("ScriptSource", out var scriptSource));
            Assert.Equal(code, scriptSource);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static string GetFixturePath(string fileName)
    {
        var root = FindRepoRoot();
        return Path.Combine(root, "Tests", "data", fileName);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "Source", "HopTracer.sln")))
            {
                return dir;
            }

            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root for test data.");
    }
}
