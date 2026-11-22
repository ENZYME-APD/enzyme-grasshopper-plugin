# Third-party notices

The Enzyme Grasshopper Plugin is licensed under the terms in `LICENSE`. The parts and services below keep their own licences.

## Libraries bundled in the plug-in

| Library | Licence | Source |
|---|---|---|
| Newtonsoft.Json 13 | MIT | https://www.newtonsoft.com/json |
| AnimatedGif 1.0.5 | MIT | https://github.com/mrousavy/AnimatedGif |
| Costura.Fody / Fody (build-time embedding) | MIT | https://github.com/Fody/Costura |

RhinoCommon and Grasshopper are referenced at build time and provided by Rhino (McNeel); they are not redistributed.

## Methods and code adapted from other projects

| Project | Licence | Used for |
|---|---|---|
| Meerkat (T.-T. Tran) | MIT | Contour and band methods (`Utils/FieldMath.cs`), palette list (`Utils/Palettes.cs`), Analysis Heatmap ideas |
| VoxCity (Fujiwara et al. 2025, NUS / Takenaka) | MIT | Porous tree canopy approach (Sun Hours), site data pipeline ideas (Site Fetch) |
| pythermalcomfort (Center for the Built Environment, UC Berkeley) | MIT | UTCI polynomial (Bröde et al. 2012), where ported: see the code comments |
| Open Infra Studio (OpenAEC Foundation), FreeCAD Road | GPL-3.0 | Methods only (no code): road corridor, junctions, Priority-Flood use. Implementations are original. |

Palette colour stops follow public colour maps: matplotlib viridis family (CC0), cividis (Nuñez et al. 2018), Turbo (Google, Apache 2.0), ColorBrewer (Apache 2.0, Cynthia Brewer), cmocean (MIT), seaborn (BSD-3-Clause), Ladybug legend colours (colour values only).

Permission notice for the MIT-licensed works above (MIT licence):

> Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions: The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software. THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND.

## Data services used at run time (Site Fetch)

| Data | Licence / terms | Attribution required |
|---|---|---|
| OpenStreetMap via Overpass API | ODbL 1.0 | Yes: "© OpenStreetMap contributors" on any map, drawing or report |
| Copernicus DEM GLO-30 (ESA / Airbus, AWS Open Data) | Copernicus DEM licence (free use with credit) | Yes |
| ESA WorldCover 2021 v200 | CC BY 4.0 | Yes |

Site Fetch's **Sources** output gives the attribution text to place on outputs.
