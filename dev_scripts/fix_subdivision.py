import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

old_block = """                double length = nCrv.GetLength();
                int divs = Math.Max(2, (int)(length / subDist));
                
                // BULLETPROOF ARC-LENGTH DIVISION: No Chord-Jumping across hairpins
                double[] tParams = new double[divs + 1];
                double t0 = nCrv.Domain.T0;
                double t1 = nCrv.Domain.T1;
                tParams[0] = t0;
                tParams[divs] = t1;
                for (int i = 1; i < divs; i++) {
                    double targetLen = (length * i) / divs;
                    if (nCrv.LengthParameter(targetLen, out double t)) {
                        tParams[i] = t;
                    } else {
                        tParams[i] = t0 + (t1 - t0) * ((double)i / divs);
                    }
                }"""

new_block = """                double length = nCrv.GetLength();
                int divs = Math.Max(2, (int)(length / subDist));
                
                // NATIVE ARC-LENGTH DIVISION: No Chord-Jumping across hairpins, and no domain-interpolation fallback twists
                double[] tParams = nCrv.DivideByCount(divs, false); // false = strictly arc-length
                
                if (tParams == null || tParams.Length < 2) {
                    // Absolute fallback: Rebuild the curve to ensure perfectly uniform parameterization and try again
                    nCrv = nCrv.Rebuild(Math.Max(10, divs), 3, true);
                    tParams = nCrv.DivideByCount(divs, false);
                }
                
                if (tParams == null || tParams.Length < 2) {
                    // Final safety net
                    tParams = new double[divs + 1];
                    for (int i = 0; i <= divs; i++) {
                        tParams[i] = nCrv.Domain.T0 + (nCrv.Domain.T1 - nCrv.Domain.T0) * ((double)i / divs);
                    }
                }"""

content = content.replace(old_block, new_block)

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
