#!/usr/bin/env python3
"""Read-only inventory of legacy hospital/CMS references. Does not alter source or database."""
from pathlib import Path
import argparse
import json
import re
root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description="Read-only CMS/HospitalId reference inventory")
parser.add_argument("--json", action="store_true", help="Emit machine-readable JSON to stdout")
parser.add_argument("--active-only", action="store_true", help="Exclude historical EF migrations")
args = parser.parse_args()
terms = re.compile(r"\b(?:HospitalId|ClinicId|CmsHospitalId|ExternalHospitalId|Hospitals|CMS)\b")
files = sorted(p for p in root.rglob('*.cs') if not any(x in p.parts for x in ('bin','obj','.git')))
groups = {'Active application': [], 'Historical migrations': []}
for path in files:
    content = path.read_text(encoding='utf-8-sig', errors='replace')
    matches = [(i, line.strip()) for i,line in enumerate(content.splitlines(),1) if terms.search(line)]
    if matches:
        key = 'Historical migrations' if 'Migrations' in path.relative_to(root).parts else 'Active application'
        groups[key].append((path.relative_to(root),matches))
if args.active_only:
    groups.pop("Historical migrations")
if args.json:
    payload = {group: [{"file": str(path).replace("\\", "/"), "references": [{"line": line, "text": code} for line, code in matches]} for path, matches in entries] for group, entries in groups.items()}
    print(json.dumps(payload, indent=2))
    raise SystemExit(0)
for group,entries in groups.items():
    print(f"\n=== {group}: {len(entries)} files ===")
    for path,matches in entries:
        print(f"{path}: {len(matches)} references")
        for line,code in matches[:3]:
            print(f"  L{line}: {code[:150]}")
        if len(matches)>3: print(f"  ... {len(matches)-3} additional occurrences")
print('\nInventory only: do not delete historical migrations or mapped properties without a planned EF migration.')
