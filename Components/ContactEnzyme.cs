using System;
using System.Drawing;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace Enzyme.Components
{
    public class ContactEnzyme : GH_Component
    {
        public override GH_Exposure Exposure => GH_Exposure.septenary;

        public ContactEnzyme()
          : base("Contact Enzyme", "Contact",
              "Connect with us or visit our website.",
              "Enzyme", "Info")
        {
        }

        public override void CreateAttributes()
        {
            m_attributes = new ContactEnzymeAttributes(this);
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Message", "M", "Optional message to include in the email body.", GH_ParamAccess.item);
            pManager[0].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        public string CurrentMessage { get; set; } = "";

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string msg = "";
            if (DA.GetData(0, ref msg))
            {
                CurrentMessage = msg;
            }
            else
            {
                CurrentMessage = "";
            }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                try {
                    return IconLoader.Load("enzyme_logo_24.png");
                } catch {
                    return null;
                }
            }
        }

        public override Guid ComponentGuid => new Guid("59eeb6f1-da0c-4fa7-ae19-b5f7e71912a3"); 
    }

    public class ContactEnzymeAttributes : GH_ComponentAttributes
    {
        private RectangleF WebButtonBounds;
        private RectangleF EmailButtonBounds;

        public ContactEnzymeAttributes(GH_Component owner) : base(owner) { }

        protected override void Layout()
        {
            base.Layout();
            
            // Add space for two buttons at the bottom
            int buttonHeight = 22;
            int padding = 4;
            
            RectangleF bounds = Bounds;
            bounds.Height += (buttonHeight * 2) + (padding * 3);
            Bounds = bounds;
            
            WebButtonBounds = new RectangleF(
                bounds.X + 2,
                bounds.Bottom - (buttonHeight * 2) - (padding * 2),
                bounds.Width - 4,
                buttonHeight
            );
            
            EmailButtonBounds = new RectangleF(
                bounds.X + 2,
                bounds.Bottom - buttonHeight - padding,
                bounds.Width - 4,
                buttonHeight
            );
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);
            if (channel == GH_CanvasChannel.Objects)
            {
                // Draw Web Button
                GH_Capsule webCapsule = GH_Capsule.CreateTextCapsule(WebButtonBounds, WebButtonBounds, GH_Palette.Black, "Visit Website", 2, 0);
                webCapsule.Render(graphics, Selected, Owner.Locked, false);
                webCapsule.Dispose();

                // Draw Email Button
                GH_Capsule emailCapsule = GH_Capsule.CreateTextCapsule(EmailButtonBounds, EmailButtonBounds, GH_Palette.Black, "Send Email", 2, 0);
                emailCapsule.Render(graphics, Selected, Owner.Locked, false);
                emailCapsule.Dispose();
            }
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (WebButtonBounds.Contains(e.CanvasLocation))
                {
                    try {
                        var psi = new System.Diagnostics.ProcessStartInfo("https://www.weareenzyme.com/") { UseShellExecute = true };
                        System.Diagnostics.Process.Start(psi);
                    } catch {}
                    return GH_ObjectResponse.Handled;
                }
                
                if (EmailButtonBounds.Contains(e.CanvasLocation))
                {
                    try {
                        var comp = Owner as ContactEnzyme;
                        string msg = comp?.CurrentMessage ?? "";
                        string url = "mailto:hello@weareenzyme.com?subject=Enzyme%20Grasshopper%20Plugin";
                        if (!string.IsNullOrEmpty(msg))
                        {
                            url += $"&body={Uri.EscapeDataString(msg)}";
                        }
                        var psi = new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true };
                        System.Diagnostics.Process.Start(psi);
                    } catch {}
                    return GH_ObjectResponse.Handled;
                }
            }
            return base.RespondToMouseDown(sender, e);
        }
    }
}
