/* Filename:    UpdateHL7Item.cs
 * 
 * Author:      Rob Holme (rob@holme.com.au) 
 *              
 * Credits:     Code to handle the Path and LiteralPath parameter sets, and expansion of wildcards is based
 *              on Oisin Grehan's post: http://www.nivot.org/blog/post/2008/11/19/Quickstart1ACmdletThatProcessesFilesAndDirectories
 * 
 * Date:        29/08/2016
 * 
 * Notes:       Implements the cmdlet to update the value of a specific item from a HL7 v2 message.
 * 
 *  rrr
 */

namespace HL7Tools
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Linq;
    using System.Diagnostics;

    public class SetHL7Item
    {
        private string itemPosition;
        private string[] paths;
        private bool expandWildcards = false;
        private string[] filter = new string[] { };
        private bool filterConditionsMet = true;
        private string newValue;
        private bool allrepeats = false;
        private bool appendValue = false;
        private string encoding = "UTF-8";

        List<SetHL7ItemResult> result;

        public string[] LiteralPath
        {
            get { return this.paths; }
            set { this.paths = value; }
        }

        public string[] Path
        {
            get { return this.paths; }
            set
            {
                this.expandWildcards = true;
                this.paths = value;
            }
        }

        public string ItemPosition
        {
            get { return this.itemPosition; }
            set { this.itemPosition = value; }
        }

        public string Value
        {
            get { return this.newValue; }
            set { this.newValue = value; }
        }

        public string[] Filter
        {
            get { return this.filter; }
            set { this.filter = value; }
        }

        public string Encoding
        {
            get { return this.encoding; }
            set { this.encoding = value; }
        }

        /// <summary>
        /// get the HL7 item provided via the cmdlet parameter HL7ItemPosition
        /// </summary>
        protected void ProcessRecord()
        {
            // confirm the item location parameter is valid before processing any files
            if (!Common.IsHL7LocationStringValid(this.itemPosition))
            {
                throw new FileNotFoundException();
            }

            // confirm the filter parameter is valid before processing any files
            foreach (string currentFilter in this.filter)
            {
                // confirm each filter is formatted correctly
                if (!Common.IsFilterValid(currentFilter))
                {
                    throw new ArgumentException();
                }
            }

            // set the text encoding
            Encoding encoder = System.Text.Encoding.GetEncoding(this.encoding);
            Debug.WriteLine("Encoding: " + encoder.EncodingName);

            // expand the file or directory information provided in the -Path or -LiteralPath parameters
            foreach (string path in paths)
            {
                /*
                // This will hold information about the provider containing the items that this path string might resolve to.                
                ProviderInfo provider;

                // This will be used by the method that processes literal paths
                PSDriveInfo drive;
                */

                // this contains the paths to process for this iteration of the loop to resolve and optionally expand wildcards.
                List<string> filePaths = new List<string>();

                // if the path provided is a directory, expand the files in the directy and add these to the list.
                if (Directory.Exists(path))
                {
                    filePaths.AddRange(Directory.GetFiles(path));
                }

                // not a directory, could be a wildcard or literal filepath 
                else
                {
                    // expand wildcards. This assumes if the user listed a directory it is literal
                    if (expandWildcards)
                    {
                        // Turn *.txt into foo.txt,foo2.txt etc. If path is just "foo.txt," it will return unchanged. If the filepath expands into a directory ignore it.
                        string filesDir = Path.GetDirectoryName(path) ?? throw new DirectoryNotFoundException();
                        if (Directory.Exists(filesDir))
                        {
                            filePaths.AddRange(Directory.GetFiles(filesDir, Path.GetFileName(path)));
                        }
                    }
                    else
                    {
                        // no wildcards, so don't try to expand any * or ? symbols.                    
                        filePaths.Add(path);
                    }
                    // ensure that this path (or set of paths after wildcard expansion)
                    // is on the filesystem. A wildcard can never expand to span multiple providers.
                    /*
                    if (Common.IsFileSystemPath(provider, path) == false) {
                        // no, so skip to next path in paths.
                        continue;
                    }
                    */
                }

                // At this point, we have a list of paths on the filesystem, process each file. 
                foreach (string filePath in filePaths)
                {
                    // If the file does not exist display an error and return.
                    if (!File.Exists(filePath))
                    {
                        throw new FileNotFoundException("File not found", filePath);
                    }

                    // if the ItemPosition parameter is not in the correct format display an error and return
                    if (!Common.IsItemLocationValid(this.itemPosition))
                    {
                        throw new ArgumentException("The ItemPosition parameter does not appear to be in the correct format.");
                    }

                    // process the message
                    try
                    {
                        // assume the filter is true, until a failed match is found
                        this.filterConditionsMet = true;
                        // load the file into a HL7Message object for processing
                        string fileContents = File.ReadAllText(filePath, encoder);
                        HL7Message message = new HL7Message(fileContents);
                        // if a filter was supplied, evaluate if the file matches the filter condition
                        if (this.filter != null)
                        {
                            // check to see is all of the filter conditions are met (ie AND all filters supplied). 
                            foreach (string currentFilter in this.filter)
                            {
                                bool anyItemMatch = false;
                                string filterItem = Common.GetFilterItem(currentFilter);
                                string filterValue = Common.GetFilterValue(currentFilter);
                                // for repeating fields, only one of the items returned has to match for the filter to be evaluated as true.
                                foreach (string itemValue in message.GetHL7ItemValue(filterItem))
                                {
                                    if (itemValue.ToUpper() == filterValue.ToUpper())
                                    {
                                        anyItemMatch = true;
                                    }
                                }
                                // if none of the repeating field items match, then fail the filter match for this file. 
                                if (!anyItemMatch)
                                {
                                    this.filterConditionsMet = false;
                                }
                            }
                        }

                        // if the filter supplied matches this message (or no filter provided) then process the file to optain the HL7 item requested
                        if (filterConditionsMet)
                        {
                            List<HL7Item> hl7Items = message.GetHL7Item(itemPosition);
                            // if the hl7Items array is  empty, the item was not found in the message
                            if (hl7Items.Count == 0)
                            {
                                Debug.WriteLine("Item " + this.itemPosition + " not found in the message " + filePath);
                            }

                            //  items were located in the message, so proceed with replacing the original value with the new value.
                            else
                            {
                                // update all repeats/occurances of the specified item
                                if (this.allrepeats)
                                {
                                    foreach (HL7Item item in hl7Items)
                                    {
                                        // appeand the new value to the existing value of the item if -AppendToExistingValue switch is set
                                        if (appendValue)
                                        {
                                            this.newValue = item.ToString() + this.newValue;
                                        }
                                        // update the item value
                                        result.Add(new SetHL7ItemResult(this.newValue, item.ToString(), filePath, this.itemPosition));
                                        item.SetValueFromString(this.newValue);
                                    }
                                }
                                // update only the first occurrance. This is the default action.
                                else
                                {
                                    // append the new value to the existing value of the item if -AppendToExistingValue switch is set
                                    if (appendValue)
                                    {
                                        this.newValue = hl7Items.ElementAt(0).ToString() + this.newValue;
                                    }
                                    // update the item value
                                    result.Add(new SetHL7ItemResult(this.newValue, hl7Items.ElementAt(0).ToString(), filePath, this.itemPosition));
                                    hl7Items.ElementAt(0).SetValueFromString(this.newValue);
                                }
                                // Write changes to the file. Replace the segment delimeter <CR> with the system newline string as this is being written to a file.
                                string cr = ((char)0x0D).ToString();
                                string newline = System.Environment.NewLine;

                                System.IO.File.WriteAllText(filePath, message.ToString().Replace(cr, newline), encoder);

                            }
                        }
                    }

                    // if the file does not start with a MSH segment, the constructor will throw an exception. 
                    catch (System.ArgumentException)
                    {
                        throw new ArgumentException("The file does not appear to be a valid HL7 v2 message");
                    }
                }
            }
        }
    }

    /// <summary>
    /// An object containing the results to be returned to the pipeline. 
    /// </summary>
    public class SetHL7ItemResult
    {
        private string newValue;
        private string oldValue;
        private string location;
        private string filename;

        /// <summary>
        /// The new value of the HL7 item
        /// </summary>
        public string NewValue
        {
            get { return this.newValue; }
            set { this.newValue = value; }
        }

        /// <summary>
        /// The previous value of the HL7 item
        /// </summary>
        public string OldValue
        {
            get { return this.oldValue; }
            set { this.oldValue = value; }
        }

        /// <summary>
        /// The filename containing the item returned
        /// </summary>
        public string Filename
        {
            get { return this.filename; }
            set { this.filename = value; }
        }

        /// <summary>
        /// The location of the HL7 item that was changed. e.g. PID-3.1
        /// </summary>
        public string HL7Item
        {
            get { return this.location.ToUpper(); }
            set { this.location = value; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ItemValue"></param>
        /// <param name="Filename"></param>
        public SetHL7ItemResult(string NewValue, string OldValue, string Filename, string HL7Item)
        {
            this.newValue = NewValue;
            this.oldValue = OldValue;
            this.filename = Filename;
            this.location = HL7Item;
        }
    }

}
