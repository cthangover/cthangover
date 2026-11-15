"""Compile the changed component with its source dependency closure, without running Godot or any test suite."""
from pathlib import Path
import json,re,tempfile,xml.sax.saxutils as xml
root=Path.cwd();out=Path(tempfile.gettempdir())/'cthangover-streaming-compile';out.mkdir(exist_ok=True)
mods={};todo=['world_stream3d_editor','water3d_base']
while todo:
 name=todo.pop()
 if name in mods:continue
 path=next(p for p in [root/'mods'/name,root/'mods_private'/name] if p.exists());data=json.loads((path/'manifest.json').read_text(encoding='utf-8-sig'));mods[name]=path
 todo += [re.split('[<>=]',d)[0] for d in data.get('depends',[])]
items=[]
for name,path in mods.items():
 data=json.loads((path/'manifest.json').read_text(encoding='utf-8-sig'))
 for pattern in data.get('sources',[]):
  for f in path.glob(pattern):items.append('<Compile Include="'+xml.escape(str(f))+'" />')
for f in list((root/'mods_private/world_stream3d_base/test_suites/contracts/src').glob('*.cs'))+list((root/'mods_private/world_stream3d_editor/test_suites/contracts/src').glob('*.cs')):items.append('<Compile Include="'+xml.escape(str(f))+'" />')
for f in (root/'mods_private/world_stream3d_base/integrations/water/src').glob('*.cs'):items.append('<Compile Include="'+xml.escape(str(f))+'" />')
for f in (root/'mods_private/world_stream3d_base/test_suites/water_checks/src').glob('*.cs'):items.append('<Compile Include="'+xml.escape(str(f))+'" />')
for f in (root/'.godot/mono/temp/bin/Debug').glob('*.dll'):
 if f.name not in ['Godot.SourceGenerators.dll','GodotSharpEditor.dll']:items.append('<Reference Include="'+f.stem+'"><HintPath>'+str(f)+'</HintPath></Reference>')
project='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><GenerateAssemblyInfo>false</GenerateAssemblyInfo><LangVersion>9</LangVersion><Nullable>disable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup><ItemGroup>'+''.join(items)+'</ItemGroup></Project>'
(out/'component.csproj').write_text(project);print(out/'component.csproj')
