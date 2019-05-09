/**
(c) Nikolay Martyshchenko
*/

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace Pdf417
{
/// <summary>
    /// Array of bit values ​​for compact and efficient storage without having to calculate masks manually
    /// </summary>
    /// <remarks>
    /// Simplified (and optimized for 64-bit) analogue of the standard class <see cref = "System.Collections.BitArray" />
    ///
    /// Unlike the standard implementation, it does not support dynamic storage resizing.
    ///
    /// Internal view for storing values: an ulong array, so all values ​​are aligned based on this
    ///
    /// NOTE: Validation of hitting a given initial range of values ​​(0 .. length) is not performed to speed up operations,
    /// therefore possible boundary incorrect access to a non-existent index
    ///
    /// For example, with a length of 2 bits, access to bits 2..63 will also be considered valid, as well as to 0..1
    /// Error (out of range) in this case will be issued only when accessing bits with a number greater than 64
    ///
    /// For current use, this feature is not critical.
    ///
    /// (c) Nikolay Martyshchenko
    /// </remarks>
    [DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
    public struct BitVector
    {
        /// <summary>
        /// Number of bits per byte
        /// </summary>
        private const int BitsPerByte = 8;

        /// <summary>
        /// Number of bits per storage unit
        /// </summary>
        private const int BitsPerStorage = sizeof(ulong) * BitsPerByte;

        /// <summary>
        /// Array for storing bit values packed
        /// </summary>
        private readonly ulong[] _array;

        /// <summary>
        /// A sign that all bit values in the array are set to 0
        /// </summary>
        public bool IsFalseForAll => Array.TrueForAll(_array, item => item == 0UL);

        /// <summary>
        /// Debugger Mapping
        /// </summary>
        public string DebugDisplay => _array == null ? "null" : Convert.ToString((long)_array[0], 2);

        /// <summary>
        /// Initialize a new instance of the class. <see cref="BitVector" />
        /// </summary>
        /// <param name="length">Maximum number of processed bits</param>
        /// <param name="defaultValue">Default value for all bits</param>
        public BitVector(int length, bool defaultValue = false)
        {
            _array = new ulong[GetArrayLength(length, BitsPerStorage)];

            ulong fillValue = defaultValue ? unchecked ((ulong) -1) : 0UL;

            if (!defaultValue) return;

            for (int i = 0; i < _array.Length; i++)
            {
                _array[i] = fillValue;
            }
        }

        /// <summary>
        /// Initialize a new instance of the class. <see cref="BitVector" />.
        /// </summary>
        /// <param name="initValue">Values for bits</param>
        /// <param name="rotateBits">Turn bits</param>
        /// <remarks>Turning the bits is not entirely fair, because it does not shift to the right at completion,
        /// in this way, only the number of the bit that was filled is affected, and “trash” can remain from the tail.</remarks>
        public unsafe BitVector(ulong initValue, bool rotateBits)
        {
            _array = new ulong[1];

            if (!rotateBits)
                _array[0] = initValue;
            else
            {
                ulong r = initValue, v = initValue;
                for (v >>= 1; v != 0; v >>= 1)
                {
                    r <<= 1;
                    r |= v & 1;
                }

                fixed (void* dst = _array)
                    Buffer.MemoryCopy(&r, dst, sizeof(ulong), sizeof(ulong));
            }
        }

        /// <summary>
        /// Getting or setting the value of a bit at a given index
        /// </summary>
        /// <param name="index">Processed bit index</param>
        /// <returns>The value of the bit in the specified position</returns>
        public bool this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [Pure]
            get
            {
                return (_array[index/BitsPerStorage] & (1UL << (index%BitsPerStorage))) != 0UL;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value) _array[index/BitsPerStorage] |= (1UL << (index%BitsPerStorage));
                else _array[index/BitsPerStorage] &= ~(1UL << (index%BitsPerStorage));
            }
        }

        /// <summary>
        /// Reset all digits to 0
        /// </summary>
        public void Clear()
        {
            Array.Clear(_array, 0, _array.Length);
        }

        /// <summary>
        /// Setting all bits to the specified value
        /// </summary>
        /// <param name="value">Value to set all bits to</param>
        public void SetAll(bool value)
        {
            ulong fillValue = value ? unchecked((ulong)-1) : 0UL;

            for (int i = 0; i < _array.Length; i++)
            {
                _array[i] = fillValue;
            }
        }

        /// <summary>
        /// Used to calculate the required size of the array for storage <paramref name="n"/> values when storing a single item in <paramref name="div"/>
        /// </summary>
        /// <param name="n">Number of Stored Values</param>
        /// <param name="div">Capacity of one storage unit</param>
        /// <returns>The required size of the array to store a specified number of elements</returns>
        /// <remarks>
        /// (N + (div-1)) / div is actually calculated, but the formula is changed to ((n-1) / div) + 1 to avoid arithmetic overflow when calculating
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        private static int GetArrayLength(int n, int div)
        {
            return n > 0 ? (((n - 1) / div) + 1) : 0;
        }

        /// <summary>
        /// Initializing a bitmask state from a passed mask
        /// </summary>
        /// <param name="mask">Mask used as a source of state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(BitVector mask)
        {
            Array.Copy(mask._array, _array, _array.Length);
        }

        /// <summary>
        /// Copy data to byte array
        /// </summary>
        /// <param name="array">Received array</param>
        /// <param name="useMsb">Use MSB (https://en.wikipedia.org/wiki/Bit_numbering)</param>
        /// <remarks>(c) Oleg Krekov</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void CopyTo([NotNull] byte[] array, bool useMsb)
        {
            fixed (void* src = _array)
            fixed (void* dst = array)
                Buffer.MemoryCopy(src, dst, array.Length, array.Length);

            if (!useMsb) return;

            // Reverse bits in MSB
            for (var i = 0; i < array.Length; i++)
                array[i] = (byte) ((array[i] * 0x0202020202UL & 0x010884422010UL) % 1023);
        }

        /// <summary>
        /// Implementing the & operator (bitwise AND)
        /// </summary>
        /// <param name="lhs">Left argument</param>
        /// <param name="rhs">Right argument</param>
        /// <returns>
        /// Operation result
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BitVector operator &(BitVector lhs, BitVector rhs)
        {
            int length;
            int min;

            if (lhs._array.Length <= rhs._array.Length)
            {
                length = rhs._array.Length;
                min = lhs._array.Length;
            }
            else
            {
                length = lhs._array.Length;
                min = rhs._array.Length;
            }

            var r = new BitVector(length*BitsPerStorage);

            for (int i = 0; i < min; i++)
            {
                r._array[i] = lhs._array[i] & rhs._array[i];
            }

            return r;
        }
    }
}