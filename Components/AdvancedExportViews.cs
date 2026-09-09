using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Grasshopper.Kernel;
using Rhino;
using Rhino.DocObjects;
using Rhino.Display;

namespace Enzyme.Components
{
    public class AdvancedExportViews : GH_Component
    {
        private bool _isExporting = false;
        private int _stateIndex = 0;
        
        // Stored settings for the state machine
        private List<string> _ghStates = new List<string>();
        private List<string> _viewNames = new List<string>();
        private string _directory;
        private string _prefix;
        private string _suffix;
        private string _formatStr;
        private int _width;
        private int _height;
        private int _dpi;
        private bool _grid;
        private bool _worldAxes;
        private bool _cplaneAxes;
        private bool _transparent;
        private bool _scaleItems;
        private string _displayStyle;
        private string _layerState;
        private string _statusInfo = "Idle";
        
        private List<string> _savedFiles = new List<string>();

        public AdvancedExportViews()
          : base("Advanced Export Views", "AdvExport",
              "An automated state-machine exporter that cycles through GH Canvas States and Named Views.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run", "Run", "Set to true to start the batch export.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Views", "Views", "Names of the views to export. If empty, exports ALL named views.", GH_ParamAccess.list);
            pManager.AddTextParameter("GH States", "GH States", "Optional. List of GH Canvas State names to cycle through. Wire the 'Current GH State' output to your Canvas State Manager.", GH_ParamAccess.list);
            pManager.AddTextParameter("Display Style", "Display Style", "Optional. Name of the Display Mode (e.g., 'Rendered').", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Layer State", "Layer State", "Optional. Name of the saved Layer State to restore.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Directory", "Directory", "Folder path to save the images.", GH_ParamAccess.item);
            pManager.AddTextParameter("Prefix", "Prefix", "Prefix for the output filenames.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Suffix", "Suffix", "Suffix for the output filenames.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Format", "Format", "Image format: png, jpg, bmp, tiff.", GH_ParamAccess.item, "png");
            
            pManager.AddIntegerParameter("Width", "Width", "Image width in pixels.", GH_ParamAccess.item, 1920);
            pManager.AddIntegerParameter("Height", "Height", "Image height in pixels.", GH_ParamAccess.item, 1080);
            pManager.AddIntegerParameter("DPI", "DPI", "Print DPI metadata embedded into the image.", GH_ParamAccess.item, 300);
            
            pManager.AddBooleanParameter("Grid", "Grid", "Show Grid.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("World Axes", "World Axes", "Show World Axes.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("CPlane Axes", "CPlane Axes", "Show CPlane Axes.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Transparent", "Transparent", "Transparent background (PNG only).", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Scale Items", "Scale Items", "Scale line thicknesses and text sizes when capturing at high resolutions.", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Saved Files", "Files", "Paths to the exported image files.", GH_ParamAccess.list);
            pManager.AddTextParameter("Current GH State", "GHState", "Wire this into the 'Load Name' of the Canvas State Manager component.", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "Info", "Component information.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool run = false;
            DA.GetData("Run", ref run);

            if (run && !_isExporting)
            {
                // Initialize State Machine
                _viewNames = new List<string>();
                DA.GetDataList("Views", _viewNames);

                _ghStates = new List<string>();
                DA.GetDataList("GH States", _ghStates);
                if (_ghStates.Count == 0) _ghStates.Add(""); // Add a dummy state so it runs once

                _directory = ""; DA.GetData("Directory", ref _directory);
                _prefix = ""; DA.GetData("Prefix", ref _prefix);
                _suffix = ""; DA.GetData("Suffix", ref _suffix);
                _formatStr = "png"; DA.GetData("Format", ref _formatStr);
                
                _width = 1920; DA.GetData("Width", ref _width);
                _height = 1080; DA.GetData("Height", ref _height);
                _dpi = 300; DA.GetData("DPI", ref _dpi);
                
                _grid = false; DA.GetData("Grid", ref _grid);
                _worldAxes = false; DA.GetData("World Axes", ref _worldAxes);
                _cplaneAxes = false; DA.GetData("CPlane Axes", ref _cplaneAxes);
                _transparent = false; DA.GetData("Transparent", ref _transparent);
                _scaleItems = false; DA.GetData("Scale Items", ref _scaleItems);
                
                _displayStyle = ""; DA.GetData("Display Style", ref _displayStyle);
                _layerState = ""; DA.GetData("Layer State", ref _layerState);

                if (string.IsNullOrEmpty(_directory))
                    _directory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);

                if (!Directory.Exists(_directory))
                {
                    try { Directory.CreateDirectory(_directory); }
                    catch { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid directory path."); return; }
                }

                _isExporting = true;
                _stateIndex = 0;
                _savedFiles.Clear();
                _statusInfo = "Starting Export...";
            }

            if (_isExporting)
            {
                if (_stateIndex < _ghStates.Count)
                {
                    string currentState = _ghStates[_stateIndex];
                    DA.SetData(1, currentState);
                    _statusInfo = $"Exporting State {_stateIndex + 1}/{_ghStates.Count}\n{currentState}";

                    // Safely delay the capture to allow GH Canvas and geometries to fully update
                    System.Threading.Tasks.Task.Delay(500).ContinueWith(t => 
                    {
                        RhinoApp.InvokeOnUiThread(new Action(() => 
                        {
                            CaptureCurrentViews(currentState);
                            
                            _stateIndex++;
                            
                            var doc = OnPingDocument();
                            if (doc != null)
                            {
                                doc.ScheduleSolution(5, d => {
                                    this.ExpireSolution(false);
                                });
                            }
                        }));
                    });
                }
                else
                {
                    // Finished
                    _isExporting = false;
                    _statusInfo = "Export Complete\n" + _savedFiles.Count + " images";
                    DA.SetDataList(0, _savedFiles);
                }
            }
            else
            {
                DA.SetDataList(0, _savedFiles);
                if (_savedFiles.Count == 0) _statusInfo = "Idle";
            }

            string info = 
                "ADVANCED EXPORT VIEWS\n" +
                "=====================\n\n" +
                "HOW IT WORKS:\n" +
                "This component acts as an automated state-machine. It cycles through the provided 'GH States' and 'Views', pausing to let Grasshopper generate the new geometry, and then captures high-res images directly to your specified folder.\n\n" +
                "Wire the 'Current GH State' output to your Canvas State Manager to animate your definitions safely.";
            DA.SetData(2, info);
            
            sw.Stop();
            this.Message = $"ADVANCED EXPORT VIEWS\nTime: {sw.ElapsedMilliseconds} ms\n---\n{_statusInfo}";
        }

        private void CaptureCurrentViews(string ghStateName)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            
            var viewsToExport = new List<ViewInfo>();
            if (_viewNames.Count == 0)
            {
                foreach (var nv in doc.NamedViews) viewsToExport.Add(nv);
            }
            else
            {
                foreach (string name in _viewNames)
                {
                    int index = doc.NamedViews.FindByName(name);
                    if (index >= 0) viewsToExport.Add(doc.NamedViews[index]);
                }
            }

            if (viewsToExport.Count == 0) return;

            var activeView = doc.Views.ActiveView;
            if (activeView == null) return;

            var originalViewInfo = new ViewInfo(activeView.ActiveViewport);
            var originalDisplayMode = activeView.ActiveViewport.DisplayMode;
            string tempLayerState = "Enzyme_Temp_" + Guid.NewGuid().ToString();
            bool layerStateChanged = false;

            var capture = new Rhino.Display.ViewCapture
            {
                Width = _width,
                Height = _height,
                TransparentBackground = _transparent,
                DrawGrid = _grid,
                DrawAxes = _worldAxes,
                DrawGridAxes = _cplaneAxes,
                ScaleScreenItems = _scaleItems
            };

            try
            {
                if (!string.IsNullOrEmpty(_displayStyle))
                {
                    foreach (var mode in Rhino.Display.DisplayModeDescription.GetDisplayModes())
                    {
                        if (mode.EnglishName.Equals(_displayStyle, StringComparison.OrdinalIgnoreCase))
                        {
                            activeView.ActiveViewport.DisplayMode = mode;
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(_layerState))
                {
                    bool found = false;
                    foreach(var n in doc.NamedLayerStates.Names)
                    {
                        if (n.Equals(_layerState, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                    }
                    if (found)
                    {
                        doc.NamedLayerStates.Save(tempLayerState);
                        doc.NamedLayerStates.Restore(_layerState, Rhino.DocObjects.Tables.RestoreLayerProperties.All);
                        layerStateChanged = true;
                    }
                }

                foreach (var nv in viewsToExport)
                {
                    activeView.ActiveViewport.PushViewInfo(nv, false);
                    var bitmap = capture.CaptureToBitmap(activeView);
                    if (bitmap != null)
                    {
                        string safeView = string.Join("_", nv.Name.Split(Path.GetInvalidFileNameChars()));
                        string safeState = string.Join("_", ghStateName.Split(Path.GetInvalidFileNameChars()));
                        
                        string f = _formatStr.ToLower().Trim();
                        System.Drawing.Imaging.ImageFormat imgFormat = System.Drawing.Imaging.ImageFormat.Png;
                        string ext = "png";
                        if (f == "jpg" || f == "jpeg") { imgFormat = System.Drawing.Imaging.ImageFormat.Jpeg; ext = "jpg"; }
                        else if (f == "bmp") { imgFormat = System.Drawing.Imaging.ImageFormat.Bmp; ext = "bmp"; }
                        else if (f == "tif" || f == "tiff") { imgFormat = System.Drawing.Imaging.ImageFormat.Tiff; ext = "tif"; }
                        
                        string pre = string.IsNullOrEmpty(_prefix) ? "" : _prefix + "_";
                        string suf = string.IsNullOrEmpty(_suffix) ? "" : "_" + _suffix;
                        string statePart = string.IsNullOrEmpty(safeState) ? "" : safeState + "_";
                        
                        string filename = $"{pre}{statePart}{safeView}{suf}.{ext}";
                        string path = Path.Combine(_directory, filename);
                        
                        bitmap.SetResolution(_dpi, _dpi);
                        bitmap.Save(path, imgFormat);
                        _savedFiles.Add(path);
                        bitmap.Dispose();
                    }
                    activeView.ActiveViewport.PushViewInfo(originalViewInfo, false);
                }
            }
            finally
            {
                activeView.ActiveViewport.PushViewInfo(originalViewInfo, false);
                if (activeView.ActiveViewport.DisplayMode.Id != originalDisplayMode.Id)
                {
                    activeView.ActiveViewport.DisplayMode = originalDisplayMode;
                }
                if (layerStateChanged)
                {
                    doc.NamedLayerStates.Restore(tempLayerState, Rhino.DocObjects.Tables.RestoreLayerProperties.All);
                    doc.NamedLayerStates.Delete(tempLayerState);
                }
                activeView.Redraw();
            }
        }

        public override void AppendAdditionalMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Grasshopper.Kernel.GH_DocumentObject.Menu_AppendItem(menu, "Add Default Controls", Menu_AddDefaultControls_Clicked);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            Grasshopper.Kernel.GH_DocumentObject.Menu_AppendItem(menu, "Auto-create ALL Value Lists", Menu_AutoCreateAll_Clicked);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            Grasshopper.Kernel.GH_DocumentObject.Menu_AppendItem(menu, "Auto-create View List", Menu_AutoCreateViewList_Clicked);
            Grasshopper.Kernel.GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Display Style List", Menu_AutoCreateDisplayStyleList_Clicked);
            Grasshopper.Kernel.GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Layer State List", Menu_AutoCreateLayerStateList_Clicked);
            Grasshopper.Kernel.GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Format List", Menu_AutoCreateFormatList_Clicked);
        }

        private void Menu_AddDefaultControls_Clicked(object sender, EventArgs e)
        {
            var document = OnPingDocument();
            if (document == null) return;
            
            if (this.Params.Input[0].SourceCount == 0)
            {
                var btn = new Grasshopper.Kernel.Special.GH_ButtonObject();
                btn.CreateAttributes();
                btn.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 170, this.Attributes.Pivot.Y - 162);
                document.AddObject(btn, false);
                this.Params.Input[0].AddSource(btn);
            }
            
            if (this.Params.Input[5].SourceCount == 0)
            {
                var pathParam = new Grasshopper.Kernel.Parameters.Param_FilePath();
                pathParam.CreateAttributes();
                pathParam.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 140, this.Attributes.Pivot.Y - 50);
                document.AddObject(pathParam, false);
                this.Params.Input[5].AddSource(pathParam);
            }

            if (this.Params.Input[9].SourceCount == 0 && this.Params.Input[10].SourceCount == 0)
            {
                var doc2px = new PaperSizeToPixels();
                doc2px.CreateAttributes();
                doc2px.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 224, this.Attributes.Pivot.Y + 51);
                document.AddObject(doc2px, false);
                
                this.Params.Input[9].AddSource(doc2px.Params.Output[0]);
                this.Params.Input[10].AddSource(doc2px.Params.Output[1]);
                this.Params.Input[11].AddSource(doc2px.Params.Output[2]);
                
                Enzyme.Utils.AutoWireHelper.WireValueList(doc2px, document, 0,
                    new string[] { "A4", "A3", "A2", "A1", "A0", "A5", "16:9 FHD", "16:9 QHD", "16:9 4K", "1:1 Instagram" },
                    new string[] { "\"A4\"", "\"A3\"", "\"A2\"", "\"A1\"", "\"A0\"", "\"A5\"", "\"16:9 FHD\"", "\"16:9 QHD\"", "\"16:9 4K\"", "\"1:1 Instagram\"" },
                    102, -20);
                    
                Enzyme.Utils.AutoWireHelper.WireBooleanToggle(doc2px, document, 1, true, 106, 10);
            }
            
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, document, 12, false, 265, 101);
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, document, 13, false, 265, 131);
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, document, 14, false, 265, 161);
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, document, 15, true, 265, 191);
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, document, 16, false, 265, 221);

            document.ExpireSolution();
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
            var doc = Rhino.RhinoDoc.ActiveDoc;
            if (doc == null) return;

            var namedViews = doc.NamedViews;
            if (namedViews.Count == 0)
            {
                Rhino.RhinoApp.WriteLine("No named views found in the document.");
                return;
            }

            Grasshopper.Kernel.Special.GH_ValueList vl = new Grasshopper.Kernel.Special.GH_ValueList();
            vl.CreateAttributes();
            vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y - 20);
            vl.ListItems.Clear();
            foreach (var nv in namedViews)
            {
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem(nv.Name, $"\"{nv.Name}\""));
            }

            OnPingDocument().AddObject(vl, false);
            this.Params.Input[1].AddSource(vl);
            vl.ExpireSolution(true);
        }

        private void Menu_AutoCreateDisplayStyleList_Clicked(object sender, EventArgs e)
        {
            var modes = Rhino.Display.DisplayModeDescription.GetDisplayModes();
            if (modes.Length == 0) return;

            Grasshopper.Kernel.Special.GH_ValueList vl = new Grasshopper.Kernel.Special.GH_ValueList();
            vl.CreateAttributes();
            vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y - 80);
            vl.ListItems.Clear();

            foreach (var mode in modes)
            {
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem(mode.EnglishName, $"\"{mode.EnglishName}\""));
            }

            OnPingDocument().AddObject(vl, false);
            this.Params.Input[3].AddSource(vl);
            vl.ExpireSolution(true);
        }

        private void Menu_AutoCreateLayerStateList_Clicked(object sender, EventArgs e)
        {
            var doc = Rhino.RhinoDoc.ActiveDoc;
            if (doc == null) return;
            var names = doc.NamedLayerStates.Names;
            if (names.Length == 0)
            {
                Rhino.RhinoApp.WriteLine("No saved layer states found.");
                return;
            }

            Grasshopper.Kernel.Special.GH_ValueList vl = new Grasshopper.Kernel.Special.GH_ValueList();
            vl.CreateAttributes();
            vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y - 50);
            vl.ListItems.Clear();

            foreach (string n in names)
            {
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem(n, $"\"{n}\""));
            }

            OnPingDocument().AddObject(vl, false);
            this.Params.Input[4].AddSource(vl);
            vl.ExpireSolution(true);
        }

        private void Menu_AutoCreateFormatList_Clicked(object sender, EventArgs e)
        {
            Grasshopper.Kernel.Special.GH_ValueList vl = new Grasshopper.Kernel.Special.GH_ValueList();
            vl.CreateAttributes();
            vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y + 60);
            vl.ListItems.Clear();

            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("PNG", "\"png\""));
            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("JPG", "\"jpg\""));
            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("BMP", "\"bmp\""));
            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("TIFF", "\"tif\""));

            OnPingDocument().AddObject(vl, false);
            this.Params.Input[8].AddSource(vl);
            vl.ExpireSolution(true);
        }

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("AdvancedExportViews.png");

        public override Guid ComponentGuid => new Guid("B5D8F0B2-1C2A-4F8A-8C3D-7E8E9B1A2C3D");
    }
}
