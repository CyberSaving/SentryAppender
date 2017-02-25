using System;
using System.Collections.Generic;
using System.Linq;

using log4net.Layout;

using SharpRaven.Data;
using SharpRaven.Log4Net.Extra;

using log4net.Appender;
using log4net.Core;

namespace SharpRaven.Log4Net
{
    public class SentryTag
    {
        public string Name { get; set; }
        public IRawLayout Layout { get; set; }
    }

    public class SentryAppender : AppenderSkeleton
    {
        private static RavenClient ravenClient;
        public string DSN { get; set; }
        public string Logger { get; set; }
        private readonly IList<SentryTag> tagLayouts = new List<SentryTag>();

        public void AddTag(SentryTag tag)
        {
            tagLayouts.Add(tag);
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (ravenClient == null)
            {
                ravenClient = new RavenClient(DSN)
                {
                    Logger = Logger
                };
            }

            var httpExtra = HttpExtra.GetHttpExtra();
            object extra;

            if (httpExtra != null)
            {
                extra = new
                {
                    Environment = new EnvironmentExtra(),
                    Http = httpExtra
                };
            }
            else
            {
                extra = new
                {
                    Environment = new EnvironmentExtra()
                };
            }

            var exception = loggingEvent.ExceptionObject ?? loggingEvent.MessageObject as Exception;
            var level = Translate(loggingEvent.Level);

            SentryEvent @event = null;
            if (exception != null)
            {
                @event = new SentryEvent(exception);
                ravenClient.Capture(@event);
            }
            else
            {
                var message = loggingEvent.RenderedMessage;
                if (message != null)
                    @event = new SentryEvent(new SentryMessage(message));
            }
            if (@event!=null)
            {
                @event.Level = level;
                if (tagLayouts.Count>0)
                    foreach (var item in tagLayouts)
                        @event.Tags.Add(item.Name, (item.Layout.Format(loggingEvent) ?? "").ToString());
                @event.Extra = extra;
                ravenClient.Capture(@event);
            }
            
        }


        internal static ErrorLevel Translate(Level level)
        {
            switch (level.DisplayName)
            {
                case "WARN":
                    return ErrorLevel.Warning;

                case "NOTICE":
                    return ErrorLevel.Info;
            }

            ErrorLevel errorLevel;

            return !Enum.TryParse(level.DisplayName, true, out errorLevel)
                       ? ErrorLevel.Error
                       : errorLevel;
        }


        protected override void Append(LoggingEvent[] loggingEvents)
        {
            foreach (var loggingEvent in loggingEvents)
            {
                Append(loggingEvent);
            }
        }
    }
}