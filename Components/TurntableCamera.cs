using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Rhino.Display;
using Grasshopper.Kernel;

namespace Enzyme.Components
{
    public class TurntableCamera : GH_Component
    {
        private string _lastExportDate = "Never";
        private string _lastExportDuration = "-";
        private int _lastExportFrames = 0;

        public TurntableCamera()
          : base("Turntable Camera", "Turntable",
              "Automated camera rotation and sequence rendering for 360° product/site views.",
              "Enzyme", "Export")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Target", "Target", "Focal point for camera center axis", GH_ParamAccess.item, Point3d.Origin);
            pManager.AddNumberParameter("Radius", "Radius", "Orbital distance to camera location", GH_ParamAccess.item, 100.0);
            pManager.AddNumberParameter("Elevation", "Elevation", "Z-axis elevation offset of camera relative to target", GH_ParamAccess.item, 50.0);
            pManager.AddIntegerParameter("Frames", "Frames", "Number of frame captures per 360 loop", GH_ParamAccess.item, 36);
            pManager.AddTextParameter("Directory", "Dir", "Output directory where images are saved", GH_ParamAccess.item);
            pManager.AddTextParameter("Prefix", "Prefix", "Prefix for the image files", GH_ParamAccess.item, "Turntable_");
            pManager.AddIntegerParameter("Width", "Width", "Export width in pixels", GH_ParamAccess.item, 1920);
            pManager.AddIntegerParameter("Height", "Height", "Export height in pixels", GH_ParamAccess.item, 1080);
            pManager.AddBooleanParameter("Transparent", "Trans", "Export with a transparent background", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Scale Items", "ScaleItems", "Scale lineweights and screen items", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Run", "Run", "Trigger the export loop", GH_ParamAccess.item, false);

            pManager[4].Optional = true; // Directory
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Orbit Path", "Orbit", "The circular path of the camera.", GH_ParamAccess.item);
            pManager.AddPointParameter("Camera Points", "Pts", "Calculated 3D camera coordinates.", GH_ParamAccess.list);
            pManager.AddPointParameter("Target Out", "Target", "Passthrough of the focal target point.", GH_ParamAccess.item);
            pManager.AddTextParameter("File Paths", "Files", "Paths of exported PNG frames.", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "Info", "Component execution HUD.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Point3d target = Point3d.Origin;
            double radius = 100.0;
            double elevation = 50.0;
            int frames = 36;
            string dir = "";
            string prefix = "Turntable_";
            int width = 1920;
            int height = 1080;
            int dpi = 300;
            bool transparent = false;
            bool scaleItems = false;
            bool run = false;

            DA.GetData("Target", ref target);
            DA.GetData("Radius", ref radius);
            DA.GetData("Elevation", ref elevation);
            DA.GetData("Frames", ref frames);
            DA.GetData("Directory", ref dir);
            DA.GetData("Prefix", ref prefix);
            DA.GetData("Width", ref width);
            DA.GetData("Height", ref height);
            DA.GetData("DPI", ref dpi);
            DA.GetData("Transparent", ref transparent);
            DA.GetData("Scale Items", ref scaleItems);
            DA.GetData("Run", ref run);

            Stopwatch timer = Stopwatch.StartNew();

            if (frames < 1) frames = 36;
            if (Math.Abs(radius) < 1e-6) radius = 100.0;

            List<Point3d> camPoints = new List<Point3d>();
            List<string> savedFiles = new List<string>();

            double stepAngle = (2.0 * Math.PI) / frames;
            for (int i = 0; i < frames; i++)
            {
                double angle = i * stepAngle;
                double x = target.X + radius * Math.Cos(angle);
                double y = target.Y + radius * Math.Sin(angle);
                double z = target.Z + elevation;
                camPoints.Add(new Point3d(x, y, z));
            }

            Polyline pline = new Polyline(camPoints);
            pline.Add(camPoints[0]); // close it
            Curve orbitPath = pline.ToNurbsCurve();

            if (run && !string.IsNullOrWhiteSpace(dir))
            {
                if (!Directory.Exists(dir))
                {
                    try { Directory.CreateDirectory(dir); } catch { }
                }

                if (Directory.Exists(dir))
                {
                    RhinoView activeView = RhinoDoc.ActiveDoc.Views.ActiveView;
                    if (activeView != null)
                    {
                        var originalCam = new Rhino.DocObjects.ViewInfo(activeView.ActiveViewport);

                        var capture = new Rhino.Display.ViewCapture
                        {
                            Width = width,
                            Height = height,
                            TransparentBackground = transparent,
                            DrawGrid = false,
                            DrawAxes = false,
                            DrawGridAxes = false,
                            ScaleScreenItems = scaleItems
                        };

                        for (int i = 0; i < camPoints.Count; i++)
                        {
                            Point3d camLoc = camPoints[i];
                            Vector3d dirVec = target - camLoc;

                            activeView.ActiveViewport.SetCameraLocation(camLoc, false);
                            activeView.ActiveViewport.SetCameraDirection(dirVec, false);
                            activeView.ActiveViewport.SetCameraTarget(target, false);
                            
                            // Important: force update viewport
                            activeView.Redraw();
                            // RhinoApp.Wait() is intentionally removed here. Pumping messages during a Grasshopper
                            // SolveInstance causes extreme deadlocks and re-entrancy issues if the user clicks the canvas.

                            using (Bitmap bmp = capture.CaptureToBitmap(activeView))
                            {
                                if (bmp != null)
                                {
                                    bmp.SetResolution(dpi, dpi);
                                    string fileName = $"{prefix}{i:D4}.png";
                                    string fullPath = Path.Combine(dir, fileName);
                                    bmp.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png);
                                    savedFiles.Add(fullPath);
                                }
                            }
                        }

                        // Restore original camera
                        activeView.ActiveViewport.PushViewInfo(originalCam, false);
                        activeView.Redraw();
                    }
                }
            }

            timer.Stop();

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
            
            Message = $"{this.NickName}\nTime: {_lastExportDuration}\n---\nLast: {_lastExportDate}\nFrames: {_lastExportFrames}";

            DA.SetData(4, "TURNTABLE CAMERA ENGINE\n"
                + "\n"
                + "METHODOLOGY:\n"
                + "Calculates an orbital path around the target point and locks the active Rhino viewport camera to each step. Uses Enzyme's Advanced Export Engine settings to dump high-resolution imagery per frame.\n\n"
                + "INTERPRETATION & IMPORTANCE:\n"
                + "Perfect for architectural and product presentations, creating seamless rotating GIFs or video sequences of your Grasshopper geometry in its final display state.");
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                AutoWireDefaultInputs();
            }
        }

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Auto-wire Default Inputs", (s, e) => AutoWireDefaultInputs());
        }

        private void AutoWireDefaultInputs()
        {
            GH_Document doc = OnPingDocument();
            if (doc == null) return;

            Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, doc, 1, 0.1, 500.0, 100.0, 200, -100);
            Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, doc, 2, 0.0, 500.0, 50.0, 200, -80);
            Enzyme.Utils.AutoWireHelper.WireSliderInt(this, doc, 3, 1, 360, 36, 200, -60);
            Enzyme.Utils.AutoWireHelper.WirePanel(this, doc, 4, "C:\\Turntable", 200, -40, 150, 20);
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, doc, 9, false, 200, 40);
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, doc, 10, false, 200, 60);
            Enzyme.Utils.AutoWireHelper.WireButton(this, doc, 11, 200, 80);
        }

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("TurntableCamera.png"); // Uses fallback if missing

        public override Guid ComponentGuid => new Guid("4A9812DC-F41C-4B9A-A3E5-D9814C12D55B");
    }
}