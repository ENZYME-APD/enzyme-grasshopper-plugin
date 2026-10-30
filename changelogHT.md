# Changelog — HT

Running log of changes made on `main` going forward. Intended to eventually be folded into `DEVELOPMENT_GUIDE.md` / `docs/` once reviewed.

## Unreleased

**Analysis visualisation (ideas adapted from Meerkat, T.-T. Tran, MIT):**
- New **Analysis Heatmap**: one visualiser for any analysis mesh (CFD, noise, sun hours, comfort). Cells / Smooth / Contour / Classes modes; exact contour bands that line up with the isolines; masked smoothing (buildings excluded); round-number levels; isoline value labels that never overlap; peak labels; % area per band (`BandStats`) for reports; UTCI, Lawson (LDDC) and noise class schemes; geometry cached so label tweaks are instant.
- New **Analysis Difference**: option A − B on a diverging scale centred on zero, area-weighted % higher / lower / unchanged, and % area above a level in each option.
- New **Point Probe**: values at named points, interpolated in the face, several result sets at once, CSV table.
- New **Color Palette**: 32 palettes incl. colour-blind-safe Viridis / Cividis, Turbo and diverging sets; shared by all new components (`Utils/Palettes.cs`).
- **Streamlines** upgraded: regular grids (Fast CFD) are read directly with bilinear interpolation instead of a radius search per step; 4th-order Runge-Kutta steps; trace both ways from each seed; lines stop at calm zones, walls and closed loops; new outputs: speeds per vertex, speed-coloured tube mesh, legend JSON. Placed components keep their old ports and behaviour.
- **Legend Geometry**: gradients now use every colour stop (was only the first two); contour/class legends label every band.
- **Analysis Dashboard**: contour legends show the range of each band.
- `Tests/FieldMath.Tests`: Rhino-free tests for the contour / isoline / tracing maths, run in CI.
