using System;
using System.Runtime.CompilerServices;

namespace Pdf417
{
    /// <summary>
    /// Create a bar code in pdf417 format
    /// According to the description from here https://grandzebu.net/informatique/codbar-en/pdf417.htm
    /// https://github.com/harbour/core/blob/master/contrib/hbzebra/pdf417.c
    /// </summary>
    public struct Barcode
    {
        /// <summary>
        /// Internal image view
        /// </summary>
        public readonly MonoCanvas Canvas;

        /// <summary>
        /// Number of lines barcode
        /// </summary>
        public int RowsCount => _rows;

        /// <summary>
        /// The number of columns in the modules
        /// </summary>
        public int ColumnsCount => (_dataColumns + 4) * 17 + 1;

        /// <summary>
        /// Number of lines barcode
        /// </summary>
        private readonly int _rows;

        /// <summary>
        /// Number of data columns
        /// </summary>
        private readonly int _dataColumns;

        /// <summary>
        /// Internal representation of barcode data as an array of bit vectors
        /// </summary>
        private readonly BitVector[] _internalData;

        /// <summary>
        /// Barcode settings
        /// </summary>
        private readonly Settings _settings;

        /// <summary>
        /// Left indicator
        /// </summary>
        private readonly BitVector[] _leftIndicator;

        /// <summary>
        /// Right indicator
        /// </summary>
        private readonly BitVector[] _rightIndicator;

        /// <summary>
        /// PDF417 word length
        /// </summary>
        private const int WordLen = 17;

        /// <summary>
        /// Maximum number of code words to fit in the barcode
        /// </summary>
        private const int MaxCodeWords = 925;

        /// <summary>
        /// Bit Pattern - Start Pattern
        /// </summary>
        private static readonly BitVector StartPattern = new BitVector(0b11111111010101000UL, true);

        /// <summary>
        /// Bit representation - Stop Pattern
        /// </summary>
        private static readonly BitVector StopPattern = new BitVector(0b11111110100010100UL, true);

        /// <summary>
        /// Creates a new instance <see cref="Barcode"/>, with data from the byte array
        /// </summary>
        /// <param name="input">Barcode encoding data</param>
        /// <param name="settings">Barcode settings</param>
        public Barcode(byte[] input, Settings settings) : this()
        {
            _settings = settings;

            // We get an array of code words from the input
            (var data, int rdl) = GetDataFromBytes(input);

            // Determine the level of error correction, if it is not specified
            _settings.CorrectionLevel  = DetermineCorrectionLevel(settings.CorrectionLevel, rdl);

            // The total number of significant code words
            // Length + Data + Corrections
            int cl = 2 << (int) _settings.CorrectionLevel;
            int cwCount = 1 + rdl + cl;

            // Create a code word store
            (_rows, _dataColumns, _internalData) = CreateDataStorage(cwCount, settings.AspectRatio);

            // Fill in the indicators
            _leftIndicator = new BitVector[_rows];
            _rightIndicator = new BitVector[_rows];
            FillIndicators();

            // Data block length
            data[0] = _internalData.Length - cl;

            // Fill the Void
            for (int i = rdl; i < data[0]; i++)
                data[i] = 900;

            // Data
            for (int i = 0; i < data[0]; i++)
                _internalData[i] = new BitVector(Tables.LowLevel[(i / _dataColumns) % 3][data[i]], true);

            // Error correction
            var corrections = GetReedSolomonCorrections(data, data[0]);
            Array.Copy(corrections, 0, _internalData, data[0], corrections.Length);

            Canvas = FillCanvas();
        }

        /// <summary>
        /// Creates a new instance <see cref="Barcode"/>, with data from the string
        /// </summary>
        /// <param name="input">Barcode encoding data</param>
        /// <param name="settings">Barcode settings</param>
        public Barcode(string input, Settings settings) : this()
        {
            _settings = settings;

            // We get an array of code words from the input
            (var data, int rdl) = GetDataFromText(input);

            // Determine the level of error correction, if it is not specified
            _settings.CorrectionLevel  = DetermineCorrectionLevel(settings.CorrectionLevel, rdl);

            // The total number of significant code words
            // Length + Data + Corrections
            int cl = 2 << (int) _settings.CorrectionLevel;
            int cwCount = 1 + rdl + cl;

            // Create a code word store
            (_rows, _dataColumns, _internalData) = CreateDataStorage(cwCount, settings.AspectRatio);

            // Fill in the indicators
            _leftIndicator = new BitVector[_rows];
            _rightIndicator = new BitVector[_rows];
            FillIndicators();

            // Length of data block
            data[0] = _internalData.Length - cl;

            // Fill the Void
            for (int i = rdl; i < data[0]; i++)
                data[i] = 900;

            // Data
            for (int i = 0; i < data[0]; i++)
                _internalData[i] = new BitVector(Tables.LowLevel[(i / _dataColumns) % 3][data[i]], true);

            // Error correction
            var corrections = GetReedSolomonCorrections(data, data[0]);
            Array.Copy(corrections, 0, _internalData, data[0], corrections.Length);

            Canvas = FillCanvas();
        }

        /// <summary>
        /// Fill <see cref="_internalData"/> bytes
        /// </summary>
        /// <param name="input">Data in the form of an array of bytes</param>
        /// <returns>Data in the form of an array of code words, and the actual data length</returns>
        private (int[], int) GetDataFromBytes(byte[] input)
        {
            int len = input.Length;

            int dl = (len / 6) * 5 + len % 6 + 1;
            int[] data = new int[dl + 8];

            var mode = len == 1
                ? ControlChar.ShiftToByte
                : (len % 6 == 0 ? ControlChar.SwitchToByteMod6 : ControlChar.SwitchToByte);
            data[1] = (int) mode;

            int ipos = 0, opos = 2;
            for (int i = 0; i < input.Length / 6; i++)
            {
                ulong s = 0;
                for (uint j = 0; j < 6; j++, ipos++)
                    s += input[ipos] * Pow(256, 5 - j);

                for (int j = 0, b = opos; j < 5; j++, opos++, s /= 900)
                    data[b + 4 - j] = (int)(s % 900);
            }

            for (; ipos < len; opos++, ipos++)
                data[opos] = input[ipos];

            return (data, opos);
        }

        /// <summary>
        /// Get data from text
        /// </summary>
        /// <param name="input">ASCII string data</param>
        /// <returns>Data in the form of an array of code words, and the actual data length</returns>
        private (int[], int) GetDataFromText(string input)
        {
            const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ ";
            const string lower = "abcdefghijklmnopqrstuvwxyz ";
            const string mixed = "0123456789&\r\t,:#-.$/+%*=^? ";
            const string punct = ";<>@[\\]_`~!\r\t,:\n-.$/\"|*()\0{}'";
            const int utll = 27, utml = 28, utps = 29;
            const int ltus = 27, ltml = 28, ltps = 29;
            const int /*mtpl = 25,*/ mtll = 27, mtul = 28, mtps = 29;
            //const int ptul = 29;

            char mode = 'u'; // Current mode{ u | l | m | p }
            // Allocate a buffer for the formation of preliminary data of size 8 + input.Length
            int[] pre = new int[input.Length + 8];
            int pos = 1, cur = 0, pp = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                switch (mode)
                {
                    case 'u': // upper
                    {
                        int k = upper.IndexOf(c);
                        if (k >= 0) Push(k);
                        else
                        {
                            k = lower.IndexOf(c);
                            if (k >= 0)
                            {
                                mode = 'l';
                                Push(utll);
                                Push(k);
                                continue;
                            }

                            k = mixed.IndexOf(c);
                            if (k >= 0)
                            {
                                mode = 'm';
                                Push(utml);
                                Push(k);
                                continue;
                            }

                            k = punct.IndexOf(c);
                            if (k >= 0)
                            {
                                Push(utps);
                                Push(k);
                                continue;
                            }

                            throw new IndexOutOfRangeException($"Index not found for [{c}]");
                        }

                        break; // upper
                    }

                    case 'l': // lower
                    {
                        int k = lower.IndexOf(c);
                        if (k >= 0) Push(k);
                        else
                        {
                            k = upper.IndexOf(c);
                            if (k >= 0)
                            {
                                if (input.Length > i + 1 && upper.IndexOf(input[i + 1]) >= 0)
                                {
                                    mode = 'u';
                                    Push(ltml);
                                    Push(mtul);
                                    Push(k);
                                    continue;
                                }

                                Push(ltus);
                                Push(k);
                                continue;
                            }

                            k = mixed.IndexOf(c);
                            if (k >= 0)
                            {
                                mode = 'm';
                                Push(ltml);
                                Push(k);
                                continue;
                            }

                            k = punct.IndexOf(c);
                            if (k >= 0)
                            {
                                Push(ltps);
                                Push(k);
                                continue;
                            }

                            throw new IndexOutOfRangeException($"Index not found for [{c}]");
                        }

                        break; // lower
                    }

                    case 'm': // mixed
                    {
                        int k = mixed.IndexOf(c);
                        if (k >= 0) Push(k);
                        else
                        {
                            k = upper.IndexOf(c);
                            if (k >= 0)
                            {
                                mode = 'u';
                                Push(mtul);
                                Push(k);
                                continue;
                            }

                            k = lower.IndexOf(c);
                            if (k >= 0)
                            {
                                mode = 'l';
                                Push(mtll);
                                Push(k);
                                continue;
                            }

                            k = punct.IndexOf(c);
                            if (k >= 0)
                            {
                                Push(mtps);
                                Push(k);
                                continue;
                            }

                            throw new IndexOutOfRangeException($"Index not found for [{c}]");
                        }

                        break; // mixed
                    }
                }

                void Push(int val)
                {
                    if (pp % 2 == 0) cur = val * 30;
                    else pre[pos++] = cur + val;

                    pp++;
                }
            }

            if (pp % 2 == 1) pre[pos++] = cur + 29;

            return (pre, pos);
        }

        //        /// <summary>
        //        /// Fill <see cref="_internalData"/> in numbers
        //        /// </summary>
        //        /// <param name="input">Data as a string with a decimal number</param>
        //        private void FillDataNumeric(byte[] input)
        //        {
        //            throw new NotImplementedException();
        //        }

        /// <summary>
        /// Filling the Reed-Solomon codes (the array will be flipped)
        /// </summary>
        /// <param name="data">Data array</param>
        /// <param name="length">Data block length</param>
        private BitVector[] GetReedSolomonCorrections(int[] data, int length)
        {
            const int module = 929;
            int k = 2 << (int)_settings.CorrectionLevel;
            int[] c = new int[k];
            ushort[] a = GetFactors();
            BitVector[] ret = new BitVector[k];

            for (int i = 0; i < length; i++)
            {
                int t = (data[i] + c[k - 1]) % module;
                for (int j = k - 1; j >= 0; j--)
                    if (j == 0)
                        c[j] = (module - (t * a[j]) % module) % module;
                    else
                        c[j] = (c[j - 1] + module - (t * a[j]) % module) % module;
            }

            for (int j = 0; j < k; j++)
                if (c[j] != 0) c[j] = module - c[j];

            for (int i = 0; i < k; i++)
                ret[i] = new BitVector(Tables.LowLevel[((i + length) / _dataColumns) % 3][c[k - i - 1]], true);

            return ret;
        }

        /// <summary>
        /// Fill indicators
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FillIndicators()
        {
            int x1 = (_rows - 1) / 3;
            int x2 = (int) _settings.CorrectionLevel * 3 + (_rows - 1) % 3;
            int x3 = _dataColumns - 1;
            for (int i = 0; i < _rows; i++)
            {
                int t = i % 3;
                int xleft = t == 0 ? x1 : (t == 1 ? x2 : x3);
                int xright = t == 0 ? x3 : (t == 1 ? x1 : x2);
                _leftIndicator[i] = new BitVector(Tables.LowLevel[t][(i / 3) * 30 + xleft], true);
                _rightIndicator[i] = new BitVector(Tables.LowLevel[t][(i / 3) * 30 + xright], true);
            }
        }

        /// <summary>
        /// Fill canvas for drawing with PDF417 barcode image
        /// </summary>
        private MonoCanvas FillCanvas()
        {
            var s = _settings;
            var canvas = new MonoCanvas(
                (4 + _dataColumns) * WordLen * s.ModuleWidth + s.QuietZone * 2 + 1,
                _rows * s.YHeight * s.ModuleWidth + s.QuietZone * 2);

            for (int i = 0; i < _rows; i++)
            {
                int j = 0;

                // StartPattern
                for (int k = 0; k < WordLen; k++, j++) DrawModule(StartPattern[k]);

                // Left row indicator
                for (int k = 0; k < WordLen; k++, j++) DrawModule(_leftIndicator[i][k]);

                // Data
                for (int l = 0; l < _dataColumns; l++)
                for (int k = 0; k < WordLen; k++, j++)
                    DrawModule(_internalData[l + i * _dataColumns][k]);

                // Right row indicator
                for (int k = 0; k < WordLen; k++, j++) DrawModule(_rightIndicator[i][k]);

                // StopPattern
                for (int k = 0; k < WordLen; k++, j++) DrawModule(StopPattern[k]);

                DrawModule(true);

                void DrawModule(bool color)
                {
                    for (int x = 0; x < s.ModuleWidth; x++)
                    for (int y = 0; y < s.ModuleWidth * s.YHeight; y++)
                        canvas[s.QuietZone + j * s.ModuleWidth + x,
                            s.QuietZone + i * s.ModuleWidth * s.YHeight + y] = !color;
                }
            }

            return canvas;
        }

        /// <summary>
        /// Determines the level of error correction
        /// </summary>
        /// <param name="correctionLevel">Set Correction Level</param>
        /// <param name="cwDataCount">Number of code words with data</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static CorrectionLevel DetermineCorrectionLevel(CorrectionLevel correctionLevel, int cwDataCount)
        {
            var ret = correctionLevel;
            if (ret == CorrectionLevel.Auto)
            {
                switch (cwDataCount)
                {
                    case int x when x <= 40 || (x > 911 && x <= 919):
                        ret = CorrectionLevel.Level2;
                        break;

                    case int x when (x > 40 && x <= 160) || (x > 895 && x <= 911):
                        ret = CorrectionLevel.Level3;
                        break;

                    case int x when (x > 160 && x <= 320) || (x > 863 && x <= 895):
                        ret = CorrectionLevel.Level4;
                        break;

                    case int x when x > 320 && x <= 863:
                        ret = CorrectionLevel.Level5;
                        break;

                    case int x when x > 919 && x <= 923:
                        ret = CorrectionLevel.Level1;
                        break;

                    default:
                        ret = CorrectionLevel.Level0;
                        break;
                }
            }

            return ret;
        }

        /// <summary>
        /// Create a two-dimensional array with code words based on size
        /// </summary>
        /// <param name="cwCount">Number of code words</param>
        /// <param name="aspectRatio">The ratio of width to height</param>
        /// <returns>Empty array for storing data and its size</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (int rows, int dataColumns, BitVector[] data) CreateDataStorage(int cwCount, double aspectRatio)
        {
            if (cwCount > MaxCodeWords)
                throw new ArgumentException($"Codewords count {cwCount} more than maximum {MaxCodeWords}");

            // Calculate the number of rows and columns
            // Width = 1 + 17 * (4 + dataColumns (x)) of modules, Height = rows (y) * _yHeight (h) of modules
            // x * y = c, where cwCount <= c <cwCount + x => y = (c / x) * aspectRatio (a)
            // 69 + 17x = ahc / x => 17x ^ 2 + 69x - ahc = 0 =>
            // x = (sqrt (4761 + 68ahc) -69) / (2 * 17) - the number of columns with data

            int x = (int) Math.Ceiling((Math.Sqrt(4761d + 68 * aspectRatio * _settings.YHeight * cwCount) - 69) / 34);
            int y = cwCount / x + (cwCount % x == 0 ? 0 : 1);

            return (y, x, new BitVector[x * y]);
        }

        /// <summary>
        /// Fast integer exponentiation
        /// </summary>
        /// <param name="n">The number to be raised to a power</param>
        /// <param name="e">Power</param>
        /// <remarks>https://ru.wikibooks.org/wiki/Реализации_алгоритмов/Быстрое_возведение_в_степень</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong Pow(ulong n, uint e)
        {
            ulong ret = 1UL;
            while (e != 0)
            {
                if (e % 2 == 1) ret *= n;
                n *= n;
                e >>= 1;
            }

            return ret;
        }

        /// <summary>
        /// Get a table with polynomial solutions for Reed-Solomon codes for our case
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort[] GetFactors()
        {
            switch (_settings.CorrectionLevel)
            {
                case CorrectionLevel.Level0:
                    return Tables.ReedSolomon00;

                case CorrectionLevel.Level1:
                    return Tables.ReedSolomon01;

                case CorrectionLevel.Level2:
                    return Tables.ReedSolomon02;

                case CorrectionLevel.Level3:
                    return Tables.ReedSolomon03;

                case CorrectionLevel.Level4:
                    return Tables.ReedSolomon04;

                case CorrectionLevel.Level5:
                    return Tables.ReedSolomon05;

                case CorrectionLevel.Level6:
                    return Tables.ReedSolomon06;

                case CorrectionLevel.Level7:
                    return Tables.ReedSolomon07;

                case CorrectionLevel.Level8:
                    return Tables.ReedSolomon08;

                default:
                    throw new ArgumentOutOfRangeException(nameof(_settings.CorrectionLevel));
            }
        }
    }
}
