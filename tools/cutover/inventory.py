#!/usr/bin/env python3
"""Lists what the rework under Assets/Cosmic still references from the rest of Assets, and what it does not."""
import os, re, sys, collections
root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assets = os.path.join(root, 'Assets')
guid_re = re.compile(r'guid: ([0-9a-f]{32})')
path_re = re.compile(r'"(Assets/[^"\n]+?\.[A-Za-z0-9]+)"')

def meta_guid(path):
    try:
        with open(path + '.meta', encoding='utf-8', errors='ignore') as f:
            m = guid_re.search(f.read())
            return m.group(1) if m else None
    except OSError:
        return None

by_guid = {}
for dirpath, _, files in os.walk(assets):
    for name in files:
        if name.endswith('.meta'):
            full = os.path.join(dirpath, name[:-5])
            g = meta_guid(full)
            if g:
                by_guid[g] = os.path.relpath(full, root).replace(os.sep, '/')

referenced = set()
cosmic = os.path.join(assets, 'Cosmic')
for dirpath, _, files in os.walk(cosmic):
    for name in files:
        full = os.path.join(dirpath, name)
        if name.endswith('.meta') or name.endswith('.png') or name.endswith('.dll'):
            continue
        try:
            text = open(full, encoding='utf-8', errors='ignore').read()
        except OSError:
            continue
        for g in guid_re.findall(text):
            if g in by_guid:
                referenced.add(by_guid[g])
        if name.endswith('.cs') or name.endswith('.md'):
            for p in path_re.findall(text):
                referenced.add(p)
# packages and samples the rework nests
for dirpath, _, files in os.walk(os.path.join(assets, 'Samples')):
    for name in files:
        if not name.endswith('.meta'):
            referenced.add(os.path.relpath(os.path.join(dirpath, name), root).replace(os.sep, '/'))

# Content is reused byte for byte (RULES.md), so every content folder is kept whole; only code, prefabs, scenes, materials, shaders and data of the old tree go.
keep_dirs = {'Assets/Cosmic', 'Assets/Samples', 'Assets/XR', 'Assets/TextMesh Pro', 'Assets/Fonts', 'Assets/Resources', 'Assets/build_scripts', 'Assets/StreamingAssets', 'Assets/Plugins',
             'Assets/audio', 'Assets/Textures', 'Assets/models', 'Assets/ui', 'Assets/_sources', 'Assets/csc.rsp', 'Assets/XRI'}

def top(p):
    parts = p.split('/')
    return '/'.join(parts[:2])

keep, drop = collections.defaultdict(list), collections.defaultdict(list)
for dirpath, _, files in os.walk(assets):
    for name in files:
        if name.endswith('.meta'):
            continue
        rel = os.path.relpath(os.path.join(dirpath, name), root).replace(os.sep, '/')
        t = top(rel)
        if t in keep_dirs or rel in referenced:
            keep[t].append(rel)
        else:
            drop[t].append(rel)

out = os.path.join(root, 'tools', 'cutover')
with open(os.path.join(out, 'keep.txt'), 'w') as f:
    for t in sorted(keep):
        for p in sorted(keep[t]): f.write(p + '\n')
with open(os.path.join(out, 'delete.txt'), 'w') as f:
    for t in sorted(drop):
        for p in sorted(drop[t]): f.write(p + '\n')
print(f'{"folder":40} {"keep":>6} {"delete":>7}')
for t in sorted(set(keep) | set(drop)):
    print(f'{t:40} {len(keep.get(t, [])):6} {len(drop.get(t, [])):7}')
print(f'\nreferenced by Assets/Cosmic: {len(referenced)} files; lists in tools/cutover/keep.txt and delete.txt')
