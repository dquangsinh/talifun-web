using System;
using System.Collections.Specialized;
using EcmaScript.NET;

namespace Yahoo.Yui.Compressor
{
    public class CustomErrorReporter : ErrorReporter
    {
        private readonly bool _isVerboseLogging;
        public StringCollection ErrorMessages { get; private set; }

        public CustomErrorReporter(bool isVerboseLogging)
        {
            _isVerboseLogging = isVerboseLogging;
            ErrorMessages = new StringCollection();
        }

        public virtual void Warning(string message,
            string sourceName,
            int line,
            string lineSource,
            int lineOffset)
        {
            if (_isVerboseLogging)
            {
                string text = "[WARNING] " + message;
                Console.WriteLine(text);
                //ErrorMessages.Add(
                //    string.Format("[WARNING] {0} ******** Source: {1}. Line: {2}. LineSource: {3}. LineOffset: {4}",
                //                  message,
                //                  sourceName,
                //                  line,
                //                  lineSource,
                //                  lineOffset));
                ErrorMessages.Add(text);
            }
        }

        public virtual void Error(string message,
            string sourceName,
            int line,
            string lineSource,
            int lineOffset)
        {
            //throw new InvalidOperationException(
            //    string.Format("[ERROR] {0} ******** Source: {1}. Line: {2}. LineSource: {3}. LineOffset: {4}",
            //                  message,
            //                  sourceName,
            //                  line,
            //                  lineSource,
            //                  lineOffset));
            throw new InvalidOperationException("[ERROR] " + message);
        }

        public virtual EcmaScriptRuntimeException RuntimeError(string message,
            string sourceName,
            int line,
            string lineSource,
            int lineOffset)
        {
            //throw new InvalidOperationException(
            //    string.Format("[ERROR] {0} ******** Source: {1}. Line: {2}. LineSource: {3}. LineOffset: {4}",
            //                  message,
            //                  sourceName,
            //                  line,
            //                  lineSource,
            //                  lineOffset));
            throw new InvalidOperationException("[ERROR] EcmaScriptRuntimeException :: " + message);
        }
    }
}
