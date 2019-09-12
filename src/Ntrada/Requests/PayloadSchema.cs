using System.Dynamic;

namespace Ntrada.Requests
{
    internal class PayloadSchema
    {
        public ExpandoObject Payload { get; }
        public string Schema { get; }

        public PayloadSchema(ExpandoObject payload, string schema)
        {
            Payload = payload;
            Schema = schema;
        }
    }
}