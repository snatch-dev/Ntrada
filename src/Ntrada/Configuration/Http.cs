namespace Ntrada.Core.Configuration
{
    public class Http
    {
        public int Retries { get; set; }
        public bool Exponential { get; set; }
        public double Interval { get; set; }
    }
}