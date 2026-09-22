with open('Components/TurntableCamera.cs', 'r') as f:
    content = f.read()

orig = """                            // Important: force update viewport
                            activeView.Redraw();
                            RhinoApp.Wait(); // Pump messages so gh previews and viewport actually refresh"""

new = """                            // Important: force update viewport
                            activeView.Redraw();
                            // RhinoApp.Wait() is intentionally removed here. Pumping messages during a Grasshopper
                            // SolveInstance causes extreme deadlocks and re-entrancy issues if the user clicks the canvas."""

content = content.replace(orig, new)

with open('Components/TurntableCamera.cs', 'w') as f:
    f.write(content)
