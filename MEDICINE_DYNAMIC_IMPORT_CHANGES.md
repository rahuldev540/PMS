# Flexible Medicine Excel/CSV Import

Medicine bulk import was updated to accept supplier/customer files without requiring the backend template column order.

- Extra columns are accepted and ignored when they are not recognized.
- Missing optional medicine columns are accepted and use existing defaults/null-equivalent values.
- Column order can be different.
- Header matching ignores case, spaces, underscores, hyphens and punctuation.
- Common aliases are supported (Drug Name/Product Name/Item Name, Qty/Stock, MRP/Sale Price, Batch/Lot, etc.).
- Small spelling mistakes are matched conservatively with fuzzy matching; ambiguous matches are not guessed.
- Excel files may have title/company/report rows before the actual header; the importer scans the first 15 used rows for the best medicine header row.
- Blank/unknown columns do not fail the upload.
- CSV rows with fewer values use empty values; extra values are ignored.
- Medicine Name remains the only required medicine identity field. A generated MedicineCode is used when code is missing.
- Existing row-level safety validation remains (negative quantity/prices, invalid dates, etc.) so bad data is not silently committed.

No database migration is required for these changes.
