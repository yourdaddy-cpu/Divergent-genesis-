#!/usr/bin/env python3
"""
Creates the missing .meta sidecars for assets that were added by hand.

Unity assigns a random GUID the first time it imports an asset, which means a
fresh clone of a repository whose .meta files were never committed imports the
same file as a *different* asset - references break, and `validate_project.py`
fails the build. So every .cs, .shader and folder under Assets/ carries a .meta,
and this script is what makes them.

GUIDs are derived from the asset path (md5), not random, so regenerating them on
another machine is a no-op rather than a churn of 900 changed files.

    python3 Tools/gen_metas.py          # create what is missing
    python3 Tools/gen_metas.py --check  # print what is missing, create nothing

`validate_project.py` is the gate; this is the fix.
"""
import hashlib
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, 'Assets')

MONO = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

SHADER = """fileFormatVersion: 2
guid: {guid}
ShaderImporter:
  externalObjects: {{}}
  defaultTextures: []
  nonModifiableTextures: []
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

FOLDER = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def guid_for(asset_path):
    """A stable 32 hex digit id for a repo-relative asset path."""
    rel = os.path.relpath(asset_path, ROOT).replace(os.sep, '/')
    return hashlib.md5(('divergent-genesis:' + rel).encode('utf-8')).hexdigest()


def template_for(asset_path):
    if os.path.isdir(asset_path):
        return FOLDER
    if asset_path.endswith('.shader'):
        return SHADER
    return MONO


def is_asset(name):
    if name.endswith('.meta'):
        return False
    return name.endswith('.cs') or name.endswith('.shader') or name.endswith('.unity') \
        or name.endswith('.json') or name.endswith('.mat')


def walk():
    for root, dirs, files in os.walk(ASSETS):
        dirs[:] = [d for d in dirs if not d.startswith('.')]
        for d in dirs:
            yield os.path.join(root, d)
        for f in files:
            if is_asset(f):
                yield os.path.join(root, f)


def main():
    check = '--check' in sys.argv
    made = 0
    missing = []

    for asset in walk():
        meta = asset + '.meta'
        if os.path.exists(meta):
            continue
        rel = os.path.relpath(asset, ROOT).replace(os.sep, '/')
        missing.append(rel)
        if check:
            continue
        with open(meta, 'w', encoding='utf-8', newline='\n') as fh:
            fh.write(template_for(asset).format(guid=guid_for(asset)))
        made += 1

    if check and missing:
        print('missing .meta for %d assets:' % len(missing))
        for rel in missing:
            print('  ' + rel)
        return 1

    if made:
        print('created %d .meta files' % made)
    else:
        print('every asset under Assets/ already has a .meta')
    return 0


if __name__ == '__main__':
    sys.exit(main())