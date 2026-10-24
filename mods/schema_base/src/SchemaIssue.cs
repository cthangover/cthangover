namespace Cthangover.Mods.Schemas
{
    /// <summary>One problem a schema finds in a line or an entity: an error breaks the game, a warning is likely a typo.</summary>
    public class SchemaIssue
    {
        /// <summary>Creates an issue.</summary>
        public SchemaIssue(bool isError, string code, string message)
        {
            IsError = isError;
            Code = code;
            Message = message;
        }

        /// <summary>True for an error, false for a warning.</summary>
        public bool IsError { get; }

        /// <summary>Stable code (<c>SCHEMA_MISSING_PARAM</c>…), what reports and tests match on.</summary>
        public string Code { get; }

        /// <summary>English text for the report.</summary>
        public string Message { get; }
    }
}
