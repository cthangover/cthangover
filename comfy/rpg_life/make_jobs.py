"""Queue the woodland reskin through the local host watcher; no model runs in WSL."""
import json
from pathlib import Path

STYLE = ('Premium handcrafted pixel art for a modern top-down woodland RPG, orthographic three-quarter overhead view, '
         'cohesive muted pine green, deep teal shadows, warm ochre highlights, hand-placed pixel clusters, '
         'crisp readable silhouette, subtle texture, no blur, no text, no grid. ')
ASSETS = {
    'oak': 'One large old oak tree, broad irregular rounded leafy crown, twisting trunk, visible roots, small amber leaves. Single isolated sprite, entire tree visible, centered with generous margin on pure flat magenta #ff00ff background. No ground or cast shadow. The crown is wider than the trunk; width about equal to height.',
    'cottage': 'One charming small timber and plaster cottage, wide steep mossy terracotta roof with dormer and a chimney, weathered wooden beams, warm lit windows, single front door slightly left of center, flower boxes and ivy. Roof occupies upper half. Single isolated building sprite, entire building visible, front wall parallel to image plane, no side wall, centered with generous margin on pure flat magenta #ff00ff background. No ground, no scenery.',
    'grass': 'Seamless repeating texture of dark sage green meadow grass, fine tiny blades and sparse clover, tiny low contrast details, evenly lit and uniformly detailed over whole image. Flat overhead texture fills the square, no objects, no horizon.',
    'path': 'Seamless repeating texture of warm tan compacted earth with a few tiny muted pebbles, irregular tiny cracks and scattered moss. Flat overhead texture fills the square, evenly lit and low contrast. No objects, no horizon.',
    'ruin': 'One broken ancient stone archway overgrown with ivy, hollow arch opening, crumbling blue-grey stone blocks and glowing turquoise rune, mysterious woodland shrine. Single isolated sprite, entire arch visible, centered with generous margin on pure flat magenta #ff00ff background. No ground or scenery.',
    'relics': 'Four separate small game inventory icons arranged in a strict 2 by 2 grid, generous empty space between icons: top left a glowing amber resin crystal, top right a coil of silver wolf fur, bottom left a green herbal salve in a round ceramic pot, bottom right an ornate bronze lantern glowing turquoise. Each icon fully visible, same scale and lighting, no overlap. Pure flat magenta #ff00ff background.'
}

def main():
    for index, (name, subject) in enumerate(ASSETS.items()):
        job = {'workflow':'comfy/workflows/qwen_txt2img_api.json', 'patch':{
            '159.inputs.prompt':STYLE + subject,
            '159.inputs.negative_prompt':'blurry, photograph, realistic, text, watermark, isometric, perspective grid, duplicate objects, cropped, white background, smooth gradients',
            '16.inputs.seed':261026 + index, '16.inputs.steps':20, '184.inputs.strength_model':0.0,
            '193.inputs.width':1024, '193.inputs.height':1024}, 'out':'gen/comfy/rpg_life'}
        Path(f'jobs/pending/life_art_{name}.json').write_text(json.dumps(job, indent=2)+'\n', encoding='utf-8')
        print('queued', name)

if __name__ == '__main__':
    main()
