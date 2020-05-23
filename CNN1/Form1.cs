﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;

namespace CNN1
{
    public partial class Form1 : Form
    {
        bool Run = false;
        public static double[,] image = new double[28, 28];
        int imageiterator = 0;
        int testiterator = 0;
        int BatchSize = 1;
        bool Testing = false;
        NN nn = new NN();
        void Learn()
        {
            new Thread(() =>
            {
                while (Run)
                {
                    image = Reader.ReadNextImage();
                    int correct = Reader.ReadNextLabel();
                    if (!Testing)
                    {
                        for (int i = 0; i < BatchSize; i++)
                        {
                            image = Reader.ReadNextImage(); correct = Reader.ReadNextLabel();
                            nn.Run(image, correct, false);
                        }
                        nn.Run(BatchSize);
                    }
                    else
                    {
                        if (testiterator > 10000) { Run = false; MessageBox.Show("Full epoch completed"); }
                        nn.Run(image, correct, true); testiterator++;
                    }

                    Invoke((Action)delegate {
                        AvgGradTxt.Text = Math.Round(nn.AvgGradient, 15).ToString();
                        AvgCorrectTxt.Text = Math.Round(nn.PercCorrect, 15).ToString();
                        ErrorTxt.Text = Math.Round(nn.Error, 15).ToString();
                        if (imageiterator > 100)
                        {
                            imageiterator = 0;
                            pictureBox1.Image = FromTwoDimIntArrayGray(ResizeImg(Rescale(image)));
                            GuessTxt.Text = nn.Guess.ToString();
                        }
                        imageiterator++;
                    });
                }
                Data.Write(nn);
            }).Start();
        }
        
        public Form1()
        {
            InitializeComponent();
            try
            {
                Data.Read(nn);
                Batchtxt.Text = BatchSize.ToString();
                INCountTxt.Text = nn.INCount.ToString();
                HidCountTxt.Text = nn.NCount.ToString();
                OutCountTxt.Text = nn.ONCount.ToString();
                LayersTxt.Text = nn.NumLayers.ToString();
                ConvPoolsTxt.Text = nn.NumConvPools.ToString();
                AlphaTxt.Text = NN.LearningRate.ToString();
                BetaTxt.Text = NN.Momentum.ToString();
                RMSDecayTxt.Text = NN.RMSDecay.ToString();
                RMSCheck.Checked = NN.UseRMSProp;
                MomentumCheck.Checked = NN.UseMomentum;
            }
            catch { MessageBox.Show("Failed to load data; reset to default"); Data.Running = false; nn.Init(); Data.Write(nn); }
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            if (Run == true) { MessageBox.Show("Already running"); return; }
            Run = true;
            Learn();
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            Run = false;
        }

        private void Button3_Click(object sender, EventArgs e)
        {
            if (Run) { MessageBox.Show("Cannot reset while running"); return; }
            OutCountTxt.Text = "10";
            if (
                !double.TryParse(BetaTxt.Text, out double momentum)
                || !double.TryParse(AlphaTxt.Text, out double learningrate)
                || !int.TryParse(LayersTxt.Text, out int layercount)
                || !int.TryParse(ConvPoolsTxt.Text, out int convpoolcount)
                || !int.TryParse(INCountTxt.Text, out int incount)
                || !int.TryParse(HidCountTxt.Text, out int hidcount)
                || !int.TryParse(OutCountTxt.Text, out int outcount)
                )
            { MessageBox.Show("Invalid parameters"); return; }
            nn = new NN(momentum, learningrate, layercount, incount, hidcount, outcount, convpoolcount);
            nn.Init();
            Data.Write(nn);
        }

        private void Button4_Click(object sender, EventArgs e)
        {
            nn.TrialNum = 0;
        }
        
        int[,] ResizeImg(double[,] input)
        {
            int scale = 10;
            int[,] scaled = new int[28 * scale, 28 * scale];
            //Foreach int in Obstacles
            for (int j = 0; j < 28; j++)
            {
                for (int jj = 0; jj < 28; jj++)
                {
                    //Scale by scale
                    for (int i = 0; i < scale; i++)
                    {
                        for (int ii = 0; ii < scale; ii++)
                        {
                            scaled[(j * scale) + i, (jj * scale) + ii] = (int)input[jj, j];
                        }
                    }
                }
            }
            return scaled;
        }
        
        public static double[,] Rescale(double[,] array)
        {
            double setmin = 0, setmax = 0;
            //Find the minimum and maximum values of the dataset
            foreach (double d in array)
            {
                if (d > setmax) { setmax = d; }
                if (d < setmin) { setmin = d; }
            }
            //Rescale the dataset
            for (int i = 0; i < array.GetLength(0); i++)
            {
                for (int ii = 0; ii < array.GetLength(1); ii++)
                {
                    array[i, ii] = 255 * ((array[i, ii] - setmin) / (setmax - setmin));
                }
            }
            return array;
        }
        
        public static Bitmap FromTwoDimIntArrayGray(Int32[,] data)
        {
            // Transform 2-dimensional Int32 array to 1-byte-per-pixel byte array
            Int32 width = data.GetLength(0);
            Int32 height = data.GetLength(1);
            Int32 byteIndex = 0;
            Byte[] dataBytes = new Byte[height * width];
            for (Int32 y = 0; y < height; y++)
            {
                for (Int32 x = 0; x < width; x++)
                {
                    // logical AND to be 100% sure the int32 value fits inside
                    // the byte even if it contains more data (like, full ARGB).
                    dataBytes[byteIndex] = (Byte)(((UInt32)data[x, y]) & 0xFF);
                    // More efficient than multiplying
                    byteIndex++;
                }
            }
            // generate palette
            Color[] palette = new Color[256];
            for (Int32 b = 0; b < 256; b++)
                palette[b] = Color.FromArgb(b, b, b);
            // Build image
            return BuildImage(dataBytes, width, height, width, PixelFormat.Format8bppIndexed, palette, null);
        }
        
        /// <summary>
        /// Creates a bitmap based on data, width, height, stride and pixel format.
        /// </summary>
        /// <param name="sourceData">Byte array of raw source data</param>
        /// <param name="width">Width of the image</param>
        /// <param name="height">Height of the image</param>
        /// <param name="stride">Scanline length inside the data</param>
        /// <param name="pixelFormat">Pixel format</param>
        /// <param name="palette">Color palette</param>
        /// <param name="defaultColor">Default color to fill in on the palette if the given colors don't fully fill it.</param>
        /// <returns>The new image</returns>
        public static Bitmap BuildImage(Byte[] sourceData, Int32 width, Int32 height, Int32 stride, PixelFormat pixelFormat, Color[] palette, Color? defaultColor)
        {
            Bitmap newImage = new Bitmap(width, height, pixelFormat);
            BitmapData targetData = newImage.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, newImage.PixelFormat);
            Int32 newDataWidth = ((Image.GetPixelFormatSize(pixelFormat) * width) + 7) / 8;
            // Compensate for possible negative stride on BMP format.
            Boolean isFlipped = stride < 0;
            stride = Math.Abs(stride);
            // Cache these to avoid unnecessary getter calls.
            Int32 targetStride = targetData.Stride;
            Int64 scan0 = targetData.Scan0.ToInt64();
            for (Int32 y = 0; y < height; y++)
                Marshal.Copy(sourceData, y * stride, new IntPtr(scan0 + y * targetStride), newDataWidth);
            newImage.UnlockBits(targetData);
            // Fix negative stride on BMP format.
            if (isFlipped)
                newImage.RotateFlip(RotateFlipType.Rotate180FlipX);
            // For indexed images, set the palette.
            if ((pixelFormat & PixelFormat.Indexed) != 0 && palette != null)
            {
                ColorPalette pal = newImage.Palette;
                for (Int32 i = 0; i < pal.Entries.Length; i++)
                {
                    if (i < palette.Length)
                        pal.Entries[i] = palette[i];
                    else if (defaultColor.HasValue)
                        pal.Entries[i] = defaultColor.Value;
                    else
                        break;
                }
                newImage.Palette = pal;
            }
            return newImage;
        }
        
        private void RMSCheck_CheckedChanged(object sender, EventArgs e)
        {
            NN.UseRMSProp = RMSCheck.Checked;
        }

        private void MomentumCheck_CheckedChanged(object sender, EventArgs e)
        {
            NN.UseMomentum = MomentumCheck.Checked;
        }

        private void TestCheck_CheckedChanged(object sender, EventArgs e)
        {
            Reader.Testing = TestCheck.Checked;
            Testing = true;
        }

        private void RMSDecayTxt_TextChanged(object sender, EventArgs e)
        {
            if (!double.TryParse(RMSDecayTxt.Text, out double rmsrate)) { MessageBox.Show("NAN"); return; }
            if (rmsrate < 0 || rmsrate > 1) { MessageBox.Show("Invalid RMS decay rate"); return; }
            NN.RMSDecay = rmsrate;
        }

        private void BetaTxt_TextChanged(object sender, EventArgs e)
        {
            if (!double.TryParse(BetaTxt.Text, out double momentum)) { MessageBox.Show("NAN"); return; }
            if (momentum < 0 || momentum > 1) { MessageBox.Show("Invalid momentum"); return; }
            NN.Momentum = momentum;
        }

        private void AlphaTxt_TextChanged(object sender, EventArgs e)
        {
            if (!double.TryParse(AlphaTxt.Text, out double lr)) { MessageBox.Show("NAN"); return; }
            if (lr < 0 || lr > 1) { MessageBox.Show("Learning rate must be between 0 and 1"); return; }
            NN.LearningRate = lr;
        }

        private void Batchtxt_TextChanged(object sender, EventArgs e)
        {
            if (!int.TryParse(Batchtxt.Text, out int bs)) { MessageBox.Show("NAN"); return; }
            if (bs < 0 || bs > 1000) { MessageBox.Show("Batch size must be between 0 and 1000"); return; }
            BatchSize = bs;
        }
    }
}
