﻿/* Filename:    SendHL7Message.cs
 * 
 * Author:      Rob Holme (rob@holme.com.au) 
 *
 * Credits:     Code to handle the Path and LiteralPath parameter sets, and expansion of wildcards is based
 *              on Oisin Grehan's post: http://www.nivot.org/blog/post/2008/11/19/Quickstart1ACmdletThatProcessesFilesAndDirectories
 * 
 * Date:        21/07/2016
 * 
 * Notes:       Implements a cmdlet to send a HL7 v2 message via TCP (framed using MLLP).
 * 
 */

namespace HL7Tools
{
    using System;
    using System.IO;
    using System.Text;
    using System.Collections.Generic;
    using System.Management.Automation;
    using System.Net.Sockets;

    [Cmdlet("Send", "HL7Message")]
    public class SendHL7Message : PSCmdlet
    {
        private string hostname;
        private int port;
        private int delayBetweenMessages;
        private bool noACK;
        private string[] paths;
        private bool expandWildcards = false;

        // Parameter set for the -Path and -LiteralPath parameters. A parameter set ensures these options are mutually exclusive.
        // A LiteralPath is used in situations where the filename actually contains wild card characters (eg File[1-10].txt) and you want
        // to use the literaral file name instead of treating it as a wildcard search.
        [Parameter(
            Mandatory = true,
            ValueFromPipeline = false,
            ValueFromPipelineByPropertyName = true,
            ParameterSetName = "Literal")
        ]
        [Alias("PSPath", "Name", "Filename")]
        [ValidateNotNullOrEmpty]
        public string[] LiteralPath
        {
            get { return this.paths; }
            set { this.paths = value; }
        }

        [Parameter(
            Position = 0,
            Mandatory = true,
            ParameterSetName = "Path")

        ]
        [ValidateNotNullOrEmpty]
        public string[] Path
        {
            get { return paths; }
            set
            {
                this.expandWildcards = true;
                this.paths = value;
            }
        }

        // The remote IP address or Hostname to send the HL7 message to
        [Alias("ComputerName", "Server", "IPAddress")]
        [Parameter(
            Mandatory = true,
            Position = 1,
            HelpMessage = "Remote Hostname or IP Address"
        )]
        public string HostName
        {
            get { return this.hostname; }
            set { this.hostname = value; }
        }


        // The port number of the remote listener to send the message to
        [Parameter(
            Mandatory = true,
            Position = 2,
            HelpMessage = "Remote listener port number"
        )]
        [ValidateRange(1, 65535)]
        public int Port
        {
            get { return this.port; }
            set { this.port = value; }
        }

        // Do not wait for ACKs responses if this switch is set
        [Parameter(
            Mandatory = false,
            HelpMessage = "Do not wait for ACK response"
         )]
        public SwitchParameter NoACK
        {
            get { return this.noACK; }
            set { this.noACK = value; }
        }

        // The port number of the remote listener to send the message to
        [Parameter(
            Mandatory = false,
            Position = 3,
            HelpMessage = "Deley between seinding messages (seconds)"
        )]
        [ValidateRange(0,600)]
        public int Delay
        {
            get { return this.delayBetweenMessages; }
            set { this.delayBetweenMessages = value; }
        }

        /// <summary>
        /// Send each of the files provided
        /// </summary>
        protected override void ProcessRecord()
        {

            foreach (string path in paths) {
                // This will hold information about the provider containing the items that this path string might resolve to.                
                ProviderInfo provider;

                // This will be used by the method that processes literal paths
                PSDriveInfo drive;

                // this contains the paths to process for this iteration of the loop to resolve and optionally expand wildcards.
                List<string> filePaths = new List<string>();

                // if the path provided is a directory, expand the files in the directory and add these to the list.
                if (Directory.Exists(path)) {
                    filePaths.AddRange(Directory.GetFiles(path));
                }

                // not a directory, could be a wild-card or literal filepath 
                else {
                    // expand wild-cards. This assumes if the user listed a directory it is literal
                    if (expandWildcards) {
                        // Turn *.txt into foo.txt,foo2.txt etc. If path is just "foo.txt," it will return unchanged. If the filepath expands into a directory ignore it.
                        foreach (string expandedFilePath in this.GetResolvedProviderPathFromPSPath(path, out provider)) {
                            if (!Directory.Exists(expandedFilePath)) {
                                filePaths.Add(expandedFilePath);
                            }
                        }
                    }
                    else {
                        // no wildcards, so don't try to expand any * or ? symbols.                    
                        filePaths.Add(this.SessionState.Path.GetUnresolvedProviderPathFromPSPath(path, out provider, out drive));
                    }
                    // ensure that this path (or set of paths after wildcard expansion)
                    // is on the filesystem. A wildcard can never expand to span multiple providers.
                    if (Common.IsFileSystemPath(provider, path) == false) {
                        // no, so skip to next path in paths.
                        continue;
                    }
                }

                // At this point, we have a list of paths on the filesystem, send each file to the remote endpoint
                foreach (string filePath in filePaths) {
                    System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();

                    // confirm the file exists
                    if (!File.Exists(filePath)) {
                        FileNotFoundException fileException = new FileNotFoundException("File not found", filePath);
                        ErrorRecord fileNotFoundError = new ErrorRecord(fileException, "FileNotFound", ErrorCategory.ObjectNotFound, filePath);
                        WriteError(fileNotFoundError);
                        return;
                    }

                    // send the file to the endpoint using MLLP framing
                    TcpClient tcpConnection = new TcpClient();
                    tcpConnection.SendTimeout = 10000;
                    tcpConnection.ReceiveTimeout = 10000;
                    try {
                        // get the contents of the file
                        string fileContents = File.ReadAllText(filePath);

                        // save the string as a HL7Message, this will validate the file is a HL7 v2 message.
                        HL7Message message = new HL7Message(fileContents);
                        WriteVerbose("Connecting to " + this.hostname + ":" + this.port);

                        // create a TCP socket connection to the reciever, start timing the elapsed time to deliver the message and receive the ACK
                        timer.Start();
                        tcpConnection.Connect(this.hostname, this.port);
                        NetworkStream tcpStream = tcpConnection.GetStream();
                        UTF8Encoding encoder = new UTF8Encoding();
                        Byte[] writeBuffer = new Byte[4096];

                        // get the message text with MLLP framing
                        writeBuffer = encoder.GetBytes(message.GetMLLPFramedMessage());
                        tcpStream.Write(writeBuffer, 0, writeBuffer.Length);
                        tcpStream.Flush();
                        WriteVerbose("Message sent");

                        // wait for ack unless the -NoACK switch was set
                        string[] ackLines = null;
                        if (!this.noACK) {
                            WriteVerbose("Waiting for ACK ...");
                            Byte[] readBuffer = new Byte[4096];
                            int bytesRead = tcpStream.Read(readBuffer, 0, 4096);
                            string ackMessage = encoder.GetString(readBuffer, 0, bytesRead);
                            // look for the start of the MLLP frame (VT control character)
                            int start = ackMessage.IndexOf((char)0x0B);
                            if (start >= 0) {
                                // Search for the end of the MLLP frame (FS control character)
                                int end = ackMessage.IndexOf((char)0x1C);
                                if (end > start) {
                                    // split the ACK message on <CR> character (segment delineter), output each segment of the ACK on a new line
                                    // remove the last <CR> character if present, otherwise the final element in the array will be empty when splitting the string
                                    string ackString = ackMessage.Substring(start + 1, end - 1);
                                    if (ackString[ackString.Length - 1] == (char)0x0D) {
                                        ackString = ackString.Substring(0, ackString.Length - 1);
                                    }
                                    ackLines = ackString.Split((char)0x0D);
                                }
                            }
                        }

                        // stop timing the operation, output the result object
                        timer.Stop();
                        SendHL7MessageResult result = new SendHL7MessageResult("Successful", ackLines, DateTime.Now, message.ToString().Split((char)0x0D), this.hostname, this.port, filePath, timer.Elapsed.TotalMilliseconds / 1000);
                        WriteObject(result);
                        WriteVerbose("Closing TCP session\n");
                        tcpStream.Close();
                    }
                    // if the file does not start with a MSH segment, the constructor will throw an exception. 
                    catch (ArgumentException) {
                        ArgumentException argException = new ArgumentException("The file does not appear to be a valid HL7 v2 message", filePath);
                        ErrorRecord fileNotFoundError = new ErrorRecord(argException, "FileNotValid", ErrorCategory.InvalidData, filePath);
                        WriteError(fileNotFoundError);
                        return;
                    }
                    // catch failed TCP connections
                    catch (SocketException se) {
                        ErrorRecord SocketError = new ErrorRecord(se, "ConnectionError", ErrorCategory.ConnectionError, this.hostname + ":" + this.port);
                        WriteError(SocketError);
                        return;
                    }
                    finally {
                        tcpConnection.Close();
                    }

                    // delay between sending messages
                    if (this.delayBetweenMessages > 0) {
                        System.Threading.Thread.Sleep(this.delayBetweenMessages * 1000);
                    }
                }
            }
        }
    }

    /// <summary>
    /// An object containing the results to be returned to the pipeline
    /// </summary>
    public class SendHL7MessageResult
    {
        private string status;
        private string[] ackMessage;
        private DateTime timeSent;
        private string[] messageSent;
        private string filename;
        private string remoteHost;
        private int port;
        private double elapsedSeconds;

        /// <summary>
        /// The value of the HL7 item
        /// </summary>
        public string Status
        {
            get { return this.status; }
            set { this.status = value; }
        }

        /// <summary>
        /// The timestamp of when the message was sent
        /// </summary>
        public DateTime TimeSent
        {
            get { return this.timeSent; }
            set { this.timeSent = value; }
        }

        /// <summary>
        /// The ACK response received from the remote host
        /// </summary>
        public string[] ACKMessage
        {
            get { return this.ackMessage; }
            set { this.ackMessage = value; }
        }

        /// <summary>
        /// A copy of the HL7 message sent
        /// </summary>
        public string[] MessageSent
        {
            get { return this.messageSent; }
            set { this.messageSent = value; }
        }

        /// <summary>
        /// The remote host the message was sent to
        /// </summary>
        public string RemoteHost
        {
            get { return this.remoteHost; }
            set { this.remoteHost = value; }
        }

        /// <summary>
        /// The TCP port of the remote server
        /// </summary>
        public int Port
        {
            get { return this.port; }
            set { this.port = value; }
        }

        /// <summary>
        /// The filename containing the message sent
        /// </summary>
        public string Filename
        {
            get { return this.filename; }
            set { this.filename = value; }
        }

        /// <summary>
        /// The time elapsed in seconds to send the message
        /// </summary>
        public double ElapsedSeconds
        {
            get { return this.elapsedSeconds; }
            set { this.elapsedSeconds = value; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ItemValue"></param>
        public SendHL7MessageResult(string Status, string[] Ack = null, DateTime? SendTime = null, string[] HL7Message = null, string RemoteHost = null, int? Port = null, string Filename = null, double? ElapsedSeconds = null)
        {
            // null-coalescing operator. Uses the SentTime value, unless it is null in which case DateTime.Now is assigned as the value.
            this.timeSent = SendTime ?? DateTime.Now;
            this.status = Status;
            this.ackMessage = Ack;
            this.messageSent = HL7Message;
            this.remoteHost = RemoteHost;
            this.port = Port ?? 0;
            this.filename = Filename;
            this.elapsedSeconds = ElapsedSeconds ?? 0;
        }
    }
}
