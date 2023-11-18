﻿using MailKit;
using MailKit.Net.Imap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

namespace ConsoleAppImap
{
    class ImapCommandReceiverOptions
    {
        public string Server { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string MailFrom { get; set; }
        public string MailTo { get; set; }
    }

    internal class ImapCommandReceiver
    {
        private static readonly Regex _Subject = new Regex(@"cmd scene (\w+)", RegexOptions.Compiled);
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
            await _ImapClient.ConnectAsync(_Options.Server, 993, true);
            await _ImapClient.AuthenticateAsync(new NetworkCredential(_Options.Username, _Options.Password));
            IMailFolder imapFolder = await _ImapClient.GetFolderAsync(_ImapClient.PersonalNamespaces[0].Path);
            imapFolder.Open(FolderAccess.ReadWrite);
            IFetchRequest rq = new FetchRequest(MessageSummaryItems.All);
            foreach (IMessageSummary msg in await imapFolder.FetchAsync(0, -1, rq))
            {
                string from = msg.Envelope.Sender.Mailboxes.First().Address;
                if (from == "rwb@rwb.me.uk")
                {
                    Match m = _Subject.Match(msg.NormalizedSubject);
                    if (m.Success && m.Groups[1].Success)
                    {
                        Sh(s.Groups[1].Value);
                        // The UniqueId is always zero -- fuck knows what it's for. Therefore use the index.
                        await imapFolder.AddFlagsAsync(msg.Index, MessageFlags.Deleted, true);
                    }
                }
                _Logger.LogInformation(msg.NormalizedSubject);
            }
            await imapFolder.ExpungeAsync();
        }

        private void Sh(string command)
        {
            if(string.IsNullOrEmpty(command) || command == "void"){ return; }
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "/bin/sh";
            psi.Arguments = $"/root/bin/sceneSet.sh {command}";
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            try {
                using (Process process = new Process())
                {
                    process.OutputDataReceived += (sender, args) =>
                    {
                        _Logger.LogInformation(args.Data);
                    };
                    process.StartInfo = psi;
                    process.Start();
                    process.WaitForExit();

                    string output = process.StandardOutput.ReadToEnd();
                }
            } 
            catch( Exception e)
            {
                _Logger.LogError(e, $"Failed to run command /root/bin/sceneSet.sh {command}");
            }
        }
    }
}