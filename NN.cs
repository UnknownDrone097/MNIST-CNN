using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace CNN1
{
    class NN
    {
        //Must be 1 for now
        static int Resolution = 28;
        public int NumConvPools = 1;
        public int NumLayers = 3;
        public int INCount = 10;
        public int NCount = 10;
        public int ONCount = 10;
        public int ConvSteps = 2;
        public int PoolSize = 2;
        public int KernelSize = 3;
        public List<Convolution> Convolutions { get; set; }
        public List<Pooling> Poolings { get; set; }
        public List<Layer> Layers { get; set; }
        static double Momentum = .9;
        static double LearningRate = .000146;

        public double TrialNum = 0;
        public double AvgGradient = 0;
        public double PercCorrect = 0;
        public double Error = 0;
        public double Guess { get; set; }
        public void Init()
        {
            Convolutions = new List<Convolution>();
            Poolings = new List<Pooling>();
            Layers = new List<Layer>();
            int count = NCount;
            int lowercount = Resolution;
            Random r = new Random();
            for (int i = 0; i < NumConvPools; i++)
            { Convolutions.Add(new Convolution(KernelSize)); Poolings.Add(new Pooling()); }
            for (int i = 0; i < NumLayers; i++)
            {
                if (NumConvPools == 0) { lowercount = Resolution * Resolution; }
                for (int ii = 0; ii < NumConvPools; ii++)
                {
                    lowercount = (((lowercount / ConvSteps) - KernelSize) / PoolSize) + 1;
                }
                if (i != 0) { lowercount = Layers[i - 1].Length; count = NCount; }
                if (i == 0) { count = INCount; }
                if (i == NumLayers - 1) { count = ONCount; }
                Layers.Add(new Layer(count, lowercount));
                for (int j = 0; j < count; j++)
                {
                    for (int jj = 0; jj < lowercount; jj++)
                    {
                        //Weights initialized to a random number in range (-1/(2 * lowercount^2) - 1/(2 * lowercount^2))
                        //This is Lecun initialization
                        Layers[i].Weights[j, jj] = (r.NextDouble() > .5 ? -1 : 1) * r.NextDouble() * Math.Sqrt(3d / (double)(lowercount * lowercount));
                    }
                }
            }
            for (int i = 0; i < NumConvPools; i++)
            {
                //Lowercount IS A TEMP VALUE FOR WHEN CONVOLVECOUNT = 1!
                lowercount = (lowercount / ConvSteps);
                Convolutions[i].Kernel = new double[KernelSize, KernelSize];
                for (int ii = 0; ii < KernelSize; ii++)
                {
                    for (int iii = 0; iii < KernelSize; iii++)
                    {
                        Convolutions[i].Kernel[ii, iii] = (r.NextDouble() > .5 ? -1 : 1) * r.NextDouble() * Math.Sqrt(3d / (double)(lowercount * lowercount));
                    }
                }
            }
        }
        public void Run()
        {
            double[,] image = Reader.ReadNextImage();
            double[,] input = new double[28, 28];
            //Deepclone?
            for(int i = 0; i < 28; i++) { for (int ii = 0; ii < 28; ii++) { input[i, ii] = image[i, ii]; } }
            int correct = Reader.ReadNextLabel();

            //Forward
            for (int i = 0; i < NumConvPools; i++)
            {
                input = Convolutions[i].Convolve(input, ConvSteps);
                input = Poolings[i].Pool(input, PoolSize);
            }
            for (int i = 0; i < Layers.Count; i++)
            {
                if (i == 0) { Layers[i].Calculate(input); continue; }
                Layers[i].Calculate(Layers[i - 1].Values, i == Layers.Count - 1);
            }
            //Backward
            for (int i = Layers.Count - 1; i >= 0; i--)
            {
                if (i == Layers.Count - 1) { Layers[i].Backprop(correct); continue; }
                Layers[i].Backprop(Layers[i + 1]);
            }
            for (int i = NumConvPools - 1; i >= 0; i--)
            {
                if (i == 0)
                {
                    Poolings[i].Backprop(Layers[0]);
                    Convolutions[i].Backprop(Poolings[i], PoolSize);
                }
                else
                {
                    //Undefined for now
                }
            }
            //Descend
            //Need a convolution descent loop
            for (int i = 0; i < NumConvPools; i++)
            {
                Convolutions[i].Descend(image, Momentum, LearningRate);
            }
            for (int i = 0; i < Layers.Count; i++)
            {
                if (i == 0) { Layers[i].Descend(input, Momentum, LearningRate); continue; }
                Layers[i].Descend(Layers[i - 1].Values, Momentum, LearningRate, i == Layers.Count - 1);
            }

            //Report values
            int guess = -1; double certainty = -5; double error = 0;
            for (int i = 0; i < ONCount; i++)
            {
                if (Layers[Layers.Count - 1].Values[i] > certainty) { guess = i; certainty = Layers[Layers.Count - 1].Values[i]; }
                error += ((i == correct ? 1d : 0d) - Layers[Layers.Count - 1].Values[i]) * ((i == correct ? 1d : 0d) - Layers[Layers.Count - 1].Values[i]);
            }
            TrialNum++;

            PercCorrect = (PercCorrect * ((TrialNum) / (TrialNum + 1))) + ((guess == correct) ? (1 / (TrialNum)) : 0d);
            Error = (Error * ((TrialNum) / (TrialNum + 1))) + (error * (1 / (TrialNum)));
        }
        public void Run(int batchsize)
        {
            double tempavg = 0; int numneurons = 0;
            foreach (Convolution c in Convolutions)
            {
                c.Descend(batchsize, LearningRate);
            }
            foreach (Layer l in Layers)
            {
                l.Descend(batchsize, LearningRate);
                tempavg += l.AvgGradient;
                numneurons += l.Values.Length;
            }
            tempavg /= numneurons * batchsize;
            if (TrialNum == 0) { TrialNum++; }
            AvgGradient = (AvgGradient * ((TrialNum) / (TrialNum + 1))) + (tempavg * (1 / (TrialNum)));
        }
    }
    class ActivationFunctions
    {
        public static double Tanh(double number)
        {
            return (Math.Pow(Math.E, 2 * number) - 1) / (Math.Pow(Math.E, 2 * number) + 1);
        }
        public static double TanhDerriv(double number)
        {
            return (1 - Math.Pow(Tanh(number), 2));
        }
        public static double[] Normalize(double[] array)
        {
            double mean = 0;
            double stddev = 0;
            //Calc mean of data
            foreach (double d in array) { mean += d; }
            mean /= array.Length;
            //Calc std dev of data
            foreach (double d in array) { stddev += (d - mean) * (d - mean); }
            stddev /= array.Length;
            stddev = Math.Sqrt(stddev);
            //Prevent divide by zero b/c of sigma = 0
            if (stddev == 0) { stddev = .000001; }
            //Calc zscore
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = (array[i] - mean) / stddev;
            }

            return array;
        }
        public static double[,] Normalize(double[,] array, int depth, int count)
        {
            double[] smallarray = new double[depth * count];
            int iterator = 0;
            for (int i = 0; i < depth; i++)
            {
                for (int ii = 0; ii < count; ii++)
                {
                    smallarray[iterator] = array[i, ii];
                    iterator++;
                }
            }
            smallarray = Normalize(smallarray);
            iterator = 0;
            for (int i = 0; i < depth; i++)
            {
                for (int ii = 0; ii < count; ii++)
                {
                    array[i, ii] = smallarray[iterator];
                    iterator++;
                }
            }
            return array;
        }
    }
    class Layer
    {
        public double[,] Weights { get; set; }
        double[,] WeightMomentum { get; set; }
        double[,] WeightGradient { get; set; }
        public double[] Biases { get; set; }
        double[] BiasMomentum { get; set; }
        double[] BiasGradient { get; set; }
        public double[] Values { get; set; }
        public double[] ZVals { get; set; }
        public double[] Errors { get; set; }
        public double AvgGradient { get; set; }
        public int Length { get; set; }
        public int InputLength { get; set; }
        public Layer(int length, int inputlength)
        {
            Length = length;
            InputLength = inputlength;

            Weights = new double[Length, InputLength];
            WeightGradient = new double[Length, InputLength];
            WeightMomentum = new double[Length, InputLength];

            Biases = new double[Length];
            BiasGradient = new double[Length];
            BiasMomentum = new double[Length];
        }
        public void Descend(int batchsize, double learningrate)
        {
            AvgGradient = 0;
            for (int i = 0; i < Length; i++)
            {
                for (int ii = 0; ii < InputLength; ii++)
                {
                    double gradient = learningrate * WeightGradient[i, ii] * (-2d / batchsize);
                    Weights[i, ii] -= gradient;
                    AvgGradient -= gradient;
                }
                Biases[i] -= learningrate * BiasGradient[i] * (-2d / batchsize);
            }
            WeightGradient = new double[Length, InputLength];
            BiasGradient = new double[Length];
        }
        public void Descend(double[] input, double momentum, double learningrate, bool output)
        {
            for (int i = 0; i < Length; i++)
            {
                for (int ii = 0; ii < InputLength; ii++)
                {
                    //Weight gradients
                    double wgradient = input[ii] * ActivationFunctions.TanhDerriv(ZVals[i]) * Errors[i];
                    WeightMomentum[i, ii] = (WeightMomentum[i, ii] * momentum) - (learningrate * wgradient);
                    WeightGradient[i, ii] += wgradient + WeightMomentum[i, ii];
                }
                if (output) { continue; }
                //Bias gradients
                double bgradient = ActivationFunctions.TanhDerriv(ZVals[i]) * Errors[i];
                BiasMomentum[i] = (BiasMomentum[i] * momentum) - (learningrate * bgradient);
                BiasGradient[i] += bgradient + BiasMomentum[i];
            }
        }
        public void Descend(double[,] input, double momentum, double learningrate)
        {
            double[] input2 = new double[input.Length];
            int iterator = 0;
            foreach (double d in input) { input2[iterator] = d; iterator++; }
            Descend(input2, momentum, learningrate, false);
        }
        public void Backprop(Layer output)
        {
            Errors = new double[Length];
            for (int k = 0; k < output.Length; k++)
            {
                for (int j = 0; j < Length; j++)
                {
                    Errors[j] += output.Weights[k, j] * ActivationFunctions.TanhDerriv(output.ZVals[k]) * output.Errors[k];
                }
            }
        }
        public void Backprop(int correct)
        {
            Errors = new double[Length];
            for (int i = 0; i < Length; i++)
            {
                Errors[i] = 2d * ((i == correct ? 1d : 0d) - Values[i]);
            }
        }
        public void Calculate(double[] input, bool output)
        {
            ZVals = new double[Length]; Values = new double[Length];
            for (int k = 0; k < Length; k++)
            {
                for (int j = 0; j < InputLength; j++)
                {
                    ZVals[k] += ((Weights[k, j] + WeightMomentum[k, j]) * input[j]);
                }
                if (!output)
                {
                    ZVals[k] += Biases[k] + BiasMomentum[k];
                    Values[k] = ActivationFunctions.Tanh(ZVals[k]);
                }
                else { Values[k] = ZVals[k]; }
            }
        }
        public void Calculate(double[,] input)
        {
            double[] input2 = new double[input.Length];
            int iterator = 0;
            foreach (double d in input) { input2[iterator] = d; iterator++; }
            Calculate(input2, false);
        }
    }
}