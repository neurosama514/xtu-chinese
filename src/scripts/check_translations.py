import io, os, re, sys

repo = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
tr = os.path.join(repo, "translations")

pairs = [
    ("task_baml.tsv", "tr_baml.tsv"),
    ("task_knob.tsv", "tr_knob.tsv"),
    ("task_monitor.tsv", "tr_monitor.tsv"),
    ("task_perftune_a.tsv", "tr_perftune_a.tsv"),
    ("task_perftune_b.tsv", "tr_perftune_b.tsv"),
    ("task_perftune_ph.tsv", "tr_perftune_ph.tsv"),
    ("task_xtucommon.tsv", "tr_xtucommon.tsv"),
    ("task_xtucommon_ph.tsv", "tr_xtucommon_ph.tsv"),
    ("task_themes.tsv", "tr_themes_input.tsv"),
    ("task_themes2.tsv", "tr_themes2_input.tsv"),
]

def ids(path, cols):
    out = []
    if not os.path.exists(path):
        return None
    with io.open(path, encoding="utf-8-sig") as f:
        for line in f:
            parts = line.rstrip("\n").split("\t", cols - 1)
            if len(parts) >= cols and parts[0].strip().isdigit():
                out.append(int(parts[0]))
    return out

errors = 0
for task, trans in pairs:
    tp = os.path.join(tr, task)
    zp = os.path.join(tr, trans)
    ti = ids(tp, 3)
    zi = ids(zp, 2)
    if ti is None:
        print("MISSING source:", task); errors += 1; continue
    if zi is None:
        print("MISSING translation:", trans); errors += 1; continue
    if ti != zi:
        missing = sorted(set(ti) - set(zi))[:5]
        extra = sorted(set(zi) - set(ti))[:5]
        print("MISMATCH %s vs %s: src=%d tr=%d missing=%s extra=%s" % (task, trans, len(ti), len(zi), missing, extra))
        errors += 1
    else:
        print("ok  %-24s %4d entries" % (task, len(ti)))

if errors:
    print("\n翻译文件校验失败，共 %d 个问题" % errors)
    sys.exit(1)
print("\n全部翻译文件校验通过")
