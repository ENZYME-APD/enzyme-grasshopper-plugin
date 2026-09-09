using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Rhino.Display;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class AnalysisDashboard : GH_Component
    {
        private bool _run;
        private string _jsonPayload = "";
        private double _size;
        private int _anchor;
        private string _fontFace;
        private Color _fontColor;
        private double _transparency;
        private double _ox;
        private double _oy;
        private double _fitPadding;

        private bool _subscribed = false;
        
        public class DashMetric {
            public string Name;
            public string Value;
        }

        // Parsed Data
        private string _title = "";
        private string _legendType = "";
        private List<Color> _colors = new List<Color>();
        private List<string> _labels = new List<string>();
        private List<DashMetric> _metrics = new List<DashMetric>();

        public AnalysisDashboard()
          : base("Analysis Dashboard", "AnaDash",
              "Reads a JSON payload from Terrain/LEAP analysis tools and renders a responsive Legend HUD.",
              "Enzyme", "Terrain")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run", "run", "Enable HUD", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Dashboard Data", "Dashboard Data", "JSON string representing the analysis data or legend", GH_ParamAccess.item);
            pManager.AddNumberParameter("Size", "size", "Base text size", GH_ParamAccess.item, 12.0);
            pManager.AddIntegerParameter("Anchor", "anchor", "0=TL, 1=TR, 2=BL, 3=BR", GH_ParamAccess.item, 0);
            pManager.AddTextParameter("Font", "font", "Font family name", GH_ParamAccess.item, "Arial");
            pManager.AddColourParameter("Font Color", "color", "Text color", GH_ParamAccess.item, Color.White);
            pManager.AddNumberParameter("Transparency", "alpha", "HUD background opacity (0-1)", GH_ParamAccess.item, 0.85);
            pManager.AddNumberParameter("Offset X", "offX", "X margin from screen edge", GH_ParamAccess.item, 20.0);
            pManager.AddNumberParameter("Offset Y", "offY", "Y margin from screen edge", GH_ParamAccess.item, 20.0);
            pManager.AddNumberParameter("Padding", "pad", "Padding inside the HUD box", GH_ParamAccess.item, 10.0);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // No outputs, purely a visual HUD
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, document, 0, false, 200, -90);
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 2, 8.0, 32.0, 12.0, 200, -30);
                Enzyme.Utils.AutoWireHelper.WireValueList(this, document, 3, 
                    new string[] { "TL", "TR", "BL", "BR" }, 
                    new string[] { "0", "1", "2", "3" }, 200, 0);
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 6, 0.0, 1.0, 0.8, 200, 30);
                Enzyme.Utils.AutoWireHelper.WireSliderInt(this, document, 7, 0, 100, 20, 200, 60);
                Enzyme.Utils.AutoWireHelper.WireSliderInt(this, document, 8, 0, 100, 20, 200, 90);
                Enzyme.Utils.AutoWireHelper.WireSliderInt(this, document, 9, 0, 50, 10, 200, 120);
            }
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            _run = false;
            DA.GetData(0, ref _run);

            if (!DA.GetData(1, ref _jsonPayload)) _jsonPayload = "";

            _size = 12.0; DA.GetData(2, ref _size);
            _anchor = 0; DA.GetData(3, ref _anchor);
            _fontFace = "Arial"; DA.GetData(4, ref _fontFace);
            _fontColor = Color.White; DA.GetData(5, ref _fontColor);
            _transparency = 0.85; DA.GetData(6, ref _transparency);
            _ox = 20.0; DA.GetData(7, ref _ox);
            _oy = 20.0; DA.GetData(8, ref _oy);
            _fitPadding = 10.0; DA.GetData(9, ref _fitPadding);

            if (_run && !string.IsNullOrWhiteSpace(_jsonPayload))
            {
                try
                {
                    JObject parsed = JObject.Parse(_jsonPayload);
                    _legendType = parsed["Type"]?.ToString() ?? "Discrete";
                    _title = parsed["Title"]?.ToString() ?? "ANALYSIS";
                    
                    _colors.Clear();
                    var jcolors = parsed["Colors"] as JArray;
                    if (jcolors != null)
                    {
                        foreach (var token in jcolors)
                        {
                            _colors.Add(Color.FromArgb(
                                255,
                                token["R"]?.Value<int>() ?? 0,
                                token["G"]?.Value<int>() ?? 0,
                                token["B"]?.Value<int>() ?? 0
                            ));
                        }
                    }

                    _labels.Clear();
                    var jlabels = parsed["Labels"] as JArray;
                    if (jlabels != null)
                    {
                        foreach (var token in jlabels)
                            _labels.Add(token.ToString());
                    }

                    _metrics.Clear();
                    var jmetrics = parsed["Metrics"] as JArray;
                    if (jmetrics != null)
                    {
                        foreach (var token in jmetrics)
                        {
                            _metrics.Add(new DashMetric {
                                Name = token["Name"]?.ToString() ?? "",
                                Value = token["Value"]?.ToString() ?? ""
                            });
                        }
                    }

                    if (!_subscribed)
                    {
                        DisplayPipeline.DrawForeground += OnDrawForeground;
                        _subscribed = true;
                    }

                    this.Message = "HUD ACTIVE\n" + _title;
                }
                catch (Exception ex)
                {
                    this.Message = "JSON Error: " + ex.Message;
                    Unsubscribe();
                }
            }
            else
            {
                if (!_run)
                    this.Message = "STATE: OFF";
                else
                    this.Message = "WAITING FOR DATA";
                
                Unsubscribe();
            }
        }

        private void OnDrawForeground(object sender, DrawEventArgs e)
        {
            if (!_run || _colors.Count == 0) return;

            int vpW = e.Viewport.Bounds.Width;
            int vpH = e.Viewport.Bounds.Height;
            double padding = _fitPadding;
            
            double colorBoxW = _size * 2.0;
            double colorBoxH = _legendType == "Gradient" ? (_size * 1.0) : (_size * 1.5);
            double gapY = _legendType == "Gradient" ? 0.0 : (_size * 0.3);
            double textOffsetX = colorBoxW + _size * 0.5;

            int maxChars = _title.Length;
            foreach(var l in _labels) if(l.Length > maxChars) maxChars = l.Length;
            foreach(var m in _metrics) {
                int mLen = m.Name.Length + m.Value.Length + 4;
                if (mLen > maxChars) maxChars = mLen;
            }
            
            double totalTextW = maxChars * _size * 0.65;
            double contentW = Math.Max(textOffsetX + totalTextW, totalTextW);
            
            double titleH = _size * 1.5;
            double metricsH = _metrics.Count > 0 ? (_metrics.Count * (_size * 1.5)) + (_size * 0.5) : 0;
            double totalColorsH = _colors.Count * colorBoxH + Math.Max(0, _colors.Count - 1) * gapY;
            double contentH = titleH + metricsH + totalColorsH + (_size * 0.5);

            double boxW = contentW + padding * 2;
            double boxH = contentH + padding * 2;

            double x = 0, y = 0;
            if (_anchor == 1) { x = vpW - boxW - _ox; y = _oy; }
            else if (_anchor == 2) { x = _ox; y = vpH - boxH - _oy; }
            else if (_anchor == 3) { x = vpW - boxW - _ox; y = vpH - boxH - _oy; }
            else { x = _ox; y = _oy; }

            var rect = new Rectangle((int)x, (int)y, (int)boxW, (int)boxH);
            
            double alpha = Math.Max(0.0, Math.Min(1.0, _transparency));
            Color bg = Color.FromArgb((int)(alpha * 255), 25, 25, 25);
            e.Display.Draw2dRectangle(rect, bg, 0, bg);

            double curY = y + padding;
            e.Display.Draw2dText(_title.ToUpper(), _fontColor, new Point2d(x + padding, curY), false, (int)_size, _fontFace);
            curY += titleH + (_size * 0.5);

            if (_metrics.Count > 0) {
                foreach(var m in _metrics) {
                    e.Display.Draw2dText(m.Name + ": " + m.Value, _fontColor, new Point2d(x + padding, curY), false, (int)_size, _fontFace);
                    curY += _size * 1.5;
                }
                curY += _size * 0.5; // gap before legend
            }

            for (int i = _colors.Count - 1; i >= 0; i--)
            {
                var crect = new Rectangle((int)(x + padding), (int)curY, (int)colorBoxW, (int)colorBoxH);
                e.Display.Draw2dRectangle(crect, _colors[i], 0, _colors[i]);
                
                string labelStr = "";
                if (_legendType == "Discrete" && i < _labels.Count)
                {
                    labelStr = _labels[i];
                }
                else if (_legendType == "Gradient" || _legendType == "Blocks")
                {
                    if (i == _colors.Count - 1 && _labels.Count > 1) labelStr = _labels[_labels.Count - 1]; // max
                    else if (i == 0 && _labels.Count > 0) labelStr = _labels[0]; // min
                    else if (_labels.Count == _colors.Count) labelStr = _labels[i];
                }

                if (!string.IsNullOrEmpty(labelStr))
                {
                    double textY = curY; 
                    if (_legendType == "Gradient") {
                        if (i == 0 && _labels.Count > 0 && labelStr == _labels[0]) textY = curY + colorBoxH - _size;
                    }
                    else {
                        textY = curY + (colorBoxH - _size) * 0.5;
                    }
                    e.Display.Draw2dText(labelStr, _fontColor, new Point2d(x + padding + textOffsetX, textY), false, (int)_size, _fontFace);
                }

                curY += colorBoxH + gapY;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                DisplayPipeline.DrawForeground -= OnDrawForeground;
                _subscribed = false;
            }
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            Unsubscribe();
            base.RemovedFromDocument(document);
        }
        
        public override void DocumentContextChanged(GH_Document document, GH_DocumentContext context)
        {
            if (context == GH_DocumentContext.Close || context == GH_DocumentContext.Unloaded)
            {
                Unsubscribe();
            }
            base.DocumentContextChanged(document, context);
        }

        protected override Bitmap Icon => Enzyme.IconLoader.Load("Analysis Dashboard.png");

        public override Guid ComponentGuid => new Guid("B5A3E1C2-88B1-4A55-9B2D-C1A4328FF1A9");
    }
}
