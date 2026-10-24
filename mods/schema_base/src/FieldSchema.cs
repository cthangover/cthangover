namespace Cthangover.Mods.Schemas
{
    using System.Collections.Generic;

    /// <summary>
    /// One described value: a parameter of a scenario action or command, or a field of an
    /// entity kind. The description is what turns a key into a form control and a checked
    /// reference: <see cref="Type"/> picks the control (a number box, an entity picker, a
    /// formula field…), the rest fills it. Unknown types fall back to a text box, so a
    /// schema written for a newer tool still opens in an older one.
    /// </summary>
    public class FieldSchema
    {
        /// <summary>The parameter name (<c>enemies</c>) or the JSON property (<c>Traits</c>).</summary>
        public string Key { get; set; }

        /// <summary>
        /// <c>text</c>, <c>locale</c> (a locale key with a text per language), <c>int</c>,
        /// <c>number</c>, <c>bool</c>, <c>choice</c> (<see cref="Options"/>), <c>ref</c> and
        /// <c>ref_list</c> (entities of <see cref="Kind"/>), <c>picture</c> (a file of
        /// <see cref="Group"/>), <c>formula</c>, <c>condition</c>, <c>who</c> (player, party
        /// or a character), <c>scene</c>, <c>flag</c>, <c>label</c>, <c>list</c> (objects of
        /// <see cref="Of"/>), <c>map</c> (keys from <see cref="Kind"/> to numbers or formulas),
        /// <c>json</c>.
        /// </summary>
        public string Type { get; set; } = "text";

        /// <summary>Locale key of the label; the key itself is shown when empty.</summary>
        public string Name { get; set; }

        /// <summary>Locale key of the tooltip and the palette's help line.</summary>
        public string Description { get; set; }

        /// <summary>Whether the value must be given (the validator reports a missing one).</summary>
        public bool Required { get; set; }

        /// <summary>The value a form starts with (and what the game assumes when it is left out).</summary>
        public string Default { get; set; }

        /// <summary>Allowed values of a <c>choice</c>.</summary>
        public List<string> Options { get; set; }

        /// <summary>The entity kind of a <c>ref</c>, <c>ref_list</c> or the keys of a <c>map</c>.</summary>
        public string Kind { get; set; }

        /// <summary>The file group of a <c>picture</c> (<c>items</c>, <c>characters</c>).</summary>
        public string Group { get; set; }

        /// <summary>
        /// How a new <c>locale</c> key is made: <c>{id}</c> is the entity id, <c>{mod}</c> the
        /// mod id, <c>{key}</c> this field's key (<c>"{mod}/stat/{id}"</c>).
        /// </summary>
        public string KeyPattern { get; set; }

        /// <summary>The fields of each object of a <c>list</c>.</summary>
        public List<FieldSchema> Of { get; set; }

        /// <summary>The mod whose schema file declared this field (filled by the catalog).</summary>
        public string SourceMod { get; set; }
    }
}
