import os, re, collections, json

import os
WORK = os.environ.get("XTU_WORK") or os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "work"))
base = WORK

tasks = collections.OrderedDict()

# --- BAML distinct candidate values ---
baml_rows = []
with open(os.path.join(base, "cands.tsv"), encoding="utf-8-sig") as f:
    f.readline()
    for line in f:
        p = line.rstrip("\n").split("\t")
        if len(p) >= 6:
            baml_rows.append(p)
tasks["baml"] = [("v", v) for v in sorted(set(r[5] for r in baml_rows))]

for tag in ("knob", "monitor"):
    vals = []
    with open(os.path.join(base, "cand_%s.tsv" % tag), encoding="utf-8-sig") as f:
        f.readline()
        for line in f:
            p = line.rstrip("\n").split("\t")
            if len(p) >= 2:
                vals.append((p[0], p[1]))
    tasks[tag] = vals

for name in ("perftune", "xtucommon"):
    vals = []
    with open(os.path.join(base, "cand_%s.tsv" % name), encoding="utf-8-sig") as f:
        f.readline()
        for line in f:
            p = line.rstrip("\n").split("\t")
            if len(p) >= 2:
                vals.append(("v", p[1]))
    tasks[name] = vals

manifest = {}
for cat, items in tasks.items():
    with open(os.path.join(base, "task_%s.tsv" % cat), "w", encoding="utf-8", newline="\n") as f:
        for i, (kind, val) in enumerate(items):
            f.write("%d\t%s\t%s\n" % (i, kind, val.replace("\t", " ")))
    manifest[cat] = [[i, kind, val] for i, (kind, val) in enumerate(items)]
    print(cat, len(items))

with open(os.path.join(base, "manifest.json"), "w", encoding="utf-8") as f:
    json.dump(manifest, f, ensure_ascii=False, indent=1)
print("ok")
