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
        private static readonly Regex _SubjectAlarm = new Regex(@"alarm (\d{2}:\d{2})", RegexOptions.Compiled);
        private static readonly Regex _SubjectWater = new Regex(@"water (\d*)", RegexOptions.Compiled);
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
                await ProcessMessagesAsync(imapFolder);
                await _ImapClient.DisconnectAsync(true);
            }
            catch( Exception e)
            {
                StringBuilder message = new StringBuilder();
                message.AppendLine("Failed to get messages.");
                Exception? f = e;
                while( f != null)
                {
                    message.AppendLine("  " + f.Message);
                    message.AppendLine("    " + (f.StackTrace ?? "").Split(Environment.NewLine).FirstOrDefault("(no stack trace available)"));
                    f = f.InnerException;
                }
                _Logger.LogError(message.ToString());
            }
        }

        private async Task ProcessMessagesAsync(IMailFolder imapFolder)
        {
            try
            {
                imapFolder.Open(FolderAccess.ReadWrite);
                IFetchRequest rq = new FetchRequest(MessageSummaryItems.All);
                int n = 0;
                foreach (IMessageSummary msg in await imapFolder.FetchAsync(0, -1, rq))
                {
                    string from = msg.Envelope.Sender.Mailboxes.First().Address;
                    if (from == "rwb@rwb.me.uk")
                    {
                        Match m = _SubjectScene.Match(msg.NormalizedSubject.ToLowerInvariant());
                        if (m.Success && m.Groups[1].Success)
                        {
                            _Logger.LogInformation($"Processing: {msg.NormalizedSubject}");
                            ProcessScene(m.Groups[1].Value);
                            n++;
                            // The UniqueId is always zero -- fuck knows what it's for. Therefore use the index.
                            await imapFolder.AddFlagsAsync(msg.Index, MessageFlags.Deleted, true);
                        }

                        m = _SubjectSolar.Match(msg.NormalizedSubject.ToLowerInvariant());
                        if (m.Success && m.Groups[1].Success && m.Groups[2].Success)
                        {
                            _Logger.LogInformation($"Processing: {msg.NormalizedSubject}");
                            ProcessSolar(m.Groups[1].Value, int.Parse(m.Groups[2].Value));
                            n++;
                            await imapFolder.AddFlagsAsync(msg.Index, MessageFlags.Deleted, true);
                        }

                        m = _SubjectAlarm.Match(msg.NormalizedSubject.ToLowerInvariant());
                        if (m.Success && m.Groups[1].Success)
                        {
                            _Logger.LogInformation($"Processing: {msg.NormalizedSubject}");
                            ProcessAlarm(m.Groups[1].Value);
                            n++;
                            await imapFolder.AddFlagsAsync(msg.Index, MessageFlags.Deleted, true);
                        }

                        m = _SubjectWater.Match(msg.NormalizedSubject.ToLowerInvariant());
                        if (m.Success)
                        {
                            _Logger.LogInformation($"Processing: {msg.NormalizedSubject}");
                            ProcessWater(m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 8);
                            n++;
                            await imapFolder.AddFlagsAsync(msg.Index, MessageFlags.Deleted, true);
                        }

                        if(msg.NormalizedSubject.ToLower().StartsWith("free "))
                        {
                            _Logger.LogInformation($"Processing: {msg.NormalizedSubject}");
                            ProcessFree(msg.NormalizedSubject.Substring(5));
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
            catch (Exception e)
            {
                StringBuilder message = new StringBuilder();
                message.AppendLine("Failed to process messages.");
                Exception? f = e;
                while (f != null)
                {
                    message.AppendLine("  " + f.Message);
                    message.AppendLine("    " + (f.StackTrace ?? "").Split(Environment.NewLine).FirstOrDefault("(no stack trace available)"));
                    f = f.InnerException;
                }
                _Logger.LogError(message.ToString());
            }
        }

        public async Task ListenAsync(CancellationToken cancellationToken)
        {
            await _ImapClient.ConnectAsync(_Options.Server, 993, true);
            await _ImapClient.AuthenticateAsync(new NetworkCredential(_Options.Username, _Options.Password));
            await _ImapClient.Inbox.OpenAsync(FolderAccess.ReadOnly);
            //imapFolder.CountChanged += ImapFolder_CountChanged;
            _ImapClient.Inbox.CountChanged += ImapFolder_CountChanged;
            _ImapClient.Inbox.AnnotationsChanged += ImapFolder_CountChanged;
            _ImapClient.Inbox.MessageFlagsChanged += ImapFolder_CountChanged;
            _ImapClient.Inbox.MessageExpunged += ImapFolder_CountChanged;
            _ImapClient.Disconnected += _ImapClient_Disconnected;
            await _ImapClient.IdleAsync(cancellationToken);
            await _ImapClient.DisconnectAsync(true);
        }

        private void _ImapClient_Disconnected(object? sender, DisconnectedEventArgs e)
        {
            _Logger.LogInformation("Disconnected");
        }

        private void ImapFolder_CountChanged(object? sender, EventArgs e)
        {
            ImapFolder? folder = sender as ImapFolder;
            _Logger.LogInformation($"{folder?.Count ?? -1} messages in folder {folder?.Name ?? "?"}");
            if (folder != null)
            {
                ProcessMessagesAsync(folder).Wait();
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
                _Logger.LogError(e, $"Failed to run command /root/bin/scene.sh {command}");
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
                _Logger.LogError(e, $"Failed to run command /home/rwb/Luxopus/{(command == "charge" ? "chargeFromGrid" : "dischargeToGrid")}.sh {value}");
            }
        }

        private void ProcessFree(string date)
        {
            if (string.IsNullOrEmpty(date)) { return; }
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "/bin/sh";
            psi.Arguments = $"/home/rwb/Luxopus/free.sh \"{date}\"";
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
                _Logger.LogError(e, $"Failed to run command /home/rwb/Luxopus/free.sh '{date}'");
            }
        }

        private void ProcessAlarm(string time)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "/bin/sh";
            psi.Arguments = $"/root/bin/alarm.sh {time}";
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
                _Logger.LogError(e, $"Failed to run command /root/bin/alarm.sh {time}");
            }
        }

        private void ProcessWater(int holdMinutes)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "/bin/sh";
            psi.Arguments = $"/root/bin/hot-water.sh hold {holdMinutes} ImapCommandReceiver_{DateTime.Now:HH-mm}";
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
                _Logger.LogError(e, $"/root/bin/hot-water.sh hold {holdMinutes} 'ImapCommandReceiver {DateTime.Now:HH:mm}'");
            }
        }
    }
}
