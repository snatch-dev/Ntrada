namespace Ntrada.Extensions.Swagger
{
    public class SwaggerOptions : IOptions
    {
        public bool ReDocEnabled { get; set; }
        public string Name { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }
        public string RoutePrefix { get; set; }
        public bool IncludeSecurity { get; set; }
    }
}