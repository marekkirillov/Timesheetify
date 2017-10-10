namespace TogglToTimesheet
{
    using System;
    using System.Configuration;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Security.Principal;
    using System.ServiceModel;
    using Common;

    public static class EndpointProvider<T, T1> where T : ClientBase<T1> where T1 : class
    {
        private static bool CustomCertificateValidation(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors error) => true;

        public static T ConfigureEndpoint()
        {
            var url = Constants.PwaPath;
            var uri = new Uri(url);

            BasicHttpBinding binding = null;

            if (uri.Scheme.Equals(Uri.UriSchemeHttps))
            {
                ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(CustomCertificateValidation);
                binding = new BasicHttpBinding(BasicHttpSecurityMode.Transport);
            }
            else
            {
                binding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
            }

            binding.Name = "basicHttpConf";
            binding.SendTimeout = TimeSpan.MaxValue;
            binding.MaxReceivedMessageSize = 500000000;
            binding.ReaderQuotas.MaxNameTableCharCount = 500000000;
            binding.MessageEncoding = WSMessageEncoding.Text;
            binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Ntlm;

            var address = new EndpointAddress(url);
            var client = (T)Activator.CreateInstance(typeof(T), binding, address);

            client.ClientCredentials.Windows.AllowedImpersonationLevel = TokenImpersonationLevel.Impersonation;
            client.ClientCredentials.Windows.AllowNtlm = true;

            return client;
        }
    }
}
