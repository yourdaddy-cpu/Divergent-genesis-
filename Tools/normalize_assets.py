#!/usr/bin/env python3
"""
Unity's PlayerSettings.asset is a hand-maintained YAML document, and hand-editing
it easily produces the same key twice. Unity's deserializer then silently picks
one, so a stale duplicate (e.g. AndroidTargetSdkVersion: 0 next to : 35) is a
trap.

This walks the file structurally and drops any key that is a repeat of one it has
already seen at the same nesting path, keeping the first. Comments, ordering,
Unity tags and formatting are all preserved - only the offending lines go.

    python3 Tools/normalize_assets.py
"""
import sys
import re

KEY = re.compile(r'^(\s*)([A-Za-z_][A-Za-z0-9_]*):(.*)$')


def dedupe(path):
    with open(path, 'r', encoding='utf-8') as fh:
        lines = fh.read().split('\n')

    out = []
    # stack of (indent, set_of_keys_seen_at_that_indent)
    stack = [(-1, set())]
    removed = []

    for line in lines:
        m = KEY.match(line)
        if not m:
            out.append(line)
            continue

        indent = len(m.group(1))
        key = m.group(2)

        # unwind to the enclosing scope
        while len(stack) > 1 and indent <= stack[-1][0]:
            stack.pop()

        seen = stack[-1][1]
        if key in seen:
            removed.append((line, stack[-1][0], key))
            continue

        seen.add(key)
        out.append(line)

        # a key with a scalar value does not open a scope
        if m.group(3).strip() == '':
            stack.append((indent, set()))

    with open(path, 'w', encoding='utf-8') as fh:
        fh.write('\n'.join(out))

    for line, indent, key in removed:
        print("  removed duplicate  %s:%s" % (' ' * indent, key))
    return len(removed)


if __name__ == '__main__':
    targets = sys.argv[1:] or [
        'ProjectSettings/ProjectSettings.asset',
        'ProjectSettings/EditorBuildSettings.asset',
        'ProjectSettings/DynamicsManager.asset',
        'ProjectSettings/TimeManager.asset',
    ]
    total = 0
    for t in targets:
        import os
        if not os.path.exists(t):
            continue
        print("%s" % t)
        total += dedupe(t)
    print("%d duplicate key(s) removed" % total)
