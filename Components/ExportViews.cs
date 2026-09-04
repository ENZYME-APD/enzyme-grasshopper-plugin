using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Rhino;
using Rhino.DocObjects;

namespace Enzyme.Components
{
    public class ExportViews : GH_Component
    {
        public ExportViews()
          : base("Export Named Views", "ExportViews",
              "Exports Rhino Named Views to image files with custom ViewCapture settings.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run", "R", "Set to true to export the views.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Views", "V", "Names of the views to export. If empty, exports ALL named views.", GH_ParamAccess.list);
            pManager.AddTextParameter("Display Style", "DS", "Optional. Name of the Display Mode (e.g., 'Rendered'). Leaves active if empty.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Layer State", "LS", "Optional. Name of the saved Layer State to restore. Leaves active if empty.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Directory", "Dir", "Folder path to save the images.", GH_ParamAccess.item);
            pManager.AddTextParameter("Prefix", "P", "Prefix for the output filenames (optional).", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Suffix", "Suf", "Suffix for the output filenames (optional).", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Format", "Fmt", "Image format: png, jpg, bmp, tiff.", GH_ParamAccess.item, "png");
            
            pManager.AddIntegerParameter("Width", "W", "Image width in pixels.", GH_ParamAccess.item, 1920);
            pManager.AddIntegerParameter("Height", "H", "Image height in pixels.", GH_ParamAccess.item, 1080);
            pManager.AddNumberParameter("Scale", "S", "Scale multiplier for the final resolution (e.g. 2 for double size).", GH_ParamAccess.item, 1.0);
            pManager.AddIntegerParameter("DPI", "DPI", "Print DPI metadata embedded into the image.", GH_ParamAccess.item, 300);
            
            pManager.AddBooleanParameter("Grid", "G", "Show Grid.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("World Axes", "WA", "Show World Axes.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("CPlane Axes", "CA", "Show CPlane Axes.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Transparent", "T", "Transparent background (PNG only).", GH_ParamAccess.item, false);

            pManager[1].Optional = true; // Views can be empty
            pManager[2].Optional = true; // Display Style
            pManager[3].Optional = true; // Layer State
            pManager[4].Optional = true; // Directory can be empty
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Saved Files", "F", "Paths to the saved image files.", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "I", "Component information and interpretation", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool run = false;
            DA.GetData("Run", ref run);

            List<string> viewNames = new List<string>();
            DA.GetDataList("Views", viewNames);

            string directory = "";
            DA.GetData("Directory", ref directory);

            string prefix = "";
            DA.GetData("Prefix", ref prefix);

            string suffix = "";
            DA.GetData("Suffix", ref suffix);

            string formatStr = "png";
            DA.GetData("Format", ref formatStr);

            int width = 1920;
            DA.GetData("Width", ref width);

            int height = 1080;
            DA.GetData("Height", ref height);

            double scale = 1.0;
            DA.GetData("Scale", ref scale);

            int dpi = 300;
            DA.GetData("DPI", ref dpi);

            bool grid = false;
            DA.GetData("Grid", ref grid);

            bool worldAxes = false;
            DA.GetData("World Axes", ref worldAxes);

            bool cplaneAxes = false;
            DA.GetData("CPlane Axes", ref cplaneAxes);

            bool transparent = false;
            DA.GetData("Transparent", ref transparent);

            string displayStyle = "";
            DA.GetData("Display Style", ref displayStyle);

            string layerState = "";
            DA.GetData("Layer State", ref layerState);

            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;

            List<string> savedFiles = new List<string>();

            if (run)
            {
                if (string.IsNullOrEmpty(directory))
                {
                    directory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
                }

                if (!Directory.Exists(directory))
                {
                    try { Directory.CreateDirectory(directory); }
                    catch { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid directory path."); return; }
                }

                var viewsToExport = new List<ViewInfo>();

                if (viewNames.Count == 0)
                {
                    // Export all named views
                    foreach (var nv in doc.NamedViews)
                    {
                        viewsToExport.Add(nv);
                    }
                }
                else
                {
                    foreach (string name in viewNames)
                    {
                        int index = doc.NamedViews.FindByName(name);
                        if (index >= 0)
                        {
                            viewsToExport.Add(doc.NamedViews[index]);
                        }
                        else
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Named view '{name}' not found.");
                        }
                    }
                }

                if (viewsToExport.Count > 0)
                {
                    var activeView = doc.Views.ActiveView;
                    if (activeView != null)
                    {
                        // Save current view state
                        var originalViewInfo = new ViewInfo(activeView.ActiveViewport);

                        var capture = new Rhino.Display.ViewCapture
                        {
                            Width = (int)(width * scale),
                            Height = (int)(height * scale),
                            TransparentBackground = transparent,
                            DrawGrid = grid,
                            DrawAxes = worldAxes,
                            DrawGridAxes = cplaneAxes
                        };
                        
                        // Handle Display Style
                        var originalDisplayMode = activeView.ActiveViewport.DisplayMode;
                        if (!string.IsNullOrEmpty(displayStyle))
                        {
                            var modes = Rhino.Display.DisplayModeDescription.GetDisplayModes();
                            foreach (var mode in modes)
                            {
                                if (mode.EnglishName.Equals(displayStyle, StringComparison.OrdinalIgnoreCase))
                                {
                                    activeView.ActiveViewport.DisplayMode = mode;
                                    break;
                                }
                            }
                        }

                        // Handle Layer State
                        string tempLayerState = "Enzyme_Temp_" + Guid.NewGuid().ToString();
                        bool layerStateChanged = false;
                        if (!string.IsNullOrEmpty(layerState))
                        {
                            var names = doc.NamedLayerStates.Names;
                            bool found = false;
                            foreach(var n in names)
                            {
                                if (n.Equals(layerState, StringComparison.OrdinalIgnoreCase))
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (found)
                            {
                                doc.NamedLayerStates.Save(tempLayerState);
                                doc.NamedLayerStates.Restore(layerState, Rhino.DocObjects.Tables.RestoreLayerProperties.All);
                                layerStateChanged = true;
                            }
                            else
                            {
                                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Layer State '{layerState}' not found.");
                            }
                        }

                        foreach (var nv in viewsToExport)
                        {
                            // Push the named view to the active viewport
                            activeView.ActiveViewport.PushViewInfo(nv, false);
                            activeView.Redraw();
                            
                            // Let the UI update slightly to ensure redraw
                            RhinoApp.Wait();

                            var bitmap = capture.CaptureToBitmap(activeView);
                            if (bitmap != null)
                            {
                                string safeName = string.Join("_", nv.Name.Split(Path.GetInvalidFileNameChars()));
                                
                                string f = formatStr.ToLower().Trim();
                                System.Drawing.Imaging.ImageFormat imgFormat = System.Drawing.Imaging.ImageFormat.Png;
                                string ext = "png";
                                
                                if (f == "jpg" || f == "jpeg") { imgFormat = System.Drawing.Imaging.ImageFormat.Jpeg; ext = "jpg"; }
                                else if (f == "bmp") { imgFormat = System.Drawing.Imaging.ImageFormat.Bmp; ext = "bmp"; }
                                else if (f == "tif" || f == "tiff") { imgFormat = System.Drawing.Imaging.ImageFormat.Tiff; ext = "tif"; }
                                
                                string pre = string.IsNullOrEmpty(prefix) ? "" : prefix + "_";
                                string suf = string.IsNullOrEmpty(suffix) ? "" : "_" + suffix;
                                
                                string filename = $"{pre}{safeName}{suf}.{ext}";
                                string path = Path.Combine(directory, filename);
                                
                                bitmap.SetResolution(dpi, dpi);
                                bitmap.Save(path, imgFormat);
                                savedFiles.Add(path);
                                bitmap.Dispose();
                            }

                            // Restore view
                            activeView.ActiveViewport.PushViewInfo(originalViewInfo, false);
                        }
                        
                        // Ensure it's fully restored
                        
                        // Revert display mode if changed
                        if (activeView.ActiveViewport.DisplayMode.Id != originalDisplayMode.Id)
                        {
                            activeView.ActiveViewport.DisplayMode = originalDisplayMode;
                        }

                        // Revert layer state if changed
                        if (layerStateChanged)
                        {
                            doc.NamedLayerStates.Restore(tempLayerState, Rhino.DocObjects.Tables.RestoreLayerProperties.All);
                            doc.NamedLayerStates.Delete(tempLayerState);
                        }

                        activeView.Redraw();
                    }
                }
            }

            DA.SetDataList(0, savedFiles);

            string info = 
                "EXPORT NAMED VIEWS\n" +
                "==================\n\n" +
                "HOW IT WORKS:\n" +
                "Hooks into Rhino's native ViewCapture capabilities. By taking a list of view names (or leaving it blank to capture all Named Views), it cycles the active viewport through each camera angle and captures high-res images directly to your specified folder.\n\n" +
                "INTERPRETATION & IMPORTANCE:\n" +
                "Automates the tedious task of exporting presentation images. Ensures that every iteration of your Grasshopper definition can be instantly batch-exported into standard, perfectly aligned viewpoints for reports and presentations.";
            DA.SetData(1, info);

            sw.Stop();
            string viewText = viewNames != null && viewNames.Count > 0 ? (viewNames.Count == 1 ? viewNames[0] : $"{viewNames.Count} Views") : "ALL VIEWS";
            if (!run) viewText = "WAITING...";
            string lsText = string.IsNullOrEmpty(layerState) ? "ACTIVE" : layerState.ToUpper();
            string dsText = string.IsNullOrEmpty(displayStyle) ? "ACTIVE" : displayStyle.ToUpper();
            
            this.Message = $"EXPORT NAMED VIEWS\nTime: {sw.ElapsedMilliseconds} ms\n---\nVIEW: {viewText}\nLAYER: {lsText}\nSTYLE: {dsText}";
        }


        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 0, 210, -180);
                Enzyme.Utils.AutoWireHelper.WireFilePath(this, document, 4, "C:\\", 210, -60);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 12, false, 210, 120);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 13, false, 210, 150);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 14, false, 210, 180);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 15, true, 210, 210);

                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 150, 0, 300, 300);
            }
        }

        public override void AppendAdditionalMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create ALL Value Lists", Menu_AutoCreateAll_Clicked);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create View List", Menu_AutoCreateViewList_Clicked);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Display Style List", Menu_AutoCreateDisplayStyleList_Clicked);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Layer State List", Menu_AutoCreateLayerStateList_Clicked);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Format List", Menu_AutoCreateFormatList_Clicked);
        }

        private void Menu_AutoCreateAll_Clicked(object sender, EventArgs e)
        {
            Menu_AutoCreateViewList_Clicked(sender, e);
            Menu_AutoCreateDisplayStyleList_Clicked(sender, e);
            Menu_AutoCreateLayerStateList_Clicked(sender, e);
            Menu_AutoCreateFormatList_Clicked(sender, e);
        }

        private void Menu_AutoCreateViewList_Clicked(object sender, EventArgs e)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;

            var namedViews = doc.NamedViews;
            if (namedViews.Count == 0)
            {
                RhinoApp.WriteLine("No named views found in the document.");
                return;
            }

            GH_ValueList vl = new GH_ValueList();
            vl.CreateAttributes();
            
            // Position it to the left of this component
            vl.Attributes.Pivot = new PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y - 20);
            
            vl.ListItems.Clear();
            foreach (var nv in namedViews)
            {
                vl.ListItems.Add(new GH_ValueListItem(nv.Name, $"\"{nv.Name}\""));
            }

            OnPingDocument().AddObject(vl, false);

            // Wire it to the "Views" input (Index 1)
            this.Params.Input[1].AddSource(vl);
            vl.ExpireSolution(true);
        }

        private void Menu_AutoCreateDisplayStyleList_Clicked(object sender, EventArgs e)
        {
            var modes = Rhino.Display.DisplayModeDescription.GetDisplayModes();
            if (modes.Length == 0) return;

            GH_ValueList vl = new GH_ValueList();
            vl.CreateAttributes();
            vl.Attributes.Pivot = new PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y - 80);
            vl.ListItems.Clear();

            foreach (var mode in modes)
            {
                vl.ListItems.Add(new GH_ValueListItem(mode.EnglishName, $"\"{mode.EnglishName}\""));
            }

            OnPingDocument().AddObject(vl, false);
            this.Params.Input[2].AddSource(vl);
            vl.ExpireSolution(true);
        }

        private void Menu_AutoCreateLayerStateList_Clicked(object sender, EventArgs e)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            var names = doc.NamedLayerStates.Names;
            if (names.Length == 0)
            {
                RhinoApp.WriteLine("No saved layer states found.");
                return;
            }

            GH_ValueList vl = new GH_ValueList();
            vl.CreateAttributes();
            vl.Attributes.Pivot = new PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y - 50);
            vl.ListItems.Clear();

            foreach (string n in names)
            {
                vl.ListItems.Add(new GH_ValueListItem(n, $"\"{n}\""));
            }

            OnPingDocument().AddObject(vl, false);
            this.Params.Input[3].AddSource(vl);
            vl.ExpireSolution(true);
        }

        private void Menu_AutoCreateFormatList_Clicked(object sender, EventArgs e)
        {
            GH_ValueList vl = new GH_ValueList();
            vl.CreateAttributes();
            vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y + 60);
            vl.ListItems.Clear();

            vl.ListItems.Add(new GH_ValueListItem("PNG", "\"png\""));
            vl.ListItems.Add(new GH_ValueListItem("JPG", "\"jpg\""));
            vl.ListItems.Add(new GH_ValueListItem("BMP", "\"bmp\""));
            vl.ListItems.Add(new GH_ValueListItem("TIFF", "\"tif\""));

            OnPingDocument().AddObject(vl, false);
            this.Params.Input[7].AddSource(vl);
            vl.ExpireSolution(true);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get { return Enzyme.IconLoader.Load("ExportNamedViews.png"); }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("8E5B7C2A-4F9D-4638-9B2E-1D7F5A8C9B3D"); }
        }
    }
}
