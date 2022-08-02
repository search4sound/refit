using System.Collections.Generic;
using System.Net.Http;

namespace Refit
{
    public class RegisteredHttpClientsForRefit
    {
        public Dictionary<string, HttpClient> NamedClients { get; } = new Dictionary<string, HttpClient>();
    }
}
