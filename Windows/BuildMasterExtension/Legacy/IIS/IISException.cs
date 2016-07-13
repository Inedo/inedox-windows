using System;
using System.Runtime.Serialization;
using Inedo.Diagnostics;

namespace Inedo.BuildMasterExtensions.Windows.Iis
{
    [Serializable]
    internal sealed class IISException : Exception
    {
        public IISException(MessageLevel logLevel = MessageLevel.Error)
        {
            this.LogLevel = logLevel;
        }
        public IISException(string message, MessageLevel logLevel = MessageLevel.Error)
            : base(message)
        {
            this.LogLevel = logLevel;
        }
        public IISException(string message, Exception inner, MessageLevel logLevel = MessageLevel.Error)
            : base(message, inner)
        {
            this.LogLevel = logLevel;
        }

        private IISException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.LogLevel = (MessageLevel)info.GetInt32("LogLevel");
        }

        public MessageLevel LogLevel { get; private set; }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("LogLevel", (int)this.LogLevel);
        }
    }
}
