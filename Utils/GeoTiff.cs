using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace Enzyme.Utils
{
    /// <summary>Random-access byte source: a local file, or a remote file read with HTTP range requests.</summary>
    public interface IByteSource
    {
        /// <summary>Returns up to <paramref name="length"/> bytes starting at <paramref name="offset"/>.</summary>
        byte[] Read(long offset, int length);
    }

    public sealed class FileByteSource : IByteSource
    {
        private readonly string _path;
        public FileByteSource(string path) { _path = path; }
        public byte[] Read(long offset, int length)
        {
            using (var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (offset >= fs.Length) return new byte[0];
                fs.Seek(offset, SeekOrigin.Begin);
                int n = (int)Math.Min(length, fs.Length - offset);
                var buf = new byte[n];
                int got = 0;
                while (got < n) { int r = fs.Read(buf, got, n - got); if (r <= 0) break; got += r; }
                return buf;
            }
        }
    }

    /// <summary>
    /// Minimal GeoTIFF / Cloud-Optimised GeoTIFF reader for single-band rasters (DEMs, land cover).
    /// Rhino-free so it can be unit tested. Supports classic and BigTIFF, both byte orders, tiles and
    /// strips, no compression or DEFLATE, predictors 1/2/3, 8/16/32-bit integers and 32/64-bit floats.
    /// Only the first (full-resolution) image is read. Reads are lazy, so with an HTTP range source
    /// only the header and the tiles covering the requested window are downloaded.
    /// </summary>
    public sealed class GeoTiff
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        /// <summary>Longitude / easting of the top-left corner of the top-left pixel.</summary>
        public double OriginX { get; private set; }
        /// <summary>Latitude / northing of the top-left corner of the top-left pixel.</summary>
        public double OriginY { get; private set; }
        public double PixelSizeX { get; private set; }
        public double PixelSizeY { get; private set; }
        public double NoData { get; private set; } = double.NaN;
        public bool HasGeoreference { get; private set; }

        private readonly IByteSource _src;
        private bool _le, _big;
        private int _bps, _spp, _fmt, _comp, _pred;
        private int _chunkW, _chunkH, _chunksAcross;
        private long[] _offsets, _counts;
        private readonly Dictionary<long, byte[]> _blocks = new Dictionary<long, byte[]>();
        private const int BlockSize = 65536;

        public GeoTiff(IByteSource source)
        {
            _src = source;
            ParseHeader();
        }

        // ------------------------------------------------------------------ public API

        /// <summary>
        /// Reads a window of pixels (row-major, w*h). Pixels outside the image or equal to NoData are NaN.
        /// </summary>
        public double[] ReadWindow(int x0, int y0, int w, int h)
        {
            var outv = new double[w * h];
            for (int i = 0; i < outv.Length; i++) outv[i] = double.NaN;
            int cx0 = Math.Max(0, x0) / _chunkW, cx1 = (Math.Min(Width, x0 + w) - 1) / _chunkW;
            int cy0 = Math.Max(0, y0) / _chunkH, cy1 = (Math.Min(Height, y0 + h) - 1) / _chunkH;
            if (x0 + w <= 0 || y0 + h <= 0 || x0 >= Width || y0 >= Height) return outv;

            for (int cy = cy0; cy <= cy1; cy++)
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    int idx = cy * _chunksAcross + cx;
                    int rows = Math.Min(_chunkH, Height - cy * _chunkH);
                    double[] chunk = DecodeChunk(idx, rows);
                    for (int r = 0; r < rows; r++)
                    {
                        int gy = cy * _chunkH + r;
                        if (gy < y0 || gy >= y0 + h) continue;
                        for (int c = 0; c < _chunkW; c++)
                        {
                            int gx = cx * _chunkW + c;
                            if (gx < x0 || gx >= x0 + w || gx >= Width) continue;
                            double v = chunk[r * _chunkW + c];
                            if (!double.IsNaN(NoData) && v == NoData) v = double.NaN;
                            outv[(gy - y0) * w + (gx - x0)] = v;
                        }
                    }
                }
            return outv;
        }

        /// <summary>Pixel column/row (fractional, pixel centres at .0) of a georeferenced point.</summary>
        public void ToPixel(double x, double y, out double col, out double row)
        {
            col = (x - OriginX) / PixelSizeX - 0.5;
            row = (OriginY - y) / PixelSizeY - 0.5;
        }

        // ------------------------------------------------------------------ header

        private void ParseHeader()
        {
            byte[] h = ReadBytes(0, 16);
            if (h.Length < 8) throw new InvalidDataException("Not a TIFF file (too short).");
            if (h[0] == 'I' && h[1] == 'I') _le = true;
            else if (h[0] == 'M' && h[1] == 'M') _le = false;
            else throw new InvalidDataException("Not a TIFF file.");
            int magic = U16(h, 2);
            long ifd;
            if (magic == 42) { _big = false; ifd = U32(h, 4); }
            else if (magic == 43) { _big = true; ifd = (long)U64(h, 8); }
            else throw new InvalidDataException("Unsupported TIFF version " + magic + ".");

            var tags = ReadIfd(ifd);

            Width = (int)Num(tags, 256);
            Height = (int)Num(tags, 257);
            _bps = (int)Num(tags, 258, 1);
            _spp = (int)Num(tags, 277, 1);
            _comp = (int)Num(tags, 259, 1);
            _pred = (int)Num(tags, 317, 1);
            _fmt = (int)Num(tags, 339, 1);
            int planar = (int)Num(tags, 284, 1);
            if (planar != 1 && _spp > 1) throw new NotSupportedException("Planar-separate multi-band TIFFs are not supported.");
            if (_comp != 1 && _comp != 8 && _comp != 32946)
                throw new NotSupportedException("TIFF compression " + _comp + " is not supported (only none and DEFLATE).");

            if (tags.ContainsKey(322))
            {
                _chunkW = (int)Num(tags, 322);
                _chunkH = (int)Num(tags, 323);
                _offsets = Longs(tags[324]);
                _counts = Longs(tags[325]);
            }
            else
            {
                _chunkW = Width;
                _chunkH = (int)Math.Min(Num(tags, 278, Height), Height);
                _offsets = Longs(tags[273]);
                _counts = Longs(tags[279]);
            }
            _chunksAcross = (Width + _chunkW - 1) / _chunkW;

            if (tags.ContainsKey(42113))
            {
                string nd = Ascii(tags[42113]).Trim('\0', ' ');
                if (double.TryParse(nd, NumberStyles.Float, CultureInfo.InvariantCulture, out double ndv)) NoData = ndv;
            }

            // Georeference: PixelScale + TiePoint (no rotation), or ModelTransformation.
            bool pixelIsPoint = false;
            if (tags.ContainsKey(34735))
            {
                long[] gk = Longs(tags[34735]);
                for (int i = 4; i + 3 < gk.Length; i += 4)
                    if (gk[i] == 1025 && gk[i + 1] == 0) pixelIsPoint = gk[i + 3] == 2;
            }
            if (tags.ContainsKey(33550) && tags.ContainsKey(33922))
            {
                double[] sc = Doubles(tags[33550]);
                double[] tp = Doubles(tags[33922]);
                PixelSizeX = sc[0];
                PixelSizeY = sc[1];
                OriginX = tp[3] - tp[0] * PixelSizeX;
                OriginY = tp[4] + tp[1] * PixelSizeY;
                HasGeoreference = true;
            }
            else if (tags.ContainsKey(34264))
            {
                double[] m = Doubles(tags[34264]);
                if (Math.Abs(m[1]) > 1e-12 || Math.Abs(m[4]) > 1e-12)
                    throw new NotSupportedException("Rotated GeoTIFFs are not supported.");
                PixelSizeX = m[0]; PixelSizeY = -m[5]; OriginX = m[3]; OriginY = m[7];
                HasGeoreference = true;
            }
            if (HasGeoreference && pixelIsPoint)
            {
                // Tie point refers to the pixel centre: shift to the pixel corner.
                OriginX -= PixelSizeX / 2.0;
                OriginY += PixelSizeY / 2.0;
            }
        }

        private sealed class Tag { public int Type; public long Count; public byte[] Data; }

        private Dictionary<int, Tag> ReadIfd(long offset)
        {
            int cntSize = _big ? 8 : 2, entSize = _big ? 20 : 12, inline = _big ? 8 : 4;
            byte[] c = ReadBytes(offset, cntSize);
            long n = _big ? (long)U64(c, 0) : U16(c, 0);
            byte[] e = ReadBytes(offset + cntSize, (int)(n * entSize));
            var tags = new Dictionary<int, Tag>();
            for (int i = 0; i < n; i++)
            {
                int p = i * entSize;
                int tag = U16(e, p), type = U16(e, p + 2);
                long count = _big ? (long)U64(e, p + 4) : U32(e, p + 4);
                int vpos = p + (_big ? 12 : 8);
                int size = TypeSize(type);
                if (size == 0) continue;
                long bytes = size * count;
                byte[] data;
                if (bytes <= inline) { data = new byte[bytes]; Array.Copy(e, vpos, data, 0, (int)bytes); }
                else
                {
                    long off = _big ? (long)U64(e, vpos) : U32(e, vpos);
                    data = ReadBytes(off, (int)bytes);
                }
                tags[tag] = new Tag { Type = type, Count = count, Data = data };
            }
            return tags;
        }

        private static int TypeSize(int t)
        {
            switch (t)
            {
                case 1: case 2: case 6: case 7: return 1;
                case 3: case 8: return 2;
                case 4: case 9: case 11: return 4;
                case 5: case 10: case 12: case 16: case 17: case 18: return 8;
                default: return 0;
            }
        }

        private double Num(Dictionary<int, Tag> t, int code, double def = double.NaN)
        {
            if (!t.TryGetValue(code, out Tag tag)) { if (double.IsNaN(def)) throw new InvalidDataException("Missing TIFF tag " + code + "."); return def; }
            return Longs(tag)[0];
        }

        private long[] Longs(Tag t)
        {
            var r = new long[t.Count];
            for (int i = 0; i < t.Count; i++)
            {
                switch (t.Type)
                {
                    case 1: case 7: r[i] = t.Data[i]; break;
                    case 3: r[i] = U16(t.Data, i * 2); break;
                    case 4: r[i] = U32(t.Data, i * 4); break;
                    case 16: case 18: r[i] = (long)U64(t.Data, i * 8); break;
                    default: throw new InvalidDataException("Unexpected TIFF tag type " + t.Type + ".");
                }
            }
            return r;
        }

        private double[] Doubles(Tag t)
        {
            var r = new double[t.Count];
            for (int i = 0; i < t.Count; i++)
                r[i] = t.Type == 12 ? TiffBits.Int64BitsToDouble((long)U64(t.Data, i * 8)) : Longs(t)[i];
            return r;
        }

        private static string Ascii(Tag t) => System.Text.Encoding.ASCII.GetString(t.Data);

        // ------------------------------------------------------------------ chunk decoding

        private double[] DecodeChunk(int idx, int rows)
        {
            int bytesPer = _bps / 8;
            int rowBytes = _chunkW * _spp * bytesPer;
            if (idx >= _offsets.Length || _counts[idx] == 0)
            {
                var empty = new double[_chunkW * _chunkH];
                for (int i = 0; i < empty.Length; i++) empty[i] = double.NaN;
                return empty;
            }
            byte[] raw = ReadBytes(_offsets[idx], (int)_counts[idx]);
            byte[] b = _comp == 1 ? raw : Inflate(raw, rowBytes * rows);
            if (b.Length < rowBytes * rows)
            {
                var pad = new byte[rowBytes * rows];
                Array.Copy(b, pad, b.Length);
                b = pad;
            }

            var vals = new double[_chunkW * _chunkH];
            var tmp = new byte[8];
            for (int r = 0; r < rows; r++)
            {
                int rowStart = r * rowBytes;
                if (_pred == 3)
                {
                    // Floating-point predictor: byte-wise differencing, then bytes stored MSB-plane first.
                    for (int i = _spp; i < rowBytes; i++) b[rowStart + i] = (byte)(b[rowStart + i] + b[rowStart + i - _spp]);
                    int wc = _chunkW * _spp;
                    for (int k = 0; k < _chunkW; k++)
                    {
                        int s = k * _spp; // first sample only
                        for (int j = 0; j < bytesPer; j++) tmp[bytesPer - 1 - j] = b[rowStart + j * wc + s];
                        vals[r * _chunkW + k] = bytesPer == 4 ? TiffBits.ToSingle(tmp, 0) : TiffBits.ToDouble(tmp, 0);
                    }
                    continue;
                }

                long prev = 0;
                for (int k = 0; k < _chunkW; k++)
                {
                    int p = rowStart + k * _spp * bytesPer;
                    double v;
                    if (_fmt == 3)
                    {
                        v = bytesPer == 4 ? TiffBits.Int32BitsToSingleCompat(U32raw(b, p)) : TiffBits.Int64BitsToDouble((long)U64(b, p));
                    }
                    else
                    {
                        long raw64;
                        switch (bytesPer)
                        {
                            case 1: raw64 = b[p]; break;
                            case 2: raw64 = U16(b, p); break;
                            case 4: raw64 = U32(b, p); break;
                            default: throw new NotSupportedException(_bps + "-bit integer rasters are not supported.");
                        }
                        if (_pred == 2)
                        {
                            if (k > 0) raw64 = (raw64 + prev) & (bytesPer == 4 ? 0xFFFFFFFFL : (1L << _bps) - 1);
                            prev = raw64;
                        }
                        if (_fmt == 2)
                        {
                            long sign = 1L << (_bps - 1);
                            if ((raw64 & sign) != 0) raw64 -= sign << 1;
                        }
                        v = raw64;
                    }
                    vals[r * _chunkW + k] = v;
                }
            }
            return vals;
        }

        private static byte[] Inflate(byte[] z, int expected)
        {
            // TIFF DEFLATE data is zlib-wrapped: skip the 2-byte header (no preset dictionary in practice).
            int start = (z.Length > 2 && (z[0] & 0x0F) == 8) ? 2 : 0;
            using (var ms = new MemoryStream(z, start, z.Length - start))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            using (var outMs = new MemoryStream(Math.Max(expected, 16)))
            {
                ds.CopyTo(outMs);
                return outMs.ToArray();
            }
        }

        // ------------------------------------------------------------------ byte access

        private byte[] ReadBytes(long offset, int length)
        {
            if (length <= 0) return new byte[0];
            if (length > BlockSize / 4) return _src.Read(offset, length);
            var res = new byte[length];
            int done = 0;
            while (done < length)
            {
                long pos = offset + done;
                long blk = pos / BlockSize;
                if (!_blocks.TryGetValue(blk, out byte[] data))
                {
                    data = _src.Read(blk * BlockSize, BlockSize);
                    _blocks[blk] = data;
                }
                int inBlk = (int)(pos - blk * BlockSize);
                int n = Math.Min(length - done, data.Length - inBlk);
                if (n <= 0) break;
                Array.Copy(data, inBlk, res, done, n);
                done += n;
            }
            return res;
        }

        private int U16(byte[] b, int p) => _le ? b[p] | (b[p + 1] << 8) : (b[p] << 8) | b[p + 1];
        private uint U32raw(byte[] b, int p) => _le
            ? (uint)(b[p] | (b[p + 1] << 8) | (b[p + 2] << 16) | (b[p + 3] << 24))
            : (uint)((b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3]);
        private long U32(byte[] b, int p) => U32raw(b, p);
        private ulong U64(byte[] b, int p)
        {
            ulong lo = U32raw(b, _le ? p : p + 4), hi = U32raw(b, _le ? p + 4 : p);
            return (hi << 32) | lo;
        }
    }

    internal static class TiffBits
    {
        public static float ToSingle(byte[] b, int p) => System.BitConverter.ToSingle(b, p);
        public static double ToDouble(byte[] b, int p) => System.BitConverter.ToDouble(b, p);
        public static double Int64BitsToDouble(long v) => System.BitConverter.Int64BitsToDouble(v);
        public static float Int32BitsToSingleCompat(uint v) => System.BitConverter.ToSingle(System.BitConverter.GetBytes(v), 0);
    }
}
