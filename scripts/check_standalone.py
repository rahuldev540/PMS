#!/usr/bin/env python3
"""Read-only source regression checks; does not replace dotnet build or API tests."""
from pathlib import Path
import re
root = Path(__file__).resolve().parents[1]

def check(path, label, pattern, expected=1):
    source = (root / path).read_text(encoding='utf-8-sig')
    found = len(re.findall(pattern, source))
    if found != expected:
        raise SystemExit(f'FAIL {label}: expected {expected}, found {found} in {path}')

checks = [
    ('Controllers/PharmacySuperAdminController.cs', 'audit query declaration', r'private\s+IQueryable<AuditLog>\s+BuildAuditLogQuery\s*\('),
    ('Controllers/PharmacySuperAdminController.cs', 'audit validation declaration', r'private\s+async\s+Task<IActionResult\?>\s+ValidateAuditFilters\s*\('),
    ('Controllers/PharmacySuperAdminController.cs', 'pharmacy revenue route', r'\[HttpGet\("reports/revenue"\)\]'),
    ('Controllers/PharmacySuperAdminController.cs', 'standalone audit helper', r'private\s+void\s+AddAudit\s*\(\s*string\s+action,\s*string\?\s+systemAction\s*=\s*null,\s*int\?\s+branchId\s*=\s*null\s*\)'),
]
for path, label, pattern in checks:
    check(path, label, pattern)
for name in ['Pharmacy.sln', 'PharmacyOnly.Api.csproj', 'Data/AppDbContext.cs']:
    if not (root / name).is_file():
        raise SystemExit(f'FAIL missing {name}')
for name in ['BuildAuditLogQuery', 'ValidateAuditFilters']:
    source = (root / 'Controllers/PharmacySuperAdminController.cs').read_text(encoding='utf-8-sig')
    if len(re.findall(r'\b' + name + r'\s*\(', source)) < 2:
        raise SystemExit(f'FAIL {name}: expected declaration and call sites')
audit_source = (root / 'Controllers/PharmacySuperAdminController.cs').read_text(encoding='utf-8-sig')
audit_helper = audit_source.split('private IQueryable<AuditLog> BuildAuditLogQuery(', 1)[1].split('private async Task<IActionResult?> ValidateAuditFilters(', 1)[0]
if re.search(r'\bx\.Clinic\b|\b_db\.Hospitals\b', audit_helper):
    raise SystemExit('FAIL audit search: legacy CMS clinic/hospital lookup still present')
# Permission service has no callers in this repository and must not regain legacy hospital parameters.
permission_source = (root / 'Services/UserPermissionService.cs').read_text(encoding='utf-8-sig')
for method in ('CreateDefaultPermissionsAsync', 'EnsureDefaultPermissionsAsync'):
    match = re.search(r'public\s+async\s+Task\s+' + method + r'\s*\(([^)]*)\)', permission_source)
    if not match:
        raise SystemExit(f'FAIL missing {method} declaration')
    signature = match.group(1)
    if 'hospitalId' in signature or 'HospitalId' in signature:
        raise SystemExit(f'FAIL legacy hospital parameter remains in {method}')
    if not all(name in signature for name in ('userId', 'role', 'assignedByUserId')):
        raise SystemExit(f'FAIL missing expected parameter in {method}')
if re.search(r'\bhospitalId\b', permission_source):
    raise SystemExit('FAIL permission service still references hospitalId')
print('PASS: source regression checks (run dotnet build and API smoke tests separately)')

# Phase 5B34: fresh records use the nullable CLR default for legacy CMS fields.
# Explicit clearing on EXISTING pharmacist assignments remains required.
for path in (
    'Controllers/UnifiedAuthController.cs',
    'Controllers/PharmacySuperAdminAuthController.cs',
    'Controllers/PharmacySuperAdminController.cs',
    'Services/PharmacyAuthService.cs',
    'Data/PharmacySuperAdminSeeder.cs',
    'Models/PharmacyPermissionSeeder.cs',
):
    source = (root / path).read_text(encoding='utf-8-sig')
    if re.search(r'(?m)^\s*(?:ClinicId|HospitalId)\s*=\s*null\s*,', source):
        raise SystemExit(f'FAIL redundant legacy CMS initializer in {path}')
pharmacist = (root / 'Controllers/PharmacistAdminController.cs').read_text(encoding='utf-8-sig')
if pharmacist.count('pharmacist.HospitalId = null;') != 2:
    raise SystemExit('FAIL existing pharmacist hospital isolation assignments changed')
print('PASS: Phase 5B34 nullable-default and pharmacist isolation checks')

# Phase 5B35/5B43: preserve audit branch relationship; CMS clinic link removed by migration.
audit_model = (root / 'Models/AuditLog.cs').read_text(encoding='utf-8-sig')
for field in ('public int? BranchId', 'public Branch? Branch'):
    if field not in audit_model:
        raise SystemExit(f'FAIL audit model compatibility field missing: {field}')
if not (root / 'scripts/Report-LegacyHospitalDependencies.py').is_file():
    raise SystemExit('FAIL legacy dependency report script missing')
print('PASS: Phase 5B35 audit model compatibility checks')

# Phase 5B36: obsolete CMS Swagger operation metadata must not return.
swagger = (root / 'Swagger/ClearSwaggerOperationFilter.cs').read_text(encoding='utf-8-sig')
if 'PharmacySuperAdmin.Hospitals' in swagger:
    raise SystemExit('FAIL legacy CMS Swagger operation label remains')
if 'PharmacySuperAdmin.Branches' not in swagger or 'PharmacySuperAdmin.ListAdmins' not in swagger:
    raise SystemExit('FAIL essential standalone Swagger labels missing')
print('PASS: Phase 5B36 Swagger legacy operation cleanup')

# Phase 5B37: the dependency inventory supports stable JSON and active-only views.
import subprocess
import json
reporter = root / 'scripts/Report-LegacyHospitalDependencies.py'
result = subprocess.run([__import__('sys').executable, str(reporter), '--json', '--active-only'], capture_output=True, text=True, check=True)
payload = json.loads(result.stdout)
if 'Historical migrations' in payload or 'Active application' not in payload:
    raise SystemExit('FAIL active-only JSON dependency report shape')
if not any(item['file'] == 'Data/AppDbContext.cs' for item in payload['Active application']):
    raise SystemExit('FAIL expected active legacy mapping missing from report')
print('PASS: Phase 5B37 machine-readable dependency inventory')

# Phase 5B38: SQL inspection must stay read-only.
inspection = (root / 'scripts/Inspect-LegacyHospitalSchema.sql').read_text(encoding='utf-8')
for token in ('sys.columns', 'sys.foreign_keys', 'sys.indexes'):
    if token not in inspection:
        raise SystemExit(f'FAIL missing SQL inspection target: {token}')
import re as _re
non_comment_sql = '\n'.join(line for line in inspection.splitlines() if not line.lstrip().startswith('--'))
if _re.search(r'\b(?:ALTER|DROP|TRUNCATE|UPDATE|DELETE|INSERT|EXEC(?:UTE)?)\b', non_comment_sql, _re.I):
    raise SystemExit('FAIL SQL inspection contains a destructive or executable statement')
print('PASS: Phase 5B38 read-only SQL Server preflight')

# Phase 5B39: validate that the new pre-migration inspection only touches a local temp table.
preflight = (root / 'scripts/Inspect-LegacyHospitalData.sql').read_text(encoding='utf-8')
for phrase in ('sys.columns', 'COUNT_BIG(', 'sys.sp_executesql', '#LegacyColumnCounts', 'sys.foreign_keys'):
    if phrase not in preflight:
        raise SystemExit(f'FAIL Phase 5B39 data inspection missing {phrase}')
statements = '\n'.join(line for line in preflight.splitlines() if not line.lstrip().startswith('--'))
if re.search(r'\b(?:ALTER|TRUNCATE|UPDATE|DELETE|MERGE)\b', statements, re.I):
    raise SystemExit('FAIL Phase 5B39 inspection contains a persistent mutation statement')
if re.search(r'\bDROP\s+(?!TABLE\s+#LegacyColumnCounts\b)', statements, re.I):
    raise SystemExit('FAIL Phase 5B39 inspection drops something other than its local temp table')
print('PASS: Phase 5B39 read-only data and FK inventory')

# Phase 5B40: verify non-destructive readiness gate is present.
gate = (root / 'scripts/Check-LegacyMigrationReadiness.sql').read_text(encoding='utf-8')
for phrase in ('TotalPopulatedLegacyValues', 'PopulatedColumns', 'DataReadiness', 'sys.foreign_keys', 'sys.index_columns', '#LegacyGate'):
    if phrase not in gate:
        raise SystemExit(f'FAIL Phase 5B40 readiness gate missing {phrase}')
gate_sql = '\n'.join(line for line in gate.splitlines() if not line.lstrip().startswith('--'))
if re.search(r'\b(?:ALTER|TRUNCATE|UPDATE|DELETE|MERGE)\b', gate_sql, re.I):
    raise SystemExit('FAIL Phase 5B40 readiness gate contains persistent mutation')
if re.search(r'\bDROP\s+(?!TABLE\s+#LegacyGate\b)', gate_sql, re.I):
    raise SystemExit('FAIL Phase 5B40 readiness gate drops non-temporary object')
print('PASS: Phase 5B40 read-only migration readiness gate')

# Phase 5B41: read-only tenant-key preflight is present and safe.
preflight = (root / 'scripts/Check-StandaloneTenantReadiness.sql').read_text(encoding='utf-8')
for token in ('sys.columns', 'sys.foreign_keys', 'MissingPharmacyId', 'sp_executesql'):
    if token not in preflight:
        raise SystemExit(f'FAIL Phase 5B41 missing SQL tenant preflight element: {token}')
import re as _phase41_re
code_lines = '\n'.join(line for line in preflight.splitlines() if not line.lstrip().startswith('--'))
if _phase41_re.search(r'\b(?:DROP|DELETE|UPDATE|INSERT|ALTER|TRUNCATE|MERGE)\s+(?:TABLE|FROM|INTO|DATABASE|INDEX|\w+)', code_lines, _phase41_re.I):
    raise SystemExit('FAIL Phase 5B41 tenant preflight contains a write operation')
print('PASS: Phase 5B41 read-only pharmacy tenant readiness preflight')

# Phase 5B42: classification and cross-tenant consistency report, no database mutation.
audit_sql = (root / 'scripts/Check-UserPharmacyAssignments.sql').read_text(encoding='utf-8')
for phrase in ('MissingPharmacyId', 'AssignmentReview', 'UserBranchMismatches', 'OrphanBranches', 'LEFT JOIN dbo.Branches', 'LEFT JOIN dbo.Pharmacies'):
    if phrase not in audit_sql:
        raise SystemExit(f'FAIL Phase 5B42 missing audit element: {phrase}')
import re as _phase42_re
audit_code = '\n'.join(line for line in audit_sql.splitlines() if not line.lstrip().startswith('--'))
if _phase42_re.search(r'\b(?:DROP|DELETE|UPDATE|INSERT|ALTER|TRUNCATE|MERGE|EXEC(?:UTE)?)\b', audit_code, _phase42_re.I):
    raise SystemExit('FAIL Phase 5B42 audit SQL is not read-only')
print('PASS: Phase 5B42 user assignment and branch consistency audit')

# Phase 5B43: first coordinated legacy column removal with reversible EF migration.
assert 'ClinicId' not in audit_model and 'Hospital? Clinic' not in audit_model
snapshot = (root / 'Migrations/AppDbContextModelSnapshot.cs').read_text(encoding='utf-8-sig')
audit_snapshot = snapshot.split('modelBuilder.Entity("AuthDemo.Models.AuditLog", b =>', 1)[1].split('modelBuilder.Entity("AuthDemo.Models.Branch", b =>', 1)[0]
assert 'ClinicId' not in audit_snapshot
assert '"Clinic"' not in audit_snapshot
migration = (root / 'Migrations/20261106230000_RemoveAuditLogClinicLink.cs').read_text(encoding='utf-8')
for token in ('[Migration("20261106230000_RemoveAuditLogClinicLink")]', 'DropForeignKey', 'DropIndex', 'DropColumn', 'AddForeignKey', 'THROW 51043'):
    assert token in migration, token
print('PASS: Phase 5B43 audit CMS relationship removal and migration guard')

# Phase 5B44: operational records no longer carry legacy CMS HospitalId.
for model in ('Inventory', 'Medicine', 'StockTransaction', 'PharmacyNotification'):
    source = (root / 'Models' / (model + '.cs')).read_text(encoding='utf-8-sig')
    if re.search(r'\bHospitalId\b', source):
        raise SystemExit(f'FAIL legacy HospitalId remains in {model}')
    snapshot = (root / 'Migrations/AppDbContextModelSnapshot.cs').read_text(encoding='utf-8-sig')
    segment = snapshot.split('modelBuilder.Entity("AuthDemo.Models.' + model + '", b =>', 1)[1].split('modelBuilder.Entity(', 1)[0]
    if '"HospitalId"' in segment:
        raise SystemExit(f'FAIL snapshot HospitalId remains in {model}')
migration = (root / 'Migrations/20261106233000_RemoveOperationalHospitalColumns.cs').read_text(encoding='utf-8')
for table in ('Inventories', 'Medicines', 'StockTransactions', 'PharmacyNotifications'):
    if f'migrationBuilder.DropColumn(name: "HospitalId", table: "{table}")' not in migration:
        raise SystemExit(f'FAIL missing migration column drop: {table}')
    if f'"{table}"' not in migration:
        raise SystemExit(f'FAIL missing migration safety guard: {table}')
print('PASS: Phase 5B44 four operational CMS columns removed with guarded EF migration')

# Phase 5B45: commerce-related records no longer map CMS HospitalId.
commerce = (root / 'Models/PharmacyExtensions.cs').read_text(encoding='utf-8-sig')
for model in ('Supplier', 'PurchaseOrder', 'GoodsReceipt', 'PharmacyPayment'):
    segment = commerce.split('public class ' + model + '\n', 1)[1].split('\npublic class ', 1)[0]
    if 'HospitalId' in segment:
        raise SystemExit(f'FAIL legacy HospitalId remains in {model}')
    snap = (root / 'Migrations/AppDbContextModelSnapshot.cs').read_text(encoding='utf-8-sig')
    segment = snap.split('modelBuilder.Entity("AuthDemo.Models.' + model + '", b =>', 1)[1].split('modelBuilder.Entity(', 1)[0]
    if '"HospitalId"' in segment:
        raise SystemExit(f'FAIL snapshot HospitalId remains in {model}')
migration = (root / 'Migrations/20261106234500_RemoveCommerceHospitalColumns.cs').read_text(encoding='utf-8')
for table in ('Suppliers', 'PurchaseOrders', 'GoodsReceipts', 'PharmacyPayments'):
    if f'migrationBuilder.DropColumn(name: "HospitalId", table: "{table}")' not in migration:
        raise SystemExit(f'FAIL missing migration drop for {table}')
    if f'"{table}"' not in migration or 'THROW 51045' not in migration:
        raise SystemExit(f'FAIL missing migration guard for {table}')
print('PASS: Phase 5B45 four commerce CMS columns removed with guarded EF migration')

# Phase 5B46: billing, imports, branch settings CMS fields removed.
for model, table in (("MedicineImport", "MedicineImports"), ("PharmacyBill", "PharmacyBills"), ("PharmacyBranchSetting", "PharmacyBranchSettings")):
    source = (root / "Models" / (model + ".cs")).read_text(encoding="utf-8-sig")
    assert not re.search(r"\bHospitalId\b", source), model
    snapshot = (root / "Migrations/AppDbContextModelSnapshot.cs").read_text(encoding="utf-8-sig")
    segment = snapshot.split('modelBuilder.Entity("AuthDemo.Models.' + model + '", b =>', 1)[1].split('modelBuilder.Entity(', 1)[0]
    assert '"HospitalId"' not in segment, model
    migration = (root / "Migrations/20261107000000_RemoveBillingImportSettingsHospitalColumns.cs").read_text(encoding="utf-8")
    assert f'migrationBuilder.DropColumn(name: "HospitalId", table: "{table}")' in migration
    assert f'{table}.HospitalId contains data' in migration
print("PASS: Phase 5B46 billing/import/settings CMS columns removed with migration guard")

# Phase 5B47: UserPermissions is no longer scoped to legacy CMS hospitals.
permission_model = (root / 'Models/UserPermission.cs').read_text(encoding='utf-8-sig')
assert 'HospitalId' not in permission_model and 'Hospital? Hospital' not in permission_model
snapshot = (root / 'Migrations/AppDbContextModelSnapshot.cs').read_text(encoding='utf-8-sig')
assert '"HospitalId"' not in snapshot.split('modelBuilder.Entity("AuthDemo.Models.UserPermission", b =>', 1)[1].split('modelBuilder.Entity(', 1)[0]
migration = (root / 'Migrations/20261107003000_RemoveUserPermissionHospitalColumn.cs').read_text(encoding='utf-8')
assert 'UserPermissions.HospitalId contains data' in migration
assert 'migrationBuilder.DropColumn(name: "HospitalId", table: "UserPermissions")' in migration
print('PASS: Phase 5B47 user permission CMS column removed with guarded migration')
