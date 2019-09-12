using System.Collections.Generic;
using Ntrada.Core;

namespace Ntrada.Extensions.Cors
{
    public class CorsOptions : IOptions
    {
        public bool AllowCredentials { get; set; }
        public IEnumerable<string> AllowedOrigins { get; set; }
        public IEnumerable<string> AllowedMethods { get; set; }
        public IEnumerable<string> AllowedHeaders { get; set; }
        public IEnumerable<string> ExposedHeaders { get; set; }
    }
}