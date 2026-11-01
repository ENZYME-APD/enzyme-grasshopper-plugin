using System; using System.Collections.Generic; using System.Linq; using Enzyme.Utils;
int fails=0; void Check(bool c,string m){ Console.WriteLine((c?"PASS ":"FAIL ")+m); if(!c) fails++; }
// grid mesh n x n, value = radial distance from centre
int N=41; double h=1;
var X=new List<double>(); var Y=new List<double>(); var Z=new List<double>();
for(int j=0;j<N;j++) for(int i=0;i<N;i++){ X.Add(i*h); Y.Add(j*h); Z.Add(0);} 
var F=new List<int>(); for(int j=0;j<N-1;j++) for(int i=0;i<N-1;i++){ int a=j*N+i; F.AddRange(new[]{a,a+1,a+N+1,a+N}); }
var m=new ScalarMesh(X.ToArray(),Y.ToArray(),Z.ToArray(),F.ToArray());
double cx=20,cy=20; var vv=new double[N*N]; for(int k=0;k<N*N;k++) vv[k]=Math.Sqrt((X[k]-cx)*(X[k]-cx)+(Y[k]-cy)*(Y[k]-cy));
// isoline r=10 -> one closed loop, length ~ 2*pi*10
var iso=FieldMath.Isolines(m,vv,new[]{10.0},null);
Check(iso.Count==1 && iso[0].Closed, $"r=10 isoline is one closed loop (got {iso.Count}, closed={iso.FirstOrDefault()?.Closed})");
double L=0; var p=iso[0].Xyz; for(int i=1;i<iso[0].PointCount;i++){ L+=Math.Sqrt(Math.Pow(p[3*i]-p[3*i-3],2)+Math.Pow(p[3*i+1]-p[3*i-2],2)); }
Check(Math.Abs(L-2*Math.PI*10)/(2*Math.PI*10)<0.02, $"loop length {L:F2} ~ {2*Math.PI*10:F2}");
bool onCircle=true; for(int i=0;i<iso[0].PointCount;i++){ double r=Math.Sqrt(Math.Pow(p[3*i]-cx,2)+Math.Pow(p[3*i+1]-cy,2)); if(Math.Abs(r-10)>0.1) onCircle=false; } Check(onCircle,"isoline points within 0.1 of r=10");
// bands area conservation: total area == domain area
var bounds=Enumerable.Range(0,11).Select(i=>i*3.0).ToArray();
var bands=FieldMath.Bands(m,vv,bounds,null);
double A=0; double[] perBand=new double[10];
foreach(var b in bands){ var q=b.Xyz; int n=q.Length/3; double s=0; for(int i=0;i<n;i++){int j=(i+1)%n; s+=q[3*i]*q[3*j+1]-q[3*j]*q[3*i+1];} A+=Math.Abs(s)/2; perBand[b.Band]+=Math.Abs(s)/2; }
Check(Math.Abs(A-40*40)<1e-6, $"bands tile the domain exactly (area {A:F6} vs 1600)");
Check(Math.Abs(perBand[0]-Math.PI*9)/(Math.PI*9)<0.03, $"band 0 (r<3) area {perBand[0]:F2} ~ {Math.PI*9:F2}");
// mask: face mask on a block, smoothing must not use masked faces
var mask=new bool[m.FaceCount]; for(int f=0;f<m.FaceCount;f++){ int a=m.F[4*f]; int i=a%N,j=a/N; mask[f]= i>=5&&i<10&&j>=5&&j<10; }
var fv=new double[m.FaceCount]; for(int f=0;f<m.FaceCount;f++) fv[f]= mask[f]?1000:1;
var vtx=FieldMath.FaceToVertex(m.F,N*N,fv,mask);
Check(vtx.Where(x=>!double.IsNaN(x)).All(x=>Math.Abs(x-1)<1e-12), "FaceToVertex ignores masked faces");
var sm=FieldMath.Smooth(m.F,N*N,vtx,5,mask);
Check(sm.Where(x=>!double.IsNaN(x)).All(x=>Math.Abs(x-1)<1e-9), "smoothing a constant field keeps it constant");
Check(double.IsNaN(vtx[7*N+7]), "vertex fully inside building is NaN");
// labels
var lab=FieldMath.PlaceLabels(FieldMath.Isolines(m,vv,new[]{6.0,9.0,12.0,15.0},null), 8, 3, 2);
bool sep=true; for(int i=0;i<lab.Count;i++) for(int j=i+1;j<lab.Count;j++){ double d=Math.Sqrt(Math.Pow(lab[i].X-lab[j].X,2)+Math.Pow(lab[i].Y-lab[j].Y,2)); if(d<3-1e-9) sep=false; }
Check(lab.Count>8 && sep, $"labels placed ({lab.Count}) and >= gap apart");
// peaks: two gaussian bumps
var g=new double[N*N]; for(int k=0;k<N*N;k++) g[k]=Math.Exp(-(Math.Pow(X[k]-10,2)+Math.Pow(Y[k]-10,2))/8)+0.6*Math.Exp(-(Math.Pow(X[k]-30,2)+Math.Pow(Y[k]-28,2))/8);
var pk=FieldMath.Peaks(m,g,null,true,5,5);
Check(pk.Count==2 && X[pk[0]]==10 && Y[pk[0]]==10 && X[pk[1]]==30 && Y[pk[1]]==28, $"peaks found at bumps ({string.Join(";",pk.Select(i=>X[i]+","+Y[i]))})");
// grid field + RK4: solid-body rotation, streamline should close a circle radius 8
var px=new List<double>(); var py=new List<double>(); var pz=new List<double>(); var pu=new List<double>(); var pv=new List<double>();
for(int j=0;j<N;j++) for(int i=0;i<N;i++){ if(i>=1&&i<=2&&j>=1&&j<=2) continue; px.Add(i*2.0); py.Add(j*2.0); pz.Add(5); pu.Add(-(j*2.0-40)); pv.Add(i*2.0-40);} 
var gf=GridField2D.TryBuild(px,py,pz,pu,pv);
Check(gf!=null && gf.Nx==41 && gf.CellX==2, "regular grid with holes detected");
var pts=new List<double[]>(); var spd=new List<double>();
StreamTracer.Trace(gf,48,40,0.5,2000,1,0.01,pts,spd);
double maxdev=pts.Max(q=>Math.Abs(Math.Sqrt(Math.Pow(q[0]-40,2)+Math.Pow(q[1]-40,2))-8));
var last=pts[pts.Count-1];
Check(maxdev<0.05 && Math.Sqrt(Math.Pow(last[0]-48,2)+Math.Pow(last[1]-40,2))<0.3, $"RK4 circle stays on radius (max dev {maxdev:F4}), closes loop after {pts.Count} pts");
Check(Math.Abs(pts[10][2]-5)<1e-9 && Math.Abs(spd[0]-8)<1e-6, "z and speed carried");
var jit=new List<double>(px); jit[3]+=0.4; Check(GridField2D.TryBuild(jit,py,pz,pu,pv)==null,"scattered points rejected (fallback to IDW)");
// palettes
Check(Palettes.TryGet("viridis_r", out var vr) && vr[0].R==253, "palette lookup + reverse");
Check(Palettes.TryGet("Viridis r", out var vr2) && vr2[0].R==253 && Palettes.TryGet("Ocean Thermal", out _) && Palettes.TryGet("Balance (diverging)", out _) && !Palettes.TryGet("Viridiss", out _), "palette names: space-r reverse, spaces, diverging tag, typo rejected");
Check(Palettes.Utci.ClassOf(20)==5 && Palettes.Utci.ClassOf(-50)==0 && Palettes.Utci.ClassOf(50)==9 && Palettes.Lawson.ClassOf(4)==1, "class schemes");
Check(FieldMath.NiceStep(37,10)==5, "nice step");

// report metrics
Check(Palettes.TryGetScheme("slope", out var sl) && sl.ClassOf(4)==0 && sl.ClassOf(15)==2 && sl.ClassOf(31)==3 && Palettes.TryGetScheme("Flood depth", out var fd) && fd.ClassOf(0.35)==2, "slope + flood depth schemes");
Check(ReportMetrics.Slug("Comfort (5.0 m/s)")=="comfort_5_0_m_s" && ReportMetrics.Slug("> 30% unbuildable")=="over_30pct_unbuildable", "metric key slugs");
Check(ReportMetrics.TryParseValue("12.5%", out var qv, out var qu) && qv==12.5 && qu=="%" && ReportMetrics.TryParseValue("+0.40 m/s", out var pv2, out var pu2) && pv2==0.4 && pu2=="m/s" && !ReportMetrics.TryParseValue("n/a", out _, out _), "value parsing");
string dashJson = "{\"Type\":\"Blocks\",\"Metrics\":[{\"Name\":\"Avg Speed\",\"Value\":\"3.1 m/s\"},{\"Name\":\"15-30% costly\",\"Value\":\"8.4%\"}],\"BandAreas\":[{\"Name\":\"15-30% costly\",\"Area\":25000,\"Pct\":8.4}]}";
var dm = ReportMetrics.FromDashboard(dashJson, "slope");
Check(dm.Count==3 && dm[0].Key=="slope_avg_speed" && dm[1].Key=="slope_15_30pct_costly_pct" && dm[2].Key=="slope_15_30pct_costly_ha" && dm[2].Value==2.5, "dashboard metrics -> keys, % and ha ("+string.Join(",",dm.Select(x=>x.Key))+")");
var rules = new List<FlagRule>{ new FlagRule{Theme="Buildability",Key="slope_15_30pct_costly_pct",Amber=5,Red=20}, new FlagRule{Theme="Comfort",Key="comfort_pct",Amber=60,Red=40} };
var mm = new List<MetricItem>(dm){ new MetricItem{Key="comfort_pct",Value=50,Unit="%"} };
var old = Newtonsoft.Json.Linq.JObject.Parse("{\"module\":\"C1\",\"metrics\":[{\"key\":\"elev_min_m\",\"value\":566.5,\"unit\":\"m\",\"scope\":\"site\"},{\"key\":\"comfort_pct\",\"value\":99,\"unit\":\"%\",\"scope\":\"site\"}],\"flags\":[],\"maps\":[{\"file\":\"a.png\",\"caption\":\"old\"}],\"inputs\":[{\"dataset\":\"GLO30\"}]}");
var built = ReportMetrics.Build("C1","1.11.0",new DateTime(2026,10,30),mm,rules,new[]{"GLO30","FABDEM | 1.2 | 30 m | 2023"},new[]{"a.png","b.png"},new[]{"Slope map","Flow"},"Modelled",old);
var fl=(Newtonsoft.Json.Linq.JArray)built["flags"]; var mets=(Newtonsoft.Json.Linq.JArray)built["metrics"];
Check((string)fl[0]["rating"]=="amber" && (string)fl[1]["rating"]=="amber" && (string)fl[1]["threshold"]["direction"]=="lower_is_worse", "flag ratings both directions");
Check(mets.Count==5 && (double)mets.First(m=>(string)m["key"]=="comfort_pct")["value"]==50 && mets.Any(m=>(string)m["key"]=="elev_min_m"), "merge keeps old metrics, replaces same key");
Check(((Newtonsoft.Json.Linq.JArray)built["maps"]).Count==2 && (string)built["maps"][0]["caption"]=="Slope map" && ((Newtonsoft.Json.Linq.JArray)built["inputs"]).Count==2 && (string)built["confidence"]=="modelled" && (string)built["run_date"]=="2026-10-30", "maps, sources, confidence, date");
Check(ReportMetrics.Rate(rules[0], double.NaN)=="n/a" && ReportMetrics.Rate(rules[0],25)=="red" && ReportMetrics.Rate(rules[1],70)=="green", "rate edge cases");
// canopy (Beer-Lambert through tree crowns)
Check(Math.Abs(CanopyMath.PathLengthInside(new[]{12.0,10.0},100)-2)<1e-12, "crown path: sorted entry/exit");
Check(Math.Abs(CanopyMath.PathLengthInside(new[]{3.0},100)-3)<1e-12, "crown path: origin inside crown");
Check(Math.Abs(CanopyMath.PathLengthInside(new[]{10.0,10.0,12.0,12.0},100)-2)<1e-12, "crown path: duplicate edge hits merged");
Check(Math.Abs(CanopyMath.PathLengthInside(new[]{10.0,12.0,20.0,25.0},22)-4)<1e-12 && CanopyMath.PathLengthInside(new double[0],10)==0, "crown path: two crowns, clipped to ray length; empty = 0");
Check(Math.Abs(CanopyMath.Transmittance(CanopyMath.OpticalDepth(4,1,0.5))-Math.Exp(-2))<1e-12 && CanopyMath.Transmittance(0)==1 && CanopyMath.OpticalDepth(4,0,0.5)==0, "Beer-Lambert transmittance");
// site fetch: UTM vs pyproj, GeoTIFF decoding, OSM parsing
foreach (var (la, lo, ep, E, Nn) in new[]{ (40.4168,-3.7038,32630,440290.4581,4474257.3820), (1.2903,103.8519,32648,372268.7952,142645.9810), (-23.5505,-46.6333,32723,333287.9151,7394588.3186), (22.2793,114.1628,32650,207633.0274,2466488.0031), (60.0,5.0,32632,276979.9264,6658157.2024) })
{
    int z=GeoProj.UtmZone(la,lo); GeoProj.ToUtm(la,lo,z,la>=0,out double ue,out double un); GeoProj.FromUtm(ue,un,z,la>=0,out double bla,out double blo);
    Check(GeoProj.UtmEpsg(z,la>=0)==ep && Math.Abs(ue-E)<0.005 && Math.Abs(un-Nn)<0.005 && Math.Abs(bla-la)<1e-7 && Math.Abs(blo-lo)<1e-7, $"UTM {ep}: {ue:F3},{un:F3} (round trip dlat {bla-la:E1} dlon {blo-lo:E1})");
}
var lf=new LocalFrame(40.4168,-3.7038); lf.ToLocal(40.4168,-3.7038,out double lx0,out double ly0); lf.Bounds(250,250,out double bs,out double bw,out double bn,out double be);
Check(Math.Abs(lx0)<1e-6 && Math.Abs(ly0)<1e-6 && bs<40.4168 && bn>40.4168 && Math.Abs((bn-bs)*111000-500)<10, "local frame centred, bounds ~500 m");
string fx(string f)=>System.IO.Path.Combine(AppContext.BaseDirectory,"fixtures",f);
var dem=new GeoTiff(new FileByteSource(fx("dem_f32_tiled_pred3.tif")));
var dw=dem.ReadWindow(10,20,30,25); bool demOk=true; for(int r=0;r<25;r++) for(int c=0;c<30;c++){ double ex=100+(c+10)*0.5+(r+20)*0.25; if(Math.Abs(dw[r*30+c]-ex)>1e-4) demOk=false; }
Check(dem.Width==70 && dem.Height==50 && demOk && double.IsNaN(dem.ReadWindow(4,3,1,1)[0]) && Math.Abs(dem.OriginX+3.71)<1e-12 && Math.Abs(dem.OriginY-40.42)<1e-12, "GeoTIFF float32 tiled + DEFLATE + predictor 3 + NoData");
var edge=dem.ReadWindow(65,45,10,10); Check(Math.Abs(edge[0]-(100+65*0.5+45*0.25))<1e-4 && double.IsNaN(edge[9*10+9]), "GeoTIFF window past image edge -> NaN");
var lc=new GeoTiff(new FileByteSource(fx("lc_u8_strip_pred2.tif"))); var lw=lc.ReadWindow(0,0,70,50); bool lcOk=true; for(int r=0;r<50;r++) for(int c=0;c<70;c++) if(lw[r*70+c]!=(c*3+r*7)%256) lcOk=false;
Check(lcOk, "GeoTIFF uint8 strips + DEFLATE + predictor 2");
var s16=new GeoTiff(new FileByteSource(fx("s16_be_big.tif"))); var sw16=s16.ReadWindow(0,0,70,50); bool sOk=true; for(int r=0;r<50;r++) for(int c=0;c<70;c++) if(sw16[r*70+c]!=c*10-r*20) sOk=false;
Check(sOk, "BigTIFF big-endian int16 tiles, uncompressed, negatives");
var mos=new RasterMosaic(); mos.Add(dem,40.38,-3.70,40.41,-3.67);
Check(Math.Abs(mos.Bilinear(-3.71+0.0205,40.42-0.0305)-(100+20*0.5+30*0.25))<1e-4 && Math.Abs(mos.Bilinear(-3.71+0.0210,40.42-0.0305)-(100+20.5*0.5+30*0.25))<1e-4, "mosaic bilinear at pixel centre and midway");
Check(SiteSources.CopernicusDemUrl(40,-4).EndsWith("Copernicus_DSM_COG_10_N40_00_W004_00_DEM/Copernicus_DSM_COG_10_N40_00_W004_00_DEM.tif") && SiteSources.CopernicusDemUrl(-24,-47).Contains("S24_00_W047") && SiteSources.WorldCoverUrl(39,-6).EndsWith("v200_N39W006_Map.tif") && SiteSources.Tiles(40.9,-0.1,41.1,0.1,1).Count==4 && SiteSources.Tiles(40.9,-0.1,41.1,0.1,3)[0]==(39,-3), "tile URLs and tile cover");
var ob=new List<OsmBuilding>(); var ot=new List<OsmTree>(); OsmParse.Parse(System.IO.File.ReadAllText(fx("overpass.json")),9,3,ob,ot);
Check(ob.Count==3 && ob[0].Height==12 && ob[1].Height==16.5 && ob[1].MinHeight==3 && ob[2].Height==9 && !ob[2].HeightFromTags && ob[2].Outers.Count==1 && ob[2].Outers[0].Count==5 && ob[2].Inners.Count==1, "OSM buildings: height, levels+roof, min_height, default, stitched relation with hole");
Check(ot.Count==1 && Math.Abs(ot[0].Height-9.144)<1e-9 && ot[0].CrownDiameter==6.5 && ot[0].Species=="Platanus", "OSM trees: feet, decimal comma, genus");
Check(double.IsNaN(OsmParse.ParseLength("tall")) && OsmParse.ParseLength("12;15")==12 && Math.Abs(OsmParse.ParseLength("10'6\"")-3.2004)<1e-9, "OSM length parsing");
Console.WriteLine(fails==0?"ALL PASS":$"{fails} FAILED"); return fails;
