from pathlib import Path
import json, re, uuid, struct, math

# Rebuild captured interiors without inventing exterior door coordinates.
P = Path(__file__).resolve().parents[1]
A = P / 'Assets/AzureDreams'
def meta(path, kind='DefaultImporter'):
    m = Path(str(path)+'.meta')
    if not m.exists():
        m.write_text(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n{kind}:\n  externalObjects: {{}}\n')
    return re.search(r'guid: (\w+)',m.read_text()).group(1)
def ref(path, fid):
    asset_type = 2 if path.suffix == '.mat' else 3
    return '{fileID: '+str(fid)+', guid: '+meta(path)+', type: '+str(asset_type)+'}'
for script in ['TownInteriorScene','TownHomeDialogueSelector','TownDialogueProgress','TowerGameController.Interiors']:
    meta(A/'Scripts'/f'{script}.cs','MonoImporter')
shader = meta(A/'Shaders/TownCaptured.shader')
scenes = A/'Scenes/Interiors'; scenes.mkdir(parents=True,exist_ok=True)
data = A/'Resources/Town/Interiors'; data.mkdir(parents=True,exist_ok=True)
entries=[]; report=[]
for folder in sorted((A/'Models/Town/CapturedScenes').glob('ad_model_building_*')):
    if not folder.is_dir(): continue
    name=folder.name.removeprefix('ad_model_building_')
    label=name[3:].replace('_',' ').title(); ident='Interior_'+name
    out=data/ident; out.mkdir(exist_ok=True)
    vertices=[]; uv=[]; surfaces={}; active=None
    for line in (folder/'catalog.obj').read_text().splitlines():
        f=line.split()
        if not f: continue
        if f[0]=='v':
            x,y,z=map(float,f[1:4]); vertices.append(dict(x=64-64*x,y=64*y,z=64-64*z))
        elif f[0]=='vt': uv.append(dict(x=float(f[1]),y=float(f[2])))
        elif f[0]=='usemtl': active=f[1]; surfaces.setdefault(active,[])
        elif f[0]=='f':
            ids=[int(v.split('/')[0])-1 for v in f[1:]]
            assert all(v.split('/')[0]==v.split('/')[1] for v in f[1:])
            for k in range(1,len(ids)-1): surfaces[active].extend([ids[0],ids[k],ids[k+1]])
    heights=[-32768]*16384
    for triangles in surfaces.values():
        for i in range(0,len(triangles),3):
            a,b,c=[vertices[j] for j in triangles[i:i+3]]
            if max(a['y'],b['y'],c['y'])>.65 or min(a['y'],b['y'],c['y'])<-.75: continue
            den=(b['z']-c['z'])*(a['x']-c['x'])+(c['x']-b['x'])*(a['z']-c['z'])
            if abs(den)<1e-7: continue
            for z in range(max(0,math.ceil(min(a['z'],b['z'],c['z']))),min(127,math.floor(max(a['z'],b['z'],c['z'])))+1):
                for x in range(max(0,math.ceil(min(a['x'],b['x'],c['x']))),min(127,math.floor(max(a['x'],b['x'],c['x'])))+1):
                    u=((b['z']-c['z'])*(x-c['x'])+(c['x']-b['x'])*(z-c['z']))/den
                    v=((c['z']-a['z'])*(x-c['x'])+(a['x']-c['x'])*(z-c['z']))/den
                    if min(u,v,1-u-v)>=-1e-4:
                        heights[z*128+x]=max(heights[z*128+x],round(256*(u*a['y']+v*b['y']+(1-u-v)*c['y'])))
    available={i for i,h in enumerate(heights) if h!=-32768}; groups=[]
    while available:
        seed=available.pop(); group={seed}; todo=[seed]
        while todo:
            q=todo.pop()
            for n in (q-1,q+1,q-128,q+128):
                if n in available and abs(n%128-q%128)+abs(n//128-q//128)==1:
                    available.remove(n); group.add(n); todo.append(n)
        groups.append(group)
    if not groups: raise ValueError('No walkable ground: '+name)
    largest=max(groups,key=len)
    for i in range(len(heights)):
        if i not in largest: heights[i]=-32768
    candidates=[i for i in largest if all(i+o in largest for o in (-1,1,-128,128))] or list(largest)
    cx=sum(i%128 for i in largest)/len(largest); cz=sum(i//128 for i in largest)/len(largest)
    # Prefer clear space over the geometric center, which can sit under a partition.
    obstacles = {(round(v['x']), round(v['z'])) for v in vertices if v['y'] > .65}
    def spawn_score(index):
        x, z = index % 128, index // 128
        clearance = min(9, min(((x-ox)**2+(z-oz)**2 for ox,oz in obstacles), default=9))
        return (-clearance, (x-cx)**2+(z-cz)**2, index)
    spawn = min(candidates, key=spawn_score)
    gp=out/'geometry.json'; gp.write_text(json.dumps(dict(vertices=vertices,uv=uv,surfaces=[dict(triangles=x) for x in surfaces.values()]),separators=(',',':')))
    hp=out/'heights.bytes'; hp.write_bytes(struct.pack('<16384h',*heights))
    materials=[]
    for material in surfaces:
        tex=A/'Textures/Town/CapturedScenes'/folder.name/'textures'/f'{material}.png'
        if not tex.exists(): raise FileNotFoundError(tex)
        mp=out/(material+'.mat')
        mp.write_text(f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_Name: {material}
  m_Shader: {{fileID: 4800000, guid: {shader}, type: 3}}
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 1
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _MainTex:
        m_Texture: {ref(tex,2800000)}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats: []
    m_Colors: []
''')
        materials.append(ref(mp,2100000))
    residents=[]
    names=['Wreath','Weedy'] if 'koh_house' in name else ['Nico'] if 'nico_house' in name else ['Cherrl'] if 'cherrl_house' in name else []
    for npc in names:
        pf=A/'Prefabs/NPCs'/f'NPC_{npc}.prefab'
        fid=re.search(r'--- !u!1 &(\d+)',pf.read_text()).group(1); residents.append(ref(pf,fid))
    scene=scenes/(ident+'.unity')
    scene.write_text(f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1000
GameObject:
  m_ObjectHideFlags: 0
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1001}}
  - component: {{fileID: 1002}}
  m_Layer: 0
  m_Name: {label}
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &1001
Transform:
  m_ObjectHideFlags: 0
  m_GameObject: {{fileID: 1000}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Children: []
  m_Father: {{fileID: 0}}
--- !u!114 &1002
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_GameObject: {{fileID: 1000}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {ref(A/'Scripts/TownInteriorScene.cs',11500000)}
  m_Name:
  m_EditorClassIdentifier:
  interiorId: {ident}
  displayName: {label}
  geometry: {ref(gp,4900000)}
  heights: {ref(hp,4900000)}
  materials:
'''+''.join('  - '+m+'\n' for m in materials)+f'  entryPosition: {{x: {spawn%128}, y: 0, z: {spawn//128}}}\n'+('  residentPrefabs:\n'+''.join('  - '+r+'\n' for r in residents) if residents else '  residentPrefabs: []\n')+'''--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
  - {fileID: 1001}
''')
    meta(scene); entries.append(dict(id=ident,label=label))
    report.append(dict(scene=ident,vertices=len(vertices),materials=len(materials),walkable_cells=len(largest),spawn=[spawn%128,spawn//128],source=str(folder.relative_to(P))))
(data/'catalog.json').write_text(json.dumps(dict(entries=entries),indent=2)); meta(data/'catalog.json')
base=(A/'Scenes/ReimaginedDemo.unity').read_text()
controller=meta(A/'Scripts/TowerGameController.cs')
for name,tower in [('Monsbaiya',0),('MonsterTower',1)]:
    text=base.replace(f'm_Script: {{fileID: 11500000, guid: {controller}, type: 3}}',f'm_Script: {{fileID: 11500000, guid: {controller}, type: 3}}\n  _startInTower: {tower}')
    target=A/'Scenes'/f'{name}.unity'; target.write_text(text); meta(target)
settings=P/'ProjectSettings/EditorBuildSettings.asset'; text=settings.read_text()
for scene in [A/'Scenes/Monsbaiya.unity',A/'Scenes/MonsterTower.unity']+sorted(scenes.glob('*.unity')):
    path=scene.relative_to(P).as_posix()
    if path not in text: text=text.replace('  m_configObjects:',f'  - enabled: 1\n    path: {path}\n    guid: {meta(scene)}\n  m_configObjects:',1)
settings.write_text(text)
for root in (data,scenes):
    for folder in [root]+[x for x in root.rglob('*') if x.is_dir()]:
        m=Path(str(folder)+'.meta')
        if not m.exists(): m.write_text(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n')
(P/'Temp/interior-generation.json').write_text(json.dumps(report,indent=2))
print(f'Generated {len(entries)} captured interior scenes and two playable entry scenes.')
