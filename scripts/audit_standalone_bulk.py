from pathlib import Path
import re, json
root=Path(__file__).resolve().parents[1]
areas={"models":[],"runtime":[],"ef_snapshot":[],"historical_migrations":[],"docs":[]}
for p in root.rglob("*"):
    if not p.is_file() or p.suffix.lower() not in (".cs",".json",".sql"): continue
    if "bin" in p.parts or "obj" in p.parts: continue
    s=p.read_text(encoding="utf-8",errors="ignore")
    matches=[(i,line.strip()[:180]) for i,line in enumerate(s.splitlines(),1) if re.search(r"\bHospitalId\b|\bHospital\b|\bCMS\b|External(?:Hospital|Branch|Doctor|Patient)Id|SourceSystem",line,re.I)]
    if not matches: continue
    rel=str(p.relative_to(root))
    group="historical_migrations" if "Migrations/" in rel and "Snapshot" not in rel else "ef_snapshot" if "Snapshot" in rel else "models" if rel.startswith("Models/") else "runtime"
    areas[group].append({"file":rel,"matches":matches})
report=root/"STANDALONE_BULK_AUDIT.json"
report.write_text(json.dumps(areas,indent=2))
for group,files in areas.items(): print(f"{group}: {len(files)} files, {sum(len(f['matches']) for f in files)} matches")
print("Detailed report:",report)
