import json,math,struct,sys
root=sys.argv[1]; name=sys.argv[2]; write=len(sys.argv)>3; water='quest_done' in name
g=json.load(open(f'{root}/Resources/Town/Interiors/{name}/geometry.json'));V=g['vertices']
H=[-32768]*16384; block=set()
for s in g['surfaces']:
    t=s['triangles']
    for i in range(0,len(t),3):
        a,b,c=[V[j] for j in t[i:i+3]]
        ys=[a['y'],b['y'],c['y']]
        flat=max(ys)-min(ys)<0.01
        if max(ys)>1.6:
            # obstacles standing on the platform (furniture/walls)
            if min(ys)<1.6 or flat:
                for z in range(math.floor(min(a['z'],b['z'],c['z'])),math.ceil(max(a['z'],b['z'],c['z']))):
                    for x in range(math.floor(min(a['x'],b['x'],c['x'])),math.ceil(max(a['x'],b['x'],c['x']))):
                        block.add((x,z))
            continue
        level=256 if flat and abs(ys[0]-1.0)<0.05 else (192 if water and flat and abs(ys[0]-0.75)<0.05 else None)
        if level is None: continue
        x0=min(a['x'],b['x'],c['x']);x1=max(a['x'],b['x'],c['x']);z0=min(a['z'],b['z'],c['z']);z1=max(a['z'],b['z'],c['z'])
        for z in range(math.ceil(z0),math.floor(z1)+1):
            for x in range(math.ceil(x0),math.floor(x1)+1):
                den=(b['z']-c['z'])*(a['x']-c['x'])+(c['x']-b['x'])*(a['z']-c['z'])
                if abs(den)<1e-7: continue
                u=((b['z']-c['z'])*(x-c['x'])+(c['x']-b['x'])*(z-c['z']))/den
                v=((c['z']-a['z'])*(x-c['x'])+(a['x']-c['x'])*(z-c['z']))/den
                if min(u,v,1-u-v)>=-1e-4: H[z*128+x]=max(H[z*128+x],level)
for _ in range(2):
    F=H[:]
    for z in range(1,127):
        for x in range(1,127):
            if H[z*128+x]!=-32768: continue
            l,r,u,d=H[z*128+x-1],H[z*128+x+1],H[(z-1)*128+x],H[(z+1)*128+x]
            if l!=-32768 and r!=-32768: F[z*128+x]=max(l,r)
            elif u!=-32768 and d!=-32768: F[z*128+x]=max(u,d)
    H=F
# a lattice point is walkable when all 4 surrounding cells are platform-ish: require the 4 cells' corners present
W=[-32768]*16384
for z in range(1,127):
    for x in range(1,127):
        if H[z*128+x]!=-32768 and all(H[(z+dz)*128+x+dx]!=-32768 for dx,dz in ((1,0),(-1,0),(0,1),(0,-1))) and not any((x+dx,z+dz) in block for dx in (-1,0) for dz in (-1,0)):
            W[z*128+x]=H[z*128+x]
for z in range(6,40):
    print('%2d '%z+''.join(('~' if W[z*128+x]==192 else '.') if W[z*128+x]!=-32768 else ('o' if H[z*128+x]!=-32768 else '#') for x in range(6,45)))
if write:
    open(f'{root}/Resources/Town/Interiors/{name}/heights.bytes','wb').write(struct.pack('<16384h',*W))
