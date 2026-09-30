#!/usr/bin/env python3
"""
Sanity-checks the hand-maintained Unity YAML that a normal C# compile cannot see.

  * Main.unity parses, has a SceneRoots block, and its GameBootstrap component
    points at the real GUID of Assets/Scripts/GameBootstrap.cs
  * every key the scene serialises still exists as a public field on
    GameBootstrap, and every public field is covered by the scene
  * EditorBuildSettings points at the scene with the scene's real .meta GUID
  * no .asset file contains a duplicated YAML key (see normalize_assets.py)
  * every .cs / .shader / folder under Assets has a .meta, and no .meta is
    orphaned - a missing .meta means a fresh clone imports different GUIDs and
    every reference to the asset silently breaks

    python3 Tools/validate_project.py
"""
import os
import re
import sys

import yaml

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, 'Assets')
SCENE = os.path.join(ASSETS, 'Scenes', 'Main.unity')
SCENE_META = SCENE + '.meta'
BOOTSTRAP = os.path.join(ASSETS, 'Scripts', 'GameBootstrap.cs')

errors = []
notes = []


def fail(msg):
    errors.append(msg)


def ok(msg):
    notes.append(msg)


def read_guid(asset_path):
    """Reads the stable GUID Unity assigned an asset, from its .meta sidecar.

    Accepts either the asset path or the .meta path.
    """
    meta = asset_path if asset_path.endswith('.meta') else asset_path + '.meta'
    if not os.path.exists(meta):
        return None
    with open(meta) as fh:
        for line in fh:
            if line.startswith('guid:'):
                return line.split(':', 1)[1].strip()
    return None


# ---------------------------------------------------------------- scene parse
class UnityLoader(yaml.SafeLoader):
    """SafeLoader that understands Unity's `!u!<classID>` document tags."""

    pass


UnityLoader.add_multi_constructor(
    'tag:unity3d.com,2011:',
    lambda loader, suffix, node: loader.construct_mapping(node, deep=True))


def load_unity_yaml(path):
    """
    Parse a Unity .asset/.unity document.

    Unity only declares the `%TAG !u!` handle once, at the top of the stream, but
    PyYAML scopes directives per document - so strip the handle and keep the
    anchor. The class id is not needed for structural checks, and the anchor is
    the fileID every reference points at.
    """
    with open(path) as fh:
        raw = fh.read()
    raw = re.sub(r'^--- !u!\d+ &(\d+)', r'--- &\1', raw, flags=re.M)
    return yaml.load_all(raw, Loader=UnityLoader)


def no_duplicates(loader, node, deep=False):
    keys = []
    for k, _ in node.value:
        k = loader.construct_object(k, deep=deep)
        if k in keys:
            fail('duplicate YAML key %r' % k)
        keys.append(k)
    return yaml.SafeLoader.construct_mapping(loader, node, deep)


UnityLoader.add_constructor(yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG, no_duplicates)

if not os.path.exists(SCENE):
    fail('Assets/Scenes/Main.unity is missing')
    docs = []
else:
    try:
        docs = list(load_unity_yaml(SCENE))
        ok('Main.unity parses as YAML (%d documents)' % len(docs))
    except Exception as exc:                                   # noqa: BLE001
        fail('Main.unity does not parse: %s' % exc)
        docs = []

    if not any('SceneRoots' in d for d in docs):
        fail('Main.unity has no SceneRoots block - Unity would load an empty scene')

# ----------------------------------------------- scene <-> script field parity
mono = None
for d in docs:
    for k, v in d.items():
        if isinstance(v, dict) and 'm_Script' in v:
            mono = v
if mono is None:
    fail('Main.unity contains no MonoBehaviour - the GameBootstrap object is missing')
else:
    script_guid = mono['m_Script'].get('guid')
    real_guid = read_guid(BOOTSTRAP)
    if real_guid is None:
        fail('Assets/Scripts/GameBootstrap.cs.meta is missing or has no guid')
    elif script_guid != real_guid:
        fail('Main.unity references GameBootstrap guid %s but the .meta says %s'
             % (script_guid, real_guid))
    else:
        ok('Main.unity -> GameBootstrap script GUID resolves')

    script_keys = set(re.findall(r'(?<![\w.])public\s+(?:bool|int|float)\s+(\w+)\s*[=;{]',
                                 open(BOOTSTRAP).read()))
    ignored = {'m_ObjectHideFlags', 'm_CorrespondingSourceObject', 'm_PrefabInstance',
               'm_PrefabAsset', 'm_GameObject', 'm_Enabled', 'm_EditorHideFlags',
               'm_Script', 'm_Name', 'm_EditorClassIdentifier'}
    serialized = {k for k in mono if k not in ignored}

    for name in sorted(serialized - script_keys):
        fail('Main.unity serialises "%s", which is no longer a public field on GameBootstrap' % name)
    for name in sorted(script_keys - serialized):
        fail('GameBootstrap.%s is public but missing from Main.unity - the inspector default will not apply' % name)
    if serialized == script_keys:
        ok('Main.unity serialised fields match GameBootstrap exactly (%d)' % len(script_keys))

# --------------------------------------------------------- build scene list
ebs_path = os.path.join(ROOT, 'ProjectSettings', 'EditorBuildSettings.asset')
if not os.path.exists(ebs_path):
    fail('ProjectSettings/EditorBuildSettings.asset is missing')
else:
    ebs = next(load_unity_yaml(ebs_path))
    scenes = (ebs.get('EditorBuildSettings') or {}).get('m_Scenes') or []
    entry = next((s for s in scenes if s.get('path') == 'Assets/Scenes/Main.unity'), None)
    if entry is None:
        fail('EditorBuildSettings does not list Assets/Scenes/Main.unity')
    else:
        scene_guid = read_guid(SCENE_META)
        if entry.get('guid') != scene_guid:
            fail('EditorBuildSettings guid %r does not match Main.unity.meta %r'
                 % (entry.get('guid'), scene_guid))
        else:
            ok('EditorBuildSettings -> Main.unity GUID resolves')

# ------------------------------------------------------------------ no dupes
for name in ('ProjectSettings.asset', 'EditorBuildSettings.asset', 'TimeManager.asset',
             'DynamicsManager.asset', 'TagManager.asset', 'AudioManager.asset'):
    p = os.path.join(ROOT, 'ProjectSettings', name)
    if not os.path.exists(p):
        continue
    before = len(errors)
    try:
        list(load_unity_yaml(p))
    except Exception as exc:                                   # noqa: BLE001
        fail('%s: %s' % (name, exc))
    else:
        if len(errors) == before:
            ok('%s has no duplicate keys' % name)

# ------------------------------------------------------------------- metas
missing, orphan = [], []
for root, dirs, files in os.walk(ASSETS):
    if os.path.basename(root).startswith('.'):
        continue
    for d in dirs:
        if not os.path.exists(os.path.join(root, d) + '.meta'):
            missing.append(os.path.join(root, d).replace(ROOT + os.sep, ''))
    for f in files:
        p = os.path.join(root, f)
        if f.endswith('.meta'):
            if not os.path.exists(p[:-5]):
                orphan.append(p.replace(ROOT + os.sep, ''))
        elif not os.path.exists(p + '.meta'):
            missing.append(p.replace(ROOT + os.sep, ''))

for m in missing:
    fail('missing .meta for %s - a fresh clone will import different GUIDs' % m)
for o in orphan:
    fail('orphaned .meta %s has no asset' % o)
if not missing and not orphan:
    ok('every asset under Assets/ has a matching .meta')

# ------------------------------------------------------------------ shaders
shaders = os.path.join(ASSETS, 'Resources', 'Shaders')
if os.path.isdir(shaders):
    for f in sorted(os.listdir(shaders)):
        if f.endswith('.shader'):
            body = open(os.path.join(shaders, f)).read()
            if 'Shader "' not in body:
                fail('%s does not declare a Shader name' % f)
            for req in ('Properties', 'SubShader'):
                if req not in body:
                    fail('%s has no %s block' % (f, req))
    ok('%d shader(s) declared' % len([f for f in os.listdir(shaders) if f.endswith('.shader')]))

# ------------------------------------------------------------------- report
for n in notes:
    print('  ok   %s' % n)
if errors:
    print()
    for e in errors:
        print('  FAIL %s' % e)
    print('\n%d problem(s).' % len(errors))
    sys.exit(1)

print('\nProject structure OK.')
