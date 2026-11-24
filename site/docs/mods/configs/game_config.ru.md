# Настройки установки — game_config.json

`GameConfig` читает `config/game_config.json` из дерева игры при запуске. Файл задаёт значения по умолчанию для звука, языка, экрана и логирования. Пользовательский профиль `user://settings.json` хранится отдельно. Его параметры экрана описаны в разделе [Настройки экрана](/docs/core/display-settings).

| Поля | Назначение |
|---|---|
| `audio.sounds_enabled`, `audio.musics_enabled`, `audio.ambient_enabled` | Включают эффекты, музыку и фоновые звуки |
| `audio.sounds_volume`, `audio.musics_volume`, `audio.ambient_volume` | Громкость соответствующего канала от 0 до 100 |
| `language` | Язык интерфейса, например `en-en` или `ru-ru` |
| `display.window_mode`, `display.current_screen` | Режим окна Godot и индекс монитора от 0 |
| `display.resolution_width`, `display.resolution_height` | Ширина и высота в оконном режиме. В поставляемом файле 2560×1440, значения в коде 1920×1080 |
| `display.vsync_enabled`, `display.max_fps` | Вертикальная синхронизация и лимит кадров. Нулевой лимит означает отсутствие ограничения |
| `logging.enabled`, `logging.console_enabled`, `logging.minimum_level` | Включают запись логов, вывод в консоль и порог уровня |
| `logging.max_file_size_mb`, `logging.max_files` | Размер файла и число сохраняемых файлов при ротации |
| `logging.enabled_categories` | Список категорий, который читает логгер. Фильтрация по нему сейчас отключена |

Базовый вьюпорт Godot использует 1920×1024 и режим растяжения `canvas_items`. `RenderScale3D` задаётся в пользовательском профиле. При отсутствии переопределения действует масштаб из настроек проекта.

Основной лог пишется в `user://logs/cthangover.log`. Ошибки дублируются в `user://logs/cthangover_errors.log`. Передайте `--log-file=<путь>` при запуске, чтобы изменить путь основного лога. Для диагностики ядра ищите категории `MODS_COMPILE`, `MODS_REGISTRY` и `CONFIG`.
