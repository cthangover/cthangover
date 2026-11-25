# Test Integrations — prueba de integraciones

Este mod de prueba comprueba el montaje condicional de `test_integrations__probe`. `fixture: true` lo excluye de juegos normales y `--all-mods`, salvo adición explícita con `--with-mods=test_integrations`.

`probe` solo se monta con `quests_base`. No aporta historia, contenido ni datos guardados. Las pruebas verifican presencia en juego, ausencia en el frontend y cambios de mods en el mismo proceso.

```text
dotnet run --project tests/Cthangover.TestRunner -- --test integrations --test integrations_frontend
```
