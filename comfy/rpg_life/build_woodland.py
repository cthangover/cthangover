"""Build/install a cohesive woodland atlas from the six local ComfyUI jobs.

Run on the host with the ComfyUI Python: build_woodland.py [--install].
Original tiles/maps are retained in gen/rpg_life_source, making repeated builds idempotent.
Only village_pack and the tile RPG content are written; novel art is never opened.
"""
import glob
import json
import os
from pathlib import Path
import shutil
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance

sys.path.insert(0, 'comfy/rpg_art')
from build_village import key_mask, sprite

T = 32
OUT = Path('gen/woodland_build')
SOURCE = Path('gen/rpg_life_source')
ATLAS = Path('mods/village_pack/tilesets/village/village.png')
META = Path('mods/village_pack/tilesets/village/tileset.json')


def load_json(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def save_json(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


def original(path):
    target = SOURCE / path
    target.parent.mkdir(parents=True, exist_ok=True)
    if not target.exists():
        shutil.copyfile(path, target)
    return target


def render(name):
    paths = sorted(glob.glob(f'gen/comfy/rpg_life/life_art_{name}__*.png'))
    if not paths:
        raise RuntimeError(f'ComfyUI output missing: {name}')
    return Image.open(paths[-1]).convert('RGB')


def prop(name, w, h, region=None):
    image = render(name)
    if region:
        image = image.crop(region)
    rgb = np.asarray(image).astype(np.float32)
    # The existing alpha-weighted sprite builder removes magenta before downsampling.
    return Image.fromarray(sprite(rgb, key_mask(rgb), w, h, colors=64, line=False))


def texture(name, brightness=1.0):
    source = render(name)
    # The path render contains trees despite the texture-only prompt. Sample a clear earth patch.
    if name == 'path':
        source = source.crop((390, 110, 510, 230))
    image = source.resize((128, 128), Image.Resampling.BOX)
    image = ImageEnhance.Brightness(image).enhance(brightness)
    # Quantize a larger patch, then sample a central cell: the pixel scale matches sprites.
    return image.quantize(colors=40, dither=Image.Dither.NONE).convert('RGBA').crop((48, 48, 80, 80))


def put(sheet, index, image):
    sheet.paste(image, ((index % 8) * T, (index // 8) * T))


def block(sheet, start, image, cols, rows):
    for y in range(rows):
        for x in range(cols):
            put(sheet, start + y * cols + x, image.crop((x*T, y*T, (x+1)*T, (y+1)*T)))


def rows(grid):
    return [' '.join(f'{v:3d}' if v >= 0 else '  .' for v in row) for row in grid]


def stamp(layer, image_start, cols, height, left, top):
    for y in range(height):
        for x in range(cols):
            if 0 <= top+y < len(layer) and 0 <= left+x < len(layer[0]):
                layer[top+y][left+x] = image_start + y*cols + x


def tree(layers, x, y):
    # The trunk keeps the original solid cell; broad foliage is on Front and may overhang paths.
    stamp(layers['Front'], 64, 3, 2, x-1, y-2)
    for dx in range(-1, 2):
        if 0 <= x+dx < len(layers['Main'][0]) and 0 <= y < len(layers['Main']):
            if layers['Main'][y][x+dx] in (-1, 8):
                layers['Main'][y][x+dx] = 70 + dx+1


def plant_trees(layers, candidates, protected=()):
    """Place complete, non-overlapping crowns and roots; never clip another prop."""
    h, w = len(layers['Main']), len(layers['Main'][0])
    occupied = set()
    protected = set(protected)
    for x, y in candidates:
        x, y = max(1, min(w-2, x)), max(2, min(h-1, y))
        cells = {(xx, yy) for yy in range(y-2, y+1) for xx in range(x-1, x+2)}
        if cells & (occupied | protected):
            continue
        if any(layers[key][yy][xx] != -1 for xx, yy in cells for key in ('Main', 'Front')):
            continue
        tree(layers, x, y)
        occupied.update(cells)


def path_edges(layers):
    back = layers['Back']
    for y, row in enumerate(back):
        for x, tile in enumerate(row):
            if tile != 3:
                continue
            mask = 0
            for bit, (dx, dy) in enumerate(((0,-1),(1,0),(0,1),(-1,0))):
                nx, ny = x+dx, y+dy
                if 0 <= ny < len(back) and 0 <= nx < len(row) and back[ny][nx] not in (3, *range(160,176), 6, 7, 21):
                    mask |= 1 << bit
            back[y][x] = 160 + mask


def remake_map(path):
    data = load_json(original(path))
    m = data['Items'][0]
    layers = {key: [[-1 if t == '.' else int(t) for t in row.split()] for row in values] for key, values in m['Layers'].items()}
    old_trees = [(x,y) for y,row in enumerate(layers['Main']) for x,t in enumerate(row) if t == 8]
    for row in layers['Front']:
        for x,t in enumerate(row):
            if t == 9:
                row[x] = -1
    # Broad trees every third boundary cell instead of identical vertical pickets.
    selected = [(x,y) for x,y in old_trees if x in (0,m['Width']-1) or y not in (1,m['Height']-1) or x % 3 == 1]
    for x,y in old_trees:
        layers['Main'][y][x] = -1
    # Place buildings first so foliage cannot overwrite their tile fragments.
    collision = [list(row) for row in m['Collision']]
    for x,y in old_trees:
        collision[y][x] = '#' if x in (0,m['Width']-1) or y in (0,m['Height']-1) else '.'
    if m['Id'] == 'rpg_village':
        for left in (4,19):
            stamp(layers['Main'],80,6,5,left,2)
            # Logical door remains at the original cell, regardless of decorative art.
            collision[6][left+2] = 'o'
        # A natural pond outline uses the same A2 water entry, preserving collision rules.
        for x,y in ((23,12),(29,12),(23,16),(28,16),(29,16)):
            layers['Back'][y][x] = 0
        layers['Back'][14][22] = 40
        # Small garden plots and stepping stones add lived-in detail without blocking existing paths.
        for x,y in ((2,11),(2,12),(3,11),(7,12),(8,12),(20,12),(21,12)):
            if layers['Main'][y][x] == -1:
                layers['Back'][y][x] = 2
    protected = set()
    for root in ('mods/rpg_demo_game', 'mods/rpg_plus_game'):
        for event_file in Path(root, 'map_events', m['Id']).glob('*.json'):
            for event in load_json(event_file).get('Events', []):
                protected.add((event['X'], event['Y']))
    plant_trees(layers, selected, protected)
    m['Collision'] = [''.join(row) for row in collision]
    if m['Id'] != 'rpg_elder_house':
        m['Atmosphere'] = 'woodland'
        path_edges(layers)
    if m.get('Encounters'):
        m['Encounters']['Steps'] = 48
    m['Layers'] = {key:rows(value) for key,value in layers.items()}
    return data


def shrine_map():
    w,h = 24,16
    layers = {'Back':[[24 for x in range(w)] for y in range(h)],'Main':[[-1]*w for y in range(h)],'Front':[[-1]*w for y in range(h)]}
    for y in range(3,9):
        for x in range(8,17):
            layers['Back'][y][x] = 6 if 9<=x<=15 and 4<=y<=7 else 4
    for x in range(1,13):
        layers['Back'][11][x] = 3
    for y in range(7,12):
        layers['Back'][y][12] = 3
    stamp(layers['Front'],112,4,4,10,1)
    for x,y in ((8,4),(16,4),(8,8),(16,8)):
        layers['Main'][y][x] = 37
    collision = [['.']*w for y in range(h)]
    for y in range(h):
        for x in range(w):
            if y in (0,h-1) or x in (0,w-1):
                collision[y][x]='#'
    # The stone piers and rear altar are solid; the arch opening remains a passage.
    for x,y in ((10,3),(13,3),(10,4),(11,4),(12,4),(13,4)):
        collision[y][x] = '#'
    candidates = [(x,y) for y in (2,h-1) for x in range(1,w-1,3)]
    candidates += [(x,y) for y in range(5,h-1,3) for x in (1,w-2)]
    candidates += [(4,5),(19,5),(5,9),(20,12)]
    plant_trees(layers,candidates,[(1,11),(2,11),(12,5),(12,6)])
    path_edges(layers)
    return {'Kind':'rpg_map','Items':[{'Id':'rpg_woodland_shrine','Name':'rpg_plus/map/shrine','Tileset':'village','Width':w,'Height':h,'Zoom':2,'Ambient':'#a3bbc1','Atmosphere':'woodland','Layers':{k:rows(v) for k,v in layers.items()},'Collision':[''.join(r) for r in collision],'Spawn':{'X':2,'Y':11,'Dir':'Right'}}]}


def make_atlas():
    old = Image.open(original(ATLAS)).convert('RGBA')
    sheet = Image.new('RGBA',(256,704))
    sheet.paste(old,(0,0))
    grass = texture('grass',1.6)
    dirt = texture('path',1.0)
    put(sheet,0,grass)
    # Original scatter overlays are recovered against the old grass rather than flattened away.
    for i in (1,2):
        oldbase = np.asarray(old.crop((0,0,T,T))).astype(int)
        tile = np.asarray(old.crop((i*T,0,(i+1)*T,T))).copy()
        mask = np.linalg.norm(tile[:,:,:3].astype(int)-oldbase[:,:,:3],axis=2)>28
        base = np.asarray(grass).copy()
        base[mask] = tile[mask]
        put(sheet,i,Image.fromarray(base))
    put(sheet,3,dirt)
    # Reduce the original floor's bright checkerboard contrast and warm the shrine border.
    stone = old.crop((6*T,0,7*T,T))
    stone = ImageEnhance.Brightness(stone).enhance(0.73)
    stone = ImageEnhance.Color(stone).enhance(0.55)
    put(sheet,6,stone)
    put(sheet,4,Image.blend(dirt, grass, 0.35))
    dark = ImageEnhance.Brightness(grass).enhance(0.72)
    put(sheet,24,dark)
    # Deep teal water harmonises with the woodland palette.
    water = np.asarray(old.crop((5*T,0,6*T,T))).copy()
    lum = water[:,:,:3].mean(axis=2)/max(1,water[:,:,:3].mean())
    water[:,:,:3] = np.clip(lum[:,:,None]*np.array([47,100,105]),0,255).astype(np.uint8)
    put(sheet,5,Image.fromarray(water))
    block(sheet,64,prop('oak',96,96),3,3)
    block(sheet,80,prop('cottage',192,160),6,5)
    block(sheet,112,prop('ruin',128,128),4,4)
    # Single-cell event art uses the same generated icon set.
    icons = {'amber':(0,0,512,512),'wolf_pelt':(512,0,1024,512),'salve':(0,512,512,1024),'ward_lantern':(512,512,1024,1024)}
    for name,region in icons.items():
        image = prop('relics',64,64,region)
        target = OUT/'items'/f'{name}.png'
        target.parent.mkdir(parents=True,exist_ok=True)
        image.resize((256,256),Image.Resampling.NEAREST).save(target)
    resin = np.asarray(Image.open(OUT/'items/amber.png').convert('RGBA')).copy()
    light = resin[:,:,:3].mean(axis=2)
    for channel, factor in enumerate((0.55, 1.15, 0.85)):
        resin[:,:,channel] = np.clip(light * factor, 0, 255)
    Image.fromarray(resin).save(OUT/'items/resin.png')
    put(sheet,36,prop('relics',26,32,icons['ward_lantern']).resize((32,32),Image.Resampling.NEAREST))
    roots = Image.new('RGBA',(32,32))
    roots.alpha_composite(prop('oak',30,30),(1,2))
    roots.alpha_composite(prop('relics',12,18,icons['amber']),(10,11))
    put(sheet,38,roots)
    put(sheet,37,old.crop((11*T%256,11//8*T,11*T%256+T,11//8*T+T)))
    # Hearth is a small animated-world cue, not a new generated image: stones plus warm light.
    hearth = Image.new('RGBA',(32,32))
    hd = ImageDraw.Draw(hearth)
    hd.ellipse((3,17,29,29),fill='#293b37')
    hd.ellipse((5,19,27,27),fill='#746f5b',outline='#333c35',width=2)
    hd.polygon([(12,23),(10,17),(15,8),(16,16),(20,11),(23,20),(20,25)],fill='#e7a953')
    hd.polygon([(15,24),(14,19),(18,15),(19,22)],fill='#f6d695')
    put(sheet,35,hearth)
    # Pixel-art carpenter's bench: planked top, legs, vice and hammer.
    bench = Image.new('RGBA',(32,32))
    bd = ImageDraw.Draw(bench)
    bd.ellipse((1,24,31,31),fill='#26342c90')
    for x in (5,23):
        bd.rectangle((x,16,x+3,29),fill='#392b24')
        bd.rectangle((x+1,18,x+2,27),fill='#91613b')
    bd.rectangle((6,23,26,25),fill='#553b2c')
    bd.polygon([(2,10),(26,8),(30,13),(30,19),(3,21),(1,16)],fill='#332c27')
    bd.polygon([(2,10),(26,8),(30,13),(4,16)],fill='#c49a60')
    bd.line((3,12,27,10),fill='#e0b976',width=1)
    bd.line((4,14,28,12),fill='#6b492f',width=1)
    bd.polygon([(4,16),(30,13),(30,18),(4,21)],fill='#845735')
    bd.line((5,17,28,15),fill='#b6834a',width=1)
    bd.rectangle((7,6,9,12),fill='#805135')
    bd.rectangle((5,5,13,7),fill='#c0c9b6')
    bd.rectangle((22,12,25,21),fill='#52666a')
    bd.rectangle((20,12,27,15),fill='#9aaca6')
    bd.line((23,20,29,20),fill='#c5c8ae',width=1)
    bd.rectangle((16,9,20,11),fill='#5a684e')
    put(sheet,39,bench)
    # Feather path edges against grass; all sixteen N/E/S/W combinations stay editable tiles.
    a,b=np.asarray(dirt).copy(),np.asarray(grass).copy()
    yy,xx=np.mgrid[0:T,0:T]
    for mask in range(16):
        distance=np.full((T,T),99.0)
        for bit,d in enumerate((yy,T-1-xx,T-1-yy,xx)):
            if mask & (1<<bit):
                distance=np.minimum(distance,d)
        threshold=2.5+np.sin(xx*.7)*.8+np.cos(yy*.8)*.6
        tile=a.copy()
        tile[distance<threshold]=b[distance<threshold]
        put(sheet,160+mask,Image.fromarray(tile))
    return sheet


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    sheet=make_atlas()
    sheet.save(OUT/'village.png')
    maps={}
    for path in Path('mods/rpg_demo_game/maps').glob('*.json'):
        maps[str(path)] = remake_map(path)
    maps['mods/rpg_plus_game/maps/rpg_woodland_shrine.json']=shrine_map()
    for path,data in maps.items():
        save_json(OUT/'maps'/Path(path).name,data)
    # Rebuild the shore from the same grass, earth and water pixels as the new atlas.
    auto_path=Path('mods/village_pack/tilesets/village/water_auto.png')
    original(auto_path)
    sys.path.insert(0, 'comfy/rpg_plus')
    import make_water_autotile as water_autotile
    water_autotile.BAND = 3
    water_autotile.RADIUS = 14
    water_autotile.main(str(OUT/'village.png'), str(OUT/'water_auto.png'))
    auto=Image.open(OUT/'water_auto.png').convert('RGBA')
    meta_path=next(Path('mods/village_pack/tilesets').rglob('*.json'))
    meta=load_json(original(meta_path))
    tiles=meta['Items'][0]['Tiles']
    for start,count,tag,solid in ((39,1,'workbench',True),(35,1,'hearth',True),(36,1,'ward_lantern',True),(37,1,'waystone',True),(38,1,'living_roots',True),(64,9,'oak',False),(80,30,'cottage',True),(112,16,'shrine_arch',False),(160,16,'path_edge',False)):
        for i in range(count):
            cell = np.asarray(sheet.crop(((start+i)%8*T,(start+i)//8*T,((start+i)%8+1)*T,((start+i)//8+1)*T)))
            occupied = bool((cell[:,:,3]>128).mean() > 0.22)
            entry = {'Index':start+i,'Solid':(solid and occupied) or start==64 and i==7,'Tag':f'{tag}_{i}'}
            if start==80 and i in (19,22,25,28):
                entry['Light'] = {'Radius':2.2,'Color':'#ffd594b0'}
            tiles.append(entry)
    if '--install' in sys.argv:
        sheet.save(ATLAS)
        auto.save(auto_path)
        save_json(meta_path,meta)
        for path,data in maps.items():
            save_json(path,data)
        dest=Path('mods/rpg_plus_game/items/rpg_plus')
        dest.mkdir(parents=True,exist_ok=True)
        for icon in (OUT/'items').glob('*.png'):
            shutil.copyfile(icon,dest/icon.name)
    print('woodland atlas, icons, terrain transitions and four maps built; install=', '--install' in sys.argv)


if __name__=='__main__':
    main()
