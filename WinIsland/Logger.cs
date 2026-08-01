using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace WinIsland
{
    public class Logger
    {
        string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\WI_Latest.log";
        public Logger()
        {
            StartFileWriter();
        }
        private void StartFileWriter()
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            using(var stream = File.Open(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                String log = "Start LOG\nRunning WinIsland " + StaticStrings.version + "\n";
                byte[] info = new UTF8Encoding(true).GetBytes(log);
                try
                {
                    stream.Write(info, 0, info.Length);
                    stream.Close();
                }
                catch (Exception ex)
                {

                }
            }
        }
        public void log(string message, bool forceLog = false)
        {
            // Only log if verbose is enabled, or if forceLog is true (for critical messages)
            if (!forceLog && Settings.instance?.config?.verboseLog == false)
                return;

            DateTime currentDateTime = DateTime.Now;
            Console.WriteLine("[" + currentDateTime + "] " + message);
            using (var stream = File.Open(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                String log = "[" + currentDateTime + "] " + message + "\n";
                byte[] info = new UTF8Encoding(true).GetBytes(log);
                try
                {
                    stream.Write(info, 0, info.Length);
                    stream.Close();
                }
                catch (Exception ex)
                {

                }
            }
        }

        // Verbose logging - only logs if verboseLog is enabled
        public void logVerbose(string message)
        {
            log(message, forceLog: false);
        }

        // Critical logging - always logs regardless of verboseLog setting
        public void logCritical(string message)
        {
            log(message, forceLog: true);
        }
        public List<Stopwatch> counters = new List<Stopwatch>();
        public Stopwatch startCounter()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            counters.Add(stopwatch);
            return stopwatch;
        }
        public void stopCounter(Stopwatch stopwatch, string name, bool forceLog = false)
        {
            foreach(Stopwatch stp in counters)
            {
                if(stp == stopwatch)
                {
                    stp.Stop();
                    log(name + " took " + stp.Elapsed, forceLog);
                }
            }
        }
    }
}
