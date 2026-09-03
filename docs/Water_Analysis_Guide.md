# Water Analysis Guide

This guide explains how the different water analysis components across the **Terrain** and **LEAP** subcategories work, how they overlap, and why each is important.

## 1. The Terrain Subcategory (Physical Simulation & Visualization)
The components in this category are designed around **kinematic physical simulation**. They visually simulate how water behaves on the surface and are fantastic for architectural presentation and visual mapping.

### Raindrop Flow Engine
* **How it works:** Generates a grid of points above your site, drops a "particle" at each point, and traces its exact physical path downhill along the mesh faces until it hits a flat area or the edge.
* **Interpretation & Importance:** Gives you raw, continuous curves representing the "journey" of the water. Highly intuitive and visually striking for diagrams.

### Flow Accumulation Heatmap
* **How it works:** Acts as the translator for the Raindrop Engine. Takes the "spaghetti" of overlapping curves and counts exactly how many times a raindrop passed over every single mesh vertex, outputting a color-coded surface and raw numbers.
* **Interpretation & Importance:** Translates visual paths into quantifiable data. Tells you exactly *how severe* the drainage is at any given point, making it easy to spot where swales or culverts are necessary.

### Global Flood Engine
* **How it works:** Simulates *ponding* (accumulation volume). You input a rain intensity and duration, and the engine calculates how much water falls on the site and fills local depressions, outputting exact water depths.
* **Interpretation & Importance:** Essential for flood risk assessment. Reveals trapped water areas, calculates retention pond volumes, and shows submerged regions during storms.

---

## 2. The LEAP Subcategory (Scientific & Ecological Analysis)
LEAP (Landscape & Ecological Advanced Planning) contains tools that use **strict topological and GIS-standard mathematics**, analyzing slope relationships of the terrain graph.

### Hydro-DEM Engine
* **How it works:** Uses standard GIS algorithms (like the D8 flow direction model). Evaluates every vertex against its neighbors to calculate flow direction and accumulation, automatically extracting a strict, connected vector "stream network".
* **Interpretation & Importance:** The scientific, industry-standard approach to hydrology. Yields perfectly connected, single-line stream segments rather than overlapping curves.

### Keypoint & Keyline Engines
* **How it works:** Reads streams (from Hydro-DEM) to find the "Keypoint"—the exact inflection point where a valley slope shifts from steep to flat. The Keyline Engine then uses that point to generate perfectly offset plowing/swale lines across the topography.
* **Interpretation & Importance:** A specialized ecological design workflow (Keyline Design). Used in regenerative agriculture and masterplanning to passively manage water—slowing it down, spreading it from wet valleys to dry ridges, and reducing erosion.

## Complementary vs. Duplicated Information

**Where they duplicate:** 
The `Raindrop Flow + Heatmap` (Terrain) and the `Hydro-DEM` (LEAP) achieve the exact same end goal: identifying streams and accumulation zones. 

**Why you need both (How they complement):**
* **Aesthetic vs. Scientific:** Use the **Terrain Raindrop** tools for beautiful, intuitive diagrams of water movement. Use the **LEAP Hydro-DEM** when you need strict, singular stream centerlines for mathematical analysis.
* **Journey vs. Destination:** Use the Flow/Hydro tools to figure out how to direct water (where to dig trenches). Use the **Flood Engine** to figure out what happens when the water stops moving (where to place buildings so they don't flood).
