# Numeric expressions and dialog variables

`expressions_base` supplies the shared expression parser used by dialogs, schemas and stats. Arithmetic works in a visual novel without RPG or character stats.

```scenario
set money=rnd(3,5)
cset total "1+2+${money}"
ptext "You found ${money} coins."
action item.add id=money/coin count=${money}
end
```

The final action additionally requires `items_base` and content defining the item. The random value is calculated once and reused, including in translated `ptext` values and action parameters.

| Assignment | Result |
|---|---|
| `set name value` | Literal string |
| `set name=expression` | Calculated number |
| `cset name expression` or `cset name=expression` | Calculated number |
| `set name = value` | Invalid intended assignment: space-separated literal form stores `=` |

Quote expressions containing spaces. Names are case-sensitive. Variables belong to the current runtime; for persistent state read by conditions, use `flag`. Each concurrent background execution has its own variable dictionary.

## Grammar and errors

Numbers use a decimal point. Arithmetic: `+ - * / % ^`, parentheses and unary signs. Comparisons: `< <= > >= == !=`. Boolean results are 0/1; `! && ||` combine them. Both `condition ? yes : no` and `if(condition, yes, no)` select lazily. Multiplication precedes addition, powers associate right, and `-2^2` is -4.

Functions: `min`, `max`, `abs`, `floor`, `ceil`, `round`, `sqrt`, `clamp`, `rnd`, `rand`, `chance`. `rnd(a,b)` requires ordered Int32 integer bounds and includes both endpoints. `rand` keeps the legacy formula behaviour of reordering and rounding bounds inward. `chance(p)` uses percentages from 0 to 100. Round fractional results before passing them to integer-only item actions.

Dialog assignments reject undefined/nonnumeric variables, zero division, invalid arguments and non-finite results. They stop execution with source and line diagnostics before dependent actions and preserve the old variable value. Reading `${name}` numerically reads its value; it cannot inject expression syntax. Syntax inspection in the validator and palette does not consume randomness.

Stats formulas retain their legacy permissive treatment of unknown variables and division by zero (zero). Sharing syntax does not make these two error policies interchangeable.

## Extending the language

Public parameterless `IExpressionFunctionStrategy`, `IExpressionBinaryStrategy` and `IExpressionUnaryStrategy` implementations are discovered in mods that depend on `expressions_base`. Function strategies declare a name and argument bounds (`MaxArguments = -1` for unlimited); evaluate operands with `context.Value` and use `context.Random`. Operator strategies declare a symbol and precedence (1–100), with optional right associativity for binary operators. The first registration owns a name; duplicate registrations cannot override built-ins. Unloading removes strategies and registry changes invalidate parser caches.

The source-compatible `Cthangover.Mods.Stats.Formulas` APIs remain available. Implementation and examples: `mods/expressions_base/src/` and that mod's bundled guide.
