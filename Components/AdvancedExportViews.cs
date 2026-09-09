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
        private string _displayStyle;
        private string _layerState;
        
        private List<string> _savedFiles = new List<string>();

        public AdvancedExportViews()
          : base("Advanced Export Views", "AdvExport",
              "An automated state-machine exporter that cycles through GH Canvas States and Named Views.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run", "R", "Set to true to start the batch export.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Views", "V", "Names of the views to export. If empty, exports ALL named views.", GH_ParamAccess.list);
            pManager.AddTextParameter("GH States", "GHS", "Optional. List of GH Canvas State names to cycle through. Wire the 'Current GH State' output to your Canvas State Manager.", GH_ParamAccess.list);
            pManager.AddTextParameter("Display Style", "DS", "Optional. Name of the Display Mode (e.g., 'Rendered').", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Layer State", "LS", "Optional. Name of the saved Layer State to restore.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Directory", "Dir", "Folder path to save the images.", GH_ParamAccess.item);
            pManager.AddTextParameter("Prefix", "P", "Prefix for the output filenames.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Suffix", "Suf", "Suffix for the output filenames.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Format", "Fmt", "Image format: png, jpg, bmp, tiff.", GH_ParamAccess.item, "png");
            
            pManager.AddIntegerParameter("Width", "W", "Image width in pixels.", GH_ParamAccess.item, 1920);
            pManager.AddIntegerParameter("Height", "H", "Image height in pixels.", GH_ParamAccess.item, 1080);
            pManager.AddIntegerParameter("DPI", "DPI", "Print DPI metadata embedded into the image.", GH_ParamAccess.item, 300);
            
            pManager.AddBooleanParameter("Grid", "G", "Show Grid.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("World Axes", "WA", "Show World Axes.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("CPlane Axes", "CA", "Show CPlane Axes.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Transparent", "T", "Transparent background (PNG only).", GH_ParamAccess.item, false);

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
                Message = "Starting Export...";
            }

            if (_isExporting)
            {
                if (_stateIndex < _ghStates.Count)
                {
                    string currentState = _ghStates[_stateIndex];
                    DA.SetData(1, currentState);
                    Message = $"Exporting State {_stateIndex + 1}/{_ghStates.Count}\n{currentState}";

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
                    Message = "Export Complete\n" + _savedFiles.Count + " images";
                    DA.SetDataList(0, _savedFiles);
                }
            }
            else
            {
                DA.SetDataList(0, _savedFiles);
                Message = "Idle";
            }

            string info = 
                "ADVANCED EXPORT VIEWS\n" +
                "=====================\n\n" +
                "HOW IT WORKS:\n" +
                "This component acts as an automated state-machine. It cycles through the provided 'GH States' and 'Views', pausing to let Grasshopper generate the new geometry, and then captures high-res images directly to your specified folder.\n\n" +
                "Wire the 'Current GH State' output to your Canvas State Manager to animate your definitions safely.";
            DA.SetData(2, info);
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
                DrawGridAxes = _cplaneAxes
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

        protected override Bitmap Icon => Enzyme.IconLoader.Load("Export Views.png");

        public override Guid ComponentGuid => new Guid("B5D8F0B2-1C2A-4F8A-8C3D-7E8E9B1A2C3D");
    }
}
