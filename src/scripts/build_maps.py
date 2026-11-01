import os, re, json, sys, html

import os
WORK = os.environ.get("XTU_WORK") or os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "work"))
base = WORK
out = os.path.join(base, "out")
apply_dir = os.path.join(base, "apply")
os.makedirs(apply_dir, exist_ok=True)

manifest = json.load(open(os.path.join(base, "manifest.json"), encoding="utf-8"))

def load_tr(cat):
    p = os.path.join(out, "tr_%s.tsv" % cat)
    d = {}
    if not os.path.exists(p):
        print("MISSING", p); return d
    with open(p, encoding="utf-8-sig") as f:
        for line in f:
            line = line.rstrip("\n").rstrip("\r")
            if not line: continue
            if "\t" not in line: continue
            i, zh = line.split("\t", 1)
            i = i.strip()
            if not i.isdigit(): continue
            d[int(i)] = zh
    return d

tr = {cat: load_tr(cat if cat != "perftune" else "perftune_a") for cat in []}
# perftune is split in two files; merge by shifting ids
tr["baml"] = load_tr("baml")
tr["knob"] = load_tr("knob")
tr["monitor"] = load_tr("monitor")
tr["xtucommon"] = load_tr("xtucommon")
a = load_tr("perftune_a"); b = load_tr("perftune_b")
tr["perftune"] = dict(a)
for k, v in b.items():
    tr["perftune"][k] = v

for cat in ("baml", "knob", "monitor", "perftune", "xtucommon"):
    total = len(manifest[cat])
    have = len([i for i in range(total) if i in tr[cat]])
    print("%-10s total=%4d translated=%4d missing=%4d" % (cat, total, have, total - have))

def esc(s):
    return s.replace("\\", "\\\\").replace("\t", "\\t").replace("\r", "\\r").replace("\n", "\\n")

# ---------- BAML map ----------
rows = []
missing_baml = 0
with open(os.path.join(base, "cands.tsv"), encoding="utf-8-sig") as f:
    f.readline()
    for line in f:
        p = line.rstrip("\n").split("\t")
        if len(p) < 6: continue
        fil, idx, kind, owner, prop, val = p
        # find translation by value
        hit = None
        for i, k, v in manifest["baml"]:
            if v == val and i in tr["baml"]:
                hit = tr["baml"][i]; break
        if hit is None:
            missing_baml += 1
            continue
        rows.append("\t".join([fil, idx, kind, owner, prop, esc(hit)]))
with open(os.path.join(apply_dir, "baml_map.tsv"), "w", encoding="utf-8", newline="\n") as f:
    f.write("file\tindex\tkind\towner\tprop\tvalue\n")
    for r in rows:
        f.write(r + "\n")
print("baml rows=%d missing=%d" % (len(rows), missing_baml))

# ---------- XML metadata ----------
def patch_xml(src, dst, cat, kinds):
    txt = open(src, encoding="utf-8-sig").read()
    mapping = {}
    for i, k, v in manifest[cat]:
        if i in tr[cat]:
            mapping[(k, v)] = tr[cat][i]
    cnt = 0
    pat = re.compile(r'<item name="([A-Za-z]+)"([^>]*)>(.*?)</item>', re.S)

    def repl2(m):
        nonlocal cnt
        kind, attrs, body = m.group(1), m.group(2), m.group(3)
        if kind not in kinds:
            return m.group(0)
        key = (kind, html.unescape(body))
        if key in mapping:
            cnt += 1
            return '<item name="%s"%s>%s</item>' % (kind, attrs, html.escape(mapping[key], quote=False))
        return m.group(0)

    new = pat.sub(repl2, txt)
    open(dst, "w", encoding="utf-8", newline="\n").write(new)
    print("%s replaced=%d" % (cat, cnt))

patch_xml(os.path.join(base, "sdkres", "IntelOverclockingSDK.Metadata.KnobMetadata.xml"),
          os.path.join(apply_dir, "KnobMetadata.xml"), "knob", {"Name", "Description", "Enumeration"})
patch_xml(os.path.join(base, "sdkres", "IntelOverclockingSDK.Metadata.MonitorMetadata.xml"),
          os.path.join(apply_dir, "MonitorMetadata.xml"), "monitor", {"Name", "Description", "Enumeration"})

# ---------- code string maps ----------
def write_strmap(cat, path):
    lines = []
    for i, k, v in manifest[cat]:
        if i in tr[cat] and tr[cat][i] != v:
            lines.append(esc(v) + "\t" + esc(tr[cat][i]))
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + ("\n" if lines else ""))
    print("%s strmap entries=%d" % (cat, len(lines)))

write_strmap("perftune", os.path.join(apply_dir, "strmap_perftune.tsv"))
write_strmap("xtucommon", os.path.join(apply_dir, "strmap_xtucommon.tsv"))

# ---------- round 2: placeholder templates ----------
def append_ph(taskname, trname, outpath):
    task_p = os.path.join(base, taskname)
    tr_p = os.path.join(base, "out", trname)
    if not (os.path.exists(task_p) and os.path.exists(tr_p)):
        print("skip ph:", taskname)
        return
    o, z = {}, {}
    for line in open(task_p, encoding="utf-8"):
        p = line.rstrip("\n").split("\t", 2)
        if len(p) == 3 and p[0].isdigit():
            o[int(p[0])] = p[2]
    for line in open(tr_p, encoding="utf-8-sig"):
        p = line.rstrip("\n").split("\t", 1)
        if len(p) == 2 and p[0].strip().isdigit():
            z[int(p[0])] = p[1]
    n = 0
    with open(outpath, "a", encoding="utf-8", newline="\n") as f:
        for i in sorted(o):
            if i in z and z[i] != o[i]:
                f.write(esc(o[i]) + "\t" + esc(z[i]) + "\n")
                n += 1
    print("ph appended:", n, "->", os.path.basename(outpath))

append_ph("task_perftune_ph.tsv", "tr_perftune_ph.tsv", os.path.join(apply_dir, "strmap_perftune.tsv"))
append_ph("task_xtucommon_ph.tsv", "tr_xtucommon_ph.tsv", os.path.join(apply_dir, "strmap_xtucommon.tsv"))

# ---------- Category.xml (tab display names) ----------
cat_map = {
    "Core": "核心", "Cache": "缓存", "Graphics": "显卡", "Memory": "内存",
    "Real-Time Memory": "实时内存", "Bus": "总线",
    "Per Point Voltage Offset": "逐点电压偏移", "Other": "其他",
}
txt = open(os.path.join(base, "sdkres", "Category.xml"), encoding="utf-8-sig").read()

def numref(s):
    out = []
    for c in s:
        out.append(c if ord(c) < 128 else "&#x%X;" % ord(c))
    return "".join(out)

def catrepl(m):
    val = m.group(1)
    if val in cat_map:
        return 'displayName="%s"' % numref(cat_map[val])
    mm = re.match(r"^XMP Slot (\d+) Profile (\d+)$", val)
    if mm:
        return 'displayName="%s"' % numref("XMP 插槽 %s 配置文件 %s" % (mm.group(1), mm.group(2)))
    return m.group(0)

newcat = re.sub(r'displayName="([^"]*)"', catrepl, txt)
open(os.path.join(apply_dir, "Category.xml"), "w", encoding="utf-8", newline="\n").write(newcat)
print("Category.xml updated")

# ---------- resource maps ----------
open(os.path.join(apply_dir, "resmap_perftune.tsv"), "w", encoding="utf-8", newline="\n").write(
    "PerfTune.g.resources\t%s\nPerfTune.Initialization.Category.xml\t%s\nPerfTune.Initialization.IntelBlueColorTheme.xaml\t%s\nPerfTune.Initialization.IntelDarkColorTheme.xaml\t%s\n" % (
        os.path.join(apply_dir, "PerfTune.g.resources"), os.path.join(apply_dir, "Category.xml"),
        os.path.join(apply_dir, "IntelBlueColorTheme.xaml"), os.path.join(apply_dir, "IntelDarkColorTheme.xaml")))
open(os.path.join(apply_dir, "resmap_sdk.tsv"), "w", encoding="utf-8", newline="\n").write(
    "IntelOverclockingSDK.Metadata.KnobMetadata.xml\t%s\nIntelOverclockingSDK.Metadata.MonitorMetadata.xml\t%s\n" % (
        os.path.join(apply_dir, "KnobMetadata.xml"), os.path.join(apply_dir, "MonitorMetadata.xml")))
open(os.path.join(apply_dir, "neut_sdk.tsv"), "w", encoding="utf-8", newline="\n").write(
    "Intel.Overclocking.SDK.SoftwareTrustVerification.SoftwareTrustVerification\tCreateLogFile\t1\n")
print("done")



# ---------- extra single-word UI labels ----------
extra = {"Benchmarking": "基准测试", "Profiles": "配置文件", "All": "全部"}
with open(os.path.join(apply_dir, "strmap_perftune.tsv"), "a", encoding="utf-8", newline="\n") as f:
    for k, v in extra.items():
        f.write(k + "\t" + v + "\n")
print("extra labels added:", len(extra))