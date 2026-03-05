# 📊 Citation Graph Analysis Tool

A sophisticated desktop application built with C# and WPF to model, analyze, and visualize citation relationships between scientific papers using advanced **Graph Theory** methods.

---

## 🚀 Technical Excellence

This project was developed under strict constraints, avoiding external data processing libraries to demonstrate custom algorithmic implementations.

- **Custom JSON Parser:** Developed a stack-based original JSON parser to process raw citation data into memory-resident directed graphs.
- **Optimized Centrality:** Implemented **Brandes' Algorithm** for Betweenness Centrality, achieving $O(VE)$ complexity, which is significantly more efficient than Floyd-Warshall for sparse citation graphs.
- **Structural Resilience:** Features **K-Core (K-Çekirdek) Decomposition** to identify the most robust academic "cores" within the network.
- **Scientific Metrics:** Calculates **H-Index**, **H-Core**, and **H-Median** metrics to quantify individual paper impact.

---

## 🎨 Visualization & Interaction

The application provides an interactive analysis environment using two primary mathematical layout models:

1.  **Spiral Layout:** Positions nodes based on citation density (In-Degree), placing high-impact papers at the geometric center.
2.  **Circular (Polar) Layout:** Distributes nodes along a wide orbit based on Paper IDs, ideal for observing general complexity and edge flow.

**Key Interaction Features:**
- **Dynamic Expansion:** Clicking a node expands its H-Core neighbors, allowing for incremental exploration of citation paths.
- **Smart Navigation:** Interactive pan and zoom features for navigating large-scale networks without UI freezing.

---

## 🛠 Built With
- **Language:** C#
- **UI Framework:** WPF (Windows Presentation Foundation)
- **Algorithms:** Brandes, BFS, K-Core 
