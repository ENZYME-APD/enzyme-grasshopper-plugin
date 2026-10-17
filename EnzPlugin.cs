using Grasshopper.Kernel;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;

namespace Enzyme
{
    public class EnzPlugin : GH_AssemblyInfo
    {
        public EnzPlugin()
        {
            // Initialize trace configuration
            TraceConfig.EnsureInitialized();
            Trace.WriteLine("Enzyme plugin initialized");
        }

        public override string Name => "Enzyme-Grasshopper-Plugin";

        public override Bitmap Icon
        {
            get
            {
                Trace.WriteLine("Loading plugin icon");
                return IconLoader.Load("enzyme_logo.png");
            }
        }

        public override string Description => "Enzyme APD urban design toolkit: masterplanning and urban prototyping, site and terrain analysis, road network modelling, facade design, sustainability and canvas utilities.";
        public override string AuthorName => "Enzyme APD";
        public override string AuthorContact => "hello@weareenzyme.com | https://www.weareenzyme.com";
        public override Guid Id => new Guid("8dfb3e6b-73c9-40f6-b03f-4343c789749b");
    }
}
