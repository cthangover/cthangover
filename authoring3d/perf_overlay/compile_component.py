"""Focused compile only: monitor, its optional contract and their dependency closure."""
from pathlib import Path
import json,re,tempfile,xml.sax.saxutils as xml
root=Path.cwd();out=Path(tempfile.gettempdir())/'cthangover-perf-compile';out.mkdir(exist_ok=True)
mods={};todo=['perf_overlay','scene3d_base','dialog_base']
while todo:
 name=todo.pop()
 if name in mods:continue
 path=next(p for p in [root/'mods'/name,root/'mods_private'/name] if p.exists());data=json.loads((path/'manifest.json').read_text(encoding='utf-8-sig'));mods[name]=path
 todo += [re.split('[<>=]',d)[0] for d in data.get('depends',[])]
items=[]
for path in mods.values():
 data=json.loads((path/'manifest.json').read_text(encoding='utf-8-sig'))
 for pattern in data.get('sources',[]):
  for f in path.glob(pattern):items.append('<Compile Include="'+xml.escape(str(f))+'" />')
for f in (root/'mods/perf_overlay/test_suites/contracts').rglob('*.cs'):items.append('<Compile Include="'+xml.escape(str(f))+'" />')
for f in (root/'.godot/mono/temp/bin/Debug').glob('*.dll'):
 if f.name not in ['Godot.SourceGenerators.dll','GodotSharpEditor.dll']:items.append('<Reference Include="'+f.stem+'"><HintPath>'+str(f)+'</HintPath></Reference>')
project='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><GenerateAssemblyInfo>false</GenerateAssemblyInfo><LangVersion>9</LangVersion><Nullable>disable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup><ItemGroup>'+''.join(items)+'</ItemGroup></Project>'
(out/'component.csproj').write_text(project);print(out/'component.csproj')
