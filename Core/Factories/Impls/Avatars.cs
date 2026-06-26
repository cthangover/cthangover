namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Compile-time catalog of avatar IDs used by scenario scripts.
    /// Each nested class maps a character name to a set of emotional states,
    /// forming a type-safe alternative to raw string lookups — a typo in
    /// a constant produces a build error, whereas a typo in a string literal
    /// silently returns a null texture at runtime.
    /// </summary>
    public static class Avatars
    {
        public static class Book
        {
            public const string Variant1 = "book_1";
            public const string Variant2 = "book_2";
            public const string Variant3 = "book_3";
            public const string Variant4 = "book_4";
        }

        public static class Marao
        {
            public const string Angry = "marao/angry";
            public const string Think = "marao/think";
            public const string What = "marao/what";
            public const string Relax = "marao/relax";
            public const string Smile = "marao/smile";
        }

        public static class Murakami
        {
            public const string Fear1 = "murakami/fear_1";
            public const string Fear2 = "murakami/fear_2";
            public const string Normal1 = "murakami/normal_1";
            public const string Normal2 = "murakami/normal_2";
            public const string Normal3 = "murakami/normal_3";
        }
    }
}
