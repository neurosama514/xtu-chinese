import re, json, sys, collections
import os
WORK = os.environ.get("XTU_WORK") or os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "work"))


ROWSP = os.path.join(WORK, "dump_orig.tsv")
rows = []
with open(ROWSP, encoding="utf-8-sig") as f:
    f.readline()
    for line in f:
        p = line.rstrip("\n").split("\t")
        if len(p) >= 6:
            rows.append(p)

STRUCT_PROPS = set("""Name x:Name AutomationId Key Style Template TargetType DataType Source Path Converter ConverterParameter
Command CommandParameter Binding RelativeSource ElementName Data BasedOn Property Value Uid XmlnsDefinition""".split())

URI = re.compile(r'^(pack:|http|/|\{|#|[a-z]{2,6}://)')
NUM = re.compile(r'^[\s\d.,*/+\-=:()%]+$')
IDENT = re.compile(r'^[A-Za-z_][A-Za-z0-9_.]*$')

def is_candidate(r):
    f, idx, kind, owner, prop, val = r
    v = val.strip()
    if not v: return False
    if not re.search(r'[A-Za-z]', v): return False
    if v.startswith('{') and v.endswith('}'): return False
    if URI.match(v): return False
    if NUM.match(v): return False
    if prop in STRUCT_PROPS: return False
    if kind == 'prop' and prop not in ('Text','Content','Header','Title','ToolTip','Description','Caption','Label','Watermark','PlaceholderText','DefaultText'):
        # allow Value for setters referencing strings? skip
        return False
    if kind in ('text','literal'):
        # skip bare identifiers (resource keys/type names) and single words that look like identifiers
        if IDENT.match(v) and len(v.split()) == 1:
            return False
    return True

cands = [r for r in rows if is_candidate(r)]
print("candidates:", len(cands))
with open(os.path.join(WORK, "cands.tsv"), "w", encoding="utf-8-sig") as out:
    out.write("file\tindex\tkind\towner\tprop\tvalue\n")
    for r in cands:
        out.write("\t".join(r) + "\n")

# distribution by file
c = collections.Counter(r[0] for r in cands)
with open(os.path.join(WORK, "cands_by_file.txt"), "w", encoding="utf-8") as out:
    for k, v in c.most_common():
        out.write(f"{v:5d} {k}\n")

# distinct values
distinct = sorted(set(r[5] for r in cands))
with open(os.path.join(WORK, "cands_distinct.txt"), "w", encoding="utf-8") as out:
    for d in distinct:
        out.write(d + "\n")
print("distinct:", len(distinct))
