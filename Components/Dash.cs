using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Newtonsoft.Json;
using Rhino.Display;
using Rhino.Geometry;
using Enzyme;

namespace Enzyme.Components
{
    public class DashComponent : GH_Component
    {
        private bool _run = false;
        private string _jsonPayload = "";
        private bool _groupByBldg = false;
        private string _title = "MASTERPLAN SUMMARY";
        private string _suffix = "";
        private double _targetGlobal = 0.0;
        private string _targetJson = "";
        private double _size = 12.0;
        private int _anchor = 0;
        private string _fontFace = "Arial";
        private double _transparency = 0.8;
        private double _ox = 20.0;
        private double _oy = 20.0;
        private double _fitPadding = 10.0;
        
        private bool _subscribed = false;

        private List<DashDisplayItem> _displayData = new List<DashDisplayItem>();

        public DashComponent()
          : base("Masterplan Dashboard", "DASH",
              "Reads a summarized JSON payload from the OOP Masterplan Engine and renders a responsive HUD.",
              Enzyme.Utils.TabInfo.TabName, "Masterplan (Beta)")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 0, false, 210, -160);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 2, false, 210, -120);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, 0.0, 2.0, 0.0, 330, -80);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 7, 0.0, 24, 12.0, 330, -40);
                Enzyme.Utils.AutoWireHelper.WireValueList(this, document, 8, new string[]{"TL", "TR", "BL", "BR"}, new string[]{"0", "1", "2", "3"}, 300, 0);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 10, 0.0, 1.0, 0.8, 330, 40);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 11, 0.0, 40, 20.0, 330, 80);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 12, 0.0, 40, 20.0, 330, 120);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 13, 0.0, 20, 10.0, 330, 160);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run", "run", "Enable HUD", GH_ParamAccess.item, false);
            pManager.AddTextParameter("JSON Payload", "JSON_Payload", "JSON Payload from OOP", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Group By Bldg", "GroupByBldg", "Group By Building", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Title", "title", "HUD Title", GH_ParamAccess.item, "MASTERPLAN SUMMARY");
            pManager.AddTextParameter("Suffix", "suffix", "Unit suffix", GH_ParamAccess.item, "");
            pManager.AddNumberParameter("Target Global", "TargetGlobal", "Target Global Area", GH_ParamAccess.item, 0.0);
            pManager.AddTextParameter("Target JSON", "TargetJSON", "JSON containing Targets", GH_ParamAccess.item, "");
            pManager.AddNumberParameter("Size", "size", "Font size", GH_ParamAccess.item, 12.0);
            pManager.AddIntegerParameter("Anchor", "anchor", "Anchor (0=TL, 1=TR, 2=BL, 3=BR)", GH_ParamAccess.item, 0);
            pManager.AddTextParameter("Font", "font", "Font Face", GH_ParamAccess.item, "Arial");
            pManager.AddNumberParameter("Transparency", "transparency", "Background Opacity (0.0 - 1.0)", GH_ParamAccess.item, 0.8);
            pManager.AddNumberParameter("Offset X", "ox", "Offset X", GH_ParamAccess.item, 20.0);
            pManager.AddNumberParameter("Offset Y", "oy", "Offset Y", GH_ParamAccess.item, 20.0);
            pManager.AddNumberParameter("Padding", "fit_padding", "Padding", GH_ParamAccess.item, 10.0);

            for (int i = 0; i < pManager.ParamCount; i++)
            {
                pManager[i].Optional = true;
            }
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            // No outputs, this component only draws to the HUD
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Fetch inputs
            DA.GetData(0, ref _run);
            DA.GetData(1, ref _jsonPayload);
            DA.GetData(2, ref _groupByBldg);
            DA.GetData(3, ref _title);
            DA.GetData(4, ref _suffix);
            DA.GetData(5, ref _targetGlobal);
            DA.GetData(6, ref _targetJson);
            DA.GetData(7, ref _size);
            DA.GetData(8, ref _anchor);
            DA.GetData(9, ref _fontFace);
            DA.GetData(10, ref _transparency);
            DA.GetData(11, ref _ox);
            DA.GetData(12, ref _oy);
            DA.GetData(13, ref _fitPadding);

            if (_run && !string.IsNullOrEmpty(_jsonPayload))
            {
                try
                {
                    _displayData.Clear();
                    
                    var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(_jsonPayload);
                    
                    var progAreas = new Dictionary<string, double>();
                    if (data.ContainsKey("programs") && data["programs"] != null)
                        progAreas = JsonConvert.DeserializeObject<Dictionary<string, double>>(data["programs"].ToString());
                    
                    double totalArea = 0.0;
                    if (data.ContainsKey("total_area") && data["total_area"] != null)
                        totalArea = Convert.ToDouble(data["total_area"]);
                    
                    var bldgData = new Dictionary<string, Dictionary<string, object>>();
                    if (data.ContainsKey("buildings") && data["buildings"] != null)
                        bldgData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(data["buildings"].ToString());

                    var cleanTargetData = new Dictionary<string, double>();
                    if (!string.IsNullOrEmpty(_targetJson))
                    {
                        try
                        {
                            var rawTargets = JsonConvert.DeserializeObject<Dictionary<string, object>>(_targetJson);
                            if (rawTargets != null)
                            {
                                foreach (var kvp in rawTargets)
                                {
                                    if (kvp.Value != null)
                                        cleanTargetData[kvp.Key.Trim().ToUpper()] = Convert.ToDouble(kvp.Value);
                                }
                            }
                        }
                        catch { }
                    }

                    if (!string.IsNullOrEmpty(_title))
                    {
                        _displayData.Add(new DashDisplayItem($"=== {_title.ToUpper()} ===", "", false));
                    }

                    if (_groupByBldg)
                    {
                        foreach (var kvp in bldgData)
                        {
                            string bName = kvp.Key;
                            var bStats = kvp.Value;
                            _displayData.Add(new DashDisplayItem($"--- {bName.ToUpper()} ---", "", false));
                            
                            if (bStats.ContainsKey("programs") && bStats["programs"] != null)
                            {
                                var pAreas = JsonConvert.DeserializeObject<Dictionary<string, double>>(bStats["programs"].ToString());
                                foreach (var pKvp in pAreas)
                                {
                                    string pName = pKvp.Key;
                                    double valArea = pKvp.Value;
                                    string lineLabel = $"  {pName}: ";
                                    string lineVal = $"{FormatNum(valArea)}{_suffix}";
                                    _displayData.Add(new DashDisplayItem(lineLabel, lineVal, false));
                                }
                            }
                            
                            double bTotArea = 0.0;
                            if (bStats.ContainsKey("total_area") && bStats["total_area"] != null)
                                bTotArea = Convert.ToDouble(bStats["total_area"]);
                                
                            string bTotVal = $"{FormatNum(bTotArea)}{_suffix}";
                            _displayData.Add(new DashDisplayItem("  SUBTOTAL: ", bTotVal, false));
                            _displayData.Add(new DashDisplayItem(" ", " ", false));
                        }

                        if (cleanTargetData.Count > 0)
                        {
                            _displayData.Add(new DashDisplayItem("--- TARGET TRACKING ---", "", false));
                            foreach (var pKvp in progAreas)
                            {
                                string progName = pKvp.Key;
                                double valArea = pKvp.Value;
                                string progNameUpper = progName.Trim().ToUpper();
                                double target = cleanTargetData.ContainsKey(progNameUpper) ? cleanTargetData[progNameUpper] : 0.0;
                                if (target > 0)
                                {
                                    double pct = (valArea / target) * 100.0;
                                    string lineLabel = $"  {progName}: ";
                                    string lineVal = $"{FormatNum(valArea)}{_suffix} / {FormatNum(target)}{_suffix} ({pct:F1}%)";
                                    _displayData.Add(new DashDisplayItem(lineLabel, lineVal, valArea > target));
                                }
                            }
                            _displayData.Add(new DashDisplayItem(" ", " ", false));
                        }

                        _displayData.Add(new DashDisplayItem(new string('=', 15), "", false));
                    }
                    else
                    {
                        foreach (var pKvp in progAreas)
                        {
                            string progName = pKvp.Key;
                            double valArea = pKvp.Value;
                            string progNameUpper = progName.Trim().ToUpper();
                            double target = cleanTargetData.ContainsKey(progNameUpper) ? cleanTargetData[progNameUpper] : 0.0;
                            
                            string lineLabel = $"{progName.ToUpper()}: ";
                            string lineVal = $"{FormatNum(valArea)}{_suffix}";
                            bool overTarget = false;
                            
                            if (target > 0)
                            {
                                double pct = (valArea / target) * 100.0;
                                lineVal += $" / {FormatNum(target)}{_suffix} ({pct:F1}%)";
                                if (valArea > target) overTarget = true;
                            }
                            
                            _displayData.Add(new DashDisplayItem(lineLabel, lineVal, overTarget));
                        }
                        _displayData.Add(new DashDisplayItem(new string('-', 15), "", false));
                    }

                    bool totIsRed = false;
                    string totStr = $"{FormatNum(totalArea)}{_suffix}";
                    
                    if (_targetGlobal > 0)
                    {
                        double totPct = (totalArea / _targetGlobal) * 100.0;
                        totStr += $" [Target: {FormatNum(_targetGlobal)}{_suffix} ({totPct:F1}%)]";
                        if (totalArea > _targetGlobal) totIsRed = true;
                    }
                    
                    _displayData.Add(new DashDisplayItem("TOTAL AREA: ", totStr, totIsRed));

                    if (!_subscribed)
                    {
                        DisplayPipeline.DrawForeground += OnDrawForeground;
                        _subscribed = true;
                    }

                    string modeMsg = _groupByBldg ? "Per-Building Mode" : "Global Mode";
                    this.Message = $"HUD ACTIVE\n{modeMsg}";
                }
                catch (Exception ex)
                {
                    this.Message = "JSON Error: " + ex.Message;
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
            if (!_run || _displayData.Count == 0) return;

            int vpW = e.Viewport.Bounds.Width;
            int vpH = e.Viewport.Bounds.Height;
            double lineH = _size * 1.6;
            double titleGap = 20.0;

            int maxLabelChars = 10;
            foreach (var d in _displayData)
                if (d.Label.Length > maxLabelChars) maxLabelChars = d.Label.Length;

            double valueStartOffset = maxLabelChars * (_size * 0.6);

            int maxTotalChars = 10;
            foreach (var d in _displayData)
            {
                int len = d.Label.Length + d.Val.Length;
                if (len > maxTotalChars) maxTotalChars = len;
            }
            
            double boxW = (maxTotalChars * _size * 0.55) + (_fitPadding * 3.0);

            int titleCount = 0;
            foreach (var d in _displayData)
            {
                if (d.Label.Contains("---") || d.Label.Contains("==="))
                    titleCount++;
            }

            double boxH = (_displayData.Count * lineH) + (titleCount * titleGap) + (_fitPadding * 2.5);

            double x = 0, y = 0;
            if (_anchor == 1) { x = vpW - boxW - _ox; y = _oy; }
            else if (_anchor == 2) { x = _ox; y = vpH - boxH - _oy; }
            else if (_anchor == 3) { x = vpW - boxW - _ox; y = vpH - boxH - _oy; }
            else { x = _ox; y = _oy + 35; }

            double transparencyClamped = Math.Max(0.0, Math.Min(1.0, _transparency));
            int alpha = (int)(transparencyClamped * 255);
            Color bg = Color.FromArgb(alpha, 25, 25, 25);
            Color white = Color.White;
            Color warningRed = Color.OrangeRed;

            var rect = new Rectangle((int)x, (int)y, (int)boxW, (int)boxH);
            e.Display.Draw2dRectangle(rect, bg, 1, bg);

            double currentY = y + _fitPadding;
            foreach (var item in _displayData)
            {
                if (string.IsNullOrWhiteSpace(item.Label) && string.IsNullOrWhiteSpace(item.Val))
                {
                    currentY += lineH;
                    continue;
                }

                e.Display.Draw2dText(item.Label, white, new Point2d(x + _fitPadding, currentY), false, (int)_size, _fontFace);
                if (!item.Label.Contains("---") && !item.Label.Contains("==="))
                {
                    var valP = new Point2d(x + _fitPadding + valueStartOffset, currentY);
                    Color color = item.IsRed ? warningRed : white;
                    e.Display.Draw2dText(item.Val, color, valP, false, (int)_size, _fontFace);
                }

                currentY += (item.Label.Contains("---") || item.Label.Contains("===")) ? (lineH + titleGap) : lineH;
            }
        }

        private string FormatNum(double n)
        {
            return n.ToString("N2");
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

        protected override Bitmap Icon => IconLoader.Load("DASH.png");

        public override Guid ComponentGuid => new Guid("B453E8A0-E3A9-4A9E-B19F-D7C6B372F26C");

        private class DashDisplayItem
        {
            public string Label { get; }
            public string Val { get; }
            public bool IsRed { get; }

            public DashDisplayItem(string label, string val, bool isRed)
            {
                Label = label ?? "";
                Val = val ?? "";
                IsRed = isRed;
            }
        }
    }
}
