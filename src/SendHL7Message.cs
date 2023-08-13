/* Filename:    SendHL7Message.cs
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
    using System.Net.Sockets;
    using System.Net.Security;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Diagnostics;
    using static System.Net.WebRequestMethods;
    using System.Collections;

    public class SendHL7Message
    {
        private string hostname;
        private int port;
        private int delayBetweenMessages;
        private bool noACK;
        private string[] paths;
        private bool expandWildcards = false;
        private string encoding = "UTF-8";
        private bool useTls;
        private bool skipCertificateCheck = false;

        private SendHL7MessageResult result;


        public SendHL7Message(string[] Path, string HostName, int Port, bool NoACK = false, bool ExpandWildcards = false, int Delay = 0, string Encoding = "UTF-8", bool UseTLS = false, bool SkipCertificateCheck = false)
        {
            this.paths = Path;
            this.expandWildcards = ExpandWildcards;

            this.hostname = HostName;
            this.port = Port;
            this.noACK = NoACK;
            this.encoding = Encoding;
            this.useTls = UseTLS;
            this.skipCertificateCheck = SkipCertificateCheck;

        }

        // Parameter set for the -Path and -LiteralPath parameters. A parameter set ensures these options are mutually exclusive.
        // A LiteralPath is used in situations where the filename actually contains wild card characters (eg File[1-10].txt) and you want
        // to use the literal file name instead of treating it as a wildcard search.
        public string[] LiteralPath
        {
            get { return this.paths; }
            set { this.paths = value; }
        }

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
        public string HostName
        {
            get { return this.hostname; }
            set { this.hostname = value; }
        }


        // The port number of the remote listener to send the message to
        public int Port
        {
            get { return this.port; }
            set { this.port = value; }
        }

        // Do not wait for ACKs responses if this switch is set
        public bool NoACK
        {
            get { return this.noACK; }
            set { this.noACK = value; }
        }

        // wait between sending messages
        public int Delay
        {
            get { return this.delayBetweenMessages; }
            set { this.delayBetweenMessages = value; }
        }

        // The encoding used when sending the message
        // "UTF-8", "ISO-8859-1"
        public string Encoding
        {
            get { return this.encoding; }
            set { this.encoding = value; }
        }

        // secure the connection to the server using TLS
        public bool UseTLS
        {
            get { return this.useTls; }
            set { this.useTls = value; }
        }

        // ignore TLS certificate errors, connect regardless of trust or validity errors.
        public bool SkipCertificateCheck
        {
            get { return this.skipCertificateCheck; }
            set { this.skipCertificateCheck = value; }
        }

        public SendHL7MessageResult Result
        {
            get { return this.result; }
        }


        /// <summary>
        /// Send each of the files provided
        /// </summary>
        public void ProcessRecord()
        {

            foreach (string path in paths)
            {

                // this contains the paths to process for this iteration of the loop to resolve and optionally expand wildcards.
                List<string> filePaths = Common.GetFilesFromPath(path, expandWildcards);

                // At this point, we have a list of paths on the filesystem, send each file to the remote endpoint
                foreach (string filePath in filePaths)
                {
                    System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();

                    // confirm the file exists
                    if (!System.IO.File.Exists(filePath))
                    {
                        throw new FileNotFoundException("File not found", filePath);
                    }

                    // send the file to the endpoint using MLLP framing
                    TcpClient tcpConnection = new TcpClient();
                    tcpConnection.SendTimeout = 10000;
                    tcpConnection.ReceiveTimeout = 10000;
                    try
                    {
                        // get the contents of the file
                        string fileContents = System.IO.File.ReadAllText(filePath);

                        // save the string as a HL7Message, this will validate the file is a HL7 v2 message.
                        HL7Message message = new HL7Message(fileContents);
                        Debug.WriteLine("Connecting to " + this.hostname + ":" + this.port);

                        // create a TCP socket connection to the receiver, start timing the elapsed time to deliver the message and receive the ACK
                        timer.Start();
                        tcpConnection.Connect(this.hostname, this.port);

                        string[] ackLines = null;
                        // connect using TLS if -UseTLS switch supplied, otherwise use plain text
                        if (this.useTls)
                        {
                            ackLines = SendMessageTLS(tcpConnection, message, this.skipCertificateCheck);
                        }
                        else
                        {
                            ackLines = SendMessage(tcpConnection, message);
                        }

                        // stop timing the operation, output the result object
                        timer.Stop();
                        result = new SendHL7MessageResult("Successful", ackLines, DateTime.Now, message.ToString().Split((char)0x0D), this.hostname, this.port, filePath, timer.Elapsed.TotalMilliseconds / 1000);
                        // TODO WriteObject(result);
                        Debug.WriteLine("Closing TCP session\n");
                    }
                    // if the file does not start with a MSH segment, the constructor will throw an exception. 
                    catch (ArgumentException ae)
                    {
                        Debug.WriteLine($"Exception: {ae}");
                        result = new SendHL7MessageResult("Error", new string[] { "File does not start with a MSH segment" }, DateTime.Now, null, this.hostname, this.port, filePath, timer.Elapsed.TotalMilliseconds / 1000);
                        //throw new ArgumentException("The file does not appear to be a valid HL7 v2 message", filePath);
                    }
                    // catch failed TCP connections
                    catch (SocketException)
                    {
                        result = new SendHL7MessageResult("Error", new string[] { "Failed TCP connections" }, DateTime.Now, null, this.hostname, this.port, filePath, timer.Elapsed.TotalMilliseconds / 1000);
                        // throw new SocketException();
                    }
                    finally
                    {
                        tcpConnection.Close();
                    }

                    // delay between sending messages
                    if (this.delayBetweenMessages > 0)
                    {
                        System.Threading.Thread.Sleep(this.delayBetweenMessages * 1000);
                    }
                }
            }
        }


        /// <summary>
        /// Send the message via MLLP using a TLS secured connection
        /// </summary>
        private string[] SendMessageTLS(TcpClient Connection, HL7Message Message, bool SkipCertCheck)
        {
            // set the text encoding
            Encoding encoder = System.Text.Encoding.GetEncoding(this.encoding);
            Debug.WriteLine("Encoding: " + encoder.EncodingName);

            // get the ssl stream. Use hostname as SNI name. Ignore cert errors if -SkipCertificateCheck is set
            Debug.WriteLine("Using TLS");
            SslStream sslStream;
            if (!SkipCertCheck)
            {
                Debug.WriteLine("Enforcing certificate validation");
                sslStream = new SslStream(Connection.GetStream());
            }
            else
            {
                Debug.WriteLine("Ignoring certificate validation errors");
                sslStream = new SslStream(Connection.GetStream(), false, new RemoteCertificateValidationCallback(SkipServerCertificateValidation), null);
            }
            sslStream.AuthenticateAsClient(this.hostname);

            // get the message text with MLLP framing
            Byte[] writeBuffer = new Byte[4096];
            writeBuffer = encoder.GetBytes(Message.GetMLLPFramedMessage());
            sslStream.Write(writeBuffer, 0, writeBuffer.Length);
            sslStream.Flush();
            Debug.WriteLine("Message sent");

            // wait for ack unless the -NoACK switch was set
            string[] ackLines = null;
            if (!this.noACK)
            {
                Debug.WriteLine("Waiting for ACK ...");
                Byte[] readBuffer = new Byte[4096];
                int bytesRead = sslStream.Read(readBuffer, 0, 4096);
                string ackMessage = encoder.GetString(readBuffer, 0, bytesRead);
                ackLines = StripMLLPFrame(ackMessage);
            }
            sslStream.Close();
            return ackLines;
        }


        /// <summary>
        /// Send the message via MLLP
        /// </summary>
        private string[] SendMessage(TcpClient Connection, HL7Message Message)
        {
            // set the text encoding
            Encoding encoder = System.Text.Encoding.GetEncoding(this.encoding);
            Debug.WriteLine("Encoding: " + encoder.EncodingName);

            NetworkStream tcpStream = Connection.GetStream();

            // get the message text with MLLP framing
            Byte[] writeBuffer = new Byte[4096];
            writeBuffer = encoder.GetBytes(Message.GetMLLPFramedMessage());
            tcpStream.Write(writeBuffer, 0, writeBuffer.Length);
            tcpStream.Flush();
            Debug.WriteLine("Message sent");

            // wait for ack unless the -NoACK switch was set
            string[] ackLines = null;
            if (!this.noACK)
            {
                Debug.WriteLine("Waiting for ACK ...");
                Byte[] readBuffer = new Byte[4096];
                int bytesRead = tcpStream.Read(readBuffer, 0, 4096);
                string ackMessage = encoder.GetString(readBuffer, 0, bytesRead);
                ackLines = StripMLLPFrame(ackMessage);
            }
            tcpStream.Close();
            return ackLines;
        }


        /// <summary>
        /// Strip the MLLP framing from the message string
        /// </summary>
        private string[] StripMLLPFrame(string MLLPFramedMessage)
        {
            string[] messageLines = null;
            // look for the start of the MLLP frame (VT control character)
            int start = MLLPFramedMessage.IndexOf((char)0x0B);
            if (start >= 0)
            {
                // Search for the end of the MLLP frame (FS control character)
                int end = MLLPFramedMessage.IndexOf((char)0x1C);
                if (end > start)
                {
                    // split the ACK message on <CR> character (segment delimiter), output each segment of the ACK on a new line
                    // remove the last <CR> character if present, otherwise the final element in the array will be empty when splitting the string
                    string ackString = MLLPFramedMessage.Substring(start + 1, end - 1);
                    if (ackString[ackString.Length - 1] == (char)0x0D)
                    {
                        ackString = ackString.Substring(0, ackString.Length - 1);
                    }
                    messageLines = ackString.Split((char)0x0D);
                }
            }
            return messageLines;
        }

        /// <summary>
        /// The following method is invoked by the RemoteCertificateValidationDelegate.
        /// Always return true, to ignore 
        /// </summary>
        public static bool SkipServerCertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
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
