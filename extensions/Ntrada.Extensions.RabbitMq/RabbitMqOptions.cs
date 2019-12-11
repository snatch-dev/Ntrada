using System.Collections.Generic;

namespace Ntrada.Extensions.RabbitMq
{
    public class RabbitMqOptions : IOptions
    {
        public string ConnectionName { get; set; }
        public IEnumerable<string> Hostnames { get; set; }
        public int Port { get; set; }
        public string VirtualHost { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int RequestedConnectionTimeout { get; set; } = 30000;
        public int SocketReadTimeout { get; set; } = 30000;
        public int SocketWriteTimeout { get; set; } = 30000;
        public ushort RequestedChannelMax { get; set; }
        public uint RequestedFrameMax { get; set; }
        public ushort RequestedHeartbeat { get; set; }
        public bool UseBackgroundThreadsForIO { get; set; }
        public ExchangeOptions Exchange { get; set; }
        public SslOptions Ssl { get; set; }
        public MessageContextOptions MessageContext { get; set; }
        public LoggerOptions Logger { get; set; }
        public string SpanContextHeader { get; set; }
        public IDictionary<string, object> Headers { get; set; }

        public class SslOptions
        {
            public bool Enabled { get; set; }
            public string ServerName { get; set; }
            public string CertificatePath { get; set; }
        }

        public class ExchangeOptions
        {
            public bool DeclareExchange { get; set; }
            public bool Durable { get; set; }
            public bool AutoDelete { get; set; }
            public string Type { get; set; }
        }

        public class MessageContextOptions
        {
            public bool Enabled { get; set; }
            public string Header { get; set; }
        }

        public class LoggerOptions
        {
            public bool Enabled { get; set; }
            public string Level { get; set; }
        }
    }
}