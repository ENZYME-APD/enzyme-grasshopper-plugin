using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Rhino.Display;
using Rhino.DocObjects;
using Grasshopper.Kernel;

namespace Enzyme.Components
{
    public class CreateNamedViews : GH_Component
    {
        public CreateNamedViews()
          : base("Create Named Views", "NamedViews",
              "Converts a list of camera locations and targets into Rhino Named Views. Combine with Advanced Export Views for flythroughs and animations.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Locations", "Locs", "List of 3D points for the camera positions.", GH_ParamAccess.list);
            pManager.AddPointParameter("Targets", "Targs", "List of 3D points for the camera targets. If only one is provided, the camera will always look at it.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Lens Length", "Lens", "Camera lens length in mm (e.g. 50).", GH_ParamAccess.item, 50.0);
            pManager.AddTextParameter("Prefix", "Prefix", "Prefix for the generated Named Views.", GH_ParamAccess.item, "Shot_");
            pManager.AddBooleanParameter("Run", "Run", "Trigger to generate the views in the Rhino document.", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("View Names", "Names", "List of generated Named View names (Plug this into Advanced Export Views!)", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "Info", "Component execution HUD.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Point3d> locs = new List<Point3d>();
            List<Point3d> targets = new List<Point3d>();
            double lens = 50.0;
            string prefix = "Shot_";
            bool run = false;

            if (!DA.GetDataList(0, locs)) return;
            if (!DA.GetDataList(1, targets)) return;
            DA.GetData(2, ref lens);
            DA.GetData(3, ref prefix);
            DA.GetData(4, ref run);

            if (locs.Count == 0 || targets.Count == 0) return;

            List<string> generatedNames = new List<string>();
            System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();

            if (run)
            {
                var doc = RhinoDoc.ActiveDoc;
                RhinoView activeView = doc.Views.ActiveView;
                
                if (activeView != null)
                {
                    // Save the user's current camera so we don't mess up their viewport
                    var originalCam = new ViewInfo(activeView.ActiveViewport);

                    for (int i = 0; i < locs.Count; i++)
                    {
                        Point3d loc = locs[i];
                        Point3d target = targets.Count > 1 ? targets[i % targets.Count] : targets[0];

                        Vector3d dirVec = target - loc;
                        if (dirVec.IsTiny()) dirVec = Vector3d.YAxis; // fallback if points are identical

                        activeView.ActiveViewport.SetCameraLocation(loc, false);
                        activeView.ActiveViewport.SetCameraDirection(dirVec, false);
                        activeView.ActiveViewport.SetCameraTarget(target, false);
                        activeView.ActiveViewport.Camera35mmLensLength = lens;

                        string name = $"{prefix}{i:D4}";

                        // Check if a view with this name already exists and delete it to overwrite
                        int existingIdx = doc.NamedViews.FindByName(name);
                        if (existingIdx >= 0)
                        {
                            doc.NamedViews.Delete(existingIdx);
                        }

                        var newView = new ViewInfo(activeView.ActiveViewport);
                        newView.Name = name;
                        doc.NamedViews.Add(newView);
                        
                        generatedNames.Add(name);
                    }

                    // Restore the user's viewport
                    activeView.ActiveViewport.PushViewInfo(originalCam, false);
                    activeView.Redraw();
                }
            }

            timer.Stop();

            DA.SetDataList(0, generatedNames);

            string status = run ? $"Generated {generatedNames.Count} Named Views" : "Sleeping (Run is False)";
            Message = $"{this.NickName}\nTime: {timer.ElapsedMilliseconds} ms\n---\n{status}";
            
            DA.SetData(1, "CREATE NAMED VIEWS\n"
                + "\n"
                + "METHODOLOGY:\n"
                + "Iterates through the provided locations and targets, configures the active viewport camera, and saves a Rhino Named View for each frame.\n\n"
                + "INTERPRETATION & IMPORTANCE:\n"
                + "Decouples camera animation from exporting. Plug the output of this component into Advanced Export Views to apply Layer States, Canvas States, and high-res capturing to your flythrough paths.");
        }

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("CreateNamedViews.png");

        
        public override GH_Exposure Exposure => GH_Exposure.secondary;
public override Guid ComponentGuid => new Guid("7F2A3D1B-5B2E-4D9E-8A11-C345F12A709A");
    }
}
