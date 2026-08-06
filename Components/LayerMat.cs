using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Rhino;
using Rhino.DocObjects;
using System.Drawing;
using Enzyme;

namespace Enzyme.Components
{
    public class LayerMat : GH_Component
    {
        public LayerMat()
          : base("Assign Layer Materials", "LAYERMAT",
              "Assigns render materials to layers and auto-populates connected Value Lists.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("LayerNames", "LN", "Layer names or paths. Connect a Value List here!", GH_ParamAccess.list);
            pManager[0].Optional = true;
            pManager.AddBooleanParameter("UseFullPath", "UFP", "If True, material name = 'Parent::Child'. If False, just 'Child'.", GH_ParamAccess.item, true);
            pManager[1].Optional = true;
            pManager.AddBooleanParameter("RunScript", "RS", "Connect a Button/Toggle to execute.", GH_ParamAccess.item, false);
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Instructions_Out", "IO", "Component interface documentation.", GH_ParamAccess.item);
            pManager.AddTextParameter("LogOutput", "LO", "Persistent execution log.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("UpdatedCount", "UC", "Number of layers that received a new material.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("SkippedCount", "SC", "Number of layers that already had a material.", GH_ParamAccess.item);
        }

        private string _logOutput = "Connect a boolean to RunScript.";
        private int _updatedCount = 0;
        private int _skippedCount = 0;
        private string _hudMessage = "LAYERMAT\nMode: --\n-- WAITING --\n---\n● Assigned: 0\n○ Skipped: 0";

        private const string Instructions = @"Grasshopper C# Component — Assign Layer Materials
======================================================
DESCRIPTION:
  Assigns render materials to Rhino layers based on their display color.
  It safely persists data between execution cycles and features an auto-fill 
  routine: connect a native Value List to the LayerNames input, and it will 
  automatically populate with all existing Rhino layers.

INPUTS:
  LayerNames  : List [str] - Layer names or paths. Connect a Value List here!
  UseFullPath : Item [bool] - If True, material name = 'Parent::Child'. If False, just 'Child'.
  RunScript   : Item [bool] - Connect a Button/Toggle to execute.

OUTPUTS:
  Instructions_Out : [str] - Component interface documentation.
  LogOutput        : [str] - Persistent execution log.
  UpdatedCount     : [int] - Number of layers that received a new material.
  SkippedCount     : [int] - Number of layers that already had a material.";

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();
            
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            
            if (Params.Input.Count < 1) return;
            var layerInput = Params.Input[0];
            if (layerInput.Sources.Count == 0) return;
            
            var validLayers = doc.Layers.Where(l => !l.IsDeleted).ToList();
            var targetNames = validLayers.Select(l => l.Name).ToList();
            
            foreach (var source in layerInput.Sources)
            {
                if (source is GH_ValueList valueList)
                {
                    var currentNames = valueList.ListItems.Select(item => item.Name).ToList();
                    
                    if (!currentNames.SequenceEqual(targetNames))
                    {
                        valueList.ListItems.Clear();
                        foreach (var l in validLayers)
                        {
                            var item = new GH_ValueListItem(l.Name, $"\"{l.FullPath}\"");
                            valueList.ListItems.Add(item);
                        }
                        valueList.Attributes.ExpireLayout();
                    }
                }
            }
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<string> layerNames = new List<string>();
            bool useFullPath = true;
            bool runScript = false;

            DA.GetDataList(0, layerNames);
            DA.GetData(1, ref useFullPath);
            DA.GetData(2, ref runScript);

            DA.SetData(0, Instructions);

            if (runScript)
            {
                Stopwatch sw = Stopwatch.StartNew();
                var doc = RhinoDoc.ActiveDoc;
                if (doc != null)
                {
                    List<string> messages = new List<string> { "=== Assign Layer Materials ===" };
                    int updated = 0;
                    int skipped = 0;

                    string modeStr = useFullPath ? "Full Path" : "Short Name";

                    List<string> names = layerNames.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
                    List<Layer> layersToProcess = new List<Layer>();

                    if (names.Count > 0)
                    {
                        messages.Add($"Target: {names.Count} layer name(s) provided");
                        foreach (var name in names)
                        {
                            var trimmedName = name.Trim();
                            var layer = FindLayer(doc, trimmedName);
                            if (layer != null)
                            {
                                layersToProcess.Add(layer);
                            }
                            else
                            {
                                messages.Add($"  WARN  Layer not found: '{name}'");
                            }
                        }
                    }
                    else
                    {
                        layersToProcess = doc.Layers.Where(l => !l.IsDeleted).ToList();
                        messages.Add($"Target: Processing ALL {layersToProcess.Count} layer(s)");
                    }

                    foreach (var layer in layersToProcess)
                    {
                        if (LayerUsesDefaultMaterial(layer))
                        {
                            int matIndex = GetOrCreateMaterial(layer, doc, useFullPath);
                            layer.RenderMaterialIndex = matIndex;
                            doc.Layers.Modify(layer, layer.Index, false);
                            messages.Add($"  OK    {layer.FullPath} (Assigned)");
                            updated++;
                        }
                        else
                        {
                            messages.Add($"  SKIP  {layer.FullPath} (Has Material)");
                            skipped++;
                        }
                    }

                    doc.Views.Redraw();

                    messages.Add("");
                    messages.Add($"Done. Assigned: {updated} | Skipped: {skipped}");

                    sw.Stop();
                    double elapsedMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);

                    _hudMessage = $@"LAYERMAT
Mode: {modeStr}
Time: {elapsedMs} ms
---
● Assigned: {updated}
○ Skipped: {skipped}";

                    _logOutput = string.Join("\n", messages);
                    _updatedCount = updated;
                    _skippedCount = skipped;
                }
            }

            Message = _hudMessage;
            
            DA.SetData(1, _logOutput);
            DA.SetData(2, _updatedCount);
            DA.SetData(3, _skippedCount);
        }

        private bool LayerUsesDefaultMaterial(Layer layer)
        {
            return layer.RenderMaterialIndex < 0;
        }

        private Layer FindLayer(RhinoDoc doc, string name)
        {
            int idx = doc.Layers.FindByFullPath(name, true);
            if (idx >= 0) return doc.Layers[idx];

            foreach (var layer in doc.Layers)
            {
                if (!layer.IsDeleted && layer.Name == name) return layer;
            }
            return null;
        }

        private int GetOrCreateMaterial(Layer layer, RhinoDoc doc, bool useFullPath)
        {
            string matName = useFullPath ? layer.FullPath : layer.Name;
            int existing = doc.Materials.Find(matName, true);
            if (existing >= 0) return existing;

            var mat = new Material();
            mat.Name = matName;
            
            var layerColor = layer.Color;
            mat.DiffuseColor = layerColor;

            double alpha = layerColor.A;
            double transparency = 1.0 - (alpha / 255.0);
            mat.Transparency = Math.Max(0.0, Math.Min(1.0, transparency));
            mat.SpecularColor = Color.White;
            mat.Shine = 30.0;

            if (transparency > 0.0)
            {
                mat.IndexOfRefraction = 1.0;
                mat.ReflectionColor = Color.White;
            }

            mat.CommitChanges();
            return doc.Materials.Add(mat);
        }

        protected override Bitmap Icon
        {
            get { return IconLoader.Load("LAYERMAT.png"); }
        }
        
        public override Guid ComponentGuid
        {
            get { return new Guid("d14f24bd-3b47-49f3-8b7c-3f41a31d4e6b"); }
        }
    }
}
