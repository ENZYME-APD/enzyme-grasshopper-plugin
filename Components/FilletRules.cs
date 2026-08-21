using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Newtonsoft.Json;

namespace Enzyme.Components
{
    public class FilletRulesComponent : GH_Component
    {
        public FilletRulesComponent()
          : base("Fillet Rule Configurator", "Fillet_Rules",
              "Compiles Grasshopper lists into a Fillet Rules JSON.",
              "Enzyme", "Masterplan (Beta)")
        {
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
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 0, 0.0, 2.0, 0.0, 330, -40);
                Enzyme.Utils.AutoWireHelper.WireValueList(this, document, 1, new string[]{"Tower", "Program", "Building"}, new string[]{"\"Tower\"", "\"Program\"", "\"Building\""}, 300, 0);
                Enzyme.Utils.AutoWireHelper.WireValueList(this, document, 2, new string[]{"Main_Tower", "Retail", "*"}, new string[]{"\"Main_Tower\"", "\"Retail\"", "\"*\""}, 300, 40);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 0, 220, -34, 180, 22);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 220, 11, 180, 22);
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("DefaultRadius", "DefaultRadius", "The base radius if no rules match.", GH_ParamAccess.item, 0.0);
            pManager.AddTextParameter("RuleTypes", "RuleTypes", "List of target types ('Tower', 'Program', 'Building').", GH_ParamAccess.list);
            pManager.AddTextParameter("RuleMatches", "RuleMatches", "List of target names ('Main_Tower', 'Retail', '*').", GH_ParamAccess.list);
            pManager.AddNumberParameter("RuleRadii", "RuleRadii", "List of radii to apply to the matched targets.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("ExactMatch", "ExactMatch", "True = Exact, False = Contains. Defaults to True.", GH_ParamAccess.list);
            
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Fillet_JSON", "Fillet_JSON", "The compiled JSON string to feed into BIM_JSON.", GH_ParamAccess.item);
            pManager.AddTextParameter("Instructions", "Instructions", "Detailed usage instructions.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double defaultRad = 0.0;
            DA.GetData(0, ref defaultRad);

            List<string> ruleTypes = new List<string>();
            DA.GetDataList(1, ruleTypes);

            List<string> ruleMatches = new List<string>();
            DA.GetDataList(2, ruleMatches);

            List<double> ruleRadii = new List<double>();
            DA.GetDataList(3, ruleRadii);

            List<bool> exactMatches = new List<bool>();
            DA.GetDataList(4, exactMatches);

            int ruleCount = Math.Min(Math.Min(ruleTypes.Count, ruleMatches.Count), ruleRadii.Count);
            
            var rules = new List<object>();

            for (int i = 0; i < ruleCount; i++)
            {
                bool isExact = true;
                if (i < exactMatches.Count)
                {
                    isExact = exactMatches[i];
                }

                rules.Add(new
                {
                    type = ruleTypes[i]?.Trim() ?? "",
                    match = ruleMatches[i]?.Trim() ?? "",
                    radius = ruleRadii[i],
                    exact = isExact
                });
            }

            var configDict = new
            {
                default_radius = defaultRad,
                rules = rules
            };

            string jsonOutput = JsonConvert.SerializeObject(configDict, Formatting.Indented);

            Message = $"{this.NickName}\nRULES BUILT\n---\nValid Rules: {ruleCount}\nDefault: {defaultRad}m";

            DA.SetData(0, jsonOutput);

            string instructions = @"FILLET RULE CONFIGURATOR
================================================================================
A UI helper node that compiles standard Grasshopper lists into a strict JSON 
schema for parametric corner rounding. Prevents syntax errors from manual typing.

HOW TO USE NEGATIVE RULES (e.g., ""Everything except Office""):
Because the Engine reads top-down, put your exception first, then a wildcard:
1. Type: Program | Match: Office | Radius: 0.0 (Exception)
2. Type: Program | Match: * | Radius: 3.0 (Everything Else)

INPUTS:
    DefaultRadius (float) : The base radius if no rules match.
    RuleTypes     (str)   : List of target types ('Tower', 'Program', 'Building').
    RuleMatches   (str)   : List of target names ('Main_Tower', 'Retail', '*').
    RuleRadii     (float) : List of radii to apply to the matched targets.
    ExactMatch    (bool)  [List Access] : True = Exact, False = Contains. Defaults to True.

OUTPUTS:
    Fillet_JSON   (str)   : The compiled JSON string to feed into BIM_JSON.
    Instructions  (str)   : Detailed usage instructions.
================================================================================";
            
            DA.SetData(1, instructions);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("Fillet_Rules.png");
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override Guid ComponentGuid
        {
            get { return new Guid("b8c1c4f0-32a4-43e6-94e8-8a8f15d9cf7b"); }
        }
    }
}
