using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace V_Max_Tool
{
    public partial class Form1 : Form
    {
        ///**************** These have been converted to c# ***********************
        ///* Name:        lz.c
        ///* Author:      Marcus Geelnard
        ///* Description: LZ77 coder/decoder implementation.
        ///* Reentrant:   Yes
        ///*
        ///* The LZ77 compression scheme is a substitutional compression scheme
        ///* proposed by Abraham Lempel and Jakob Ziv in 1977. It is very simple in
        ///* its design, and uses no fancy bit level compression.
        ///*
        ///* This is my first attempt at an implementation of a LZ77 code/decoder.
        ///*
        ///* The principle of the LZ77 compression algorithm is to store repeated
        ///* occurrences of strings as references to previous occurrences of the same
        ///* string. The point is that the reference consumes less space than the
        ///* string itself, provided that the string is long enough (in this
        ///* implementation, the string has to be at least 4 bytes long, since the
        ///* minimum coded reference is 3 bytes long). Also note that the term
        ///* "string" refers to any kind of byte sequence (it does not have to be
        ///* an ASCII string, for instance).
        ///*
        ///* The coder uses a brute force approach to finding string matches in the
        ///* history buffer (or "sliding window", if you wish), which is very, very
        ///* slow. I recon the complexity is somewhere between O(n^2) and O(n^3),
        ///* depending on the input data.
        ///*
        ///* There is also a faster implementation that uses a large working buffer
        ///* in which a "jump table" is stored, which is used to quickly find
        ///* possible string matches (see the source code for LZ_CompressFast() for
        ///* more information). The faster method is an order of magnitude faster,
        ///* but still quite slow compared to other compression methods.
        ///*
        ///* The upside is that decompression is very fast, and the compression ratio
        ///* is often very good.
        ///*
        ///* The reference to a string is coded as a (length,offset) pair, where the
        ///* length indicates the length of the string, and the offset gives the
        ///* offset from the current data position. To distinguish between string
        ///* references and literal strings (uncompressed bytes), a string reference
        ///* is preceded by a marker byte, which is chosen as the least common byte
        ///* symbol in the input data stream (this marker byte is stored in the
        ///* output stream as the first byte).
        ///*
        ///* Occurrences of the marker byte in the stream are encoded as the marker
        ///* byte followed by a zero byte, which means that occurrences of the marker
        ///* byte have to be coded with two bytes.
        ///*
        ///* The lengths and offsets are coded in a variable length fashion, allowing
        ///* values of any magnitude (up to 4294967295 in this implementation).
        ///*
        ///* With this compression scheme, the worst case compression result is
        ///* (257/256)*insize + 1.
        ///*
        ///*-------------------------------------------------------------------------
        ///* Copyright (c) 2003-2006 Marcus Geelnard
        ///*
        ///* This software is provided 'as-is', without any express or implied
        ///* warranty. In no event will the authors be held liable for any damages
        ///* arising from the use of this software.
        ///*
        ///* Permission is granted to anyone to use this software for any purpose,
        ///* including commercial applications, and to alter it and redistribute it
        ///* freely, subject to the following restrictions:
        ///*
        ///* 1. The origin of this software must not be misrepresented; you must not
        ///*    claim that you wrote the original software. If you use this software
        ///*    in a product, an acknowledgment in the product documentation would
        ///*    be appreciated but is not required.
        ///*
        ///* 2. Altered source versions must be plainly marked as such, and must not
        ///*    be misrepresented as being the original software.
        ///*
        ///* 3. This notice may not be removed or altered from any source
        ///*    distribution.
        ///*
        ///* Marcus Geelnard
        ///* marcus.geelnard at home.se
        ///*************************************************************************/


        /*************************************************************************
        * Constants used for LZ77 coding
        *************************************************************************/

        /* Maximum offset (can be any size < 2^31). Lower values give faster
           compression, while higher values gives better compression. The default
           value of 100000 is quite high. Experiment to see what works best for
           you. */

        //const int LZ_MAX_OFFSET = 8192;
        const int LZ_MAX_OFFSET = 100000;

        private static int LZ_ReadVarSize(ref int x, byte[] buf, int startPos)
        {
            int y = 0;
            int numBytes = 0;
            int b;

            // Read complete value (stop when byte contains zero in 8th bit)
            do
            {
                b = buf[startPos++];
                y = (y << 7) | (b & 0x7F);
                numBytes++;
            } while ((b & 0x80) != 0);

            // Store value in x
            x = y;

            // Return number of bytes read
            return numBytes;
        }

        private static unsafe int _LZ_WriteVarSize(uint x, byte* buf)
        {
            uint y;
            int numBytes, i, b;
        
            /* Determine number of bytes needed to store the number x */
            y = x >> 3;
            for (numBytes = 5; numBytes >= 2; --numBytes)
            {
                if ((y & 0xfe000000) != 0) break;
                y <<= 7;
            }
        
            /* Write all bytes, seven bits in each, with 8th bit set for all */
            /* but the last byte. */
            for (i = numBytes - 1; i >= 0; --i)
            {
                b = (int)((x >> (i * 7)) & 0x0000007f);
                if (i > 0)
                {
                    b |= 0x00000080;
                }
                *buf++ = (byte)b;
            }
        
            /* Return number of bytes written */
            return numBytes;
        }

        private static unsafe uint LZ_StringCompare(byte* ptr1, byte* ptr2, uint start, uint maxlength)
        {
            uint length = 0;
            for (uint i = start; i < maxlength; i++)
            {
                if (ptr1[i] != ptr2[i])
                {
                    break;
                }
                length++;
            }
            return length;
        }

        ///*************************************************************************
        ///*                            PUBLIC FUNCTIONS                            *
        ///*************************************************************************/
        ///
        ///
        ///*************************************************************************
        ///* LZ_Compress() - Compress a block of data using an LZ77 coder.
        ///*  in     - Input (uncompressed) buffer.
        ///*  out    - Output (compressed) buffer. This buffer must be 0.4% larger
        ///*           than the input buffer, plus one byte.
        ///*  insize - Number of input bytes.
        ///* The function returns the size of the compressed data.
        ///*************************************************************************/

        public static unsafe int LZ_Compress(byte* input, byte* output, uint insize)
        {
            byte marker, symbol;
            uint inpos = 0, outpos = 0, bytesleft = insize, i;
            uint maxoffset, offset, bestoffset;
            uint maxlength, length, bestlength;
            uint[] histogram = new uint[256];
            byte* ptr1, ptr2;

            // Do we have anything to compress?
            if (insize < 1)
            {
                return 0;
            }

            // Create histogram
            for (i = 0; i < 256; ++i)
            {
                histogram[i] = 0;
            }
            for (i = 0; i < insize; ++i)
            {
                //input = &inp[i];
                ++histogram[input[i]];
            }

            // Find the least common byte, and use it as the marker symbol
            marker = 0;
            for (i = 1; i < 256; ++i)
            {
                if (histogram[i] < histogram[marker])
                {
                    marker = (byte)i;
                }
            }

            // Remember the marker symbol for the decoder
            output[0] = marker;

            // Start of compression
            outpos = 1;

            // Main compression loop
            do
            {
                // Determine most distant position
                maxoffset = (inpos > LZ_MAX_OFFSET) ? LZ_MAX_OFFSET : inpos;

                // Get pointer to current position
                ptr1 = &input[inpos];

                // Search history window for maximum length string match
                bestlength = 3;
                bestoffset = 0;
                for (offset = 3; offset <= maxoffset; ++offset)
                {
                    // Get pointer to candidate string
                    ptr2 = &ptr1[-(int)offset];

                    // Quickly determine if this is a candidate (for speed)
                    if ((ptr1[0] == ptr2[0]) && (ptr1[bestlength] == ptr2[bestlength]))
                    {
                        // Determine maximum length for this offset
                        maxlength = (bytesleft < offset ? bytesleft : offset);

                        // Count maximum length match at this offset
                        length = LZ_StringCompare(ptr1, ptr2, 0, maxlength);

                        // Better match than any previous match?
                        if (length > bestlength)
                        {
                            bestlength = length;
                            bestoffset = offset;
                        }
                    }
                }

                // Was there a good enough match?
                if ((bestlength >= 8) ||
                    ((bestlength == 4) && (bestoffset <= 0x0000007f)) ||
                    ((bestlength == 5) && (bestoffset <= 0x00003fff)) ||
                    ((bestlength == 6) && (bestoffset <= 0x001fffff)) ||
                    ((bestlength == 7) && (bestoffset <= 0x0fffffff)))
                {
                    output[outpos++] = marker;
                    outpos += (uint)_LZ_WriteVarSize(bestlength, output + outpos);
                    outpos += (uint)_LZ_WriteVarSize(bestoffset, output + outpos);
                    inpos += bestlength;
                    bytesleft -= bestlength;
                }
                else
                {
                    // Output single byte (or two bytes if marker byte)
                    symbol = input[inpos++];
                    output[outpos++] = symbol;
                    if (symbol == marker)
                    {
                        output[outpos++] = 0;
                    }
                    --bytesleft;
                }
            } while (bytesleft > 3);

            // Dump remaining bytes, if any
            while (inpos < insize)
            {
                if (input[inpos] == marker)
                {
                    output[outpos++] = marker;
                    output[outpos++] = 0;
                }
                else
                {
                    output[outpos++] = input[inpos];
                }
                ++inpos;
            }

            return (int)outpos;
        }

        ///*************************************************************************
        ///* LZ_CompressFast() - Compress a block of data using an LZ77 coder.
        ///*  in     - Input (uncompressed) buffer.
        ///*  out    - Output (compressed) buffer. This buffer must be 0.4% larger
        ///*           than the input buffer, plus one byte.
        ///*  insize - Number of input bytes.
        ///*  work   - Pointer to a temporary buffer (internal working buffer), which
        ///*           must be able to hold (insize+65536) unsigned integers.
        ///* The function returns the size of the compressed data.
        /// *************************************************************************
        /// *************************************************************************

        public static unsafe int LZ_CompressFast(byte* input, byte* output, uint insize)
        {
            byte marker;
            uint inpos, outpos, bytesleft, i, index, symbols;
            uint offset, bestoffset;
            uint maxlength, length, bestlength;
            uint[] histogram = new uint[256];
            uint* lastindex, jumptable;
            byte* ptr1, ptr2;
            uint* work;

            if (insize < 1)
            {
                return 0;
            }

            work = (uint*)Marshal.AllocHGlobal((int)(insize + 65536) * sizeof(uint));

            lastindex = work;
            jumptable = work + 65536;

            for (i = 0; i < 65536; ++i)
            {
                lastindex[i] = 0xffffffff;
            }
            for (i = 0; i < insize - 1; ++i)
            {
                symbols = (uint)(input[i] << 8) | input[i + 1];
                index = lastindex[symbols];
                lastindex[symbols] = i;
                jumptable[i] = index;
            }
            jumptable[insize - 1] = 0xffffffff;

            for (i = 0; i < 256; ++i)
            {
                histogram[i] = 0;
            }
            for (i = 0; i < insize; ++i)
            {
                ++histogram[input[i]];
            }

            marker = 0;
            for (i = 1; i < 256; ++i)
            {
                if (histogram[i] < histogram[marker])
                {
                    marker = (byte)i;
                }
            }

            output[0] = marker;

            inpos = 0;
            outpos = 1;
            bytesleft = insize;

            do
            {
                ptr1 = &input[inpos];

                bestlength = 3;
                bestoffset = 0;
                index = jumptable[inpos];
                while (index != 0xffffffff && (inpos - index) < LZ_MAX_OFFSET)
                {
                    ptr2 = &input[index];

                    if (ptr2[bestlength] == ptr1[bestlength])
                    {
                        offset = inpos - index;
                        maxlength = (bytesleft < offset ? bytesleft : offset);

                        length = (uint)LZ_StringCompare(ptr1, ptr2, 2, maxlength);

                        if (length > bestlength)
                        {
                            bestlength = length;
                            bestoffset = offset;
                        }
                    }

                    index = jumptable[index];
                }

                if ((bestlength >= 8) ||
                    ((bestlength == 4) && (bestoffset <= 0x0000007f)) ||
                    ((bestlength == 5) && (bestoffset <= 0x00003fff)) ||
                    ((bestlength == 6) && (bestoffset <= 0x001fffff)) ||
                    ((bestlength == 7) && (bestoffset <= 0x0fffffff)))
                {
                    output[outpos++] = marker;
                    outpos += (uint)_LZ_WriteVarSize(bestlength, output + outpos);
                    outpos += (uint)_LZ_WriteVarSize(bestoffset, output + outpos);
                    inpos += bestlength;
                    bytesleft -= bestlength;
                }
                else
                {
                    byte symbol = input[inpos++];
                    output[outpos++] = symbol;
                    if (symbol == marker)
                    {
                        output[outpos++] = 0;
                    }
                    --bytesleft;
                }
            }
            while (bytesleft > 3);

            while (inpos < insize)
            {
                if (input[inpos] == marker)
                {
                    output[outpos++] = marker;
                    output[outpos++] = 0;
                }
                else
                {
                    output[outpos++] = input[inpos];
                }
                ++inpos;
            }

            Marshal.FreeHGlobal((IntPtr)work);

            return (int)outpos;
        }

        ///*************************************************************************
        ///* LZ_Uncompress() - Uncompress a block of data using an LZ77 decoder.
        ///*  in      - Input (compressed) buffer.
        ///*  out     - Output (uncompressed) buffer. This buffer must be large
        ///*            enough to hold the uncompressed data.
        ///*  insize  - Number of input bytes.
        ///*************************************************************************/

        byte[] LZ_Uncompress(byte[] input)
        {
            int insize = input.Length;
            byte marker, symbol;
            int inpos;
            int outpos;
            int length = 0;
            int offset = 0;
            byte[] output = FastArray.Init(700000, 0x00);

            // Do we have anything to uncompress?
            if (insize < 1)
            {
                return null;
            }

            // Get marker symbol from input stream
            marker = input[0];
            inpos = 1;

            // Main decompression loop
            outpos = 0;
            do
            {
                symbol = input[inpos++];
                if (symbol == marker)
                {
                    // We had a marker byte
                    if (input[inpos] == 0)
                    {
                        // It was a single occurrence of the marker byte
                        output[outpos++] = marker;
                        inpos++;
                    }
                    else
                    {
                        // Extract true length and offset
                        inpos += LZ_ReadVarSize(ref length, input, inpos);
                        inpos += LZ_ReadVarSize(ref offset, input, inpos);

                        // Copy corresponding data from history window
                        for (int i = 0; i < length; i++)
                        {
                            output[outpos] = output[outpos - offset];
                            outpos++;
                        }
                    }
                }
                else
                {
                    // No marker, plain copy
                    output[outpos++] = symbol;
                }
            }
            while (inpos < insize);
            byte[] temp = new byte[outpos];
            Buffer.BlockCopy(output, 0, temp, 0, outpos);
            return temp;
        }

        public static unsafe byte[] LZfast(byte[] inputData)
        {
            if (inputData == null || inputData.Length == 0)
                throw new ArgumentException("Input data cannot be null or empty.");

            byte[] outputData = new byte[inputData.Length * 2];

            fixed (byte* inPtr = inputData)
            fixed (byte* outPtr = outputData)
            {
                int compressedSize = LZ_CompressFast(inPtr, outPtr, (uint)inputData.Length);
                Array.Resize(ref outputData, compressedSize);
            }

            return outputData;
        }

        unsafe byte[] LZslow(byte[] inputData)
        {
            if (inputData == null || inputData.Length == 0)
                throw new ArgumentException("Input data cannot be null or empty.");

            // Allocate memory for input and output
            byte[] outputData = new byte[inputData.Length * 2]; // Assuming compressed data will not be larger than twice the input size

            fixed (byte* inPtr = inputData)
            fixed (byte* outPtr = outputData)
            {
                // Call the compression function
                int compressedSize = LZ_Compress(inPtr, outPtr, (uint)inputData.Length);

                // Resize the output buffer to the actual compressed size
                Array.Resize(ref outputData, compressedSize);
            }

            return outputData;
        }
    }
}
