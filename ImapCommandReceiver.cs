using MailKit;
using MailKit.Net.Imap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Rwb.ImapCommandReceiver
{
    class ImapCommandReceiverOptions
    {
        public string? Server { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? MailFrom { get; set; }
        public string? MailTo { get; set; }
    }

    internal class ImapCommandReceiver
    {
        private static readonly Regex _SubjectScene = new Regex(@"scene (\w+)", RegexOptions.Compiled);
        private static readonly Regex _SubjectSolar = new Regex(@"solar (\w+) (\d+)", RegexOptions.Compiled);
        private readonly ILogger<ImapCommandReceiver> _Logger;
        private readonly ImapCommandReceiverOptions _Options;
        private readonly ImapClient _ImapClient;

        public ImapCommandReceiver(ILogger<ImapCommandReceiver> logger, IOptions<ImapCommandReceiverOptions> options, ImapClient imapClient)
        {
            _Logger = logger;
            _Options = options.Value;
            _ImapClient = imapClient;
        }
        public async Task RunAsync()
        {
            try
            {
                await _ImapClient.ConnectAsync(_Options.Server, 993, true);
                await _ImapClient.AuthenticateAsync(new NetworkCredential(_Options.Username, _Options.Password));
                IMailFolder imapFolder = await _ImapClient.GetFolderAsync(_ImapClient.PersonalNamespaces[0].Path);
                imapFolder.Open(FolderAccess.ReadWrite);
                IFetchRequest rq = new FetchRequest(MessageSummaryItems.All);
                int n = 0;
                foreach (IMessageSummary msg in await imapFolder.FetchAsync(0, -1, rq))
                {
                    string from = msg.Envelope.Sender.Mailboxes.First().Address;
                    if (from == "rwb@rwb.me.uk")
                    {
                        Match m = _SubjectScene.Match(msg.NormalizedSubject);
                        if (m.Success && m.Groups[1].Success)
                        {
                            _Logger.LogInformation($"Processing: {msg.NormalizedSubject}");
                            ProcessScene(m.Groups[1].Value);
                            n++;
                            // The UniqueId is always zero -- fuck knows what it's for. Therefore use the index.
                            await imapFolder.AddFlagsAsync(msg.Index, MessageFlags.Deleted, true);
                        }

                        m = _SubjectSolar.Match(msg.NormalizedSubject);
                        if (m.Success && m.Groups[1].Success && m.Groups[2].Success)
                        {
                            _Logger.LogInformation($"Processing: {msg.NormalizedSubject}");
                            ProcessSolar(m.Groups[1].Value, int.Parse(m.Groups[2].Value));
                            n++;
                            await imapFolder.AddFlagsAsync(msg.Index, MessageFlags.Deleted, true);

                        }
                    }
                }
                await imapFolder.ExpungeAsync();
                if (n > 0)
                {
                    _Logger.LogInformation($"Processed {n} messages");
                }
                else
                {
                    _Logger.LogDebug($"Processed {n} messages");
                }
            }
            catch( Exception e)
            {
                StringBuilder message = new StringBuilder();
                message.AppendLine("Failed to get messages.");
                Exception f = e;
                while( f != null)
                {
                    message.AppendLine("  " + f.Message);
                    message.AppendLine("    " + (f.StackTrace ?? "").Split(Environment.NewLine).FirstOrDefault("(no stack trace available)"));
                }
            }
        }

        private void ProcessScene(string command)
        {
            if (string.IsNullOrEmpty(command) || command == "void") { return; }
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "/bin/sh";
            if (command == "report")
            {
                psi.Arguments = $"/root/bin/sceneReport.sh";
            }
            else
            {
                psi.Arguments = $"/root/bin/scene.sh {command} email";
            }
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;

            try
            {
                using (Process process = new Process())
                {
                    // These events don't do anything.
                    //process.OutputDataReceived += (sender, args) =>
                    //{
                    //    _Logger.LogInformation(args.Data);
                    //};
                    //process.ErrorDataReceived += (sender, args) =>
                    //{
                    //    _Logger.LogInformation(args.Data);
                    //};
                    process.StartInfo = psi;
                    process.Start();
                    _Logger.LogInformation(process.StandardOutput.ReadToEnd());
                    _Logger.LogInformation(process.StandardError.ReadToEnd());
                    process.WaitForExit();

                    string output = process.StandardOutput.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                _Logger.LogError(e, $"Failed to run command /root/bin/sceneSet.sh {command}");
            }
        }

        private void ProcessSolar(string command, int value)
        {
            if (string.IsNullOrEmpty(command) || command == "void") { return; }
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "/bin/sh";
            psi.Arguments = $"/home/rwb/Luxopus/{(command == "charge" ? "chargeFromGrid" : "dischargeToGrid")}.sh {value}";
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;

            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = psi;
                    process.Start();
                    _Logger.LogInformation(process.StandardOutput.ReadToEnd());
                    _Logger.LogInformation(process.StandardError.ReadToEnd());
                    process.WaitForExit();

                    string output = process.StandardOutput.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                _Logger.LogError(e, $"Failed to run command /root/bin/sceneSet.sh {command}");
            }
        }
    }
}
