hljs.registerLanguage("jsonc", function (hljs) {
  "use strict";

  return {
    name: "JSON with Comments",
    aliases: ["jsonc"],
    contains: [
      hljs.C_LINE_COMMENT_MODE,
      hljs.C_BLOCK_COMMENT_MODE,
      {
        className: "attr",
        begin: /"(\\.|[^\\"\r\n])*"(?=\s*:)/,
        relevance: 0,
      },
      {
        className: "punctuation",
        begin: /[{}[\],:]/,
      },
      hljs.QUOTE_STRING_MODE,
      {
        className: "literal",
        begin: /\b(true|false|null)\b/,
      },
      hljs.C_NUMBER_MODE,
    ],
  };
});
