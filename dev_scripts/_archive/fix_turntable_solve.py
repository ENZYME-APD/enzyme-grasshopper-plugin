with open('Components/TurntableCamera.cs', 'r') as f:
    content = f.read()

import re

# We will use regex to replace from timer.Stop() to the end of the method
pattern = re.compile(r'timer\.Stop\(\);.*?DA\.SetData\(3, "TURNTABLE CAMERA ENGINE\\n"', re.DOTALL)

replacement = """timer.Stop();

            if (run && savedFiles.Count > 0)
            {
                _lastExportDate = DateTime.Now.ToString("dd MMM yyyy HH:mm");
                _lastExportDuration = timer.ElapsedMilliseconds.ToString() + " ms";
                _lastExportFrames = savedFiles.Count;
            }

            DA.SetData(0, orbitPath);
            DA.SetDataList(1, camPoints);
            DA.SetData(2, target);
            DA.SetDataList(3, savedFiles);
            
            Message = $"{this.NickName}\\nTime: {_lastExportDuration}\\n---\\nLast: {_lastExportDate}\\nFrames: {_lastExportFrames}";

            DA.SetData(4, "TURNTABLE CAMERA ENGINE\\n" """

content = pattern.sub(replacement, content)

with open('Components/TurntableCamera.cs', 'w') as f:
    f.write(content)
