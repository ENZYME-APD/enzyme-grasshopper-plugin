using System;
using System.Collections.Generic;

namespace Enzyme.Utils
{
    /// <summary>
    /// Flow routing on any terrain graph (mesh topology vertices or grid cells). Rhino-free.
    ///
    /// Depressions are filled with Priority-Flood (Barnes, Lehman &amp; Mulla 2014, "Priority-Flood: An
    /// optimal depression-filling and watershed-labeling algorithm for digital elevation models",
    /// Computers &amp; Geosciences 62): the flood starts at the outlets (the terrain's open edge) and
    /// grows inward through the lowest node first, raising every node to at least the level it was
    /// reached from. A node then drains by steepest descent on the filled surface; on flats and inside
    /// filled pits it drains to the node that flooded it, so every node reaches an outlet and the
    /// routing has no cycles (each step lowers the (filled level, flood order) key).
    /// </summary>
    public static class Hydrology
    {
        public sealed class Result
        {
            /// <summary>Filled (pit-free) level per node.</summary>
            public double[] Filled;
            /// <summary>Downstream node, -1 for an outlet.</summary>
            public int[] Down;
            /// <summary>Nodes from upstream to downstream (process in this order to accumulate).</summary>
            public int[] UpToDown;
            /// <summary>Outlet node each node drains to (catchment label).</summary>
            public int[] Outlet;
        }

        /// <param name="z">Ground level per node.</param>
        /// <param name="neighbours">Adjacency per node.</param>
        /// <param name="edgeLength">Optional length of each neighbour edge (same layout); slope = drop / length. Null = drop only.</param>
        /// <param name="outlet">True for nodes where water leaves the model (open edge). If none, the lowest node.</param>
        /// <param name="fill">False = plain steepest descent on the raw levels (pits keep their water, Down = -1 there).</param>
        public static Result Route(double[] z, int[][] neighbours, double[][] edgeLength, bool[] outlet, bool fill = true)
        {
            int n = z.Length;
            var filled = (double[])z.Clone();
            var down = new int[n];
            var order = new int[n];          // flood order (pop rank)
            var parent = new int[n];
            for (int i = 0; i < n; i++) { down[i] = -1; parent[i] = -1; order[i] = i; }

            if (fill && n > 0)
            {
                for (int i = 0; i < n; i++) order[i] = -1;
                var heap = new MinHeap(n);
                var seen = new bool[n];
                int rank = 0;
                // Each disconnected part floods from its own outlets (or its lowest node).
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int start = 0; start < n; start++)
                    {
                        if (seen[start]) continue;
                        if (pass == 0) { if (outlet == null || !outlet[start]) continue; heap.Push(filled[start], start); seen[start] = true; continue; }
                        // pass 1: a part with no outlet; seed at its lowest node
                        int low = LowestInPart(start, z, neighbours, seen);
                        heap.Push(filled[low], low); seen[low] = true;
                        Flood(heap, filled, neighbours, seen, parent, order, ref rank);
                    }
                    if (pass == 0) Flood(heap, filled, neighbours, seen, parent, order, ref rank);
                }
            }

            for (int i = 0; i < n; i++)
            {
                double best = 0; int target = -1;
                for (int k = 0; k < neighbours[i].Length; k++)
                {
                    int nb = neighbours[i][k];
                    double drop = filled[i] - filled[nb];
                    if (drop <= 0) continue;
                    double s = edgeLength == null ? drop : drop / Math.Max(1e-9, edgeLength[i][k]);
                    if (s > best) { best = s; target = nb; }
                }
                down[i] = target >= 0 ? target : (fill ? parent[i] : -1);
            }

            var idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            // Upstream first: highest filled level, then latest flood order. Every Down step lowers this key.
            Array.Sort(idx, (a, b) =>
            {
                int c = filled[b].CompareTo(filled[a]);
                return c != 0 ? c : order[b].CompareTo(order[a]);
            });
            var outletOf = new int[n];
            for (int k = n - 1; k >= 0; k--)
            {
                int i = idx[k];
                outletOf[i] = down[i] < 0 ? i : outletOf[down[i]];
            }
            return new Result { Filled = filled, Down = down, UpToDown = idx, Outlet = outletOf };
        }

        private static void Flood(MinHeap heap, double[] filled, int[][] nbrs, bool[] seen, int[] parent, int[] order, ref int rank)
        {
            while (heap.Count > 0)
            {
                int c = heap.Pop();
                order[c] = rank++;
                foreach (int nb in nbrs[c])
                {
                    if (seen[nb]) continue;
                    seen[nb] = true;
                    if (filled[nb] < filled[c]) filled[nb] = filled[c];
                    parent[nb] = c;
                    heap.Push(filled[nb], nb);
                }
            }
        }

        private static int LowestInPart(int start, double[] z, int[][] nbrs, bool[] seen)
        {
            var visited = new HashSet<int> { start };
            var st = new Stack<int>(); st.Push(start);
            int low = start;
            while (st.Count > 0)
            {
                int c = st.Pop();
                if (z[c] < z[low]) low = c;
                foreach (int nb in nbrs[c]) if (!seen[nb] && visited.Add(nb)) st.Push(nb);
            }
            return low;
        }

        /// <summary>Accumulates a per-node quantity (rain, area) downstream.</summary>
        public static double[] Accumulate(Result r, double[] own)
        {
            var acc = (double[])own.Clone();
            foreach (int i in r.UpToDown)
                if (r.Down[i] >= 0) acc[r.Down[i]] += acc[i];
            return acc;
        }

        /// <summary>Groups wet nodes (depth &gt; minDepth) into ponds; returns pond id per node (-1 dry) and count.</summary>
        public static int[] Ponds(double[] z, double[] filled, int[][] neighbours, double minDepth, out int count)
        {
            int n = z.Length;
            var id = new int[n];
            for (int i = 0; i < n; i++) id[i] = -1;
            count = 0;
            var stack = new Stack<int>();
            for (int i = 0; i < n; i++)
            {
                if (id[i] >= 0 || filled[i] - z[i] <= minDepth) continue;
                id[i] = count; stack.Push(i);
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    foreach (int nb in neighbours[c])
                        if (id[nb] < 0 && filled[nb] - z[nb] > minDepth && Math.Abs(filled[nb] - filled[c]) < 1e-9)
                        { id[nb] = count; stack.Push(nb); }
                }
                count++;
            }
            return id;
        }

        private sealed class MinHeap
        {
            private double[] _k; private int[] _v; private long[] _seq; private long _next;
            public int Count;
            public MinHeap(int cap) { cap = Math.Max(4, cap); _k = new double[cap]; _v = new int[cap]; _seq = new long[cap]; }
            private bool Less(int a, int b) => _k[a] < _k[b] || (_k[a] == _k[b] && _seq[a] < _seq[b]);
            private void Swap(int a, int b)
            {
                double k = _k[a]; _k[a] = _k[b]; _k[b] = k;
                int v = _v[a]; _v[a] = _v[b]; _v[b] = v;
                long s = _seq[a]; _seq[a] = _seq[b]; _seq[b] = s;
            }
            public void Push(double key, int val)
            {
                if (Count == _k.Length)
                {
                    Array.Resize(ref _k, Count * 2); Array.Resize(ref _v, Count * 2); Array.Resize(ref _seq, Count * 2);
                }
                int i = Count++;
                _k[i] = key; _v[i] = val; _seq[i] = _next++;
                while (i > 0) { int p = (i - 1) / 2; if (!Less(i, p)) break; Swap(i, p); i = p; }
            }
            public int Pop()
            {
                int top = _v[0];
                Count--;
                if (Count > 0)
                {
                    _k[0] = _k[Count]; _v[0] = _v[Count]; _seq[0] = _seq[Count];
                    int i = 0;
                    while (true)
                    {
                        int l = 2 * i + 1, r = l + 1, m = i;
                        if (l < Count && Less(l, m)) m = l;
                        if (r < Count && Less(r, m)) m = r;
                        if (m == i) break;
                        Swap(i, m); i = m;
                    }
                }
                return top;
            }
        }
    }
}
