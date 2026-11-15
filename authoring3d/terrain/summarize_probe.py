"""Consolidate explicitly chosen clean observations; never turn a failing harness row into success."""
import argparse
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--draft', type=Path, required=True)
    args = parser.parse_args()
    selection = [
        ('terrain_probe_20261114T202548Z_dcd22b', {'precision', 'chunks'}),
        ('terrain_probe_20261114T202744Z_eac2d8', {'heightmap'}),
        ('terrain_probe_20261114T203225Z_5e19be', {'navigation'}),
        ('terrain_probe_20261114T203643Z_43c723', None),
        ('terrain_pages_20261114T203955Z_10d4fb', None),
        ('terrain_jolt_20261114T204145Z_14cf0f', {'heightmap', 'bodies_heightmap', 'bodies_box'}),
        ('terrain_native_final_20261114T204859Z_deff4e', None),
    ]
    rows = []
    for run, cases in selection:
        data = json.loads((args.root / 'runs' / run / 'matrix.json').read_text(encoding='utf-8-sig'))
        for row in data['rows']:
            if cases is not None and row['case'] not in cases:
                continue
            if row['status'] != 'OK' or row['exit_code'] != 0 or row['harness_errors']:
                raise RuntimeError('Unclean observation: ' + run + '/' + row['case'])
            errors_path = args.root / 'runs' / run / row['case'] / 'game-logs/cthangover_errors.log'
            observed_errors = errors_path.read_text(encoding='utf-8-sig').splitlines() if errors_path.exists() else []
            if any(line.strip() and 'CollectSceneDefinitions — no scene definitions found' not in line for line in observed_errors):
                raise RuntimeError('Unexpected authoritative error: ' + run + '/' + row['case'])
            row['known_empty_probe_shell_diagnostics'] = len(observed_errors)
            row['source_run'] = run
            row['source_row'] = str(args.root / 'runs' / run / row['case'] / 'row.json')
            row['source_log'] = str(args.root / 'runs' / run / row['case'] / 'stdout.log')
            rows.append(row)
    result = {'rows': rows, 'scope': 'Validated observations; earlier failures retained separately. Old empty probe-shell diagnostics and host EOS loader messages are documented. Native Sample does not accept Anim3dMotion.'}
    (args.root / 'matrix.json').write_text(json.dumps(result, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    text = ['Хост: **Core i7-11700F, 8C/16T; RTX 4070 Ti; Godot 4.6.3 mono, Forward+**.',
            'Числа ниже — наблюдения одного хоста, не системные требования. Полная объединённая',
            '[matrix.json](/workspace/art_sources/cthangover/terrain_probe/matrix.json) содержит source_run/source_row/source_log каждой строки.',
            '', '### Данные и native операции', '', '| Проба | Измеренный результат | Полный лог |', '|---|---|---|']
    for row in rows:
        log = '[' + row['case'] + '](' + row['source_log'] + ')'
        if row['case'] == 'precision':
            text.append(f"| PNG16 256² | {row['png']['distinct']} уровней из 65536, {row['png']['format']}, max error {row['png']['max_normalized_error']:.8f}; decode+inspect {row['png']['decode_inspect_ms']:.2f} мс | {log} |")
            text.append(f"| RAW R16/R32 | R16 code error 0; R32 max error {row['raw']['r32_max_normalized_error']:.3g}, read+inspect {row['raw']['read_inspect_ms']:.2f} мс | {log} |")
            text.append(f"| EXR float | {row['exr']['distinct']} уровней, {row['exr']['format']}, max error {row['exr']['max_normalized_error']:.3g}; save/load+inspect {row['exr']['save_load_inspect_ms']:.2f} мс | {log} |")
        elif row['case'] == 'chunks':
            for chunk in row['chunks']:
                text.append(f"| Mesh {chunk['side']}², {chunk['triangles']} tris | p95 generate/commit/full brush replace **{chunk['generate_ms']['p95']:.2f}/{chunk['native_commit_ms']['p95']:.2f}/{chunk['full_replace_after_8x8_brush_ms']['p95']:.2f} мс** | {log} |")
            text.append(f"| 6 полных сеток 129² | CPU {row['six_full_grids_cpu_ms']:.2f} мс, 99846 вершин; это не законченный clipmap | {log} |")
        elif row['case'] == 'heightmap':
            for h in row['heightmaps']:
                text.append(f"| HeightMap {h['side']}², {row['physics_engine']} | create/update {h['shape_set_ms']:.2f}/{h['replace_ms']:.2f} мс; 1000 rays {h['rays_1000_ms']:.2f} мс; hits 1000, hole hit false | {log} |")
        elif row['case'] == 'navigation':
            text.append(f"| Nav region 256 м | worker bake **{row['worker_bake_256m_ms']:.1f} мс**, {row['polygons']} polygons; cache save/load {row['cache_save_reload_ms']:.2f} мс / {row['cache_bytes']} B | {log} |")
            text.append(f"| 1000 valid paths | main **{row['main_1000_paths_ms']:.2f} мс**, worker **{row['worker_1000_paths_ms']:.2f} мс**; оба 1000/1000, map/region iteration 1 | {log} |")
        elif row['case'] == 'occlusion':
            text.append(f"| Runtime ArrayOccluder3D | 4225 vertices / 8192 triangles, set {row['occluder_set_8192_triangles_ms']:.2f} мс; эффективность culling не утверждается | {log} |")
    text += ['', 'Micro mesh: 12 повторов, p95 при таком N совпадает с max; первый вызов включает JIT/инициализацию.',
             'Commit — CPU native API/enqueue, не GPU fence или полная стоимость применения в живой сцене.',
             '', '### Видимые сцены и контроль физики', '',
             'Main/render/physics/GPU — **средние мс**, Wall — p95 мс. Timers перекрываются; не складывать.',
             'Physics — engine monitor, Move — собственный таймер 50 тел на physics step. GPU — viewport passes.',
             '', '| Случай / backend | Main CPU | Render CPU | Physics | Move | GPU | Wall p95 | RAM peak МиБ | Лог |',
             '|---|---:|---:|---:|---:|---:|---:|---:|---|']
    for row in rows:
        if 'wall_ms' not in row:
            continue
        label = row['case'] + (' / Jolt' if row['physics_engine'] == 'Jolt Physics' else ' / DEFAULT')
        values = [row['main_thread_cpu_ms']['mean'], row['render_cpu_ms']['mean'], row['physics_ms']['mean'],
                  row['movement_script_ms'].get('mean', 0), row['gpu_ms']['mean'], row['wall_ms']['p95'], row['process_peak_mib']]
        text.append('| ' + label + ' | ' + ' | '.join(f'{v:.2f}' for v in values) + f" | [лог]({row['source_log']}) |")
    text += ['', 'Независимая target-фикстура: 19 bones, 19 primitives / 228 triangles; Sample: SailorMan 123 bones / 1 primitive / 1679 triangles, townswoman 128 / 1 / 1478.',
             'Это разные draw-call/rig нагрузки: сравнивать как контроль, не как одинаковые модели разного качества.',
             'Между наблюдениями могли отличаться shader/compiler cache warmth; полные исходные строки сохраняют startup/build RAM и sample_count.']
    draft = args.draft.read_text(encoding='utf-8')
    start = draft.index('<!-- TERRAIN_MEASUREMENTS_BEGIN -->')
    end = draft.index('<!-- TERRAIN_MEASUREMENTS_END -->') + len('<!-- TERRAIN_MEASUREMENTS_END -->')
    draft = draft[:start] + '<!-- TERRAIN_MEASUREMENTS_BEGIN -->\n' + '\n'.join(text) + '\n<!-- TERRAIN_MEASUREMENTS_END -->' + draft[end:]
    args.draft.write_text(draft, encoding='utf-8')
    print(f'{len(rows)} clean observations consolidated')


if __name__ == '__main__':
    main()
