# Locale — Localization

All in-game text lives in `.properties` files, organized by mod. The engine resolves locale keys at runtime based on the player's selected language.

## Directory structure

```filestree
locale/
├── en.properties               # UI text, English
├── ru.properties               # UI text, Russian
├── scenarios_en.properties     # Scenario dialogue, English
└── scenarios_ru.properties     # Scenario dialogue, Russian
```

- `{lang}.properties` — general UI text (menus, settings, item names, character names)
- `scenarios_{lang}.properties` — scenario dialogue lines referenced by `key=` parameters

Files are matched by locale code: an exact `{lang}.properties` name or any file
ending with `_{lang}.properties`. Locale files from **all** loaded mods are
merged into one translation, in mod load order — later mods override keys
defined by earlier ones.

## File format

Standard Java `.properties` format:

```properties
# Comment lines start with #
key = value
settings/language = Language
settings/volume = Volume
```

- One `key = value` per line
- Keys use `/` as a namespace separator
- Whitespace around `=` is ignored
- Lines starting with `#` are comments

## Key naming conventions

| Prefix | Purpose |
|---|---|
| `characters/` | Character display names |
| `items/` | Item names and descriptions |
| `rooms/` | Room/scene interaction prompts |
| `quest/` | Quest names and status descriptions |
| `ui/` | Interface labels (menus, battle UI, cooking) |
| `settings/` | Settings screen labels |
| `prolog/` | Prologue narrative text |
| `meeting_murakami/` | Murakami quest dialogue |
| `recipe/` | Recipe names and descriptions |

## Real examples

From `mods/core/locale/en.properties`:

```properties
# Character names
characters/player/marao = Marao
characters/enemies/wolf = Wolf
characters/enemies/werewolf_girl = Werewolf

# Items
items/food/ration/name = Ration
items/food/ration/desc = A daily ration unit for one person
items/food/wolf_meat/name = Wolf cutlet
items/food/wolf_meat/desc = Raw wolf meat

# Settings
settings/back = Back
settings/audio = Audio
settings/music = Music
settings/language = Language
settings/lang_en = English
settings/lang_ru = Russian

# Battle UI
ui/battle/victory = Victory
ui/battle/next = End turn

# Cooking UI
ui/cook/cook = Cook
ui/cook/close = Close
ui/cook/recipe_time = {0} minutes
```

Keys can include format placeholders (`{0}`, `{1}`) for dynamic data.

## Scenario locale files

Separate `scenarios_{lang}.properties` files contain dialogue lines:

```properties
rooms/kitchen/prompt = A room equipped as a kitchen
rooms/kitchen/opt_exit = Leave
rooms/kitchen/opt_cook = Cook
meeting_murakami/after_wolf/greeting = - Dear lady!
meeting_murakami/after_wolf/help = - Help me...
```

These are referenced in `.scenario` files via the `key=` parameter:

```scenario
text "- Dear lady!" key=meeting_murakami/after_wolf/greeting first=marao/think
```

## Supported languages

The set of languages is fixed in code (`LocaleLoader.LangToLocale`) and the
settings dropdown lists exactly these three:

| Settings language | Locale code |
|---|---|
| Russian | `ru-ru` |
| English | `en` |
| Chinese | `zh` |

An unsupported settings value silently falls back to the `ru` locale.

### Loader API

Two static methods on `LocaleLoader` drive loading:

- `LoadCurrentLanguage()` — reads the language from the saved settings, maps it
  through `LangToLocale` (falling back to `ru`), and delegates to `LoadLocale`
- `LoadLocale(string locale)` — merges the `.properties` files for that locale
  across **all** loaded mods into one Godot `Translation`, replaces any previous
  translation for that locale on `TranslationServer`, and sets it as the active
  locale

`LoadLocale` can be called directly to switch languages programmatically.

## Adding a new language

Adding a language requires a code change, not just new files:

1. Add a mapping to `LocaleLoader.LangToLocale` (e.g. `{ "de", "de" }`)
2. Add the option to the language dropdown in `SettingsMenu`
3. Create `locale/{locale}.properties` (or any `*_{locale}.properties`) with UI text translations
4. Create scenario translation files following the same naming convention

## Adding a new locale key

1. Add the key to all language `.properties` files
2. Reference it in scenarios or UI code using the exact key string
3. If referenced from a `.scenario` file, place it in `scenarios_{lang}.properties`
4. If referenced from C# code, use `GodotTranslationProvider.Instance.Get("key")` — it returns `null` for untranslated keys (or Godot's `TranslationServer`)
