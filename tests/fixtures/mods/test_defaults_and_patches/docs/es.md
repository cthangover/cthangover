# Test Defaults and Patches — un fixture de prueba para los parches de propiedades

Este mod no es contenido. Es un **fixture de prueba**: un pequeño conjunto de parches de propiedades que la prueba `property_patches` carga para comprobar que los parches sobre personajes e items de otros mods se combinan como el juego promete. Nunca forma parte de un juego y no está pensado para que lo activen los jugadores.

## Qué parchea

Un parche de propiedades es un archivo `patches/<kind>.json`. Cada entrada nombra una entidad por su `Id` y enumera propiedades que añadir o cambiar; el `Id` `*` es un **comodín** que alcanza a cada entidad de esa clase. El fixture trae un parche dirigido y otro comodín por clase:

```json
{
  "Items": [
    { "Id": "Marao", "TestPatched": 42, "TestDefault": 100 },
    { "Id": "*", "TestDefault": -20, "TestGlobalPatch": 1 }
  ]
}
```

```json
{
  "Items": [
    { "Id": "food/ration", "Durability": 99, "SpecialProp": true },
    { "Id": "*", "Durability": 50, "GlobalItemProp": "patched" }
  ]
}
```

Las reglas que estos parches comprueban:

- el parche **dirigido** gana al comodín: Marao recibe `TestDefault` 100, no -20;
- el **comodín** alcanza a todas las demás entidades: cualquier otro personaje recibe `TestDefault` -20 y cada item, `Durability` 50;
- las claves que solo uno de ellos define se conservan todas: Marao recibe además `TestGlobalPatch` 1, y la ración, `GlobalItemProp`;
- una clave dirigida nunca se filtra a otras entidades;
- los campos tipados propios de la entidad, como el nombre de la ración, sobreviven a la combinación.

## Cómo ejecutar la prueba

```text
godot --path . res://scenes/menu/launcher.tscn -- --with-mods=test_defaults_and_patches --itest=property_patches
dotnet run --project tests/Cthangover.TestRunner -- --test property_patches
```

El fixture se carga solo porque `--with-mods=` lo nombra. Su salida en una ejecución real:

```text
[PASS] Marao: targeted value: TestPatched=42
[PASS] Marao: targeted beats wildcard: TestDefault=100
[PASS] Marao: wildcard-only key: TestGlobalPatch=1
[PASS] werewolf_girl_1: wildcard value: TestDefault=-20
[PASS] werewolf_girl_1: no targeted key
[PASS] ration: targeted beats wildcard: Durability=99
[PASS] ration: targeted-only key: SpecialProp=true
[PASS] ration: wildcard-only key: GlobalItemProp=patched
[PASS] ration: typed field survives the merge
[PASS] wolf_meat: wildcard value: Durability=50
[TEST_RESULT] name=property_patches passed=14 failed=0
```

La prueba forma parte del mod (`src/PropertyPatchesTest.cs`) y solo está activa con `--itest=property_patches`.

## Por qué se queda fuera de los juegos

Su manifiesto dice `"fixture": true`. Un fixture nunca forma parte de `--all-mods` ni del conjunto de mods de un juego; se carga solo cuando `--with-mods=` lo nombra, así que sus parches no pueden filtrarse a otras pruebas ni a la partida. Activado en un juego real, añadiría propiedades de prueba a cada personaje y cada item de Cthangover: los parches comodín lo tocan todo. Depende de los mods de Cthangover que parchea (`cthangover_game`, `cthangover_murakami`, `cooking`).

El mismo mod es además un lugar seguro donde las pruebas de herramientas pueden escribir: la prueba `tools_windows` crea en él una escena con el editor de escenas y luego la elimina.
