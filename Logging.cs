// Copyright (C) 2024 Jens-Kristian Myklebust
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

namespace PrinterConnector
{
    internal class Logging
    {
        private StreamWriter LogFileWriter { get; set; }

        public enum LogSeverity { 
            Information,
            Warning,
            Error
        }

        public Logging()
        {
            ConfigureLogger(@"C:\temp", "printerlog.log");
            if(null  == LogFileWriter)
            {
                throw new NullReferenceException("Creating Log file writer failed");
            }
        }

        public Logging (string logFolder, string logfileName)
        {
            ConfigureLogger(logFolder, logfileName);
            if (null == LogFileWriter)
            {
                throw new NullReferenceException("Creating Log file writer failed");
            }
        }

        private void ConfigureLogger(string logFolder, string logfileName)
        {
            string logPath = Path.Combine(logFolder, logfileName);
            FileStream fileStream;
            // Try to create/open logfile in designated path, if it fails we fall back to using the user's temp folder.
            try
            {
                fileStream = File.Open(logPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            }
            catch
            {
                fileStream = File.Open(Path.Combine(Path.GetTempPath(), logfileName), FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            }
            LogFileWriter = new StreamWriter(fileStream);
        }

        public void TeeLogMessage(string message)
        {
            TeeLogMessage(message, LogSeverity.Information);
        }

        public void TeeLogMessage(string message, LogSeverity logSeverity)
        {
            string logString = LogString(message, logSeverity);
            Console.WriteLine(logString);
            LogFileWriter.WriteLine(logString);
            LogFileWriter.Flush();
        }
        
        private static string LogString(string message, LogSeverity logSeverity)
        {
            return DateTime.Now.ToString("s") + " [" + logSeverity.ToString() + "] - " + message;
        }
    }
}
