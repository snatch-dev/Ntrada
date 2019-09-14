using System.Collections.Generic;

namespace Ntrada
{
    internal interface IPolicyManager
    {
        IDictionary<string, string>  GetClaims(string policy);
    }
}