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
Console.WriteLine(fails==0?"ALL PASS":$"{fails} FAILED"); return fails;
