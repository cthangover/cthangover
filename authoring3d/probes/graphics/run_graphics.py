"""Host watcher only. Sequential, bounded, isolated 3D graphics experiments; never edits installed mods or core."""
import argparse, hashlib, json, os, re, shutil, subprocess, tempfile, time
from pathlib import Path


def dump(path, obj):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding='utf-8')


def run_command(command, cwd, log, timeout=240, env=None):
    log.parent.mkdir(parents=True, exist_ok=True)
    with log.open('wb') as stream:
        p = subprocess.Popen(command, cwd=cwd, env=env, stdout=stream, stderr=subprocess.STDOUT)
        try:
            deadline = time.monotonic()+timeout
            while p.poll() is None and time.monotonic()<deadline:
                time.sleep(.5)
                marker=log.parent/'scene-ready.json'
                if marker.exists() and not (log.parent/'desktop-reference.json').exists():
                    try:
                        from desktop_reference import capture
                        result=capture(p.pid,log.parent/'desktop-reference.png')
                    except Exception as error:
                        result={'error':str(error)}
                    dump(log.parent/'desktop-reference.json',result)
                # Compilation diagnostics may be file-only; copy them while the child is alive.
                if env and 'APPDATA' in env:
                    logs = Path(env['APPDATA'])/'cthangover/logs'
                    if logs.exists():
                        shutil.copytree(logs, log.parent/'game-logs', dirs_exist_ok=True)
                        auth = logs/'cthangover.log'
                        if auth.exists() and 'error CS' in auth.read_text(encoding='utf-8-sig',errors='replace'):
                            subprocess.run(['taskkill','/PID',str(p.pid),'/T','/F'],capture_output=True)
                            p.wait(timeout=20)
                            break
            code = p.wait(timeout=1)
        except subprocess.TimeoutExpired:
            subprocess.run(['taskkill', '/PID', str(p.pid), '/T', '/F'], capture_output=True)
            p.wait(timeout=20)
            code = 124
    return code


def snapshot(root, target):
    target.mkdir()
    ignore = shutil.ignore_patterns('.git', '.git-template', 'bin', 'obj', 'test_suites', 'tests', 'docs', '__pycache__')
    for folder in ['Core', 'scenes', 'config']:
        shutil.copytree(root/folder, target/folder, ignore=ignore)
    for name in ['Cthangover.csproj', 'global.json', 'Directory.Build.props', 'Directory.Build.targets', 'project.godot', 'export_presets.cfg']:
        shutil.copy2(root/name, target/name)
    shutil.copytree(root/'.godot/mono/temp/bin/Debug', target/'.godot/mono/temp/bin/Debug')
    names = {'terrain_base', 'proto_sea_pack', 'menu_base'}
    pending = list(names)
    while pending:
        name = pending.pop()
        source = next(p for p in [root/'mods'/name, root/'mods_private'/name] if p.exists())
        shutil.copytree(source, target/source.parent.name/name, ignore=ignore)
        for dep in json.loads((source/'manifest.json').read_text(encoding='utf-8-sig')).get('depends', []):
            dep = re.split('[><=]', dep)[0]
            if dep not in names:
                names.add(dep)
                pending.append(dep)
    mod = target/'mods_private/test_graphics_probe'
    (mod/'src').mkdir(parents=True)
    for src in (root/'authoring3d/probes/graphics').glob('*.cs.txt'):
        shutil.copy2(src, mod/'src'/src.name.removesuffix('.txt'))
    shutil.copy2(root/'authoring3d/probes/graphics/compute.glsl', mod/'compute.glsl')
    shutil.copy2(root/'authoring3d/probes/graphics/quality_presets.json', mod/'quality_presets.json')
    shutil.copytree(root/'mods_private/.git-template', mod/'.git')
    for name in ['.gitignore', '.gitattributes']:
        shutil.copy2(root/'mods_private/world3d_base'/name, mod/name)
    dump(mod/'manifest.json', {'id':'test_graphics_probe','name':'Disposable graphics backend probe','version':'0.1.0','author':'ct','depends':['terrain_base>=0.1.0','proto_sea_pack>=0.1.0','ui_base>=0.1.0'],'sources':['src/**/*.cs']})
    fixture = root/'mods_private/terrain_base/test_suites/contracts'
    for name in ['terrains', 'terrain', 'models3d', 'profiles3d', 'definitions']:
        if (fixture/name).exists():
            shutil.copytree(fixture/name, mod/name)
    dump(mod/'scenes/placeholder.json', {'Name':'graphics_probe_placeholder','SaveAllowed':False})
    dump(target/'collections/frontend/collection.json', {'id':'frontend','kind':'frontend','version':'0.1.0','name':'Graphics probe frontend','mods':['ui_base','menu_base']})
    # Candidate user:// engine override hook only in the isolated project. The installed project stays untouched.
    path = target/'project.godot'
    text = path.read_text(encoding='utf-8').replace('[application]', '[application]\nconfig/project_settings_override="user://graphics_override.cfg"', 1)
    path.write_text(text, encoding='utf-8')
    return sorted(names | {'test_graphics_probe'})


def launch(args, envdata, name, driver=None, method=None, mode='matrix', release=False, settings=None, extra_env=None, shared=None, expect_boot_failure=False):
    out = args.out/name
    out.mkdir(exist_ok=False)
    data = Path(envdata['scratch'])/('user_'+(shared or name))
    env = os.environ.copy()
    for key, folder in [('APPDATA','roaming'),('LOCALAPPDATA','local'),('TMP','temp'),('TEMP','temp')]:
        env[key] = str(data/folder)
        Path(env[key]).mkdir(parents=True, exist_ok=True)
    env['CTHANGOVER_TEST_CACHE'] = str(Path(envdata['scratch'])/'compiler-cache')
    if release:
        system_root=env.get('SYSTEMROOT') or env.get('SystemRoot')
        if not system_root:raise RuntimeError('Windows system root unavailable')
        env['PATH']=str(Path(system_root)/'System32')+';'+system_root
        env['NUGET_PACKAGES']=str(data/'empty-nuget')
        env['DOTNET_ROOT']=str(data/'no-sdk')
    if extra_env:
        env.update(extra_env)
    project = Path(envdata['project'])
    executable = args.godot
    if release:
        project = Path(envdata['release'])
        executable = str(project/'cthangover.exe')
    override = project/'override.cfg'
    if override.exists():
        override.unlink()
    if settings:
        override.write_text(settings, encoding='utf-8')
    command = [executable]
    if not release:
        command += ['--path', str(project)]
    command += ['--verbose', '--resolution', '1280x720', '--disable-crash-handler']
    if driver:
        command += ['--rendering-driver', driver]
    if method:
        command += ['--rendering-method', method]
    command += ['--', '--frontend', '--itest=graphics_probe', '--with-mods=test_graphics_probe', '--graphics-case='+mode, '--graphics-out='+str(out)]
    dump(out/'probe-inputs.json', {q.name:hashlib.sha256(q.read_bytes()).hexdigest() for q in (Path(envdata['project'])/'mods_private/test_graphics_probe/src').glob('*.cs')})
    dump(out/'launch.json', {'command':command,'isolated_user':str(data),'root_override':settings,'extra_env':extra_env})
    start = time.monotonic()
    if mode=='crash_marker':
        with (out/'stdout.log').open('wb') as stream:
            child=subprocess.Popen(command,cwd=project,env=env,stdout=stream,stderr=subprocess.STDOUT)
            deadline=time.monotonic()+45
            while child.poll() is None and not (out/'crash-ready.json').exists() and time.monotonic()<deadline:time.sleep(.25)
            if child.poll() is None:
                subprocess.run(['taskkill','/PID',str(child.pid),'/T','/F'],capture_output=True)
            code=child.wait(timeout=20)
        dump(out/'controlled-kill.json',{'ready':(out/'crash-ready.json').exists(),'exit':code,'scope':'Only this isolated owned child process; unclean-start protocol test'})
    else:
        code = run_command(command, project, out/'stdout.log', 240 if mode=='toggles' else 150, env)
    text = (out/'stdout.log').read_text(encoding='utf-8-sig', errors='replace')
    if mode in ['restart','restart_user']:
        deadline = time.monotonic()+90
        while not (out/'resumed/row.json').exists() and time.monotonic()<deadline:
            time.sleep(.5)
    if mode in ['restart','restart_user'] and (out/'resumed/row.json').exists():
        import ctypes
        child=json.loads((out/'resumed/row.json').read_text());pid=child.get('pid')
        kernel=ctypes.windll.kernel32;kernel.OpenProcess.restype=ctypes.c_void_p
        handle=kernel.OpenProcess(0x00100000|0x1000,False,pid) if pid else None
        try:
            (out/'resumed/parent-ack').write_text('parent holds child process handle')
            info={'pid':pid,'handle_opened':bool(handle)}
            if handle:
                kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_ulong]
                info['wait']=kernel.WaitForSingleObject(handle,30000)
                result=ctypes.c_ulong();kernel.GetExitCodeProcess.argtypes=[ctypes.c_void_p,ctypes.POINTER(ctypes.c_ulong)];kernel.GetExitCodeProcess(handle,ctypes.byref(result));info['exit_code']=result.value
            dump(out/'resumed/process-exit.json',info)
            if not handle or info.get('wait')!=0 or info.get('exit_code')!=0:raise RuntimeError('Restart child exit could not be verified: '+str(info))
        finally:
            if handle:kernel.CloseHandle.argtypes=[ctypes.c_void_p];kernel.CloseHandle(handle)
    logs = data/'roaming/cthangover/logs'
    if logs.exists():
        shutil.copytree(logs, out/'game-logs', dirs_exist_ok=True)
    row = json.loads((out/'row.json').read_text()) if (out/'row.json').exists() else {'status':'NO_RUNTIME_ROW'}
    issues = []
    if not expect_boot_failure:
        if code or row.get('status')!='OK' or '[TEST_RESULT] name=graphics_probe passed=1 failed=0' not in text:
            issues.append('exit/status/marker failure')
        for line in text.splitlines():
            lower=line.lower()
            if any(s in lower for s in ['error cs','instances leaked','rid allocations','still in use at exit','leaked at exit','lack lifecycle binding']):
                issues.append(line)
        errors = out/'game-logs/cthangover_errors.log'
        if errors.exists():
            issues += [s for s in errors.read_text(encoding='utf-8-sig', errors='replace').splitlines() if s.strip()]
    row.update(name=name, exit_code=code, launch_seconds=time.monotonic()-start, expected_boot_failure=expect_boot_failure, issues=issues)
    dump(out/'result.json', row)
    print(json.dumps({'name':name,'exit':code,'status':row.get('status'),'driver':row.get('driver'),'method':row.get('method'),'issues':issues}), flush=True)
    if issues:
        raise RuntimeError(f'{name}: {issues}; inspect {out}')
    return row


def export_release(args, data):
    project = Path(data['project'])
    frozen=args.out/'frozen/matrix-probe'
    if not frozen.exists():shutil.copytree(project/'mods_private/test_graphics_probe',frozen,ignore=shutil.ignore_patterns('.git'))
    for part in ['Core','mods','mods_private']:
        for source in (project/part).rglob('*.cs'):
            if '.git' not in source.parts:
                dest=args.out/'frozen/source'/source.relative_to(project);dest.parent.mkdir(parents=True,exist_ok=True)
                if not dest.exists():shutil.copy2(source,dest)
    for source in (args.project/'authoring3d/probes/graphics').glob('*.cs.txt'):
        shutil.copy2(source,project/'mods_private/test_graphics_probe/src'/source.name.removesuffix('.txt'))
    shutil.copy2(args.project/'authoring3d/probes/graphics/quality_presets.json',project/'mods_private/test_graphics_probe/quality_presets.json')
    shutil.copytree(project/'mods_private/test_graphics_probe',args.out/'frozen/release-probe',ignore=shutil.ignore_patterns('.git'),dirs_exist_ok=True)
    release = Path(data['scratch'])/'release_verified'
    release.mkdir(exist_ok=True)
    # A minimal solution is build metadata for this copied core only; no unit/test-runner projects enter the export.
    sln=project/'Cthangover.sln'
    if not sln.exists():
        code=run_command(['dotnet','new','sln','--name','Cthangover','--format','sln'],project,args.out/'solution-create.log')
        if code:raise RuntimeError('Could not create isolated core solution')
        code=run_command(['dotnet','sln','Cthangover.sln','add','Cthangover.csproj'],project,args.out/'solution-add.log')
        if code:raise RuntimeError('Could not add copied core project to isolated solution')
    for filename in ['export.log','export-result.json','release-hashes.json']:
        old=args.out/filename
        if old.exists():
            prior=args.out/('incomplete-no-sln-'+filename)
            if prior.exists():prior=args.out/(str(int(time.time()))+'-'+filename)
            old.rename(prior)
    # Private runtime providers must stay loose; the editor must not import raw DDS files with historical .tga names.
    private=project/'mods_private';staged=Path(data['scratch'])/'private-during-export'
    shutil.move(private,staged)
    # Include the D3D12 dependencies in this experimental candidate package; record the shipping preset separately.
    preset = project/'export_presets.cfg'
    preset.write_text(preset.read_text().replace('application/export_d3d12=0','application/export_d3d12=1'), encoding='utf-8')
    try:
        code = run_command([args.godot,'--headless','--path',str(project),'--export-release','Windows Desktop',str(release/'cthangover.exe')],project,args.out/'export.log',600)
    finally:
        shutil.move(staged,private)
    managed=list(release.rglob('Cthangover.dll'))
    if not managed:raise RuntimeError('Export produced no managed game assembly; exit code alone is insufficient')
    dump(args.out/'export-result.json', {'exit':code,'candidate_d3d12_dependencies':True,'shipping_export_d3d12':False})
    if code or not (release/'cthangover.exe').exists():
        raise RuntimeError('Fresh ExportRelease failed; inspect export.log')
    for name in ['mods','mods_private','collections','config']:
        shutil.copytree(project/name,release/name,ignore=shutil.ignore_patterns('.git','.git-template','obj','bin','test_suites','tests','docs'),dirs_exist_ok=True)
    data['release']=str(release)
    dump(args.out/'environment.json',data)
    hashes={str(p.relative_to(release)):hashlib.sha256(p.read_bytes()).hexdigest() for p in release.rglob('*') if p.is_file() and (p.name in ['cthangover.exe','cthangover.pck','Cthangover.dll','Cthangover.runtimeconfig.json'] or p.name.lower().startswith(('d3d12','dxil','dxcompiler')))}
    dump(args.out/'release-hashes.json',hashes)


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--project',type=Path,required=True);p.add_argument('--out',type=Path,required=True);p.add_argument('--godot',required=True);p.add_argument('--stage',choices=['setup','smoke','matrix','export','release','refresh','toggles','settings','settings_extra','cleanup'],required=True)
    args=p.parse_args();args.out.mkdir(parents=True,exist_ok=True)
    file=args.out/'environment.json'
    if args.stage=='setup':
        scratch=Path(tempfile.mkdtemp(prefix='cthangover-graphics-'));project=scratch/'project';mods=snapshot(args.project,project)
        data={'scratch':str(scratch),'project':str(project),'mods':mods,'cpu':os.environ.get('PROCESSOR_IDENTIFIER'),'godot':args.godot,'source_project_rendering':(args.project/'project.godot').read_text().split('[rendering]')[-1],'inputs':{str(q.relative_to(project)):hashlib.sha256(q.read_bytes()).hexdigest() for name in ['Core','mods','mods_private'] for q in (project/name).rglob('*') if q.is_file() and '.git' not in q.parts and q.suffix in ['.cs','.json','.glb','.gdshader','.r16']}}
        dump(file,data)
        code=run_command(['dotnet','build','Cthangover.csproj','--nologo','-v','minimal'],project,args.out/'build.log')
        if code:raise RuntimeError('Snapshot core build failed')
        launch(args,data,'smoke_vulkan_forward','vulkan','forward_plus')
    else:
        data=json.loads(file.read_text())
        if args.stage=='smoke':
            mod=Path(data['project'])/'mods_private/test_graphics_probe/src'
            fixture=args.project/'mods_private/terrain_base/test_suites/contracts'
            for part in ['terrains','terrain','models3d','definitions']:
                shutil.copytree(fixture/part,mod.parent/part,dirs_exist_ok=True)
            for q in (args.project/'authoring3d/probes/graphics').glob('*.cs.txt'):shutil.copy2(q,mod/q.name.removesuffix('.txt'))
            shutil.copy2(args.project/'authoring3d/probes/graphics/quality_presets.json',mod.parent/'quality_presets.json')
            launch(args,data,'smoke_fixed_'+str(int(time.time())),'vulkan','forward_plus')
        elif args.stage=='matrix':
            rows=[launch(args,data,'matrix_default')]
            for driver in ['vulkan','d3d12','opengl3']:
                for method in ['forward_plus','mobile','gl_compatibility']:
                    # Invalid cross-pairs are expected startup rejections; record actual runtime if Godot corrects them.
                    invalid=(driver=='opengl3')!=(method=='gl_compatibility')
                    rows.append(launch(args,data,'matrix_'+driver+'_'+method,driver,method,expect_boot_failure=invalid))
            dump(args.out/'matrix.json',rows)
        elif args.stage=='export':export_release(args,data)
        elif args.stage=='release':
            for driver in ['vulkan','d3d12','opengl3']:
                launch(args,data,'release_verified_'+driver,driver,'gl_compatibility' if driver=='opengl3' else 'forward_plus',release=True)
            # Emulate the installed preset's disabled optional D3D12 payload, with SDK/Godot dirs absent from PATH.
            release=Path(data['release']);quarantine=Path(data['scratch'])/'d3d12-disabled';quarantine.mkdir(exist_ok=True)
            moved=[]
            for item in list(release.rglob('*')):
                if item.is_file() and item.name.lower() in ['dxil.dll','dxcompiler.dll','d3d12core.dll','d3d12sdklayers.dll']:
                    relative=item.relative_to(release);destination=quarantine/relative;destination.parent.mkdir(parents=True,exist_ok=True);shutil.move(item,destination);moved.append(relative)
            dump(args.out/'disabled-d3d12-payload.json',[str(x) for x in moved])
            try:launch(args,data,'release_shipping_d3d12_payload_off','d3d12','forward_plus',mode='settings',release=True,expect_boot_failure=True)
            finally:
                for relative in moved:shutil.move(quarantine/relative,release/relative)
        elif args.stage=='refresh':
            for target in [Path(data['project']),Path(data['release'])]:
                for source in (args.project/'authoring3d/probes/graphics').glob('*.cs.txt'):shutil.copy2(source,target/'mods_private/test_graphics_probe/src'/source.name.removesuffix('.txt'))
            shutil.copytree(Path(data['release'])/'mods_private/test_graphics_probe',args.out/'frozen/quality-settings-probe',ignore=shutil.ignore_patterns('.git'),dirs_exist_ok=True)
        elif args.stage=='toggles':
            for driver,method in [('vulkan','forward_plus'),('d3d12','forward_plus'),('vulkan','mobile'),('opengl3','gl_compatibility')]:
                launch(args,data,'toggles_'+driver+'_'+method,driver,method,mode='toggles',release=True)
        elif args.stage=='settings':
            launch(args,data,'settings_native_default','vulkan','forward_plus',mode='settings',release=True)
            launch(args,data,'settings_physics_jolt','vulkan','forward_plus',mode='settings',release=True,settings='[physics]\n3d/physics_engine="Jolt Physics"\n')
            launch(args,data,'settings_root_override',mode='settings',release=True,settings='[rendering]\nrenderer/rendering_method="gl_compatibility"\nrendering_device/driver.windows="opengl3"\n')
            user=Path(data['scratch'])/'user_user_override/roaming/cthangover';user.mkdir(parents=True,exist_ok=True)
            (user/'graphics_override.cfg').write_text('[rendering]\nrenderer/rendering_method="mobile"\nrendering_device/driver.windows="vulkan"\n')
            launch(args,data,'settings_user_override',mode='settings',release=True,shared='user_override')
            launch(args,data,'settings_cli_over_user','opengl3','gl_compatibility',mode='settings',release=True,shared='user_override')
            launch(args,data,'restart_from_game','vulkan','forward_plus',mode='restart',release=True,shared='restart')
            launch(args,data,'invalid_driver','nonexistent_driver','forward_plus',mode='settings',release=True,expect_boot_failure=True)
            broken=str(Path(data['scratch'])/'absent_vulkan_driver.json')
            launch(args,data,'fallback_vulkan_to_d3d12','vulkan','forward_plus',mode='settings',release=True,extra_env={'VK_DRIVER_FILES':broken,'VK_ICD_FILENAMES':broken})
            launch(args,data,'fallback_vulkan_to_gl','vulkan','forward_plus',mode='settings',release=True,extra_env={'VK_DRIVER_FILES':broken,'VK_ICD_FILENAMES':broken},settings='[rendering]\nrendering_device/fallback_to_d3d12=false\nrendering_device/fallback_to_opengl3=true\n')
            launch(args,data,'controlled_unclean_exit','vulkan','forward_plus',mode='crash_marker',release=True,shared='safe_mode',expect_boot_failure=True)
            user=Path(data['scratch'])/'user_safe_mode/roaming/cthangover';user.mkdir(parents=True,exist_ok=True)
            # The external launcher detects the pre-runtime marker and writes a user override before the next process.
            marker_detected=(user/'graphics_starting.marker').exists()
            if marker_detected:(user/'graphics_override.cfg').write_text('[rendering]\nrenderer/rendering_method="gl_compatibility"\nrendering_device/driver.windows="opengl3"\n')
            dump(args.out/'safe-mode-launcher.json',{'prior_unclean_marker':marker_detected,'decision':'user override opengl3/gl_compatibility'})
            launch(args,data,'safe_mode_marker',mode='settings',release=True,shared='safe_mode')
        elif args.stage=='settings_extra':
            user=Path(data['scratch'])/'user_driver_override/roaming/cthangover';user.mkdir(parents=True,exist_ok=True)
            (user/'graphics_override.cfg').write_text('[rendering]\nrenderer/rendering_method="mobile"\nrendering_device/driver.windows="d3d12"\n')
            launch(args,data,'settings_user_d3d12_mobile',mode='settings',release=True,shared='driver_override')
            launch(args,data,'restart_by_user_config','vulkan','forward_plus',mode='restart_user',release=True,shared='restart_user_config')
            for driver,method in [('vulkan','forward_plus'),('vulkan','mobile'),('opengl3','gl_compatibility')]:
                launch(args,data,'aa_'+driver+'_'+method,driver,method,mode='aa_proof',release=True)
        elif args.stage=='cleanup':shutil.rmtree(data['scratch'])


if __name__=='__main__':main()
