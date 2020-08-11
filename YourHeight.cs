using RTBPlugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

using GeoUK;
using GeoUK.Projections;
using GeoUK.Coordinates;
using GeoUK.Ellipsoids;

namespace YourHeightPlugin
{
    public class YourHeight : IPluginHeight
    {
        static MiniConfig config = null;
        internal static MiniConfig Config
        {
            get
            {
                if (config == null)
                    config = new MiniConfig(typeof(MiniConfig).Assembly.Location + ".config");
                return config;
            }
        }

        /// <summary>
        /// User control for displaying the slider on the RTB's New Venue window.
        /// </summary>
        ucNewProjectSettings ucNewProjectSettings = new ucNewProjectSettings();

        /// <summary>
        /// User control for displaying the slider on the RTB's Venue Properties window.
        /// </summary>
        ucProjectSettings ucProjectSettings = new ucProjectSettings();

        /// <summary>
        /// The amount the height will be multiplied by in the GetWaveHeight function.
        /// </summary>
        float HeightMultiplier = 1;

        public InputMethods InputMethod { get { return InputMethods.MetersXZ; } }

        public int TIMER_WAIT_SUCCESS { get { return 0; } }

        public int TIMER_WAIT_FAILED { get { return 0; } }

        public int MaximumPairCount { get { return 1000; } }

        public string Description { get { return "Scotland Phase 3 Lidar Height Plugin"; } }

        public string About { get { return "50cm resolution"; } }

        public int currentdatablock = 0;

        double[,] data = new double[5000, 5000];

        public void Initialize()
        {
            // Check log file directory exists

            if (!Directory.Exists(@"E:\temp"))
            {
                Directory.CreateDirectory(@"E:\temp");
            }
        }

        public void RenderNewProjectSettings(Panel panel)
        {
            ucNewProjectSettings.Dock = DockStyle.Fill;
            panel.Controls.Add(ucNewProjectSettings);
        }

        public void RenderProjectSettings(Panel panel)
        {
            ucProjectSettings.HeightMultiplier = HeightMultiplier; // Set the height setting based on the current project's value.
            ucProjectSettings.Dock = DockStyle.Fill;
            panel.Controls.Add(ucProjectSettings);
        }

        public bool ValidateNewProjectSettings(out string errorMessage)
        {
            errorMessage = "";
            return true;
        }

        public bool ValidateProjectSettings(out string errorMessage)
        {
            errorMessage = "";
            return true;
        }

        public void AcceptNewProjectSettings()
        {
            // Save the Noise setting.
            ucNewProjectSettings.AcceptNewProjectSettings();
            Config.Save();

            HeightMultiplier = ucNewProjectSettings.HeightMultiplier;
        }

        public void AcceptProjectSettings()
        {
            HeightMultiplier = ucProjectSettings.HeightMultiplier;
        }

        public double Fetch(double latitude_or_z, double longitude_or_x)
        {
            string lat = latitude_or_z.ToString();
            string lon = longitude_or_x.ToString();
            string[] fetchlog = { DateTime.Now + ": Fetch 1 lat " + lat + " lon " + lon + "\n" };
            File.AppendAllLines(@"E:\temp\Log.txt", fetchlog);

            return GetScot50cmLidarHeight(latitude_or_z, longitude_or_x);
        }

        public List<double> Fetch(List<LatLong> latitude_longitude_pairs)
        {
            List<double> heights = new List<double>(latitude_longitude_pairs.Count);
            foreach (var ll in latitude_longitude_pairs)
            {
                string[] flog = { DateTime.Now + ": Fetch 2 lat " + ll.latitude_or_z + " lon " + ll.longitude_or_x + "\n" };
                File.AppendAllLines(@"E:\temp\Log.txt", flog);

                heights.Add(GetScot50cmLidarHeight(ll.latitude_or_z, ll.longitude_or_x));
            }
            return heights;
        }

        private double GetScot50cmLidarHeight(double latitude, double longitude)
        {
            var latLong = new LatitudeLongitude(latitude, longitude);
            var bng = GeoUK.OSTN.Transform.Etrs89ToOsgb(latLong);

            string[] logentry0 = { DateTime.Now + ": fetching lat " + latitude + " lon " + longitude + "\n" };
            File.AppendAllLines(@"E:\temp\Log.txt", logentry0);

            // Calculate file location for requested data.
            int easting = ((int)(bng.Easting / 2500.0)) * 25;
            int northing = ((int)(bng.Northing / 2500.0)) * 25;
            int newdatablock = (easting * 10000) + northing;

            // file location is eastingnorthing.asc
            string path = @"E:\GIS_Scotland\50cm\" + easting + northing + ".asc";
            string[] logentry = { DateTime.Now + ": file " + path + "\n" };
            File.AppendAllLines(@"E:\temp\Log.txt", logentry);

            // Open required file.
            if (newdatablock != currentdatablock)
            {
                try
                {
                    using (TextReader file = File.OpenText(path))
                    {
                        string text;
                        string[] bits;
                        int counter = 0;

                        string[] logentry1 = { DateTime.Now + ": Loading file \n" };
                        File.AppendAllLines(@"E:\temp\Log.txt", logentry1);

                        // skip first 6 lines: no data we need here (for now)
                        text = file.ReadLine(); // no. of columns
                        text = file.ReadLine(); // no.of rows
                        text = file.ReadLine(); // x corner reference
                        text = file.ReadLine(); // y corner reference
                        text = file.ReadLine(); // cell size
                        text = file.ReadLine(); // null value

                        while ((text = file.ReadLine()) != null)
                        {
                            bits = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 0; i < 5000; i++)
                            {
                                data[counter, i] = double.Parse(bits[i]);
                            }
                            counter++;
                        }
                    }
                }
                catch (FileNotFoundException e)
                {
                    //Console.WriteLine($"Couldn't find '{e}'");
                    string[] logentry2 = { DateTime.Now + ": Couldn't find " + e + "\n " };
                    File.AppendAllLines(@"E:\temp\Log.txt", logentry2);

                    return (0.0);
                }
                catch (DirectoryNotFoundException e)
                {
                    //Console.WriteLine($"No such directory '{e}'");
                    string[] logentry3 = { DateTime.Now + ": No such directory " + e + "\n " };
                    File.AppendAllLines(@"E:\temp\Log.txt", logentry3);

                    return (0.0);
                }
                catch (IOException e)
                {
                    //Console.WriteLine($"Couldn't open '{e}'");
                    string[] logentry4 = { DateTime.Now + ": Couldn't open " + e + "\n " };
                    File.AppendAllLines(@"E:\temp\Log.txt", logentry4);

                    return (0.0);
                }
            }

            int eastloc = (int)((bng.Easting - (easting * 100)) * 2);
            int northloc = 5000 - (int)((bng.Northing - (northing * 100)) * 2);

            string[] logentry5 = { DateTime.Now + ": data location east " + eastloc + "north " + northloc + "\n" };
            File.AppendAllLines(@"E:\temp\Log.txt", logentry5);

            double returnval = data[northloc, eastloc];
            if (returnval < 0.0) returnval = 0.0;

            string[] logentry6 = { DateTime.Now + ": returning height " + returnval + "\n" };
            File.AppendAllLines(@"E:\temp\Log.txt", logentry6);

            currentdatablock = newdatablock;
            return (returnval);
        }

        public List<GameEngines> GetSupportedEngines()
        {
            List<GameEngines> support = new List<GameEngines>();
            support.Add(GameEngines.None);
            support.Add(GameEngines.AssettoCorsa);
            support.Add(GameEngines.rFactor);
            return support;
        }

        /// <summary>
        /// This is called when RTB Saves a project. Use it to store values that are specific to this project, that can then be reloaded via the Load() function.
        /// </summary>
        /// <param name="xml"></param>
        public void Save(string filename)
        {
            // Replace existing file if it exists.
            if (File.Exists(filename)) File.Delete(filename);

            // Create new file.
            using (FileStream fs = File.Create(filename))
            {
                using (BinaryWriter bw = new BinaryWriter(fs, Encoding.UTF8))
                {
                    bw.Write(HeightMultiplier);
                }
            }
        }

        /// <summary>
        /// This is called when RTB loads an existing project. Use it to load values specific to this project.
        /// </summary>
        /// <param name="xmlNode"></param>
        public void Load(string filename)
        {
            if (!File.Exists(filename)) return;
            using (FileStream fs = File.OpenRead(filename))
            {
                using (BinaryReader br = new BinaryReader(fs, System.Text.Encoding.UTF8))
                {
                    HeightMultiplier = br.ReadSingle();
                }
            }
        }
    }
}
