using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace FociFRAPnorm_dragonfly
{
    class Program
    {
        static void Main(string[] args)
        {
            string input;
            string temp;
            int avgFrames;
            Dictionary<string, Data> inputFiles = new Dictionary<string, Data>();
            do
            {
                Console.WriteLine("Select input directory:");
                input = Console.ReadLine();

                if (!input.EndsWith(@"\")) input += @"\";
            }
            while (!Directory.Exists(input));

            do
            {
                Console.WriteLine("Set number of frames for averaging:");
                temp = Console.ReadLine();

            }
            while (!int.TryParse(temp, out avgFrames));

            try
            {
                GetAllFiles(".txt", input, inputFiles);
                ProcessFiles(inputFiles, avgFrames);
            }
            catch(Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
            try
            {
                PrintResults(inputFiles, input);
            }
            catch(Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
            Console.WriteLine("Done!");
            Console.ReadLine();
        }
        /// <summary>
        /// Browse for files in a given directory
        /// </summary>
        /// <param name="ext"></param>
        /// <param name="dir"></param>
        /// <param name="inputFiles"></param>
        private static void GetAllFiles(string ext, string dir, Dictionary<string, Data> inputFiles)
        {
            foreach (string file in Directory.GetFiles(dir))
                if (file.EndsWith(ext) && !file.EndsWith("_Results.txt"))
                    inputFiles.Add(file, new Data());

            foreach (string folder in Directory.GetDirectories(dir))
                GetAllFiles(ext, folder, inputFiles);
        }
        /// <summary>
        /// Read and process the files in parallel
        /// </summary>
        /// <param name="inputFiles"></param>
        private static void ProcessFiles(Dictionary<string, Data> inputFiles, int avgFrames)
        {

            Parallel.ForEach(inputFiles, (file) => {
                file.Value.FileName = Path.GetFileNameWithoutExtension(file.Key);                
                file.Value.SetInputValues(File.ReadAllLines(file.Key));
                file.Value.ProcessFiles(avgFrames);
            });
        }
        //Write the final results to the directory
        private static void PrintResults(Dictionary<string, Data> inputFiles,string dir)
        {
            //find max
            int maxLength = int.MinValue;
            int TimeIndex = 0;
            string[] temp = new string[inputFiles.Count + 1];
            temp[0] = "Time";

            for (int i = 0; i < inputFiles.Count; i++)
            {
                temp[i + 1] = inputFiles.ElementAt(i).Value.FileName;
                if (inputFiles.ElementAt(i).Value.GetResult.Length > maxLength)
                {
                    maxLength = inputFiles.ElementAt(i).Value.GetResult.Length;
                    TimeIndex = i;
                }
            }

            string[] results = new string[maxLength + 1];
            results[0] = string.Join("\t", temp);

            for (int i = 0; i < maxLength; i++)
            {
                temp[0] = inputFiles.ElementAt(TimeIndex).Value.GetTime[i].ToString();
                for (int j = 0; j < inputFiles.Count; j++)
                    if (inputFiles.ElementAt(j).Value.GetResult.Length > i)
                        temp[j + 1] = inputFiles.ElementAt(j).Value.GetResult[i].ToString();
                    else
                        temp[j + 1] = "0";

                results[i+1] = string.Join("\t", temp);
            }

            File.WriteAllLines(dir + "Final_Results.txt",results);
        }
    }
    /// <summary>
    /// Class with data for each file
    /// </summary>
    class Data
    {
        private string _FileName;
        private double[] Time;
        private double[] MP1;//FRAP
        private double[] MP2;//no FRAP
        private double[] results;
        public string FileName
        {
            set
            {
                this._FileName = value;
            }
            get
            {
                return this._FileName;
            }
        }
        public double[] GetTime
        {
            get
            {
                return this.Time;
            }
        }
        public double[] GetResult
        {
            get
            {
                return this.results;
            }
        }
        public void ProcessFiles(int avgFrames)
        {
            int i, j, linesRemoved;
            double minVal = MP1.Min();
            bool include = false;
            double[] temp;

            for (i = avgFrames, j = avgFrames; i < this.MP1.Length; i++)
            {
                if (this.MP1[i] == minVal)
                    include = true;

                if (include)
                {
                    this.MP1[j] = this.MP1[i];
                    this.MP2[j] = this.MP2[i];
                    j++;
                }                
            }
            linesRemoved = i - j;

            temp = new double[j];
            Array.Copy(this.MP1, temp, temp.Length);
            temp = NormalizeTo1(temp, avgFrames);
            this.MP1 = temp;

            temp = new double[j];
            Array.Copy(this.MP2, temp, temp.Length);
            temp = NormalizeTo1(temp, avgFrames);
            this.MP2 = temp;

            this.results = new double[this.MP1.Length];
            //devide MP1 to MP2
            for (i = 0; i < this.MP1.Length; i++)
                this.results[i] = this.MP1[i] / this.MP2[i];

            minVal = this.results[avgFrames];

            for (i = 0; i < this.results.Length; i++)
                this.results[i] -= minVal;

            this.results = NormalizeTo1(this.results, avgFrames);

            Console.WriteLine(this.FileName + " lines removed: " + linesRemoved + "\tfinal length: " + this.results.Length + "");
        }
        
        public void SetInputValues(string[] inputValues)
        {
            int colL = inputValues.Length - 3;
            string[] temp;
            double valMP,valBG;

            Time = new double[colL];
            MP1 = new double[colL];
            MP2 = new double[colL];

            for(int row = 3, ind = 0; row < inputValues.Length; row++,ind++)
            {
                temp = inputValues[row].Split(new string[] { "\t" }, StringSplitOptions.None);

                if (temp.Length != 5) continue;

                double.TryParse(temp[0], out Time[ind]);
                
                double.TryParse(temp[1], out valMP);
                double.TryParse(temp[2], out valBG);
                this.MP1[ind] = valMP;// - valBG;

                double.TryParse(temp[3], out valMP);
                double.TryParse(temp[4], out valBG);
                this.MP2[ind] = valMP;// - valBG;
            }           
        }
        private double[] NormalizeTo1(double[] input, int avgFrames)
        {
            double max = 0;

            for (int i = 0; i < avgFrames; i++)
                max += input[i];

            max /= avgFrames;

            if (max != 0)
                for (int i = 0; i < input.Length; i++)
                    input[i] /= max;
            return input;
        }
    }
}
