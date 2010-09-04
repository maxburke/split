using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;

namespace Slim
{
    public class DebugFileLogger : IDisposable
    {
        public DebugFileLogger(string filenameTag)
        {
            AddFileLogger(filenameTag);
        }

        const string ListenerName = "SlimFileLogger";
        static TextWriterTraceListener Listener;

        [Conditional("DEBUG")]
        static void AddFileLogger(string filenameTag)
        {
            string fileName = string.Format("C:\\split_log\\DEBUG_{0}.log", Path.GetFileName(filenameTag));
            File.Delete(fileName);

            Listener = new TextWriterTraceListener(fileName, ListenerName);
            Debug.Listeners.Add(Listener);
        }

        [Conditional("DEBUG")]
        static void RemoveFileLogger()
        {
            Listener.Flush();
            Debug.Listeners.Remove(Listener);
        }

        public void Dispose()
        {
            if (Listener != null)
                RemoveFileLogger();
        }
    }
}
