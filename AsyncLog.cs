using log4net;
using log4net.Core;
using log4net.Util;
using System;
using System.Collections.Concurrent;

namespace Woof.log4net {

    /// <summary>
    /// Assynchronous (non-blocking) wrapper for log4net logs.
    /// Public methods will return before appender is called.
    /// This method must be disposed to ensure all events are properly written to appenders.
    /// </summary>
    public sealed class AsyncLog : ILog, IDisposable {

        /// <summary>
        /// Creates asynchronous version of provided base log.
        /// </summary>
        /// <param name="baseLog">Synchronous log.</param>
        public AsyncLog(ILog baseLog) : this() => this.baseLog = baseLog;

        #region Asynchronous operation implementaion

        /// <summary>
        /// Base log.
        /// </summary>
        private readonly ILog baseLog;

        /// <summary>
        /// Dequeueing timer.
        /// </summary>
        private readonly System.Timers.Timer t;

        /// <summary>
        /// Logging events queue.
        /// </summary>
        private readonly ConcurrentQueue<LoggingEvent> q;

        /// <summary>
        /// Initializes timer and queue.
        /// </summary>
        private AsyncLog() {
            q = new ConcurrentQueue<LoggingEvent>();
            t = new System.Timers.Timer() {
                AutoReset = false,
                Interval = 1
            };
            t.Elapsed += Timer_Elapsed;
        }

        /// <summary>
        /// Flushes events.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            lock(t) while (q.TryDequeue(out LoggingEvent evt)) baseLog.Logger.Log(evt);
        }

        /// <summary>
        /// Disposes the timer and flushes all remaining events.
        /// </summary>
        public void Dispose() {
            lock (t) t.Dispose();
            while (q.TryDequeue(out LoggingEvent evt)) baseLog.Logger.Log(evt);
        }

        /// <summary>
        /// Finalizer checks if there are no discarded events and throws exception if they are.
        /// </summary>
        ~AsyncLog() {
            if (!q.IsEmpty) {
                t.Dispose();
                throw new InvalidOperationException($"Dispose() not called on AsyncLog. {q.Count} events discarded.");
            }
        }

        #endregion

        #region Extended logging (signaling)

        /// <summary>
        /// Sends a signal event.
        /// </summary>
        /// <param name="level">Level of logging event.</param>
        /// <param name="id">Event type identifier.</param>
        /// <param name="message">The application supplied message.</param>
        public void Signal(Level level, int id, string message = null) {
            if (!IsLevelEnabled(level)) return;
            q.Enqueue(GetEvent(level, message, id));
            t.Start();
        }

        /// <summary>
        /// Sends a signal event.
        /// </summary>
        /// <param name="level">Level of logging event.</param>
        /// <param name="id">Event type identifier.</param>
        /// <param name="message">The application supplied message.</param>
        /// <param name="properties">Additional event properties.</param>
        public void Signal(Level level, int id, string message, PropertiesDictionary properties) {
            if (!IsLevelEnabled(level)) return;
            q.Enqueue(GetEvent(level, message, id, properties));
            t.Start();
        }

        /// <summary>
        /// Sends a signal event.
        /// </summary>
        /// <param name="level">Level of logging event.</param>
        /// <param name="id">Event type identifier.</param>
        /// <param name="message">The application supplied message.</param>
        /// <param name="exception">Application exception.</param>
        public void Signal(Level level, int id, string message, Exception exception) {
            if (!IsLevelEnabled(level)) return;
            q.Enqueue(GetEvent(level, message, id, null, exception));
            t.Start();
        }

        /// <summary>
        /// Sends a signal event.
        /// </summary>
        /// <param name="level">Level of logging event.</param>
        /// <param name="id">Event type identifier.</param>
        /// <param name="message">The application supplied message.</param>
        /// <param name="properties">Additional event properties.</param>
        /// <param name="exception">Application exception.</param>
        public void Signal(Level level, int id, string message, PropertiesDictionary properties, Exception exception) {
            if (!IsLevelEnabled(level)) return;
            q.Enqueue(GetEvent(level, message, id, properties, exception));
            t.Start();
        }

        /// <summary>
        /// Determines if specified logging level is enabled.
        /// </summary>
        /// <param name="level">Logging level.</param>
        /// <returns>True if it is enabled in current log.</returns>
        private bool IsLevelEnabled(Level level) =>
            (level == Level.Debug && IsDebugEnabled) ||
            (level == Level.Info && IsInfoEnabled) ||
            (level == Level.Warn && IsWarnEnabled) ||
            (level == Level.Error && IsErrorEnabled) ||
            (level == Level.Fatal && IsFatalEnabled);

        #endregion

        #region Logging event generation

        /// <summary>
        /// Creates a new logging event.
        /// </summary>
        /// <param name="level">Level of logging event.</param>
        /// <param name="message">The application supplied message.</param>
        /// <param name="eventId">Optional event type identifier.</param>
        /// <param name="properties">Optional event properties.</param>
        /// <param name="exception">Optional exception.</param>
        /// <returns>New logging event.</returns>
        private LoggingEvent GetEvent(Level level, string message, int eventId = 0, PropertiesDictionary properties = null, Exception exception = null) {
            var eventData = new LoggingEventData {
                LoggerName = baseLog.Logger.Name,
                Level = level,
                TimeStampUtc = DateTime.UtcNow,
                Message = message
            };
            if (eventId > 0) {
                if (properties == null) properties = new PropertiesDictionary { ["EventID"] = eventId };
                else properties["EventID"] = eventId;
            }
            if (properties != null) eventData.Properties = properties;
            if (exception != null) eventData.ExceptionString = exception.ToString();
            return new LoggingEvent(eventData);
        }

        #endregion

        #region Interface implementation

        #region Properties

        public bool IsDebugEnabled => baseLog.IsDebugEnabled;

        public bool IsInfoEnabled => baseLog.IsInfoEnabled;

        public bool IsWarnEnabled => baseLog.IsWarnEnabled;

        public bool IsErrorEnabled => baseLog.IsErrorEnabled;

        public bool IsFatalEnabled => baseLog.IsFatalEnabled;

        public ILogger Logger => baseLog.Logger;

        #endregion

        #region Methods

        #region Debug level

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">Message content.</param>
        public void Debug(object message) {
            if (!baseLog.IsDebugEnabled) return;
            q.Enqueue(GetEvent(Level.Debug, (string)message));
            t.Start();
        }

        /// <summary>
        /// Logs a debug message and an exception.
        /// </summary>
        /// <param name="message">Message content.</param>
        /// <param name="exception">Application exception.</param>
        public void Debug(object message, Exception exception) {
            if (!baseLog.IsDebugEnabled) return;
            q.Enqueue(GetEvent(Level.Debug, (string)message, 0, null, exception));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted debug message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        public void DebugFormat(string format, params object[] args) {
            if (!baseLog.IsDebugEnabled) return;
            var message = String.Format(format, args);
            q.Enqueue(GetEvent(Level.Debug, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted debug message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        public void DebugFormat(string format, object arg0) {
            if (!baseLog.IsDebugEnabled) return;
            var message = String.Format(format, arg0);
            q.Enqueue(GetEvent(Level.Debug, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted debug message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        /// <param name="arg1">The second object to format.</param>
        public void DebugFormat(string format, object arg0, object arg1) {
            if (!baseLog.IsDebugEnabled) return;
            var message = String.Format(format, arg0, arg1);
            q.Enqueue(GetEvent(Level.Debug, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted debug message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        /// <param name="arg1">The second object to format.</param>
        /// <param name="arg2">The third object to format.</param>
        public void DebugFormat(string format, object arg0, object arg1, object arg2) {
            if (!baseLog.IsDebugEnabled) return;
            var message = String.Format(format, arg0, arg1, arg2);
            q.Enqueue(GetEvent(Level.Debug, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted debug message.
        /// </summary>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        public void DebugFormat(IFormatProvider provider, string format, params object[] args) {
            if (!baseLog.IsDebugEnabled) return;
            var message = String.Format(provider, format, args);
            q.Enqueue(GetEvent(Level.Debug, message));
            t.Start();
        }

        #endregion

        #region Info level

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">Message content.</param>
        public void Info(object message) {
            if (!baseLog.IsInfoEnabled) return;
            q.Enqueue(GetEvent(Level.Info, (string)message));
            t.Start();
        }

        /// <summary>
        /// Logs an informational message and an exception.
        /// </summary>
        /// <param name="message">Message content.</param>
        /// <param name="exception">Application exception.</param>
        public void Info(object message, Exception exception) {
            if (!baseLog.IsInfoEnabled) return;
            q.Enqueue(GetEvent(Level.Info, (string)message, 0, null, exception));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted informational message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        public void InfoFormat(string format, params object[] args) {
            if (!baseLog.IsInfoEnabled) return;
            var message = String.Format(format, args);
            q.Enqueue(GetEvent(Level.Info, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted informational message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        public void InfoFormat(string format, object arg0) {
            if (!baseLog.IsInfoEnabled) return;
            var message = String.Format(format, arg0);
            q.Enqueue(GetEvent(Level.Info, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted informational message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        /// <param name="arg1">The second object to format.</param>
        public void InfoFormat(string format, object arg0, object arg1) {
            if (!baseLog.IsInfoEnabled) return;
            var message = String.Format(format, arg0, arg1);
            q.Enqueue(GetEvent(Level.Info, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted informational message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        /// <param name="arg1">The second object to format.</param>
        /// <param name="arg2">The third object to format.</param>
        public void InfoFormat(string format, object arg0, object arg1, object arg2) {
            if (!baseLog.IsInfoEnabled) return;
            var message = String.Format(format, arg0, arg1, arg2);
            q.Enqueue(GetEvent(Level.Info, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted informational message.
        /// </summary>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        public void InfoFormat(IFormatProvider provider, string format, params object[] args) {
            if (!baseLog.IsInfoEnabled) return;
            var message = String.Format(provider, format, args);
            q.Enqueue(GetEvent(Level.Info, message));
            t.Start();
        }

        #endregion

        #region Warn level

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">Message content.</param>
        public void Warn(object message) {
            if (!baseLog.IsWarnEnabled) return;
            q.Enqueue(GetEvent(Level.Warn, (string)message));
            t.Start();
        }

        /// <summary>
        /// Logs a warning message and an exception.
        /// </summary>
        /// <param name="message">Message content.</param>
        /// <param name="exception">Application exception.</param>
        public void Warn(object message, Exception exception) {
            if (!baseLog.IsWarnEnabled) return;
            q.Enqueue(GetEvent(Level.Warn, (string)message, 0, null, exception));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted warning message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        public void WarnFormat(string format, params object[] args) {
            if (!baseLog.IsWarnEnabled) return;
            var message = String.Format(format, args);
            q.Enqueue(GetEvent(Level.Warn, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted warning message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        public void WarnFormat(string format, object arg0) {
            if (!baseLog.IsWarnEnabled) return;
            var message = String.Format(format, arg0);
            q.Enqueue(GetEvent(Level.Warn, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted warning message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        /// <param name="arg1">The second object to format.</param>
        public void WarnFormat(string format, object arg0, object arg1) {
            if (!baseLog.IsWarnEnabled) return;
            var message = String.Format(format, arg0, arg1);
            q.Enqueue(GetEvent(Level.Warn, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted warning message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        /// <param name="arg1">The second object to format.</param>
        /// <param name="arg2">The third object to format.</param>
        public void WarnFormat(string format, object arg0, object arg1, object arg2) {
            if (!baseLog.IsWarnEnabled) return;
            var message = String.Format(format, arg0, arg1, arg2);
            q.Enqueue(GetEvent(Level.Warn, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted warning message.
        /// </summary>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        public void WarnFormat(IFormatProvider provider, string format, params object[] args) {
            if (!baseLog.IsWarnEnabled) return;
            var message = String.Format(provider, format, args);
            q.Enqueue(GetEvent(Level.Warn, message));
            t.Start();
        }

        #endregion

        #region Error level

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">Message content.</param>
        public void Error(object message) {
            if (!baseLog.IsErrorEnabled) return;
            q.Enqueue(GetEvent(Level.Error, (string)message));
            t.Start();
        }

        /// <summary>
        /// Logs an error message and an exception.
        /// </summary>
        /// <param name="message">Message content.</param>
        /// <param name="exception">Application exception.</param>
        public void Error(object message, Exception exception) {
            if (!baseLog.IsErrorEnabled) return;
            q.Enqueue(GetEvent(Level.Error, (string)message, 0, null, exception));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted error message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        public void ErrorFormat(string format, params object[] args) {
            if (!baseLog.IsErrorEnabled) return;
            var message = String.Format(format, args);
            q.Enqueue(GetEvent(Level.Error, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted error message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        public void ErrorFormat(string format, object arg0) {
            if (!baseLog.IsErrorEnabled) return;
            var message = String.Format(format, arg0);
            q.Enqueue(GetEvent(Level.Error, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted error message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        /// <param name="arg1">The second object to format.</param>
        public void ErrorFormat(string format, object arg0, object arg1) {
            if (!baseLog.IsErrorEnabled) return;
            var message = String.Format(format, arg0, arg1);
            q.Enqueue(GetEvent(Level.Error, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted error message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        /// <param name="arg1">The second object to format.</param>
        /// <param name="arg2">The third object to format.</param>
        public void ErrorFormat(string format, object arg0, object arg1, object arg2) {
            if (!baseLog.IsErrorEnabled) return;
            var message = String.Format(format, arg0, arg1, arg2);
            q.Enqueue(GetEvent(Level.Error, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted error message.
        /// </summary>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        public void ErrorFormat(IFormatProvider provider, string format, params object[] args) {
            if (!baseLog.IsErrorEnabled) return;
            var message = String.Format(provider, format, args);
            q.Enqueue(GetEvent(Level.Error, message));
            t.Start();
        }

        #endregion

        #region Fatal level

        /// <summary>
        /// Logs a fatal error message.
        /// </summary>
        /// <param name="message">Message content.</param>
        public void Fatal(object message) {
            if (!baseLog.IsFatalEnabled) return;
            q.Enqueue(GetEvent(Level.Fatal, (string)message));
            t.Start();
        }

        /// <summary>
        /// Logs a fatal error message and an exception.
        /// </summary>
        /// <param name="message">Message content.</param>
        /// <param name="exception">Application exception.</param>
        public void Fatal(object message, Exception exception) {
            if (!baseLog.IsFatalEnabled) return;
            q.Enqueue(GetEvent(Level.Fatal, (string)message, 0, null, exception));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted fatal error message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        public void FatalFormat(string format, params object[] args) {
            if (!baseLog.IsFatalEnabled) return;
            var message = String.Format(format, args);
            q.Enqueue(GetEvent(Level.Fatal, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted fatal error message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        public void FatalFormat(string format, object arg0) {
            if (!baseLog.IsFatalEnabled) return;
            var message = String.Format(format, arg0);
            q.Enqueue(GetEvent(Level.Fatal, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted fatal error message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        /// <param name="arg1">The second object to format.</param>
        public void FatalFormat(string format, object arg0, object arg1) {
            if (!baseLog.IsFatalEnabled) return;
            var message = String.Format(format, arg0, arg1);
            q.Enqueue(GetEvent(Level.Fatal, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted fatal error message.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        /// <param name="arg1">The second object to format.</param>
        /// <param name="arg2">The third object to format.</param>
        public void FatalFormat(string format, object arg0, object arg1, object arg2) {
            if (!baseLog.IsFatalEnabled) return;
            var message = String.Format(format, arg0, arg1, arg2);
            q.Enqueue(GetEvent(Level.Fatal, message));
            t.Start();
        }

        /// <summary>
        /// Logs a formatted fatal error message.
        /// </summary>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        public void FatalFormat(IFormatProvider provider, string format, params object[] args) {
            if (!baseLog.IsFatalEnabled) return;
            var message = String.Format(provider, format, args);
            q.Enqueue(GetEvent(Level.Fatal, message));
            t.Start();
        }

        #endregion

        #endregion

        #endregion

    }

}