using System.Collections.Generic;

namespace Ntrada.Configuration
{
    public class Cors
    {
        public bool Enabled { get; set; }
        public IEnumerable<string> Headers { get; set; }
    }
}