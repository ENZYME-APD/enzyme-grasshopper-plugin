import os
import re

components = {
    "AdaptiveTerrainGrader": {
        "file": "Components/AdaptiveTerrainGrader.cs",
        "name": "ADAPTIVE TERRAIN GRADER",
        "how": "Calculates localized cut-and-fill operations by projecting building pads or roads onto the terrain mesh. It adapts the mesh topology to create flat plateaus and sloped retaining embankments.",
        "why": "Essential for calculating earthworks (cut/fill volumes) early in the design phase. It shows how much soil must be moved to accommodate the masterplan, directly impacting project cost and environmental disruption."
    },
    "SlopeAnalysisMesh": {
        "file": "Components/SlopeAnalysisMesh.cs",
        "name": "SLOPE ANALYSIS MESH",
        "how": "Evaluates the normal vector of every mesh face against the global Z-axis to calculate the steepness (in degrees or percentage), mapping the results as a color gradient.",
        "why": "Crucial for identifying buildable vs. non-buildable zones. Helps quickly spot areas too steep for roads (e.g., >15%) or areas flat enough for building pads (e.g., <5%)."
    },
    "SlopeTerrainPlus": {
        "file": "Components/SlopeTerrainPlus.cs",
        "name": "SLOPE TERRAIN PLUS",
        "how": "An advanced version of the slope analyzer that not only maps steepness but also extracts vector arrows pointing downhill for every face.",
        "why": "Combines slope severity with flow direction. Perfect for understanding not just how steep a hill is, but exactly which way the land naturally drains or faces (aspect analysis)."
    },
    "TerrainSections": {
        "file": "Components/TerrainSections.cs",
        "name": "TERRAIN SECTIONS",
        "how": "Slices the 3D terrain mesh using a configurable grid of X and Y planes, extracting both 3D contours 'in-place' and cleanly unrolled 2D flat profiles.",
        "why": "Standard architectural deliverable. Allows designers and engineers to understand the topographic profile across the entire site, which is vital for designing stepped foundations and underground structures."
    },
    "RoadSlopeAnalyzer": {
        "file": "Components/RoadSlopeAnalyzer.cs",
        "name": "ROAD SLOPE ANALYZER",
        "how": "Evaluates curves representing road centerlines against the terrain, calculating the longitudinal slope at discrete intervals along the path.",
        "why": "Ensures road networks comply with accessibility and vehicular safety standards (e.g., keeping grades under 8-10%). Prevents designing impossible infrastructure on steep sites."
    },
    "HeightMapAnalysisMesh": {
        "file": "Components/HeightMapAnalysisMesh.cs",
        "name": "HEIGHT MAP ANALYSIS",
        "how": "Sorts all mesh vertices by their Z-elevation and maps them to a customizable color gradient from the lowest to the highest point.",
        "why": "Provides a quick, intuitive read of the site's macro-topography. Helps in zoning the site (e.g., placing critical infrastructure above the flood plain or historical high-water marks)."
    },
    "MeshHeightAnalysis": {
        "file": "Components/MeshHeightAnalysis.cs",
        "name": "MESH HEIGHT ANALYSIS",
        "how": "Analyzes mesh elevations to generate detailed HUD metrics (average, min, max heights) and identifies localized peaks and valleys.",
        "why": "Provides quantitative tabular data summarizing the site's verticality. Knowing the highest peaks and lowest basins is critical for locating water towers, telecom equipment, or drainage ponds."
    },
    "TerrainGeneratorPro": {
        "file": "Components/TerrainGeneratorPro.cs",
        "name": "TERRAIN GENERATOR PRO",
        "how": "Takes raw input data (points, curves, or GIS contour lines) and triangulates a clean, unified, watertight 3D mesh.",
        "why": "The foundational step for all digital site analysis. It converts messy, disconnected surveyor data into a usable computational surface for grading, water, and slope analysis."
    },
    "ElevationLabel": {
        "file": "Components/ElevationLabel.cs",
        "name": "ELEVATION LABEL",
        "how": "Extracts sample points across the terrain and generates 3D text tags displaying their exact Z-height above sea level.",
        "why": "Turns a purely visual 3D model into a readable engineering drawing. Essential for communicating precise ground levels to contractors and consultants."
    },
    "LegendGeometry": {
        "file": "Components/LegendGeometry.cs",
        "name": "LEGEND GEOMETRY",
        "how": "Reads the domains and color gradients from the analysis components (Slope, Height, Flow) and bakes a scaled 3D legend into the Rhino scene.",
        "why": "Ensures that visual diagrams are scientifically readable. Without a legend, a heatmap is just pretty colors; with it, it becomes an actionable data map."
    },
    "WindEngine": {
        "file": "Components/WindEngine.cs",
        "name": "URBAN WIND ENGINE",
        "how": "Uses basic kinematic simulation to model wind vectors hitting topography and massing, generating deflected vector paths and speed multipliers.",
        "why": "Identifies wind tunnels, sheltered zones, and high-velocity exposure areas. Crucial for designing comfortable pedestrian plazas and optimizing building orientation for natural ventilation."
    },
    "WindEngineHTVer": {
        "file": "Components/WindEngineHTVer.cs",
        "name": "URBAN WIND ENGINE (HIGH-RES)",
        "how": "A higher-resolution version of the wind vector engine, providing denser grid analysis and more accurate deflection around complex urban geometry.",
        "why": "Used in later design stages when exact massing is known, helping to fine-tune facade porosity and outdoor comfort strategies."
    },
    "DataVisualizer": {
        "file": "Components/DataVisualizer.cs",
        "name": "LEAP DATA VISUALIZER",
        "how": "A generic visualization module that takes numerical data streams from LEAP components and maps them to charts, graphs, or colored geometry.",
        "why": "Bridges the gap between raw spreadsheet data and spatial intuition. It allows designers to 'see' abstract ecological metrics directly overlaid on their 3D model."
    }
}

markdown_content = "# Terrain & LEAP Analysis Guide\n\nThis guide covers the remaining components in the Terrain and LEAP toolsets, explaining their underlying mechanics and architectural importance.\n\n"

for key, data in components.items():
    markdown_content += f"## {data['name']}\n"
    markdown_content += f"**How it works:** {data['how']}\n\n"
    markdown_content += f"**Interpretation & Importance:** {data['why']}\n\n---\n\n"

with open("docs/Terrain_and_LEAP_Guide.md", "w") as f:
    f.write(markdown_content)

print("Generated Terrain_and_LEAP_Guide.md")

