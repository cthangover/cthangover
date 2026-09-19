using System.Xml.Linq;

namespace Cthangover.TestRunner
{
    /// <summary>
    /// Reads xUnit counters from a Visual Studio TRX report produced by
    /// <c>dotnet test --logger trx</c>. TRX is used instead of parsing console
    /// output because the CLI summary text is localized (the project runs on
    /// Russian Windows and would break English-only parsers).
    /// </summary>
    public static class TrxParser
    {
        public static (int Passed, int Failed, int Skipped)? TryParseCounters(string trxPath)
        {
            if (!File.Exists(trxPath))
                return null;

            var document = XDocument.Load(trxPath);
            var counters = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Counters");
            if (counters == null)
                return null;

            return (Read(counters, "passed"), Read(counters, "failed"), Read(counters, "skipped"));
        }

        private static int Read(XElement element, string attribute)
        {
            return int.TryParse(element.Attribute(attribute)?.Value, out var value) ? value : 0;
        }
    }
}
