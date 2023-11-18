using MailKit;
using Microsoft.Extensions.Logging;

namespace ConsoleAppImap
{
    internal class MailkitLogger : IProtocolLogger
    {
        private ILogger<MailkitLogger> _Logger;
        public MailkitLogger(ILogger<MailkitLogger> logger)
        {
            _Logger = logger;
        }

        public IAuthenticationSecretDetector AuthenticationSecretDetector { get; set; }

        public void Dispose() { }

        public void LogClient(byte[] buffer, int offset, int count)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (ProtocolLogger l = new ProtocolLogger(ms, true) { AuthenticationSecretDetector = AuthenticationSecretDetector })
                {
                    l.LogClient(buffer, offset, count);
                }
                ms.Flush();
                ms.Position = 0;
                using (StreamReader r = new StreamReader(ms))
                {
                    string msg = r.ReadToEnd();
                    _Logger.LogInformation(msg);
                }
            }
        }

        public void LogConnect(Uri uri)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (ProtocolLogger l = new ProtocolLogger(ms, true) { AuthenticationSecretDetector = AuthenticationSecretDetector })
                {
                    l.LogConnect(uri);
                }
                ms.Flush();
                ms.Position = 0;
                using (StreamReader r = new StreamReader(ms))
                {
                    string msg = r.ReadToEnd();
                    _Logger.LogInformation(msg);
                }
            }
        }

        public void LogServer(byte[] buffer, int offset, int count)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (ProtocolLogger l = new ProtocolLogger(ms, true) { AuthenticationSecretDetector = AuthenticationSecretDetector })
                {
                    l.LogServer(buffer, offset, count);
                }
                ms.Flush();
                ms.Position = 0;
                using (StreamReader r = new StreamReader(ms))
                {
                    string msg = r.ReadToEnd();
                    _Logger.LogInformation(msg);
                }
            }
        }
    }
}
