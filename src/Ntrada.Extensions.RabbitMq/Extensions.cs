namespace Ntrada.Extensions.RabbitMq
{
    public static class Extensions
    {
        public static INtradaBuilder UseRabbitMq(this INtradaBuilder builder)
        {
            builder.UseExtension<RabbitMqDispatcher>();
            
            return builder;
        }
    }
}