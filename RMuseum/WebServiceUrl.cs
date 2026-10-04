using Microsoft.Extensions.Configuration;
using System.IO;

namespace RMuseum
{
    public static class WebServiceUrl
    {
        /// <summary>
        /// url
        /// </summary>
        public static string Url
        {
            get
            {
                if (!string.IsNullOrEmpty(_url))
                    return _url;
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").AddEnvironmentVariables() // divan: honour env overrides (Docker)
                    .Build();
                _url = configuration["WebServiceUrl"];
                return _url;
            }
        }

        private static string _url = "";
    }
}
