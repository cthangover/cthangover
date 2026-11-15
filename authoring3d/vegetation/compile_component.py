"""Focused compile of vegetation/editor and optional adapters, without running the game."""
from pathlib import Path
import json,os,re,tempfile,xml.sax.saxutils as xml
root=Path.cwd();out=Path(tempfile.gettempdir())/'cthangover-vegetation-compile';out.mkdir(exist_ok=True)
terrain_source=Path(os.environ.get('CTHANGOVER_TERRAIN_SOURCE',root))
mods={};todo=['terrain_editor','world_stream3d_base','weather_base','actor3d_base','combat3d_base','perf_overlay','sky3d_base']
while todo:
 name=todo.pop()
 if name in mods:continue
 base=terrain_source if name in ['terrain_base','terrain_editor'] else root
 path=next(p for p in [base/'mods'/name,base/'mods_private'/name] if p.exists());data=json.loads((path/'manifest.json').read_text(encoding='utf-8-sig'));mods[name]=path
 todo += [re.split('[<>=]',d)[0] for d in data.get('depends',[])]
items=[]
for path in mods.values():
 data=json.loads((path/'manifest.json').read_text(encoding='utf-8-sig'))
 for pattern in data.get('sources',[]):
  for f in path.glob(pattern):items.append('<Compile Include="'+xml.escape(str(f))+'" />')
for folder in (terrain_source/'mods_private/terrain_base/integrations').iterdir():
 for f in folder.rglob('*.cs'):items.append('<Compile Include="'+xml.escape(str(f))+'" />')
for f in (terrain_source/'mods_private/terrain_base/test_suites/vegetation').rglob('*.cs'):items.append('<Compile Include="'+xml.escape(str(f))+'" />')
for f in (root/'.godot/mono/temp/bin/Debug').glob('*.dll'):
 if f.name not in ['Godot.SourceGenerators.dll','GodotSharpEditor.dll']:items.append('<Reference Include="'+f.stem+'"><HintPath>'+str(f)+'</HintPath></Reference>')
project='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><GenerateAssemblyInfo>false</GenerateAssemblyInfo><LangVersion>9</LangVersion><Nullable>disable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup><ItemGroup>'+''.join(items)+'</ItemGroup></Project>'
(out/'component.csproj').write_text(project);print(out/'component.csproj')
