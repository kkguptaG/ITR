"""Convert the RBI IFSC master xlsx -> a gzipped, IFSC-sorted TSV for bundling.

Output line format (tab-separated): IFSC \t BANK \t BRANCH
Deduped on IFSC (first wins). Run once when the master refreshes.
"""
import gzip
import sys
import openpyxl

SRC = r"C:\Users\kkgup\OneDrive\Desktop\ifsccode.xlsx"
DST = r"D:\TallyGTax\backend\src\TallyG.Tax.Api\Modules\BankAccounts\Data\ifsc.tsv.gz"

wb = openpyxl.load_workbook(SRC, read_only=True, data_only=True)
ws = wb["ifsc_mstr_1"]

rows = ws.iter_rows(values_only=True)
header = next(rows)
print("header:", header, file=sys.stderr)

# Locate columns by header name (BANK / IFSC / BRANCH), case-insensitive.
idx = {str(h).strip().upper(): i for i, h in enumerate(header) if h is not None}
i_bank, i_ifsc, i_branch = idx["BANK"], idx["IFSC"], idx["BRANCH"]

seen = {}
total = 0
for r in rows:
    if r is None:
        continue
    ifsc = r[i_ifsc]
    if ifsc is None:
        continue
    ifsc = str(ifsc).strip().upper()
    if not ifsc or ifsc in seen:
        continue
    bank = ("" if r[i_bank] is None else str(r[i_bank]).strip())
    branch = ("" if r[i_branch] is None else str(r[i_branch]).strip())
    # Strip tabs/newlines that would corrupt the TSV.
    bank = bank.replace("\t", " ").replace("\n", " ").replace("\r", " ")
    branch = branch.replace("\t", " ").replace("\n", " ").replace("\r", " ")
    seen[ifsc] = (bank, branch)
    total += 1

print(f"rows={total} unique_ifsc={len(seen)}", file=sys.stderr)

lines = [f"{ifsc}\t{b}\t{br}" for ifsc, (b, br) in sorted(seen.items())]
payload = ("\n".join(lines) + "\n").encode("utf-8")
with gzip.open(DST, "wb", compresslevel=9) as f:
    f.write(payload)

import os
print(f"wrote {DST} raw={len(payload)} gz={os.path.getsize(DST)}", file=sys.stderr)
