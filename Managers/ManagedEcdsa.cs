/*
 * ii Reborn
 * Copyright (C) 2026 @corgisolutions
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * Do not remove this notice.
 */

using System;
using System.Security.Cryptography;
using System.Text;

namespace iiMenu.Managers
{
    public class ManagedEcdsa
    {
        // p = 2^256 - 2^224 + 2^192 + 2^96 - 1
        private static readonly uint[] P = {
            0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0x00000000,
            0x00000000, 0x00000000, 0x00000001, 0xFFFFFFFF
        };

        // a = -3 mod p
        private static readonly uint[] A = {
            0xFFFFFFFC, 0xFFFFFFFF, 0xFFFFFFFF, 0x00000000,
            0x00000000, 0x00000000, 0x00000001, 0xFFFFFFFF
        };

        // b
        private static readonly uint[] B = {
            0x27D2604B, 0x3BCE3C3E, 0xCC53B0F6, 0x651D06B0,
            0x769886BC, 0xB3EBBD55, 0xAA3A93E7, 0x5AC635D8
        };

        // gen point x-coordinate
        private static readonly uint[] Gx = {
            0xD898C296, 0xF4A13945, 0x2DEB33A0, 0x77037D81,
            0x63A440F2, 0xF8BCE6E5, 0xE12C4247, 0x6B17D1F2
        };

        // gen point y-coordinate
        private static readonly uint[] Gy = {
            0x37BF51F5, 0xCBB64068, 0x6B315ECE, 0x2BCE3357,
            0x7C0F9E16, 0x8EE7EB4A, 0xFE1A7F9B, 0x4FE342E2
        };

        // n curve order
        private static readonly uint[] N = {
            0xFC632551, 0xF3B9CAC2, 0xA7179E84, 0xBCE6FAAD,
            0xFFFFFFFF, 0xFFFFFFFF, 0x00000000, 0xFFFFFFFF
        };

        // 2^256 - P
        private static readonly uint[] PNeg = {
            0x00000001, 0x00000000, 0x00000000, 0xFFFFFFFF,
            0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFE, 0x00000000
        };

        // 2^256 - N
        private static readonly uint[] NNeg = {
            0x039CDAAF, 0x0C46353D, 0x58E8617B, 0x43190552,
            0x00000000, 0x00000000, 0xFFFFFFFF, 0x00000000
        };

        private uint[] _pubX; // pub key x-coordinate (affine)
        private uint[] _pubY; // pub key y-coordinate (affine)

        private static int Compare256(uint[] a, uint[] b)
        {
            for (int i = 7; i >= 0; i--)
            {
                if (a[i] > b[i]) return 1;
                if (a[i] < b[i]) return -1;
            }
            return 0;
        }

        private static uint Add256(uint[] result, uint[] a, uint[] b)
        {
            ulong carry = 0;
            for (int i = 0; i < 8; i++)
            {
                ulong sum = (ulong)a[i] + b[i] + carry;
                result[i] = (uint)sum;
                carry = sum >> 32;
            }
            return (uint)carry;
        }

        private static uint Subtract256(uint[] result, uint[] a, uint[] b)
        {
            long borrow = 0;
            for (int i = 0; i < 8; i++)
            {
                long diff = (long)a[i] - (long)b[i] - borrow;
                if (diff < 0)
                {
                    result[i] = (uint)(diff + 0x100000000L);
                    borrow = 1;
                }
                else
                {
                    result[i] = (uint)diff;
                    borrow = 0;
                }
            }
            return (uint)borrow;
        }

        private static bool IsZero(uint[] a)
        {
            for (int i = 0; i < 8; i++)
                if (a[i] != 0) return false;
            return true;
        }

        private static void Clear256(uint[] a)
        {
            for (int i = 0; i < 8; i++) a[i] = 0;
        }

        private static void Copy256(uint[] dst, uint[] src)
        {
            for (int i = 0; i < 8; i++) dst[i] = src[i];
        }

        // r = (a + b) mod m, where a, b < m
        private static void ModAdd(uint[] r, uint[] a, uint[] b, uint[] m, uint[] mNeg)
        {
            uint carry = Add256(r, a, b);

            if (carry != 0)
            {
                // overflow true sum = 2^256 + r
                // result mod m = r + (2^256 - m) = r + mNeg
                ulong c = 0;
                for (int i = 0; i < 8; i++)
                {
                    ulong sum = (ulong)r[i] + mNeg[i] + c;
                    r[i] = (uint)sum;
                    c = sum >> 32;
                }
            }
            else if (Compare256(r, m) >= 0)
            {
                Subtract256(r, r, m);
            }
        }

        // r = 2*r mod m (doubling)
        private static void ModDouble(uint[] r, uint[] m, uint[] mNeg)
        {
            // shift left by 1 bit
            uint carry = 0;
            for (int i = 0; i < 8; i++)
            {
                uint newCarry = r[i] >> 31;
                r[i] = (r[i] << 1) | carry;
                carry = newCarry;
            }

            if (carry != 0)
            {
                // overflow true value = 2^256 + r_shifted
                // result mod m = r_shifted + (2^256 - m) = r_shifted + mNeg
                ulong c = 0;
                for (int i = 0; i < 8; i++)
                {
                    ulong sum = (ulong)r[i] + mNeg[i] + c;
                    r[i] = (uint)sum;
                    c = sum >> 32;
                }
            }
            else if (Compare256(r, m) >= 0)
            {
                Subtract256(r, r, m);
            }
        }

        // r = a * b mod m, using double-and-add
        private static void ModMul(uint[] r, uint[] a, uint[] b, uint[] m, uint[] mNeg)
        {
            // !!!
            Clear256(r);

            // each bit of 'a' from msb to lsb
            for (int bit = 255; bit >= 0; bit--)
            {
                // r = r * 2 mod m
                ModDouble(r, m, mNeg);

                // if bit is set, r = r + b mod m
                int word = bit / 32;
                int bitInWord = bit % 32;
                if ((a[word] & (1u << bitInWord)) != 0)
                {
                    ModAdd(r, r, b, m, mNeg);
                }
            }
        }

        // r = a^exponent mod m, using square-and-multiply
        private static void ModPow(uint[] r, uint[] a, uint[] exponent, uint[] m, uint[] mNeg)
        {
            uint[] result = new uint[8];
            uint[] temp = new uint[8];
            uint[] baseVal = new uint[8];

            // result = 1
            Clear256(result);
            result[0] = 1;

            // baseVal = a
            Copy256(baseVal, a);

            // square-and-multiply ltr
            for (int bit = 255; bit >= 0; bit--)
            {
                // temp = result^2, then result = temp.
                // ModMul cannot take its own output as an input,
                // so the product goes through a separate buffer.
                ModMul(temp, result, result, m, mNeg);
                Copy256(result, temp);

                int word = bit / 32;
                int bitInWord = bit % 32;
                if ((exponent[word] & (1u << bitInWord)) != 0)
                {
                    // temp = result * baseVal, then result = temp
                    ModMul(temp, result, baseVal, m, mNeg);
                    Copy256(result, temp);
                }
            }

            Copy256(r, result);
        }

        // r = a^(-1) mod m, via fermat's little theorem a^(m-2) mod m
        private static void ModInverse(uint[] r, uint[] a, uint[] m, uint[] mNeg)
        {
            // exponent = m - 2
            uint[] exponent = new uint[8];
            Copy256(exponent, m);

            // subtract 2
            ulong borrow = 2;
            for (int i = 0; i < 8; i++)
            {
                ulong val = exponent[i];
                if (val >= borrow)
                {
                    exponent[i] = (uint)(val - borrow);
                    borrow = 0;
                }
                else
                {
                    exponent[i] = (uint)(val + 0x100000000UL - borrow);
                    borrow = 1;
                }
            }

            // a^(m-2) mod m
            ModPow(r, a, exponent, m, mNeg);
        }

        // a point in jacobian coordinates: (X, Y, Z)
        // affine: (x, y) = (X/Z^2, Y/Z^3)
        // point at inf: Z = 0

        private static bool IsInfinity(uint[] x, uint[] y, uint[] z)
        {
            return IsZero(z);
        }

        private static void SetInfinity(uint[] x, uint[] y, uint[] z)
        {
            Clear256(x);
            Clear256(y);
            Clear256(z);
        }

        // point doubling in jacobian coordinates for a = -3
        // P = (X1, Y1, Z1) => 2P = (X3, Y3, Z3)
        // safe to call with output aliasing input (x3 == x1, y3 == y1, z3 == z1)
        //
        // S = 4*X1*Y1^2
        // M = 3*(X1 - Z1^2)*(X1 + Z1^2)
        // X3 = M^2 - 2*S
        // Y3 = M*(S - X3) - 8*Y1^4
        // Z3 = 2*Y1*Z1
        private static void PointDouble(uint[] x3, uint[] y3, uint[] z3,
                                        uint[] x1, uint[] y1, uint[] z1)
        {
            if (IsZero(z1) || IsZero(y1))
            {
                SetInfinity(x3, y3, z3);
                return;
            }

            // y1 and z1 are needed for Z3 at the end, but the outputs may
            // alias them. save copies before anything writes to the outputs.
            uint[] y1s = new uint[8];
            uint[] z1s = new uint[8];
            Copy256(y1s, y1);
            Copy256(z1s, z1);

            uint[] t1 = new uint[8];
            uint[] t2 = new uint[8];
            uint[] t3 = new uint[8];
            uint[] t4 = new uint[8];

            // t1 = Z1^2
            ModMul(t1, z1s, z1s, P, PNeg);

            // t2 = Y1^2
            ModMul(t2, y1s, y1s, P, PNeg);

            // t3 = (X1 - Z1^2)*(X1 + Z1^2) = X1^2 - Z1^4
            {
                uint[] temp1 = new uint[8];
                uint[] temp2 = new uint[8];

                // temp1 = X1 - Z1^2
                uint borrow = Subtract256(temp1, x1, t1);
                if (borrow != 0)
                {
                    Subtract256(temp1, temp1, PNeg);
                }

                // temp2 = X1 + Z1^2
                ModAdd(temp2, x1, t1, P, PNeg);

                ModMul(t3, temp1, temp2, P, PNeg);
            }

            // t3 = M = 3*t3 = t3 + t3 + t3
            {
                uint[] temp = new uint[8];
                ModAdd(temp, t3, t3, P, PNeg);
                ModAdd(t3, temp, t3, P, PNeg);
            }

            // t4 = S = 4*X1*Y1^2
            ModMul(t4, x1, t2, P, PNeg);
            ModDouble(t4, P, PNeg);
            ModDouble(t4, P, PNeg);

            // X3 = M^2 - 2*S
            ModMul(x3, t3, t3, P, PNeg);
            {
                uint[] temp = new uint[8];
                // temp = 2*S
                Copy256(temp, t4);
                ModDouble(temp, P, PNeg);
                uint borrow = Subtract256(x3, x3, temp);
                if (borrow != 0)
                {
                    Subtract256(x3, x3, PNeg);
                }
            }

            // Y3 = M*(S - X3) - 8*Y1^4
            {
                uint[] temp1 = new uint[8];
                uint[] temp2 = new uint[8];
                uint[] y4 = new uint[8];

                // temp1 = S - X3
                uint borrow = Subtract256(temp1, t4, x3);
                if (borrow != 0)
                {
                    Subtract256(temp1, temp1, PNeg);
                }

                // temp2 = M*(S - X3)
                ModMul(temp2, t3, temp1, P, PNeg);

                // y4 = Y1^4 = (Y1^2)^2. needs a separate destination,
                // ModMul cannot take its own output as an input.
                ModMul(y4, t2, t2, P, PNeg);

                // y4 = 8*Y1^4
                ModDouble(y4, P, PNeg);
                ModDouble(y4, P, PNeg);
                ModDouble(y4, P, PNeg);

                // Y3 = M*(S - X3) - 8*Y1^4
                borrow = Subtract256(y3, temp2, y4);
                if (borrow != 0)
                {
                    Subtract256(y3, y3, PNeg);
                }
            }

            // Z3 = 2*Y1*Z1, from the saved copies since the outputs may
            // have overwritten the originals by now
            ModMul(z3, y1s, z1s, P, PNeg);
            ModDouble(z3, P, PNeg);
        }

        // jacobian + affine -> jacobian
        // P1 = (X1, Y1, Z1) in jacobian, P2 = (x2, y2) in affine (Z2 = 1)
        // output P3 = P1 + P2
        // safe to call with output aliasing P1 (x3 == x1, y3 == y1, z3 == z1)
        private static void PointAddMixed(uint[] x3, uint[] y3, uint[] z3,
                                          uint[] x1, uint[] y1, uint[] z1,
                                          uint[] x2, uint[] y2)
        {
            if (IsZero(z1))
            {
                // P1 is infinity, result is P2 as jacobian with Z = 1
                Copy256(x3, x2);
                Copy256(y3, y2);
                z3[0] = 1;
                for (int i = 1; i < 8; i++) z3[i] = 0;
                return;
            }

            // z1 is needed for Z3 at the end, but z3 may alias it.
            // save a copy before anything writes to the output.
            uint[] z1s = new uint[8];
            Copy256(z1s, z1);

            uint[] t1 = new uint[8];
            uint[] t2 = new uint[8];
            uint[] u2 = new uint[8];
            uint[] s2 = new uint[8];
            uint[] h = new uint[8];
            uint[] r = new uint[8];

            // t1 = Z1^2
            ModMul(t1, z1s, z1s, P, PNeg);

            // t2 = Z1^3 = Z1^2 * Z1
            ModMul(t2, t1, z1s, P, PNeg);

            // U2 = x2 * Z1^2
            ModMul(u2, x2, t1, P, PNeg);

            // S2 = y2 * Z1^3
            ModMul(s2, y2, t2, P, PNeg);

            // H = U2 - X1
            uint borrow = Subtract256(h, u2, x1);
            if (borrow != 0)
            {
                Subtract256(h, h, PNeg);
            }

            // R = S2 - Y1
            borrow = Subtract256(r, s2, y1);
            if (borrow != 0)
            {
                Subtract256(r, r, PNeg);
            }

            if (IsZero(h))
            {
                if (IsZero(r))
                {
                    // P1 == P2, doubling
                    PointDouble(x3, y3, z3, x1, y1, z1);
                    return;
                }
                else
                {
                    // P1 == -P2, result is infinity
                    SetInfinity(x3, y3, z3);
                    return;
                }
            }

            // H^2 and H^3
            uint[] h2 = new uint[8];
            ModMul(h2, h, h, P, PNeg);
            uint[] h3 = new uint[8];
            ModMul(h3, h2, h, P, PNeg);

            // U1 * H^2, where U1 = X1
            uint[] u1h2 = new uint[8];
            ModMul(u1h2, x1, h2, P, PNeg);

            // X3 = R^2 - H^3 - 2*U1*H^2
            ModMul(x3, r, r, P, PNeg);
            {
                uint[] temp = new uint[8];

                Copy256(temp, h3);
                borrow = Subtract256(x3, x3, temp);
                if (borrow != 0) Subtract256(x3, x3, PNeg);

                Copy256(temp, u1h2);
                ModDouble(temp, P, PNeg);
                borrow = Subtract256(x3, x3, temp);
                if (borrow != 0) Subtract256(x3, x3, PNeg);
            }

            // Y3 = R*(U1*H^2 - X3) - Y1*H^3
            {
                uint[] temp1 = new uint[8];
                uint[] temp2 = new uint[8];

                // temp1 = U1*H^2 - X3
                borrow = Subtract256(temp1, u1h2, x3);
                if (borrow != 0) Subtract256(temp1, temp1, PNeg);

                // temp2 = R * temp1
                ModMul(temp2, r, temp1, P, PNeg);

                // temp1 = Y1 * H^3. y3 may alias y1 but is not written yet
                ModMul(temp1, y1, h3, P, PNeg);

                // Y3 = temp2 - temp1
                borrow = Subtract256(y3, temp2, temp1);
                if (borrow != 0) Subtract256(y3, y3, PNeg);
            }

            // Z3 = Z1 * H, from the saved copy since z3 may alias z1
            ModMul(z3, z1s, h, P, PNeg);
        }

        // jacobian + jacobian -> jacobian
        // P1 = (X1, Y1, Z1), P2 = (X2, Y2, Z2), both in jacobian
        // output P3 = P1 + P2
        private static void PointAddJacobian(uint[] x3, uint[] y3, uint[] z3,
                                            uint[] x1, uint[] y1, uint[] z1,
                                            uint[] x2, uint[] y2, uint[] z2)
        {
            if (IsZero(z1))
            {
                Copy256(x3, x2);
                Copy256(y3, y2);
                Copy256(z3, z2);
                return;
            }
            if (IsZero(z2))
            {
                Copy256(x3, x1);
                Copy256(y3, y1);
                Copy256(z3, z1);
                return;
            }

            uint[] t1 = new uint[8];
            uint[] t2 = new uint[8];
            uint[] t3 = new uint[8];
            uint[] t4 = new uint[8];
            uint[] u1 = new uint[8];
            uint[] u2 = new uint[8];
            uint[] s1 = new uint[8];
            uint[] s2 = new uint[8];
            uint[] h = new uint[8];
            uint[] r = new uint[8];

            // t1 = Z1^2, t2 = Z2^2
            ModMul(t1, z1, z1, P, PNeg);
            ModMul(t2, z2, z2, P, PNeg);

            // U1 = X1 * Z2^2, U2 = X2 * Z1^2
            ModMul(u1, x1, t2, P, PNeg);
            ModMul(u2, x2, t1, P, PNeg);

            // t3 = Z1^3, t4 = Z2^3
            ModMul(t3, t1, z1, P, PNeg);
            ModMul(t4, t2, z2, P, PNeg);

            // S1 = Y1 * Z2^3, S2 = Y2 * Z1^3
            ModMul(s1, y1, t4, P, PNeg);
            ModMul(s2, y2, t3, P, PNeg);

            // H = U2 - U1, R = S2 - S1
            uint borrow = Subtract256(h, u2, u1);
            if (borrow != 0) Subtract256(h, h, PNeg);

            borrow = Subtract256(r, s2, s1);
            if (borrow != 0) Subtract256(r, r, PNeg);

            if (IsZero(h))
            {
                if (IsZero(r))
                {
                    // P1 == P2, doubling
                    PointDouble(x3, y3, z3, x1, y1, z1);
                    return;
                }
                else
                {
                    // P1 == -P2, result is infinity
                    SetInfinity(x3, y3, z3);
                    return;
                }
            }

            // H^2 and H^3
            uint[] h2 = new uint[8];
            ModMul(h2, h, h, P, PNeg);
            uint[] h3 = new uint[8];
            ModMul(h3, h2, h, P, PNeg);

            // U1 * H^2
            uint[] u1h2 = new uint[8];
            ModMul(u1h2, u1, h2, P, PNeg);

            // X3 = R^2 - H^3 - 2*U1*H^2
            ModMul(x3, r, r, P, PNeg);
            {
                uint[] temp = new uint[8];

                Copy256(temp, h3);
                borrow = Subtract256(x3, x3, temp);
                if (borrow != 0) Subtract256(x3, x3, PNeg);

                Copy256(temp, u1h2);
                ModDouble(temp, P, PNeg);
                borrow = Subtract256(x3, x3, temp);
                if (borrow != 0) Subtract256(x3, x3, PNeg);
            }

            // Y3 = R*(U1*H^2 - X3) - S1*H^3
            {
                uint[] temp1 = new uint[8];
                uint[] temp2 = new uint[8];

                // temp1 = U1*H^2 - X3
                borrow = Subtract256(temp1, u1h2, x3);
                if (borrow != 0) Subtract256(temp1, temp1, PNeg);

                // temp2 = R * temp1
                ModMul(temp2, r, temp1, P, PNeg);

                // temp1 = S1 * H^3
                ModMul(temp1, s1, h3, P, PNeg);

                // Y3 = temp2 - temp1
                borrow = Subtract256(y3, temp2, temp1);
                if (borrow != 0) Subtract256(y3, y3, PNeg);
            }

            // Z3 = Z1 * Z2 * H. the intermediate product needs its own
            // buffer, ModMul cannot feed its own output back in as an input.
            {
                uint[] ztemp = new uint[8];
                ModMul(ztemp, z1, z2, P, PNeg);
                ModMul(z3, ztemp, h, P, PNeg);
            }
        }

        // result = k * point, where point is in affine coordinates, and result is in jacobian coordinates.
        private static void ScalarMult(uint[] x3, uint[] y3, uint[] z3,
                                        uint[] k, uint[] px, uint[] py)
        {
            // initialize result to infinity
            SetInfinity(x3, y3, z3);

            // process bits of k from msb to lsb
            for (int bit = 255; bit >= 0; bit--)
            {
                // double the accumulator
                PointDouble(x3, y3, z3, x3, y3, z3);

                // if bit is set, add the base point
                int word = bit / 32;
                int bitInWord = bit % 32;
                if ((k[word] & (1u << bitInWord)) != 0)
                {
                    PointAddMixed(x3, y3, z3, x3, y3, z3, px, py);
                }
            }
        }

        private static void FromBytesBE(uint[] result, byte[] bytes, int offset)
        {
            for (int i = 0; i < 8; i++)
            {
                // result[0] gets bytes[28..31] (least significant)
                // result[7] gets bytes[0..3] (most significant)
                int byteIdx = offset + (7 - i) * 4;
                result[i] = ((uint)bytes[byteIdx] << 24) |
                            ((uint)bytes[byteIdx + 1] << 16) |
                            ((uint)bytes[byteIdx + 2] << 8) |
                            ((uint)bytes[byteIdx + 3]);
            }
        }

        private static void ToBytesBE(byte[] bytes, int offset, uint[] value)
        {
            for (int i = 0; i < 8; i++)
            {
                // value[7] goes to bytes[0..3] (most significant)
                // value[0] goes to bytes[28..31] (least significant)
                int byteIdx = offset + (7 - i) * 4;
                bytes[byteIdx] = (byte)(value[i] >> 24);
                bytes[byteIdx + 1] = (byte)(value[i] >> 16);
                bytes[byteIdx + 2] = (byte)(value[i] >> 8);
                bytes[byteIdx + 3] = (byte)(value[i]);
            }
        }

        private static int ReadDerLength(byte[] data, ref int offset)
        {
            if (offset >= data.Length)
                throw new FormatException("DER: unexpected end of data reading length");

            int first = data[offset++];

            if (first < 0x80)
            {
                // short form where length is directly encoded
                return first;
            }
            else if (first == 0x80)
            {
                throw new FormatException("DER: indefinite length not supported");
            }
            else
            {
                // long form where first & 0x7F gives number of length bytes
                int numBytes = first & 0x7F;
                if (numBytes > 4 || offset + numBytes > data.Length)
                    throw new FormatException("DER: invalid long-form length");

                int length = 0;
                for (int i = 0; i < numBytes; i++)
                {
                    length = (length << 8) | data[offset++];
                }
                return length;
            }
        }

        private static void ReadDerTag(byte[] data, ref int offset, byte expectedTag)
        {
            if (offset >= data.Length)
                throw new FormatException("DER: unexpected end of data reading tag");

            byte tag = data[offset++];
            if (tag != expectedTag)
                throw new FormatException(
                    string.Format("DER: expected tag 0x{0:X2}, got 0x{1:X2}", expectedTag, tag));
        }

        private static void ReadDerInteger(byte[] data, ref int offset, uint[] result)
        {
            ReadDerTag(data, ref offset, 0x02);
            int length = ReadDerLength(data, ref offset);

            if (length < 1 || length > 33)
                throw new FormatException("DER: invalid INTEGER length for P-256");

            // skip leading zero byte (sign byte for positive integers)
            int start = offset;
            int len = length;
            if (data[start] == 0x00 && length > 1)
            {
                start++;
                len--;
            }

            if (len > 32)
                throw new FormatException("DER: INTEGER too large for P-256");

            // !!!
            Clear256(result);

            // read big-endian bytes into little-endian uint[8], pad with leading zeros if necessary
            byte[] temp = new byte[32];
            int padCount = 32 - len;
            for (int i = 0; i < len; i++)
            {
                temp[padCount + i] = data[start + i];
            }
            FromBytesBE(result, temp, 0);

            offset += length;
        }

        public void ImportSubjectPublicKeyInfo(byte[] spki, out int bytesRead)
        {
            if (spki == null)
                throw new ArgumentNullException("spki");

            int offset = 0;

            // parse outer SEQUENCE
            ReadDerTag(spki, ref offset, 0x30);
            int outerLen = ReadDerLength(spki, ref offset);
            int outerEnd = offset + outerLen;

            // parse AlgorithmIdentifier SEQUENCE
            ReadDerTag(spki, ref offset, 0x30);
            int algLen = ReadDerLength(spki, ref offset);
            int algEnd = offset + algLen;

            // parse first OID (algorithm), should be 1.2.840.10045.2.1 (ec pk)
            ReadDerTag(spki, ref offset, 0x06);
            int oid1Len = ReadDerLength(spki, ref offset);

            // expecting 06 07 2A 86 48 CE 3D 02 01
            byte[] ecOid = { 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x02, 0x01 };
            if (oid1Len != 7)
                throw new FormatException("Not an EC public key");
            for (int i = 0; i < 7; i++)
            {
                if (spki[offset + i] != ecOid[i])
                    throw new FormatException("Not an EC public key");
            }
            offset += oid1Len;

            // parse second OID (namedCurve), should be 1.2.840.10045.3.1.7 (p-256)
            ReadDerTag(spki, ref offset, 0x06);
            int oid2Len = ReadDerLength(spki, ref offset);

            // expecting 06 08 2A 86 48 CE 3D 03 01 07
            byte[] p256Oid = { 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x03, 0x01, 0x07 };
            if (oid2Len != 8)
                throw new FormatException("Not P-256 curve");
            for (int i = 0; i < 8; i++)
            {
                if (spki[offset + i] != p256Oid[i])
                    throw new FormatException("Not P-256 curve");
            }
            offset += oid2Len;

            if (offset != algEnd)
                throw new FormatException("Unexpected extra data in AlgorithmIdentifier");

            // parse BIT STRING (subjectPublicKey)
            ReadDerTag(spki, ref offset, 0x03);
            int bitStringLen = ReadDerLength(spki, ref offset);

            // first byte of BIT STRING is the number of unused bits, should be 0
            if (offset >= spki.Length || spki[offset] != 0x00)
                throw new FormatException("BIT STRING: non-zero unused bits");
            offset++;
            bitStringLen--;

            // the remaining bytes should be 04 || X (32 bytes) || Y (32 bytes)
            if (bitStringLen != 65)
                throw new FormatException("Invalid EC point length (expected 65 bytes)");

            if (spki[offset] != 0x04)
                throw new FormatException("Only uncompressed EC points supported");
            offset++;

            // read X and Y coordinates
            _pubX = new uint[8];
            _pubY = new uint[8];
            FromBytesBE(_pubX, spki, offset);
            offset += 32;
            FromBytesBE(_pubY, spki, offset);
            offset += 32;

            // is point on curve, y^2 = x^3 + ax + b (mod p)
            VerifyPointOnCurve(_pubX, _pubY);

            bytesRead = offset;
        }

        // y^2 mod p == (x^3 + a*x + b) mod p
        private static void VerifyPointOnCurve(uint[] x, uint[] y)
        {
            uint[] lhs = new uint[8];   // y^2
            uint[] rhs = new uint[8];   // x^3 + a*x + b
            uint[] temp = new uint[8];

            // lhs = y^2 mod p
            ModMul(lhs, y, y, P, PNeg);

            // rhs = x^3 + a*x + b mod p
            // temp = x^2
            ModMul(temp, x, x, P, PNeg);
            // rhs = x^3 = x^2 * x
            ModMul(rhs, temp, x, P, PNeg);

            // temp = a * x
            ModMul(temp, A, x, P, PNeg);
            // rhs = x^3 + a*x
            ModAdd(rhs, rhs, temp, P, PNeg);

            // rhs = x^3 + a*x + b
            ModAdd(rhs, rhs, B, P, PNeg);

            if (Compare256(lhs, rhs) != 0)
                throw new ArgumentException("Public key point is not on the P-256 curve");
        }

        public bool VerifyData(byte[] data, byte[] signature)
        {
            if (data == null)
                throw new ArgumentNullException("data");
            if (signature == null)
                throw new ArgumentNullException("signature");
            if (_pubX == null || _pubY == null)
                throw new InvalidOperationException("Public key not imported");

            uint[] r = new uint[8];
            uint[] s = new uint[8];

            if (signature.Length == 64)
            {
                FromBytesBE(r, signature, 0);
                FromBytesBE(s, signature, 32);
            }
            else
            {
                int offset = 0;

                try
                {
                    ReadDerTag(signature, ref offset, 0x30);
                    int seqLen = ReadDerLength(signature, ref offset);

                    ReadDerInteger(signature, ref offset, r);
                    ReadDerInteger(signature, ref offset, s);
                }
                catch (FormatException)
                {
                    return false; // malformed
                }
            }

            // r and s must be in [1, n-1]
            if (IsZero(r) || IsZero(s))
                return false;

            if (Compare256(r, N) >= 0 || Compare256(s, N) >= 0)
                return false;

            byte[] hashBytes;
            using (SHA256 sha256 = SHA256.Create())
            {
                hashBytes = sha256.ComputeHash(data);
            }

            // convert hash to uint[8]
            uint[] e = new uint[8];
            FromBytesBE(e, hashBytes, 0);

            // if hash >= n, reduce mod n. this is rare but possible
            if (Compare256(e, N) >= 0)
            {
                Subtract256(e, e, N);
            }

            // 1: w = s^(-1) mod n
            uint[] w = new uint[8];
            ModInverse(w, s, N, NNeg);

            // 2: u1 = e * w mod n
            uint[] u1 = new uint[8];
            ModMul(u1, e, w, N, NNeg);

            // 3: u2 = r * w mod n
            uint[] u2 = new uint[8];
            ModMul(u2, r, w, N, NNeg);

            // 4: R = u1 * G + u2 * Q
            // first: P1 = u1 * G
            uint[] x1 = new uint[8];
            uint[] y1 = new uint[8];
            uint[] z1 = new uint[8];
            ScalarMult(x1, y1, z1, u1, Gx, Gy);

            // second: P2 = u2 * Q (Q is pubkey)
            uint[] x2 = new uint[8];
            uint[] y2 = new uint[8];
            uint[] z2 = new uint[8];
            ScalarMult(x2, y2, z2, u2, _pubX, _pubY);

            // third: R = P1 + P2
            uint[] x3 = new uint[8];
            uint[] y3 = new uint[8];
            uint[] z3 = new uint[8];
            PointAddJacobian(x3, y3, z3, x1, y1, z1, x2, y2, z2);

            // 5: check if R is infinity
            if (IsZero(z3))
                return false;

            // 6: convert R to affine coordinates
            // x_affine = X / Z^2 = X * Z^(-2)
            // y_affine = Y / Z^3 = Y * Z^(-3)

            // Z^(-1) mod p
            uint[] zInv = new uint[8];
            ModInverse(zInv, z3, P, PNeg);

            // Z^(-2) = (Z^(-1))^2
            uint[] zInv2 = new uint[8];
            ModMul(zInv2, zInv, zInv, P, PNeg);

            // x_affine = X * Z^(-2)
            uint[] xAffine = new uint[8];
            ModMul(xAffine, x3, zInv2, P, PNeg);

            // 7: v = x_affine mod n (x_affine < p, and p > n, so we may need to subtract n)
            uint[] v = new uint[8];
            Copy256(v, xAffine);
            if (Compare256(v, N) >= 0)
            {
                Subtract256(v, v, N);
            }

            // 8: signature is valid if v == r
            return Compare256(v, r) == 0;
        }
    }
}
