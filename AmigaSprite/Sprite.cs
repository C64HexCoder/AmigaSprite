using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.IO;


namespace AmigaSprite
{
    public class Sprite
    {
        const int LowWord = 0, HiWord = 1, MainSprite = 0, AttachedSprite = 1;
        public ToolStripProgressBar progressBar;
        List<ColorCount> colors = new List<ColorCount>();
        Bitmap bitmap;

        // MedianCut registers
        List<Color>[] Backets = new List<Color>[16];
        List<Color> OrgenizedColors = new List<Color>();
        Color[] Pallate = new Color[16];

        enum DominantColor
        {
            Red,
            Green,
            Blue
        }

        public int Height
        {
            get
            {
                return bitmap.Height;
            }
        }

        public int Width
        {
            get
            {
                return bitmap.Width;
            }
        }

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

        public enum ColorReductionAlgorithem
        {
            DeepCycle,           // My algorithem. Slow but thorow. It goes over all the colors in the image and reducing by Everaging every two dimilar colors each time.
            MedianCut
        }

        struct ColorCount
        {
            public Color color;
            public uint Instances;
        }

        struct SimilarColors
        {
            public ColorCount color1;
            public uint color1Idx;
            public ColorCount color2;
            public uint color2Idx;
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
        public void ImportImage(string fileName, ColorReductionAlgorithem colorAlg = ColorReductionAlgorithem.DeepCycle, ColorAvaragingMethod cam = ColorAvaragingMethod.RelaativeToNumberOfInstances)
        {
            //Sprite spriteImage = new Sprite();

            bitmap = new Bitmap(fileName);

            ColorPalette pallate;
            IntPtr intPtr = IntPtr.Zero;

            if (bitmap.PixelFormat == PixelFormat.Format4bppIndexed || bitmap.PixelFormat == PixelFormat.Format1bppIndexed)
            {

                Rectangle rec = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                BitmapData data = bitmap.LockBits(rec, ImageLockMode.ReadWrite, PixelFormat.Format4bppIndexed);

                pallate = bitmap.Palette;
                if (pallate.Entries.Count() <= 16)
                {
                    if (pallate.Entries.Count() > 4) spriteData.Attached = true;
                    for (int i = 0; i < pallate.Entries.Count(); i++)
                    {
                        spriteData.Colors[i] = pallate.Entries[i].ToArgb();

                    }


                    spriteData.NumOfRaws = bitmap.Height;
                    spriteData.SpriteWidth = (byte)bitmap.Width;

                    if (pallate.Entries.Count() > 4)
                    {
                        spriteData.Attached = true;             
                    }
                    else
                    {
                        spriteData.Attached = false;
                    }

                    spriteData.SpriteData = new ulong[NumOfSprites(), 2, spriteData.NumOfRaws];

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

                    bitmap.UnlockBits(data);

                }
            }
            else if (bitmap.PixelFormat == PixelFormat.Format24bppRgb || bitmap.PixelFormat == PixelFormat.Format32bppArgb)
            {
                
                    spriteData.Attached = true;

                    switch (colorAlg)
                    {
                        case ColorReductionAlgorithem.DeepCycle:
                            spriteData.NumOfRaws = Height;
                            spriteData.SpriteWidth = (byte)Width;
                            CountColors();
                            ReduceTo16Colors(colorAlg,cam);
                            spriteData.SpriteData = new ulong[NumOfSprites(), 2, spriteData.NumOfRaws];
                            CopyColorsToPallate(ColorReductionAlgorithem.DeepCycle);
                            ConvertImageToBitplanes(ColorReductionAlgorithem.DeepCycle);


                          
                        // Run My Algorithem

                            break;
                        case ColorReductionAlgorithem.MedianCut:
                            ListTheColors();
                            SortByColor(FindTheDominantColor());
                            //Backets[0] = new List<Color>();
                            //Backets[0].Add(SystemColors.Control);
                            DevideToBackets();
                            AvaragingToPallate();
                            CopyColorsToPallate(ColorReductionAlgorithem.MedianCut);
                            spriteData.SpriteWidth = (byte)Width;
                            spriteData.NumOfRaws = Height;
                            spriteData.Attached = true;
                            spriteData.SpriteData = new ulong[NumOfSprites(), 2, spriteData.NumOfRaws];
                            ConvertImageToBitplanes(ColorReductionAlgorithem.MedianCut);
                        break;

                    }
               
            }
            else
                MessageBox.Show("Color Pallate need to be 4 or 16 colors only!", "Pallate Error", MessageBoxButtons.OK);

        }

        public void CountColors()
        {
            colors.Clear();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    Color Tcolor = new Color();
                    ColorCount colorCount = new ColorCount();
                    Tcolor = bitmap.GetPixel(x, y);
                    colorCount.color = Tcolor;
                    //colorCount.color = Tcolor;
                    colorCount.Instances = 1;
                    if (!isInList(colorCount.color) /*&& colorCount.color.A != 0*/)
                    {
                        colors.Add(colorCount);
                    }
                }

        }

        private int NumOfSprites ()
        {
            return spriteData.Attached ? 2 : 1;
        }

        public void ImportIndexedImage(string fileName)
        {
            //Sprite spriteImage = new Sprite();

            bitmap = new Bitmap(fileName);

            ColorPalette pallate;
            IntPtr intPtr = IntPtr.Zero;

            Rectangle rec = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(rec, ImageLockMode.ReadWrite, PixelFormat.Format4bppIndexed);

            intPtr = data.Scan0;
            int NumOfBytes = Math.Abs(data.Stride) * data.Height;
            byte[] indexValues = new byte[NumOfBytes];
            Marshal.Copy(intPtr, indexValues, 0, NumOfBytes);
            int NumOfSprites;

            switch (bitmap.PixelFormat)
            {
                case PixelFormat.Format4bppIndexed:

                    pallate = bitmap.Palette;
                    if (pallate.Entries.Count() <= 16)
                    {
                        if (pallate.Entries.Count() > 4) spriteData.Attached = true;
                        for (int i = 0; i < pallate.Entries.Count(); i++)
                        {
                            spriteData.Colors[i] = pallate.Entries[i].ToArgb();

                        }


                        spriteData.NumOfRaws = bitmap.Height;
                        spriteData.SpriteWidth = (byte)bitmap.Width;

                        if (pallate.Entries.Count() > 4)
                        {
                            spriteData.Attached = true;
                            NumOfSprites = 2;
                        }
                        else
                        {
                            spriteData.Attached=false;
                            NumOfSprites = 1;
                        }

                        spriteData.SpriteData = new ulong[NumOfSprites,2,spriteData.NumOfRaws];

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

                        bitmap.UnlockBits(data);

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
       public enum ColorAvaragingMethod
        {
            RelaativeToNumberOfInstances,
            NonRelative
        }
        public void ReduceTo16Colors(ColorReductionAlgorithem redunctionAlg = ColorReductionAlgorithem.DeepCycle,ColorAvaragingMethod cam = ColorAvaragingMethod.RelaativeToNumberOfInstances)
        {
            spriteData.Attached = true;
            spriteData.NumOfColors = 16;

            switch (redunctionAlg)
            {
                case ColorReductionAlgorithem.DeepCycle:
                    SimilarColors similarColors;
                    Color AvgColor;
                    int ProgressBarMax = colors.Count;
                    if (progressBar != null)
                    {
                        progressBar.Maximum = colors.Count;
                        progressBar.Minimum = 16;
                        //progressBar.Value = 0;
                    }


                    while (colors.Count > 16)
                    {
                        similarColors = FindCloseColors();
                        if (cam == ColorAvaragingMethod.RelaativeToNumberOfInstances)
                            AvgColor = AvarageColors(similarColors.color1, similarColors.color2);
                        else
                            AvgColor = AvarageColors(similarColors.color1.color, similarColors.color2.color);

                        ReplaceColors(similarColors, AvgColor);
                        ColorCount newCC = new ColorCount();
                        newCC.color = AvgColor;
                        newCC.Instances = similarColors.color1.Instances + similarColors.color2.Instances;

                        colors.RemoveAll(x => x.color == similarColors.color1.color);
                        colors.RemoveAll(x => x.color == similarColors.color2.color);
                        colors.Add(newCC);
                        if (progressBar != null)
                            progressBar.Value = ProgressBarMax - colors.Count + 16;
                    }
                    break;
                case ColorReductionAlgorithem.MedianCut:
                    break;
            }
        }

        public Color AvarageColors(Color color1, Color color2)
        {

            int Red = (color1.R + color2.R) / 2;
            int Green = (color1.G + color2.G) / 2;
            int Blue = (color1.B + color2.B) / 2;
            return Color.FromArgb(255, Red, Green, Blue);
        }

        private Color AvarageColors(ColorCount color1, ColorCount color2)
        {
            int TotalInstances = (int)(color1.Instances + color2.Instances);
            int Red = (int)Math.Round((decimal)(color1.Instances * color1.color.R + color2.Instances * color2.color.R) / TotalInstances);
            int Green = (int)Math.Round((decimal)(color1.Instances * color1.color.G + color2.Instances * color2.color.G) / TotalInstances);
            int Blue = (int)Math.Round((decimal)(color1.Instances * color1.color.B + color2.Instances * color2.color.B) / TotalInstances);
            return Color.FromArgb(255, Red, Green, Blue);

        }


        private void ReplaceColors(SimilarColors sc, Color NewColor)
        {
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                {

                    if (bitmap.GetPixel(x, y) == sc.color1.color || bitmap.GetPixel(x, y) == sc.color2.color)
                    {
                        bitmap.SetPixel(x, y, NewColor);
                    }
                }
        }

        private SimilarColors FindCloseColors()
        {
            //ColorCount[] TwoCloseColors = new ColorCount[2];
            SimilarColors TwoCloseColors = new SimilarColors();
            float Difiration = 1, TempDifiration;

            for (int i = 1; i < colors.Count; i++)
                for (int j = i + 1; j < colors.Count; j++)
                {
                    TempDifiration = ColorDifference(colors[i].color, colors[j].color);

                    if (TempDifiration < Difiration)
                    {
                        Difiration = TempDifiration;
                        TwoCloseColors.color1 = colors[i];
                        TwoCloseColors.color1Idx = (uint)i;
                        TwoCloseColors.color2 = colors[j];
                        TwoCloseColors.color2Idx = (uint)j;
                    }
                }

            return TwoCloseColors;

        }

        private float ColorDifference(Color color1, Color color2)
        {
            float RedDiff = Math.Abs(color1.R - color2.R) / 255f;
            return (Math.Abs(color1.R - color2.R) / 255f + Math.Abs(color1.G - color2.G) / 255f + Math.Abs(color1.B - color2.B) / 255f) / 3f;
        }

        //-------------------------------------------------------------------------------------
        // MedianCut methods
        //-------------------------------------------------------------------------------------

        private DominantColor FindTheDominantColor()
        {
            Color pixelColor;
            uint Red = 0, Green = 0, Blue = 0;

            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                {
                    pixelColor = bitmap.GetPixel(x, y);
                    Red += pixelColor.R;
                    Green += pixelColor.G;
                    Blue += pixelColor.B;
                }

            if (Red > Green && Red > Blue)
                return DominantColor.Red;
            else if (Blue > Green && Blue > Red)
                return DominantColor.Blue;
            else
                return DominantColor.Green;
        }

        private void SortByColor(DominantColor color)
        {

            int DeltaColor = 0;
            while (colors.Count > 0)
            {
                int selectedColorIdx = 0;
                for (int i = 0; i < colors.Count; i++)
                {

                    switch (color)
                    {
                        case DominantColor.Red:
                            if (colors[i].color.R > colors[selectedColorIdx].color.R)
                                selectedColorIdx = i;
                            break;
                        case DominantColor.Green:
                            if (colors[i].color.G > colors[selectedColorIdx].color.G)
                                selectedColorIdx = i;
                            break;
                        case DominantColor.Blue:
                            if (colors[i].color.B > colors[selectedColorIdx].color.B)
                                selectedColorIdx = i;
                            break;
                    }
                }
                OrgenizedColors.Add(colors[selectedColorIdx].color);
                colors.RemoveAt(selectedColorIdx);
            }
        }

        public void ListTheColors()
        {
            colors.Clear();
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                {
                    Color Tcolor = new Color();
                    ColorCount colorCount = new ColorCount();
                    Tcolor = bitmap.GetPixel(x, y);

                    // Fix transparent colors on images that have colors that are transparency

                    colorCount.color = Color.FromArgb(255, Tcolor.R, Tcolor.G, Tcolor.B);
                    //colorCount.color = Tcolor;
                    colorCount.Instances = 1;
                    if (!isInList(colorCount.color) && colorCount.color.A != 0)
                    {
                        colors.Add(colorCount);

                    }
                }

        }

        private bool isInList(Color color)
        {
            for (int i = 0; i < colors.Count; i++)
            {
                if (colors[i].color == color)
                {
                    ColorCount count = new ColorCount();
                    count.color = colors[i].color;
                    count.Instances = colors[i].Instances + 1;
                    colors[i] = count;
                    return true;
                }
            }
            return false;
        }

        const int NumOfColorsInPallate = 15;
        private void DevideToBackets()
        {
            for (int i = 0; i < OrgenizedColors.Count; i++)
            {
                int idx = i / (OrgenizedColors.Count / NumOfColorsInPallate);
                if (idx > NumOfColorsInPallate - 1) idx = NumOfColorsInPallate - 1;

                if (Backets[idx] == null)
                    Backets[idx] = new List<Color>();

                Backets[idx].Add(OrgenizedColors[i]);
            }
        }

        private void AvaragingToPallate()
        {
            Pallate[0] = SystemColors.Control;
            for (int i = 0; i < 15; i++)
                Pallate[i + 1] = AvaragingListColors(Backets[i]);
        }

        private Color AvaragingListColors(List<Color> colorList)
        {
            int Red = 0, Green = 0, Blue = 0;

            for (int i = 0; i < colorList.Count; i++)
            {
                Red += colorList[i].R;
                Green += colorList[i].G;
                Blue += colorList[i].B;
            }

            Red /= colorList.Count;
            Green /= colorList.Count;
            Blue /= colorList.Count;

            return Color.FromArgb(Red, Green, Blue);
        }

        public void ConvertImageToBitplanes(ColorReductionAlgorithem Alg)
        {
            Color pixelColor;
            int ColorIdx;
            byte colorInPalate;


            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                {
                    switch (Alg)
                    {
                        case ColorReductionAlgorithem.DeepCycle:
                            pixelColor = bitmap.GetPixel(x, y);
                            ColorIdx = GetColorPositionInPallate(pixelColor);
                            SetPixel(x, y, (ulong)ColorIdx);
                            break;
                        case ColorReductionAlgorithem.MedianCut:
                            colorInPalate = ConvertColorToPalate(bitmap.GetPixel(x, y));
                            SetPixel(x, y, colorInPalate);
                            break;
                    }
                }
        }

        public int GetColorPositionInPallate(Color colorToFind)
        {
            //if (colorToFind.A != 0)
            // {
            for (int i = 0; i < colors.Count; i++)
            {
                if (colors[i].color == colorToFind)
                {
                    return i;
                }
            }
            // }
            return 0;
        }

        private byte ConvertColorToPalate(Color color)
        {
            if (color.A != 0)
            {
                for (byte i = 0; i < 15; i++)
                    foreach (Color c in Backets[i])
                    {
                        if (c.R == color.R && c.G == color.G && c.B == color.B)
                            return (byte)(i + 1);
                    }
            }
            return 0;
        }
        private void CopyColorsToPallate(ColorReductionAlgorithem arg)
        {
            if (spriteData.Colors == null)
                spriteData.Colors = new int[16];
            switch (arg)
            {
   
                case ColorReductionAlgorithem.DeepCycle:
                    for (int i = 0; i < colors.Count; i++)
                    {
                        spriteData.Colors[i] = colors[i].color.ToArgb();
                    }

                    if (colors.Count > 4)
                        spriteData.NumOfColors = 16;
                    else
                        spriteData.NumOfColors = 4;

                    spriteData.Colors[0] = SystemColors.Control.ToArgb();
                    break;
                case ColorReductionAlgorithem.MedianCut:
                    for (int i = 0; i < 16; i++)
                    {
                        spriteData.Colors[i] = Pallate[i].ToArgb();
                    }
                    spriteData.NumOfColors = 16;
                    break;
        }
        }
    }
}