using System;
using System.Collections.Generic;

namespace DataStructuresProject
{
    public class Makale
    {
        // Data coming from JSON 
        public string Id { get; set; }
        public string Doi { get; set; }
        public string Title { get; set; }
        public int Year { get; set; }
        public string Venue { get; set; }
        public int InJsonReferenceCount { get; set; }
        public List<string> CitedBy { get; set; } = new List<string>();

        // Initializing lists in the Constructor to prevent null values
        public List<string> Authors { get; set; } = new List<string>();
        public List<string> Keywords { get; set; } = new List<string>();
        public List<string> ReferencedWorks { get; set; } = new List<string>();

        // Fields to be calculated later for Graph Analysis
        public int InDegree { get; set; }  // How many papers cited us?
        public int OutDegree { get; set; } // How many papers did we cite?

        public double BetweennessScore { get; set; } // Betweenness Centrality Score

        // Short ID for visualization purposes 
        public string ShortId
        {
            get
            {
                if (string.IsNullOrEmpty(Id)) return "N/A";
                int lastSlash = Id.LastIndexOf('/');
                return lastSlash > -1 ? Id.Substring(lastSlash + 1) : Id;
            }
        }

        public override string ToString()
        {
            return $"{ShortId} - {Title} ({Year})";
        }

        public string AuthorsText
        {
            get
            {
                if (Authors == null || Authors.Count == 0) return "Unknown";
                return string.Join(", ", Authors);
            }
        }
    }
}