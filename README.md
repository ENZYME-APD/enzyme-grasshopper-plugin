# Enzyme Grasshopper Plugin

A comprehensive suite of Grasshopper components developed for Enzyme APD, focusing on masterplanning, procedural geometry generation, site analysis, and ecological metrics.

## Key Features

### 🌍 Site & Terrain Analysis
- **Adaptive Terrain Grader:** Localized cut-and-fill operations.
- **Slope & Height Analyzers:** Gradient heatmaps, downhill vectors, and mesh peak/valley detection.
- **Terrain Colorizer & Generator Pro:** Procedural terrain generation and elevation/slope based colorizing with automatic contour extraction.

### 🛣️ Procedural Infrastructure & Roads
- **Procedural Road Generator:** Dynamically generates road meshes, lanes, and bridge pillars over topography while mathematically calculating high-accuracy zero-crossing cut and fill solids.
- **Road Slope Analyzers (2D & 3D):** Evaluates longitudinal slopes against accessibility thresholds (Degrees, Percentage, Ratio), rendering compliance segments and JSON dashboard exports.
- **Road Profile Unroller:** Unrolls 3D roads into scaled 2D engineering profiles, generating structural alignment bands (Lines, Arcs, Splines) and gradient labels onto customizable reference planes.
- **Divide Target Length by Segment:** Intelligent curve subdivision that strictly respects structural kinks and discontinuities.

### 🍃 LEAP & Microclimate
- **Urban Wind Engines:** Simulates wind vector deflections around topography and massing.
- **Microclimate & Sun Hours:** Tools to calculate direct solar exposure, Universal Thermal Climate Index (UTCI) approximations, and data visualizations for urban comfort.

## Installation

The plugin is automatically built by the GitHub Actions workflow and published to the [Yak](https://files.mcneel.com/yak/) package manager. 

To install:
1. In Rhino, run the command `PackageManager`
2. Search for **Enzyme-Grasshopper-Plugin**
3. Click **Install**
