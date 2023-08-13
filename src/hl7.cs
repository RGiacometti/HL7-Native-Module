using HL7Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HL7Tools
{
    static public class hl7
    {

        /// <summary>
        /// Set HL7 item value
        /// </summary>
        /// <param name="Path"></param>
        /// <param name="ItemPosition"></param>
        /// <param name="Value"></param>
        /// <param name="Filter"></param>
        /// <param name="Encoding"></param>
        /// <param name="ExpandWildcards"></param>
        static public void SetItem(string[] Path, string ItemPosition, string Value, string[] Filter = null, string Encoding = "UTF-8", bool ExpandWildcards = false)
        {
            SetHL7Item SetHL7I = new SetHL7Item(Path, ItemPosition, Value, Filter, Encoding, ExpandWildcards);
            SetHL7I.ProcessRecord();
        }

        /// <summary>
        /// Send HL7 Message
        /// </summary>
        /// <param name="Path"></param>
        /// <param name="HostName"></param>
        /// <param name="Port"></param>
        /// <param name="NoACK"></param>
        /// <param name="ExpandWildcards"></param>
        /// <param name="Delay"></param>
        /// <param name="Encoding"></param>
        /// <param name="UseTLS"></param>
        /// <param name="SkipCertificateCheck"></param>
        static public SendHL7MessageResult SendMessage(string[] Path, string HostName, int Port, bool NoACK = false, bool ExpandWildcards = false, int Delay = 0, string Encoding = "UTF-8", bool UseTLS = false, bool SkipCertificateCheck = false)
        {
            SendHL7Message SendHL7M = new SendHL7Message(Path, HostName, Port, NoACK, ExpandWildcards, Delay, Encoding, UseTLS, SkipCertificateCheck);
            SendHL7M.ProcessRecord();
            return SendHL7M.Result;
        }

    }
}
