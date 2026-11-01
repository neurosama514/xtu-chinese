import os, re
import os
WORK = os.environ.get("XTU_WORK") or os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "work"))
base = WORK
apply_dir = os.path.join(base, "apply")

def load_task(name):
    d = {}
    p = os.path.join(base, name)
    if not os.path.exists(p):
        return d
    for line in open(p, encoding="utf-8"):
        parts = line.rstrip("\n").split("\t", 2)
        if len(parts) == 3 and parts[0].isdigit():
            d[int(parts[0])] = parts[2]
    return d

def load_tr(name):
    d = {}
    p = os.path.join(base, name)
    if not os.path.exists(p):
        return d
    for line in open(p, encoding="utf-8-sig"):
        parts = line.rstrip("\n").split("\t", 1)
        if len(parts) == 2 and parts[0].strip().isdigit():
            d[int(parts[0])] = parts[1]
    return d

pairs = []
for task, tr in (("task_themes.tsv", "tr_themes_input.tsv"), ("task_themes2.tsv", "tr_themes2_input.tsv")):
    o, z = load_task(task), load_tr(tr)
    for i in sorted(o):
        if i in z:
            pairs.append((o[i], z[i]))
print("pairs:", len(pairs))

def numref(s):
    return "".join(c if ord(c) < 128 else "&#x%X;" % ord(c) for c in s)

for name in ("IntelBlueColorTheme.xaml", "IntelDarkColorTheme.xaml"):
    t = open(os.path.join(base, "sdkres", name), encoding="utf-8-sig").read()
    n = 0
    for o, z in pairs:
        for pat in ('Value="%s"', 'Value= "%s"', 'Value ="%s"', 'Value = "%s"'):
            needle = pat % o
            if needle in t:
                t = t.replace(needle, pat % numref(z))
                n += 1
                break
        needle_text = ">%s<" % o
        if needle_text in t:
            t = t.replace(needle_text, ">%s<" % numref(z))
            n += 1
    open(os.path.join(apply_dir, name), "w", encoding="utf-8", newline="\n").write(t)
    print(name, "replaced:", n)
