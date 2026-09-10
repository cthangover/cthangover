using System;
using System.Collections.Generic;
namespace Cthangover.Core.Utils
{

    /// <summary>
    /// Token-based string extraction helpers used primarily by the scenario
    /// scripting subsystem. The main use case is pulling text fragments enclosed
    /// between arbitrary delimiter pairs — such as curly braces in templated
    /// dialogue or square brackets in markup — without resorting to full regex.
    /// </summary>
	public static class StringParsing
	{
		/// <summary>
		/// Scans <paramref name="text"/> for substrings enclosed between
		/// <paramref name="firstQuote"/> and <paramref name="secondQuote"/>,
		/// returning the inner content of each matching pair. Processing resumes
		/// after each closing delimiter, so nested or overlapping delimiters are
		/// <em>not</em> handled — only sequential, non-overlapping pairs.
		/// </summary>
		/// <param name="text">The source string to scan.</param>
		/// <param name="firstQuote">The opening delimiter token.</param>
		/// <param name="secondQuote">The closing delimiter token.</param>
		/// <returns>All extracted inner fragments, in left-to-right order.</returns>
		/// <exception cref="ArgumentException">
		/// Thrown when an opening delimiter is found without its corresponding
		/// closing delimiter later in the string.
		/// </exception>
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
