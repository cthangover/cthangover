using System;
using System.Collections.Generic;
namespace Cthangover.Core.Utils
{

	public static class StringParsing
	{
		
		public static ICollection<string> GetIncludesInQuotes(this string text, string firstQuote, string secondQuote)
		{
			var pos = 0;
			var list = new List<string>();
			for (;;)
			{
				var firstIndex = text.IndexOf(firstQuote, pos, StringComparison.InvariantCulture);
				if (firstIndex < 0)
					break;
				var endIndex = text.IndexOf(secondQuote, firstIndex + firstQuote.Length, StringComparison.InvariantCulture);
				if (endIndex < 0)
					throw new ArgumentException("second token '" + secondQuote + "' parse exception");
				list.Add(text.Substring(firstIndex + firstQuote.Length, endIndex - firstIndex - firstQuote.Length));
				pos = endIndex + 1;
			}
			return list;
		}
		
	}
	
}
