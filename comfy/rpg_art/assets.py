"""Asset list of the village_pack art: what to generate in ComfyUI and how build_village.py turns it into tiles.

kind:
  texture   - seamless ground/surface texture, opaque 32x32 tile
  object    - one object on a magenta background, keyed out and fitted into `cells` tiles (w, h)
  turnaround - a character in three views (front, left, back) for a 3x4 charset
"""

STYLE = "16-bit SNES JRPG pixel art, crisp pixels, limited palette, clean colors"
# One render = one 32x32 tile: a close-up with a few big elements. A "seamless uniform pattern" comes out as fine
# noise that downscales into a flat colour.
TEXTURE = ("close-up of {d}, flat top-down orthographic view, the surface fills the whole image edge to edge, "
           "big simple shapes, muted natural colors, " + STYLE)
# Qwen fills empty space of a pixel-art frame with little people and puts faces on props ("sprite", "RPG Maker",
# "cheerful" make it worse). The workflow runs at cfg=1, where the negative prompt is ignored, so the positive
# prompt must say it: a large object filling the frame and nothing else; build_village.py also drops small
# detached blobs.
OBJECT = ("close-up of {d}, a single inanimate prop that fills most of the image, environment game asset, "
          "slight top-down three-quarter view, " + STYLE + ", nothing else in the picture, no people, no animals, "
          "no faces, isolated on a solid flat pure magenta background, no ground, no cast shadow")
TURN = ("character sprite reference sheet of {d}, exactly three full-body views of the same character side by side "
        "in one row with wide gaps: front view facing the viewer, left side profile view walking left, back view, "
        "cute chibi RPG Maker proportions with a big head, " + STYLE +
        ", isolated on a solid flat pure magenta background, no text, no shadows")
# NEG works only with cfg > 1 (sdxl workflow); the qwen workflow runs at cfg=1
NEG = "blurry, low quality, text, watermark, signature, ui, frame, border, jpeg artifacts, photo, 3d render, gradient background"

ASSETS = {
    # textures
    "grass":       ("texture", "a small patch of meadow grass: soft medium green ground with three small clusters of darker grass blades"),
    "dark_grass":  ("texture", "a small patch of dark forest floor: deep green moss with two small fallen brown leaves"),
    "path":        ("texture", "a small patch of packed light brown dirt road with four small pebbles"),
    "sand":        ("texture", "a small patch of pale yellow sand with a few subtle ripples and darker grains"),
    "water":       ("texture", "a small patch of calm blue lake water with two short white wave ripples"),
    "stone_floor": ("texture", "grey cobblestone pavement, exactly three by three big rounded stones with dark gaps"),
    "wood_floor":  ("texture", "a wooden floor of exactly three wide warm brown planks running horizontally with dark seams"),
    "roof":        ("texture", "a roof of terracotta red clay tiles, exactly four overlapping rows of big rounded tiles with dark shadows under each row"),
    "wall":        ("texture", "close-up of one panel of a half-timbered house wall, flat cream plaster framed by thick dark brown wooden beams along the left edge, the right edge and the bottom, flat front view, the panel fills the whole image"),
    "bridge":      ("texture", "a wooden bridge deck of exactly four weathered brown planks running horizontally, with nail heads"),
    "rock_wall":   ("texture", "a rough grey rock cliff face with three big cracked stone blocks"),
    # objects
    "tree":        ("object", "a tall slender leafy green tree, twice as tall as it is wide, narrow oval crown on a short brown trunk", (1, 2)),
    "bush":        ("object", "a round green leafy bush", (1, 1)),
    "rock":        ("object", "a grey boulder", (1, 1)),
    "fence":       ("object", "a straight wooden fence segment with two posts and two rails, front view", (1, 1)),
    "sign":        ("object", "a small wooden signpost with a blank board", (1, 1)),
    "chest":       ("object", "a closed wooden treasure chest with golden trim", (1, 1)),
    "chest_open":  ("object", "an open empty wooden treasure chest with golden trim, lid open", (1, 1)),
    "window":      ("object", "a small square house window with a wooden frame and four glass panes, front view", (1, 1)),
    "door":        ("object", "a wooden house door with an iron handle, front view", (1, 1)),
    "well":        ("object", "a round stone water well with a small wooden roof", (1, 1)),
    "stump":       ("object", "a cut tree stump with roots", (1, 1)),
    "mushrooms":   ("object", "a small cluster of three red-capped mushrooms", (1, 1)),
    "cave":        ("object", "a dark cave entrance in a grey rock cliff, front view", (1, 1)),
    "lamp":        ("object", "a tall black iron street lamp post with a glowing lantern on top", (1, 2)),
    "barrel":      ("object", "a plain wooden storage barrel with iron hoops, no decorations", (1, 1)),
    "tall_grass":  ("object", "a dense clump of tall green grass blades", (1, 1)),
    "flowers":     ("object", "a few tiny scattered wildflowers, white yellow and pink", (1, 1)),
    "tufts":       ("object", "two small tufts of green grass", (1, 1)),
    # pickups and item icons (icons for rpg_demo_game/items and trading, ground tiles 32-34)
    "branches":    ("object", "a small bundle of dry brown tree branches and twigs", (1, 1)),
    "stones":      ("object", "a small pile of three grey round stones", (1, 1)),
    "mushroom":    ("object", "a single plump forest mushroom with a brown cap and a thick white stem", (1, 1)),
    "elixir":      ("object", "a round glass potion bottle filled with glowing red healing elixir, cork stopper", (1, 1)),
    "coin":        ("object", "a single shiny gold coin with an embossed star", (1, 1)),
    # characters
    "c_hero":      ("turnaround", "a young hero boy with spiky brown hair, blue tunic, brown belt, brown boots"),
    "c_elder":     ("turnaround", "an old village elder with a long white beard, bald head, long blue robe and a wooden staff"),
    "c_villager":  ("turnaround", "a young village woman with a red headscarf, green dress and white apron"),
    "c_merchant":  ("turnaround", "a plump merchant man with a brown mustache, orange tunic, green vest and a big backpack"),
    "c_slime":     ("turnaround", "a cute green slime monster blob with big eyes"),
    "c_wolf":      ("turnaround", "a grey wolf on four legs"),
}

SIZES = {"texture": (1024, 1024), "object": (1024, 1024), "turnaround": (1536, 1024)}
TALL = (768, 1536)
