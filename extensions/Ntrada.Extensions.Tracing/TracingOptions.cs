namespace Ntrada.Extensions.Tracing
{
    public class TracingOptions : IOptions
    {
        public bool UseEmptyTracer { get; set; }
        public string ServiceName { get; set; }
        public string UdpHost { get; set; }
        public int UdpPort { get; set; }
        public int MaxPacketSize { get; set; }
        public string Sampler { get; set; }
        public double MaxTracesPerSecond { get; set; } = 5;
        public double SamplingRate { get; set; } = 0.2;
    }
}