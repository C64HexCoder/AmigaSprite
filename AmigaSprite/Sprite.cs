using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.IO;


namespace AmigaSprite
{
    public class Sprite
    {
        const int LowWord = 0, HiWord = 1, MainSprite = 0, AttachedSprite = 1;

        public enum Chipset
        {
            OCS_ECS,
            AGA
        }
        public enum SpriteWidth
        {
            _16Pixels,
            _32Pixels,
            _64Pixels
        }

        public struct SpriteStructure
        {
            public string Name;
            public Chipset chipset;
            public byte SpriteWidth;
            public int NumOfRaws;
            public ulong SPRxPOS;
            public ulong SPRxCTL;
            public ulong SPRxCTL_A;
            public bool _Attached;
            public byte NumOfColors;
            public bool Attached
            {
                get { return _Attached; }
                set
                {
                    _Attached = value;
                    if (_Attached)
                        NumOfColors = 16;
                    else
                        NumOfColors = 4;
                }

            }
            public ulong[,,] SpriteData;

            public int[] Colors;

        }
        public SpriteStructure spriteData;

        //------------------------------------------------------------------------------------------
        // Loading and converting image to Amiga Sprite format
        // It does not set Color 0 in the correct position (which is 0)
        // Need to mark color 0, which is transparent on the amiga, as transparent in the image 
        // to be converted using graphics tool
        //------------------------------------------------------------------------------------------
        public void ImportImage(string fileName)
        {
            Sprite spriteImage = new Sprite();

            Bitmap bmp = new Bitmap(fileName);

            ColorPalette pallate;
            IntPtr intPtr = IntPtr.Zero;

            if (bmp.PixelFormat == PixelFormat.Format4bppIndexed)
            {

                Rectangle rec = new Rectangle(0, 0, bmp.Width, bmp.Height);
                BitmapData data = bmp.LockBits(rec, ImageLockMode.ReadWrite, PixelFormat.Format4bppIndexed);

                pallate = bmp.Palette;
                if (pallate.Entries.Count() <= 16)
                {
                    if (pallate.Entries.Count() > 4) spriteData.Attached = true;
                    for (int i = 0; i < pallate.Entries.Count(); i++)
                    {
                        spriteData.Colors[i] = pallate.Entries[i].ToArgb();

                    }


                    spriteData.NumOfRaws = bmp.Height;
                    spriteData.SpriteWidth = (byte)bmp.Width;

                    intPtr = data.Scan0;
                    int NumOfBytes = Math.Abs(data.Stride) * data.Height;
                    byte[] indexValues = new byte[NumOfBytes];
                    Marshal.Copy(intPtr, indexValues, 0, NumOfBytes);
                    byte Nimble;

                    for (byte y = 0; y < data.Height; y++)
                        for (byte x = 0; x < data.Stride; x++)
                        {
                            Nimble = indexValues[y * data.Stride + x];
                            Nimble >>= 4;
                            SetPixel((byte)(x * 2), y, Nimble);
                            Nimble = indexValues[y * data.Stride + x];
                            Nimble &= 0x0f;
                            SetPixel((byte)(x * 2 + 1), y, Nimble);

                        }

                    bmp.UnlockBits(data);

                }
            }

        }


        public void ImportIndexedImage(string fileName)
        {
            Sprite spriteImage = new Sprite();

            Bitmap bmp = new Bitmap(fileName);

            ColorPalette pallate;
            IntPtr intPtr = IntPtr.Zero;

            Rectangle rec = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData data = bmp.LockBits(rec, ImageLockMode.ReadWrite, PixelFormat.Format4bppIndexed);

            intPtr = data.Scan0;
            int NumOfBytes = Math.Abs(data.Stride) * data.Height;
            byte[] indexValues = new byte[NumOfBytes];
            Marshal.Copy(intPtr, indexValues, 0, NumOfBytes);

            switch (bmp.PixelFormat)
            {
                case PixelFormat.Format4bppIndexed:




                    pallate = bmp.Palette;
                    if (pallate.Entries.Count() <= 16)
                    {
                        if (pallate.Entries.Count() > 4) spriteData.Attached = true;
                        for (int i = 0; i < pallate.Entries.Count(); i++)
                        {
                            spriteData.Colors[i] = pallate.Entries[i].ToArgb();

                        }


                        spriteData.NumOfRaws = bmp.Height;
                        spriteData.SpriteWidth = (byte)bmp.Width;


                        byte Nimble;

                        for (byte y = 0; y < data.Height; y++)
                            for (byte x = 0; x < data.Stride; x++)
                            {
                                Nimble = indexValues[y * data.Stride + x];
                                Nimble >>= 4;
                                SetPixel((byte)(x * 2), y, Nimble);
                                Nimble = indexValues[y * data.Stride + x];
                                Nimble &= 0x0f;
                                SetPixel((byte)(x * 2 + 1), y, Nimble);

                            }

                        bmp.UnlockBits(data);

                    }
                    break;

                case PixelFormat.Format8bppIndexed:
                    break;

                case PixelFormat.Format1bppIndexed:
                    for (byte y = 0; y < data.Height; y++)
                        for (byte x = 0; x < data.Stride; x++)
                            spriteData.SpriteData[LowWord, y, x] = indexValues[x];
                    break;
            }
        }


        private void SetPixel(int x, int y, ulong ColorIdx)
        {
            ulong bit = 1;
            byte MSB = (byte)(spriteData.SpriteWidth - 1);
            byte BitToMask = (byte)(MSB - x);
            ulong MaskingBit = (ulong)(bit << BitToMask);
            spriteData.SpriteData[0, LowWord, y] &= (ulong)~(bit << BitToMask);
            spriteData.SpriteData[0, HiWord, y] &= (ulong)~(bit << BitToMask);
            ulong OrBit = (ulong)((ColorIdx & 0x01) << BitToMask);
            spriteData.SpriteData[0, LowWord, y] |= OrBit;
            OrBit = (ulong)(((ColorIdx & 0x02) >> 1) << (MSB - x));
            spriteData.SpriteData[0, HiWord, y] |= OrBit;

            if (spriteData.Attached)
            {
                spriteData.SpriteData[1, LowWord, y] &= (ulong)~(bit << (MSB - x));
                spriteData.SpriteData[1, HiWord, y] &= (ulong)~(bit << (MSB - x));
                OrBit = (ulong)((ColorIdx & 0x04) >> 2) << (MSB - x);
                spriteData.SpriteData[1, LowWord, y] |= OrBit;
                OrBit = (ulong)(((ColorIdx & 0x08) >> 3) << (MSB - x));
                spriteData.SpriteData[1, HiWord, y] |= OrBit;
            }
        }

        private byte GetPixel(int x, int y)
        {
            byte MSB = (byte)(spriteData.SpriteWidth - 1);
            ulong BitMasking = (ulong)1 << (MSB - x);
            byte ColorIdx = 0;

            if (spriteData.Attached)
            {
                ColorIdx |= (byte)(((spriteData.SpriteData[1, HiWord, y] & BitMasking) >> (MSB - x)) << 3);
                ColorIdx |= (byte)(((spriteData.SpriteData[1, LowWord, y] & BitMasking) >> (MSB - x) << 2));

            }

            ColorIdx |= (byte)(((spriteData.SpriteData[0, HiWord, y] & BitMasking) >> (MSB - x) << 1));
            return ColorIdx |= (byte)((spriteData.SpriteData[0, LowWord, y] & BitMasking) >> (MSB - x));
        }

        public void Load(string fileName)
        {
            Stream stream = new FileStream(fileName, FileMode.Open);
            BinaryReader Reader = new BinaryReader(stream);

            spriteData.Name = Reader.ReadString();
            spriteData.NumOfRaws = Reader.ReadInt32();
            spriteData.SPRxPOS = (ushort)Reader.ReadInt64();
            spriteData.SPRxCTL = (ushort)Reader.ReadInt64();
            spriteData.chipset = (Chipset)Reader.ReadByte();
            spriteData.SpriteWidth = Reader.ReadByte();


            spriteData.Attached = Reader.ReadBoolean();

            if (spriteData.Attached)
                spriteData.SPRxCTL_A = (ushort)Reader.ReadInt64();


            int NumOfSpritesToRead;
            if (spriteData.Attached)
            {
                NumOfSpritesToRead = 2;
            }
            else
            {
                NumOfSpritesToRead = 1;
            }
            spriteData.SpriteData = new ulong[NumOfSpritesToRead, 2, spriteData.NumOfRaws];

            // Reading image data
            for (int j = 0; j < NumOfSpritesToRead; j++)
                for (int i = 0; i < 2; i++)
                    for (int raw = 0; raw < spriteData.NumOfRaws; raw++)
                    {
                        spriteData.SpriteData[j, i, raw] = (ulong)Reader.ReadInt64();
                    }

            // Reading Colors

            spriteData.NumOfColors = (byte)(spriteData.Attached ? 16 : 4);

            for (int i = 0; i < spriteData.NumOfColors; i++)
            {
                spriteData.Colors[i] = Reader.ReadInt32();
            }

            // Transfer colors to the images on the side

            Reader.Close();


        }

        public void SaveColorsAsAssemblerSource(string fileName, string spriteLable = "Sprite", int startColorRegister = 0x1A0)
        {

            StreamWriter streamWriter = new StreamWriter(fileName);
            streamWriter.WriteLine($"{spriteLable}_Collors:");

            for (int i = 1; i < spriteData.NumOfColors; i++)
            {
                streamWriter.WriteLine($"\t DC.W ${startColorRegister + (i - 1) * 2:X3},${ConvertRGBFrom32To12Bit(Color.FromArgb(spriteData.Colors[i])):X4}");
            }

            streamWriter.Flush();
            streamWriter.Close();

        }

        private ushort ConvertRGBFrom32To12Bit(Color color)
        {
            return (ushort)(((((color.R / 16) & 0x0f) << 8) | ((color.G / 16) & 0x0f) << 4 | ((color.B / 16) & 0x0f) & 0x0fff));
        }

        public void Save(string fileName)
        {

        }
    }
}