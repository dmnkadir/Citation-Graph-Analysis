using DataStructuresProject;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MakaleGrafAnaliz
{
    public class GrafIslemleri
    {
        // To reach articles quickly by ID (Dictionary)
        public Dictionary<string, Makale> Nodes { get; set; } = new Dictionary<string, Makale>();

        public int TotalNodeCount { get; private set; }
        public int TotalCitationCount { get; private set; } // Total edge count

        // Function that takes data from the list and fits it into graph logic
        public void BuildGraph(List<Makale> rawList)
        {
            Nodes.Clear();
            TotalCitationCount = 0;
            TotalNodeCount = rawList.Count;

            // Add all articles to the dictionary and initialize their lists
            foreach (var article in rawList)
            {
                if (!string.IsNullOrEmpty(article.Id) && !Nodes.ContainsKey(article.Id))
                {
                    // Let's clear/initialize the list we just added here so it doesn't overlap
                    article.CitedBy = new List<string>();
                    article.InDegree = 0; // Reset
                    Nodes.Add(article.Id, article);
                }
            }

            // Establish relationships (Calculate and list citations)
            foreach (var article in rawList)
            {
                // 'article' -> references -> 'refId'
                foreach (var refId in article.ReferencedWorks)
                {
                    // If the referenced article exists in our dataset
                    if (Nodes.ContainsKey(refId))
                    {
                        var citedArticle = Nodes[refId];

                        // Increment InDegree
                        citedArticle.InDegree++;

                        // Record WHO made the citation 
                        if (!citedArticle.CitedBy.Contains(article.Id))
                        {
                            citedArticle.CitedBy.Add(article.Id);
                        }

                        // Increment total edge count
                        TotalCitationCount++;
                    }
                }
            }
        }

        public List<Makale> CalculateKCore(int k)
        {
            // Create Temporary Undirected Graph (To avoid breaking the original)
            // A list that holds the neighbors of each node (Undirected)
            Dictionary<string, HashSet<string>> neighbors = new Dictionary<string, HashSet<string>>();

            // A dictionary that holds the current degree of each node
            Dictionary<string, int> degrees = new Dictionary<string, int>();

            // Add everyone initially
            foreach (var id in Nodes.Keys)
            {
                neighbors[id] = new HashSet<string>();
                degrees[id] = 0;
            }

            // Establish connections (If A->B, add B to A and A to B)
            foreach (var m in Nodes.Values)
            {
                foreach (var refId in m.ReferencedWorks)
                {
                    if (Nodes.ContainsKey(refId))
                    {
                        // Add undirected edge
                        if (!neighbors[m.Id].Contains(refId))
                        {
                            neighbors[m.Id].Add(refId);
                            degrees[m.Id]++;
                        }

                        if (!neighbors[refId].Contains(m.Id))
                        {
                            neighbors[refId].Add(m.Id);
                            degrees[refId]++;
                        }
                    }
                }
            }

            // K-Core Algorithm (Iterative Removal)
            bool hasDeletion = true;
            HashSet<string> removedNodeIds = new HashSet<string>();

            while (hasDeletion)
            {
                hasDeletion = false;
                List<string> nodesToRemove = new List<string>();

                // Find nodes that are currently active (not deleted) and whose degree is less than k
                foreach (var id in Nodes.Keys)
                {
                    if (!removedNodeIds.Contains(id))
                    {
                        if (degrees[id] < k)
                        {
                            nodesToRemove.Add(id);
                        }
                    }
                }

                // Delete found nodes and decrease the degree of their neighbors
                if (nodesToRemove.Count > 0)
                {
                    hasDeletion = true;
                    foreach (var idToRemove in nodesToRemove)
                    {
                        removedNodeIds.Add(idToRemove);

                        // Go to this node's neighbors and say "I'm leaving, decrease your degree by 1"
                        foreach (var neighborId in neighbors[idToRemove])
                        {
                            if (!removedNodeIds.Contains(neighborId))
                            {
                                degrees[neighborId]--;
                            }
                        }
                    }
                }
            }

            // List the remaining nodes
            List<Makale> kCoreList = new List<Makale>();
            foreach (var id in Nodes.Keys)
            {
                if (!removedNodeIds.Contains(id))
                {
                    kCoreList.Add(Nodes[id]);
                }
            }

            return kCoreList;
        }

        // Find the most cited article
        public Makale GetMostCitedArticle()
        {
            if (Nodes.Count == 0) return null;

            // Sort values in dictionary by InDegree, take the largest
            return Nodes.Values.OrderByDescending(m => m.InDegree).FirstOrDefault();
        }

        // Find the article that cites the most
        public Makale GetTopOutDegreeArticle()
        {
            if (Nodes.Count == 0) return null;

            return Nodes.Values.OrderByDescending(m => m.OutDegree).FirstOrDefault();
        }


        public int CalculateHIndex(string ArticleId, out List<Makale> hCoreList)
        {
            hCoreList = new List<Makale>();

            // If ID doesn't exist, return 0.
            if (string.IsNullOrEmpty(ArticleId) || !Nodes.ContainsKey(ArticleId))
                return 0;

            // Find those who cite this article
            List<Makale> citingArticles = new List<Makale>();
            foreach (var m in Nodes.Values)
            {
                if (m.ReferencedWorks.Contains(ArticleId))
                {
                    citingArticles.Add(m);
                }
            }

            // Sort (From most cited to least)
            citingArticles.Sort((a, b) => b.InDegree.CompareTo(a.InDegree));

            // Calculate H-Index
            int hIndex = 0;
            for (int i = 0; i < citingArticles.Count; i++)
            {
                if (citingArticles[i].InDegree >= (i + 1))
                {
                    hIndex = i + 1;
                    hCoreList.Add(citingArticles[i]);
                }
                else
                {
                    break;
                }
            }
            return hIndex;
        }
        public int CalculateHMedian(List<Makale> hCoreList)
        {
            if (hCoreList == null || hCoreList.Count == 0) return 0;

            // Get citation counts (InDegree) and sort from smallest to largest
            List<int> citationCounts = hCoreList.Select(m => m.InDegree).OrderBy(x => x).ToList();

            int Count = citationCounts.Count;
            int medianIndex = Count / 2;

            // If odd number take middle directly, if even take average of middle two (usually integer requested, taking middle)
            if (Count % 2 != 0)
            {
                return citationCounts[medianIndex];
            }
            else
            {
                // If even, average of the middle two
                return (citationCounts[medianIndex - 1] + citationCounts[medianIndex]) / 2;
            }
        }

        public void CalculateBetweennessCentrality()
        {
            // First Create Undirected Graph Structure
            // If A -> B, we will add B to A's list and A to B's list.
            Dictionary<string, List<string>> undirectedNeighbors = new Dictionary<string, List<string>>();

            foreach (var id in Nodes.Keys)
            {
                undirectedNeighbors[id] = new List<string>();
            }

            foreach (var article in Nodes.Values)
            {
                foreach (var refId in article.ReferencedWorks)
                {
                    if (Nodes.ContainsKey(refId))
                    {
                        // Add bi-directionally (To be Undirected)
                        if (!undirectedNeighbors[article.Id].Contains(refId))
                            undirectedNeighbors[article.Id].Add(refId);

                        if (!undirectedNeighbors[refId].Contains(article.Id))
                            undirectedNeighbors[refId].Add(article.Id);
                    }
                }
            }

            // Reset scores
            foreach (var m in Nodes.Values) m.BetweennessScore = 0;

            // Brandes Algorithm (Runs for each node)
            foreach (var s in Nodes.Keys)
            {
                Stack<string> S = new Stack<string>();
                Queue<string> Q = new Queue<string>();
                Dictionary<string, List<string>> P = new Dictionary<string, List<string>>();
                Dictionary<string, int> sigma = new Dictionary<string, int>(); // Shortest path count
                Dictionary<string, int> d = new Dictionary<string, int>();     // Distance
                Dictionary<string, double> delta = new Dictionary<string, double>();

                foreach (var v in Nodes.Keys)
                {
                    P[v] = new List<string>();
                    sigma[v] = 0;
                    d[v] = -1;
                    delta[v] = 0;
                }

                sigma[s] = 1;
                d[s] = 0;
                Q.Enqueue(s);

                while (Q.Count > 0)
                {
                    string v = Q.Dequeue();
                    S.Push(v);

                    foreach (string w in undirectedNeighbors[v])
                    {
                        // if w found for first time
                        if (d[w] < 0)
                        {
                            Q.Enqueue(w);
                            d[w] = d[v] + 1;
                        }

                        // if shortest path goes through v
                        if (d[w] == d[v] + 1)
                        {
                            sigma[w] += sigma[v];
                            P[w].Add(v);
                        }
                    }
                }

                while (S.Count > 0)
                {
                    string w = S.Pop();
                    foreach (string v in P[w])
                    {
                        delta[v] += (double)sigma[v] / sigma[w] * (1.0 + delta[w]);
                    }
                    if (w != s)
                    {
                        Nodes[w].BetweennessScore += delta[w];
                    }
                }
            }

            // Normalization 
            double N = TotalNodeCount;
            double normalizationFactor = (N - 1) * (N - 2);

            if (normalizationFactor > 0)
            {
                foreach (var m in Nodes.Values)
                {
                    // Divide by 2 because it is undirected, then divide by graph size
                    m.BetweennessScore = (m.BetweennessScore / 2.0) / normalizationFactor;
                }
            }
        }

    }


}