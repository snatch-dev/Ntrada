namespace Ntrada
{
    internal interface IEnabledExtension
    {
        IExtension Extension { get; }
        IExtensionOptions Options { get; }
    }
}