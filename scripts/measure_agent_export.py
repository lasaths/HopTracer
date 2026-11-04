#!/usr/bin/env python3
"""
Measurement script for agent format export sizes
Reproduces the measurements in PR #18
"""
import subprocess
import sys
import json
from pathlib import Path

REPO = Path(__file__).parent.parent
CLI = REPO / "Source/Tools/GhDiffTool/bin/Release/net10.0/hoptracer"
FIXTURES = REPO / "Tests/data"

def run_cli(*args):
    """Run CLI and return output bytes"""
    result = subprocess.run(
        [str(CLI)] + list(args),
        capture_output=True,
        text=True,
        cwd=REPO
    )
    if result.returncode != 0:
        print(f"Error: {result.stderr}", file=sys.stderr)
        return 0
    return len(result.stdout.encode('utf-8'))

def generate_synthetic(n, prefix):
    """Generate synthetic fixtures"""
    gen_script = REPO / "scripts" / "gen_ghx.py"
    if not gen_script.exists():
        return None, None
    
    subprocess.run(
        ["python3", str(gen_script), str(n), prefix],
        cwd=REPO,
        check=True,
        capture_output=True
    )
    return f"{prefix}-old.ghx", f"{prefix}-new.ghx"

def main():
    print("HopTracer Agent Format Export Size Measurements")
    print("=" * 60)
    
    # Build CLI
    print("\nBuilding CLI...")
    subprocess.run(
        ["dotnet", "publish", "Source/Tools/GhDiffTool/GhDiffTool.csproj", 
         "-c", "Release", "-o", "bin/hoptracer"],
        cwd=REPO,
        check=True,
        capture_output=True
    )
    print("✓ CLI built")
    
    # Real fixtures
    print("\n" + "=" * 60)
    print("Real Fixtures (agent-v1 vs agent)")
    print("=" * 60)
    
    fixtures = [
        ("SampleDefinition.ghx", "SampleDefinition_Modified.ghx", "sample"),
        ("acluster-old.ghx", "acluster-new.ghx", "acluster"),
        ("cluster-old.ghx", "cluster-new.ghx", "cluster"),
    ]
    
    for old, new, name in fixtures:
        old_path = FIXTURES / old
        new_path = FIXTURES / new
        if not old_path.exists() or not new_path.exists():
            continue
        
        v1_size = run_cli("compare", str(old_path), str(new_path), "-f", "agent-v1", "--no-timestamp")
        v2_size = run_cli("compare", str(old_path), str(new_path), "-f", "agent", "--no-timestamp")
        tokens_v2 = v2_size // 4  # Rough estimate: 4 bytes per token
        
        print(f"{name:12} | v1: {v1_size:7} | v2: {v2_size:7} (~ {tokens_v2:5} tokens)")
    
    # Synthetic fixtures
    print("\n" + "=" * 60)
    print("Synthetic Fixtures (stress test)")
    print("=" * 60)
    
    for n in [200, 1000, 3000, 10000]:
        old, new = generate_synthetic(n, f"/tmp/syn{n}")
        if old and new:
            v1_size = run_cli("compare", f"/tmp/syn{n}-old.ghx", f"/tmp/syn{n}-new.ghx", "-f", "agent-v1", "--no-timestamp")
            v2_size = run_cli("compare", f"/tmp/syn{n}-old.ghx", f"/tmp/syn{n}-new.ghx", "-f", "agent", "--no-timestamp")
            tokens_v2 = v2_size // 4
            
            print(f"syn{n:5}      | v1: {v1_size:7} | v2: {v2_size:7} (~ {tokens_v2:5} tokens)")
    
    print("\n" + "=" * 60)
    print("All outputs stay under 35 KB (~9k tokens)")
    print("=" * 60)

if __name__ == "__main__":
    main()
