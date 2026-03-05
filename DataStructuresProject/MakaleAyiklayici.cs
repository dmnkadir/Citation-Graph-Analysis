using System;
using System.Collections.Generic;
using System.Text;

namespace DataStructuresProject
{
    public class MakaleAyiklayici
    {
        // Main Function: Takes the entire JSON text, returns a Makale list
        public List<Makale> AnalyzeJson(string jsonText)
        {
            List<Makale> articles = new List<Makale>();

            // cleanup: Get rid of the starting '[' and ending ']' characters
            jsonText = jsonText.Trim();
            if (jsonText.StartsWith("[")) jsonText = jsonText.Substring(1);
            if (jsonText.EndsWith("]")) jsonText = jsonText.Substring(0, jsonText.Length - 1);

            // Every article is within { ... }.
            // This part is a bit tricky because curly braces can be nested.
            // We will find the main objects with a simple loop.

            int braceCount = 0;
            int startIndex = 0;

            for (int i = 0; i < jsonText.Length; i++)
            {
                if (jsonText[i] == '{')
                {
                    if (braceCount == 0) startIndex = i; // New object started
                    braceCount++;
                }
                else if (jsonText[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0) // Object finished
                    {
                        string SingleArticleJson = jsonText.Substring(startIndex, i - startIndex + 1);
                        Makale NewArticle = ParseSingleArticle(SingleArticleJson);
                        if (NewArticle != null)
                        {
                            articles.Add(NewArticle);
                        }
                    }
                }
            }

            return articles;
        }

        // Converts a single { ... } block into a Makale object
        private Makale ParseSingleArticle(string json)
        {
            Makale m = new Makale();

            // We will get string values from between quotes.
            m.Id = GetValue(json, "\"id\":");
            m.Doi = GetValue(json, "\"doi\":");
            m.Title = GetValue(json, "\"title\":");
            m.Venue = GetValue(json, "\"venue\":");

            // Numeric values
            string yearStr = GetValue(json, "\"year\":", isNumeric: true);
            if (int.TryParse(yearStr, out int y)) m.Year = y;

            string refCountStr = GetValue(json, "\"in_json_reference_count\":", isNumeric: true);
            if (int.TryParse(refCountStr, out int rc)) m.InJsonReferenceCount = rc;

            // Lists (Authors, Keywords, ReferencedWorks)
            m.Authors = GetList(json, "\"authors\":");
            m.Keywords = GetList(json, "\"keywords\":");
            m.ReferencedWorks = GetList(json, "\"referenced_works\":");

            // Calculate OutDegree (Count of references given)
            m.OutDegree = m.ReferencedWorks.Count;

            return m;
        }

        // Finds the value in the "key": "value" structure
        private string GetValue(string json, string key, bool isNumeric = false)
        {
            int index = json.IndexOf(key);
            if (index == -1) return null;

            // Skip the key
            index += key.Length;

            // If no quotes (if numeric), take until a comma or } is seen
            if (isNumeric)
            {
                int end = json.IndexOfAny(new char[] { ',', '}' }, index);
                if (end == -1) return null;
                return json.Substring(index, end - index).Trim();
            }
            else
            {
                // If quotes exist: find the first quote, take everything until the next one
                int firstQuote = json.IndexOf('"', index);
                if (firstQuote == -1) return null;

                // Simple loop to skip escape characters (IndexOf might be enough for simple JSON but just to be safe)
                int secondQuote = json.IndexOf('"', firstQuote + 1);

                return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
            }
        }

        // Finds the list in the "key": [ "a", "b", "c" ] structure
        private List<string> GetList(string json, string key)
        {
            List<string> list = new List<string>();
            int index = json.IndexOf(key);
            if (index == -1) return list;

            int start = json.IndexOf('[', index);
            int end = json.IndexOf(']', start);

            if (start == -1 || end == -1) return list;

            string content = json.Substring(start + 1, end - start - 1);

            // Empty list check
            if (string.IsNullOrWhiteSpace(content)) return list;

            // Separate elements by comma, but be careful with commas within strings (Simple solution: split by quotes)
            string[] parts = content.Split('"');

            foreach (string p in parts)
            {
                string clean = p.Trim();
                // Take only actual data (if it's not a comma, whitespace, or :)
                if (clean.Length > 1 && !clean.Contains(","))
                {
                    list.Add(clean);
                }
            }

            return list;
        }
    }
}