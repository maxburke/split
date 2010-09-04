using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;

namespace Slim
{
    public abstract class EndianReader
    {
        public abstract byte ReadI1();
        public abstract int ReadI4();
        public abstract uint ReadU4();
        public abstract float ReadF4();
        public abstract BinaryReader BaseStream { get; }

        public Pair<int, int> ReadIntPair()
        {
            int first = ReadI4();
            int second = ReadI4();
            return new Pair<int, int>(first, second);
        }

        public IntVector3 ReadIntVector3()
        {
            int x = ReadI4();
            int y = ReadI4();
            int z = ReadI4();
            return new IntVector3(x, y, z);
        }

        public Vector2 ReadV2()
        {
            float x = ReadF4();
            float y = ReadF4();
            return new Vector2(x, y);
        }

        public Vector3 ReadV3()
        {
            float x = ReadF4();
            float y = ReadF4();
            float z = ReadF4();
            return new Vector3(x, y, z);
        }

        public Color ReadRGB()
        {
            byte r = ReadI1();
            byte g = ReadI1();
            byte b = ReadI1();
            return new Color(r, g, b);
        }

        public Color ReadRGBA()
        {
            byte r = ReadI1();
            byte g = ReadI1();
            byte b = ReadI1();
            byte a = ReadI1();
            return new Color(r, g, b, a);
        }

        public string ReadCString(int numChars)
        {
            byte[] byteData = BaseStream.ReadBytes(numChars);
            unsafe
            {
                sbyte* name = stackalloc sbyte[numChars];
                for (int i = 0; i < 64; ++i)
                {
                    name[i] = (sbyte)byteData[i];
                    if (name[i] == 0)
                        break;
                }
                return new string(name);
            }
        }
    }

    public class LittleEndianReader : EndianReader
    {
        BinaryReader BR;

        public LittleEndianReader(BinaryReader br)
        {
            BR = br;
        }

        public override byte ReadI1()
        {
            return BR.ReadByte();
        }

        public override int ReadI4()
        {
            return BR.ReadInt32();
        }

        public override uint ReadU4()
        {
            return (uint)BR.ReadInt32();
        }

        public override float ReadF4()
        {
            return BR.ReadSingle();
        }

        public override BinaryReader BaseStream { get { return BR; } }
    }

    public class BigEndianReader : EndianReader
    {
        BinaryReader BR;

        public BigEndianReader(BinaryReader br)
        {
            BR = br;
        }

        public override byte ReadI1()
        {
            return BR.ReadByte();
        }

        byte[] ReadFourEndianSwappedBytes()
        {
            byte[] bytes = BR.ReadBytes(4);
            byte temp = bytes[0];
            bytes[0] = bytes[3];
            bytes[3] = temp;

            temp = bytes[1];
            bytes[1] = bytes[2];
            bytes[2] = temp;

            return bytes;
        }

        public override int ReadI4()
        {
            return BitConverter.ToInt32(ReadFourEndianSwappedBytes(), 0);
        }

        public override uint ReadU4()
        {
            return BitConverter.ToUInt32(ReadFourEndianSwappedBytes(), 0);
        }

        public override float ReadF4()
        {
            return BitConverter.ToSingle(ReadFourEndianSwappedBytes(), 0);
        }

        public override BinaryReader BaseStream { get { return BR; } }
    }
}
