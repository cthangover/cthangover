hljs.registerLanguage("filestree", function (hljs) {
  "use strict";

  return {
    name: "Files Tree",
    contains: [
      // Tree-drawing characters
      {
        scope: "comment",
        begin: /[│├└][─\s]*/,
        relevance: 0,
      },
      // Directory names (word ending with /)
      {
        scope: "type",
        begin: /[\w.\-{}]+(?=\/)/,
        relevance: 1,
      },
      // Trailing slash after directory name
      {
        scope: "type",
        begin: /\//,
        relevance: 0,
      },
      // Comments: # ... or ← ...
      {
        scope: "string",
        begin: /[#←]/,
        end: /$/m,
        relevance: 0,
      },
    ],
  };
});
