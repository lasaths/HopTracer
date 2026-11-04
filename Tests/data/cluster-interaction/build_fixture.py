#!/usr/bin/env python3
"""Build the cluster-interaction mock JSON, wrapped HTML, and synthetic GHX pair."""
from __future__ import annotations

import argparse
import base64
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
VIEWER = HERE.parents[3] / "Source" / "HopTracer.Web" / "wwwroot" / "diff_viewer.html"


def graph_payload(nodes, edges, depth=1):
    return {"nodes": nodes, "edges": edges, "truncated": False, "depth": depth}


def node(**kwargs):
    return kwargs


def edge(source, target, source_port="", target_port=""):
    return {
        "source": source,
        "target": target,
        "sourcePort": source_port,
        "targetPort": target_port,
    }


NESTED_OLD = graph_payload(
    [
        node(
            id="nest-seed",
            name="Number Slider",
            nickname="Seed",
            x=20,
            y=30,
            w=120,
            h=24,
            properties={"Value": "1"},
            inputs=[],
            outputs=[{"id": "nest-seed-out", "name": "Value", "nickname": "V", "kind": "output", "value": "1"}],
        ),
        node(
            id="nest-gone",
            name="Panel",
            nickname="Gone",
            x=170,
            y=24,
            w=90,
            h=40,
            properties={"UserText": "old nested note", "PanelContent": "old nested note"},
            inputs=[{"id": "nest-gone-in", "name": "Input", "nickname": "I", "kind": "input"}],
            outputs=[],
        ),
    ],
    [edge("nest-seed", "nest-gone")],
    depth=2,
)

NESTED_NEW = graph_payload(
    [
        node(
            id="nest-seed",
            name="Number Slider",
            nickname="Seed",
            x=20,
            y=30,
            w=120,
            h=24,
            properties={"Value": "7"},
            inputs=[],
            outputs=[{"id": "nest-seed-out", "name": "Value", "nickname": "V", "kind": "output", "value": "7"}],
        ),
        node(
            id="nest-hello",
            name="Panel",
            nickname="Hello",
            x=170,
            y=24,
            w=90,
            h=40,
            properties={"UserText": "hello nested", "PanelContent": "hello nested"},
            inputs=[{"id": "nest-hello-in", "name": "Input", "nickname": "I", "kind": "input"}],
            outputs=[],
        ),
    ],
    [edge("nest-seed", "nest-hello")],
    depth=2,
)

INNER_OLD = graph_payload(
    [
        node(
            id="inner-slider",
            name="Number Slider",
            nickname="Count",
            x=40,
            y=80,
            w=140,
            h=24,
            properties={"Value": "10"},
            inputs=[],
            outputs=[{"id": "inner-slider-out", "name": "Value", "nickname": "V", "kind": "output", "value": "10"}],
        ),
        node(
            id="inner-csharp",
            name="C# Script",
            nickname="Inner C#",
            x=220,
            y=70,
            w=120,
            h=44,
            riskScore=91,
            riskReasons=["Script source changed"],
            properties={"ScriptSource": "using System;\npublic class Script { int a; }"},
            inputs=[{"id": "inner-csharp-in", "name": "x", "nickname": "x", "kind": "input", "value": "10"}],
            outputs=[{"id": "inner-csharp-out", "name": "A", "nickname": "A", "kind": "output"}],
        ),
        node(
            id="inner-panel",
            name="Panel",
            nickname="Note",
            x=40,
            y=160,
            w=110,
            h=48,
            properties={"UserText": "old cluster note", "PanelContent": "old cluster note"},
            inputs=[{"id": "inner-panel-in", "name": "Input", "nickname": "I", "kind": "input"}],
            outputs=[{"id": "inner-panel-out", "name": "Output", "nickname": "O", "kind": "output"}],
        ),
        node(
            id="inner-xy",
            name="XY Plane",
            nickname="XY",
            x=220,
            y=160,
            w=65,
            h=28,
            properties={},
            inputs=[{"id": "inner-xy-in", "name": "Input", "nickname": "I", "kind": "input"}],
            outputs=[],
        ),
        node(
            id="inner-nested",
            name="Cluster",
            nickname="Nested",
            x=400,
            y=80,
            w=90,
            h=28,
            isCluster=True,
            clusterHash="OLDNESTEDHASH",
            properties={
                "isCluster": "true",
                "ClusterHash": "OLDNESTEDHASH",
                "ClusterSize": "640",
                "clusterPreviewStatus": "parsed",
                "clusterPreviewMessage": "2 nodes, 1 edges.",
                "ClusterPreviewGraph": json.dumps(NESTED_OLD),
            },
            inputs=[{"id": "inner-nested-in", "name": "Input", "nickname": "I", "kind": "input"}],
            outputs=[],
        ),
    ],
    [
        edge("inner-slider", "inner-csharp"),
        edge("inner-csharp", "inner-nested"),
        edge("inner-slider", "inner-panel"),
        edge("inner-panel", "inner-xy"),
    ],
    depth=1,
)

INNER_NEW = graph_payload(
    [
        node(
            id="inner-slider",
            name="Number Slider",
            nickname="Count",
            x=40,
            y=80,
            w=140,
            h=24,
            riskScore=22,
            riskReasons=["Parameter value changed"],
            properties={"Value": "25"},
            inputs=[],
            outputs=[{"id": "inner-slider-out", "name": "Value", "nickname": "V", "kind": "output", "value": "25"}],
        ),
        node(
            id="inner-csharp",
            name="C# Script",
            nickname="Inner C#",
            x=220,
            y=70,
            w=120,
            h=44,
            riskScore=91,
            riskReasons=["Script source changed"],
            properties={"ScriptSource": "using System;\npublic class Script { }"},
            inputs=[{"id": "inner-csharp-in", "name": "x", "nickname": "x", "kind": "input", "value": "25"}],
            outputs=[{"id": "inner-csharp-out", "name": "A", "nickname": "A", "kind": "output"}],
        ),
        node(
            id="inner-area",
            name="Area",
            nickname="Area",
            x=40,
            y=160,
            w=66,
            h=24,
            riskScore=34,
            properties={},
            inputs=[{"id": "inner-area-in", "name": "Input", "nickname": "I", "kind": "input"}],
            outputs=[{"id": "inner-area-out", "name": "Area", "nickname": "A", "kind": "output"}],
        ),
        node(
            id="inner-xy",
            name="XY Plane",
            nickname="XY",
            x=220,
            y=160,
            w=65,
            h=28,
            properties={},
            inputs=[{"id": "inner-xy-in", "name": "Input", "nickname": "I", "kind": "input"}],
            outputs=[],
        ),
        node(
            id="inner-nested",
            name="Cluster",
            nickname="Nested",
            x=400,
            y=80,
            w=90,
            h=28,
            isCluster=True,
            clusterHash="NEWNESTEDHASH",
            properties={
                "isCluster": "true",
                "ClusterHash": "NEWNESTEDHASH",
                "ClusterSize": "720",
                "clusterPreviewStatus": "parsed",
                "clusterPreviewMessage": "2 nodes, 1 edges.",
                "ClusterPreviewGraph": json.dumps(NESTED_NEW),
            },
            inputs=[{"id": "inner-nested-in", "name": "Input", "nickname": "I", "kind": "input"}],
            outputs=[],
        ),
    ],
    [
        edge("inner-slider", "inner-csharp"),
        edge("inner-csharp", "inner-nested"),
        edge("inner-slider", "inner-area"),
        edge("inner-area", "inner-xy"),
    ],
    depth=1,
)

UTIL_OLD = graph_payload(
    [
        node(
            id="util-num",
            name="Number Slider",
            nickname="Util Count",
            x=30,
            y=40,
            w=130,
            h=24,
            properties={"Value": "3"},
        )
    ],
    [],
    depth=1,
)

UTIL_NEW = graph_payload(
    [
        node(
            id="util-num",
            name="Number Slider",
            nickname="Util Count",
            x=30,
            y=40,
            w=130,
            h=24,
            properties={"Value": "3"},
        ),
        node(
            id="util-relay",
            name="Relay",
            nickname="Relay",
            x=190,
            y=40,
            w=70,
            h=24,
            properties={},
        ),
    ],
    [edge("util-num", "util-relay")],
    depth=1,
)


def build_hop_data():
    return {
        "meta": {
            "fileOld": "cluster-old.ghx",
            "fileNew": "cluster-new.ghx",
            "commitHash": "clusterfix",
            "commitMessage": "cluster internals",
            "commitAuthor": "Lassie",
            "stageTimings": [
                {"name": "parse", "durationMs": 31},
                {"name": "match", "durationMs": 6},
                {"name": "diff", "durationMs": 4},
            ],
        },
        "oldMeta": {"name": "cluster-old.ghx", "date": "2026-03-12T10:15:00Z"},
        "newMeta": {"name": "cluster-new.ghx", "date": "2026-03-18T14:40:00Z"},
        "nodes": [
            {
                "id": "grp-rpg",
                "name": "Group",
                "nickname": "Random Point Generation",
                "status": "same",
                "x": 270,
                "y": 150,
                "w": 220,
                "h": 100,
                "properties": {"isGroup": "true", "GroupMemberIds": '["rectangle","csharp"]'},
            },
            {
                "id": "grp-tower",
                "name": "Group",
                "nickname": "Tower Create",
                "status": "same",
                "x": 530,
                "y": 155,
                "w": 200,
                "h": 70,
                "properties": {"isGroup": "true", "GroupMemberIds": '["xy","cluster"]'},
            },
            {
                "id": "grp-output",
                "name": "Group",
                "nickname": "Output",
                "status": "same",
                "x": 800,
                "y": 130,
                "w": 90,
                "h": 50,
                "properties": {"isGroup": "true", "GroupMemberIds": '["geo"]'},
            },
            {"id": "rectangle", "name": "Rectangle", "nickname": "Rectangle", "status": "same", "x": 279, "y": 160, "w": 65, "h": 84},
            {
                "id": "csharp",
                "name": "C# Script",
                "nickname": "C#",
                "status": "modified",
                "x": 364,
                "y": 170,
                "w": 112,
                "h": 44,
                "riskScore": 78,
                "riskReasons": ["Script source changed"],
                "dx": 0,
                "dy": 0,
                "properties": {"ScriptSource": "using System;\\npublic class Script { }"},
                "propertiesOld": {"ScriptSource": "using System;\\npublic class Script { int a; }"},
            },
            {"id": "xy", "name": "XY Plane", "nickname": "XY", "status": "same", "x": 547, "y": 175, "w": 65, "h": 28},
            {
                "id": "cluster",
                "name": "Cluster",
                "nickname": "Cluster",
                "status": "modified",
                "x": 633,
                "y": 175,
                "w": 81,
                "h": 28,
                "riskScore": 82,
                "riskReasons": ["Cluster internals changed"],
                "properties": {
                    "isCluster": "true",
                    "ClusterHash": "NEWCLUSTERHASH001",
                    "ClusterSize": "2200",
                    "clusterPreviewStatus": "parsed",
                    "clusterPreviewMessage": "5 nodes, 4 edges.",
                    "ClusterPreviewGraph": json.dumps(INNER_NEW),
                },
                "propertiesOld": {
                    "ClusterHash": "OLDCLUSTERHASH001",
                    "ClusterSize": "1800",
                    "clusterPreviewStatus": "parsed",
                    "clusterPreviewMessage": "5 nodes, 4 edges.",
                    "ClusterPreviewGraph": json.dumps(INNER_OLD),
                },
            },
            {
                "id": "util",
                "name": "Cluster",
                "nickname": "Util",
                "status": "modified",
                "x": 400,
                "y": 250,
                "w": 70,
                "h": 28,
                "riskScore": 41,
                "riskReasons": ["Cluster internals changed"],
                "properties": {
                    "isCluster": "true",
                    "ClusterHash": "NEWUTILHASH",
                    "ClusterSize": "900",
                    "clusterPreviewStatus": "parsed",
                    "clusterPreviewMessage": "2 nodes, 1 edges.",
                    "ClusterPreviewGraph": json.dumps(UTIL_NEW),
                },
                "propertiesOld": {
                    "ClusterHash": "OLDUTILHASH",
                    "ClusterSize": "700",
                    "clusterPreviewStatus": "parsed",
                    "clusterPreviewMessage": "1 nodes, 0 edges.",
                    "ClusterPreviewGraph": json.dumps(UTIL_OLD),
                },
            },
            {"id": "point-count", "name": "Point Count", "nickname": "Point Count", "status": "removed", "x": 560, "y": 220, "w": 100, "h": 28, "riskScore": 62},
            {"id": "area", "name": "Area", "nickname": "Area", "status": "added", "x": 740, "y": 175, "w": 66, "h": 24, "riskScore": 34},
            {
                "id": "geo",
                "name": "Geometry",
                "nickname": "Geo",
                "status": "modified",
                "x": 823,
                "y": 146,
                "w": 50,
                "h": 24,
                "riskScore": 8,
                "dx": 0,
                "dy": -38,
                "inputs": [{"id": "g", "status": "modified", "valueChanged": True, "valueOld": "a", "valueNew": "b"}],
            },
            {"id": "slider-a", "name": "Number Slider", "nickname": "Slider A", "status": "same", "x": 78, "y": 184, "w": 162, "h": 20, "riskScore": 12},
            {"id": "slider-b", "name": "Number Slider", "nickname": "Slider B", "status": "same", "x": 78, "y": 220, "w": 162, "h": 20, "riskScore": 10},
            {"id": "toggle", "name": "Toggle", "nickname": "Toggle", "status": "same", "x": 78, "y": 256, "w": 80, "h": 20, "riskScore": 4},
        ],
        "edges": [
            {"source": "csharp", "target": "point-count", "status": "removed"},
            {"source": "cluster", "target": "geo", "status": "same"},
            {"source": "area", "target": "geo", "status": "added"},
        ],
    }


def wrap_html(data: dict) -> str:
    src = VIEWER.read_text()
    data_js = json.dumps(data)
    hook = f"""
    <script>
    window.__pageErrors = [];
    window.addEventListener('error', e => window.__pageErrors.push(String(e.message || e.error)));
    window.addEventListener('unhandledrejection', e => window.__pageErrors.push(String(e.reason)));
    const origFetch = window.fetch.bind(window);
    window.fetch = function(url, opts) {{
      if (String(url).includes('/system/session_token')) {{
        return Promise.resolve(new Response(JSON.stringify({{ token: 'verify-token' }}), {{ headers: {{ 'Content-Type': 'application/json' }} }}));
      }}
      return origFetch(url, opts);
    }};
    window.HOP_DATA = {data_js};
    </script>
"""
    return src.replace("<script>window.HOP_DATA = {json_data};</script>", hook)


def gh_object(guid: str, name: str, nick: str, x: int, y: int, w: int, h: int, extra_chunks: str = "", extra_items: str = "") -> str:
    return f"""
                <chunk name="Object">
                  {extra_items}
                  <chunks>
                    <chunk name="Container">
                      <items>
                        <item name="InstanceGuid">{guid}</item>
                        <item name="Name">{name}</item>
                        <item name="NickName">{nick}</item>
                        <item name="Bounds">
                          <X>{x}</X>
                          <Y>{y}</Y>
                          <W>{w}</W>
                          <H>{h}</H>
                        </item>
                      </items>
                      {extra_chunks}
                    </chunk>
                  </chunks>
                </chunk>
"""


def cluster_object(guid: str, name: str, nick: str, x: int, y: int, w: int, h: int, payload_b64: str) -> str:
    return f"""
                <chunk name="Object">
                  <items>
                    <item name="ClusterDocument">
                      <stream>{payload_b64}</stream>
                    </item>
                  </items>
                  <chunks>
                    <chunk name="Container">
                      <items>
                        <item name="InstanceGuid">{guid}</item>
                        <item name="Name">{name}</item>
                        <item name="NickName">{nick}</item>
                        <item name="Bounds">
                          <X>{x}</X>
                          <Y>{y}</Y>
                          <W>{w}</W>
                          <H>{h}</H>
                        </item>
                      </items>
                    </chunk>
                  </chunks>
                </chunk>
"""


def archive(body: str) -> str:
    return f"""<Archive>
  <items>
    <item name="ArchiveVersion">
      <Major>0</Major>
      <Minor>2</Minor>
      <Revision>2</Revision>
    </item>
  </items>
  <chunks>
    <chunk name="Definition">
      <items>
        <item name="plugin_version">
          <Major>1</Major>
          <Minor>0</Minor>
          <Revision>8</Revision>
        </item>
      </items>
      <chunks>
        <chunk name="DefinitionProperties">
          <items>
            <item name="Name">Cluster Interaction Fixture</item>
            <item name="Description">Synthetic GHX with cluster internals and a nested cluster.</item>
          </items>
        </chunk>
{body}
      </chunks>
    </chunk>
  </chunks>
</Archive>
"""


def b64_xml(xml: str) -> str:
    return base64.b64encode(xml.encode("utf-8")).decode("ascii")


def build_nested_xml(seed_value: str, panel_nick: str, panel_text: str, panel_id: str) -> str:
    return f"""<Archive>
  <chunks>
{gh_object("nested-seed", "Number Slider", "Seed", 20, 30, 120, 24, extra_chunks=f'''
                      <chunks>
                        <chunk name="Slider">
                          <items>
                            <item name="Value">{seed_value}</item>
                          </items>
                        </chunk>
                      </chunks>
''')}
{gh_object(panel_id, "Panel", panel_nick, 170, 24, 90, 40, extra_chunks=f'''
                      <chunks>
                        <chunk name="PanelProperties">
                          <items>
                            <item name="UserText">{panel_text}</item>
                          </items>
                        </chunk>
                      </chunks>
''')}
  </chunks>
</Archive>
"""


def build_parent_inner_xml(*, slider_value: str, script: str, extra_nodes: str, nested_b64: str) -> str:
    script_b64 = b64_xml(script)
    return f"""<Archive>
  <chunks>
{gh_object("inner-slider", "Number Slider", "Count", 40, 80, 140, 24, extra_chunks=f'''
                      <chunks>
                        <chunk name="Slider">
                          <items>
                            <item name="Value">{slider_value}</item>
                          </items>
                        </chunk>
                      </chunks>
''')}
{gh_object("inner-csharp", "C# Script", "Inner C#", 220, 70, 120, 44, extra_chunks=f'''
                      <chunks>
                        <chunk name="Script">
                          <items>
                            <item name="Text">{script_b64}</item>
                          </items>
                        </chunk>
                      </chunks>
''')}
{extra_nodes}
{gh_object("inner-xy", "XY Plane", "XY", 220, 160, 65, 28)}
{cluster_object("inner-nested", "Cluster", "Nested", 400, 80, 90, 28, nested_b64)}
  </chunks>
</Archive>
"""


def build_ghx_pair():
    nested_old = build_nested_xml("1", "Gone", "old nested note", "nested-gone")
    nested_new = build_nested_xml("7", "Hello", "hello nested", "nested-hello")
    panel = gh_object(
        "inner-panel",
        "Panel",
        "Note",
        40,
        160,
        110,
        48,
        extra_chunks="""
                      <chunks>
                        <chunk name="PanelProperties">
                          <items>
                            <item name="UserText">old cluster note</item>
                          </items>
                        </chunk>
                      </chunks>
""",
    )
    area = gh_object("inner-area", "Area", "Area", 40, 160, 66, 24)
    parent_old = build_parent_inner_xml(
        slider_value="10",
        script="using System;\npublic class Script { int a; }",
        extra_nodes=panel,
        nested_b64=b64_xml(nested_old),
    )
    parent_new = build_parent_inner_xml(
        slider_value="25",
        script="using System;\npublic class Script { }",
        extra_nodes=area,
        nested_b64=b64_xml(nested_new),
    )
    csharp_old = b64_xml("using System;\npublic class Script { int a; }")
    csharp_new = b64_xml("using System;\npublic class Script { }")

    def root(script_b64: str, cluster_b64: str) -> str:
        body = "".join(
            [
                gh_object("csharp", "C# Script", "C#", 364, 170, 112, 44, extra_chunks=f"""
                      <chunks>
                        <chunk name="Script">
                          <items>
                            <item name="Text">{script_b64}</item>
                          </items>
                        </chunk>
                      </chunks>
"""),
                cluster_object("cluster", "Cluster", "Cluster", 633, 175, 81, 28, cluster_b64),
                gh_object("geo", "Geometry", "Geo", 823, 146, 50, 24),
            ]
        )
        return archive(body)

    return root(csharp_old, b64_xml(parent_old)), root(csharp_new, b64_xml(parent_new))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--html-out", default="")
    args = parser.parse_args()
    data = build_hop_data()
    json_path = HERE / "mock_cluster_diff.json"
    json_path.write_text(json.dumps(data, indent=2) + "\n")
    old_xml, new_xml = build_ghx_pair()
    (HERE / "cluster-old.ghx").write_text(old_xml)
    (HERE / "cluster-new.ghx").write_text(new_xml)
    print(f"wrote {json_path}")
    print(f"wrote {HERE / 'cluster-old.ghx'}")
    print(f"wrote {HERE / 'cluster-new.ghx'}")
    if args.html_out:
        html_path = Path(args.html_out)
        html_path.write_text(wrap_html(data))
        print(f"wrote {html_path}")


if __name__ == "__main__":
    main()
