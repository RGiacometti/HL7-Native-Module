/* Filename:    Common.cs
 * 
 * Author:      Rob Holme (rob@holme.com.au) 
 *              
 * Date:        29/08/2016
 * 
 * Notes:       Implements static functions common to more than one CmdLet class
 * 
 */

namespace HL7Tools
{
    using System.Collections.Generic;
    using System.IO;
    using System.Management.Automation;
    using System.Text.RegularExpressions;
    using Microsoft.PowerShell.Commands;

    public static class Common
    {
        /// <summary>
        /// Confirm that the HL7 item location string is in a valid format. It does not check to see if the item referenced exists or not.
        /// </summary>
        /// <param name="hl7ItemLocation"></param>
        /// <returns></returns>
        public static bool IsItemLocationValid(string hl7ItemLocation)
        {
            // make sure the location requested mactches the regex of a valid location string. This does not check to see if segment names exit, or items are present in the message
            if (Regex.IsMatch(hl7ItemLocation, "^[A-Z]{2}([A-Z]|[0-9])([[]([1-9]|[1-9][0-9])[]])?(([-][0-9]{1,3}([[]([1-9]|[1-9][0-9])[]])?[.][0-9]{1,3}[.][0-9]{1,3})|([-][0-9]{1,3}([[]([1-9]|[1-9][0-9])[]])?[.][0-9]{1,3})|([-][0-9]{1,3}([[]([1-9]|[1-9][0-9])[]])?))?$", RegexOptions.IgnoreCase)) // regex to confirm the HL7 element location string is valid
            {
                // make sure field, component and subcomponent values are not 0
                if (Regex.IsMatch(hl7ItemLocation, "([.]0)|([-]0)", RegexOptions.IgnoreCase)) {
                    return false;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        ///  Make sure the filter string matches the expected pattern for a filter
        /// </summary>
        /// <param name="filterString"></param>
        /// <returns></returns>
        public static bool IsFilterValid(string filterString)
        {
            if (Regex.IsMatch(filterString, "^[A-Z]{2}([A-Z]|[0-9])([[]([1-9]|[1-9][0-9])[]])?(([-][0-9]{1,3}([[]([1-9]|[1-9][0-9])[]])?[.][0-9]{1,3}[.][0-9]{1,3})|([-][0-9]{1,3}([[]([1-9]|[1-9][0-9])[]])?[.][0-9]{1,3})|([-][0-9]{1,3}([[]([1-9]|[1-9][0-9])[]])?))?=", RegexOptions.IgnoreCase)) {
                return true;
            }

            // the value provided after the -filter switch did not match the expected format of a message trigger.
            else {
                return false;
            }
        }

        /// <summary>
        /// return true if the string representing the HL7 location is valid. This does not confirm if the items exists, it only checks the formating of the string.
        /// </summary>
        /// <param name="HL7LocationString">The string identifying the location of the item within the message. eg PID-3.1</param>
        /// <returns></returns>
        public static bool IsHL7LocationStringValid(string HL7LocationString)
        {
            return (Regex.IsMatch(HL7LocationString, "^[A-Z]{2}([A-Z]|[0-9])([[]([1-9]|[1-9][0-9])[]])?(([-][0-9]{1,3}([[]([1-9]|[1-9][0-9])[]])?[.][0-9]{1,3}[.][0-9]{1,3})|([-][0-9]{1,3}([[]([1-9]|[1-9][0-9])[]])?[.][0-9]{1,3})|([-][0-9]{1,3}([[]([1-9]|[1-9][0-9])[]])?))?$", RegexOptions.IgnoreCase)); // segment([repeat])? or segment([repeat)?-field([repeat])? or segment([repeat)?-field([repeat])?.component or segment([repeat)?-field([repeat])?.component.subcomponent 
        }

        /// <summary>
        /// Check that this provider is the filesystem
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool IsFileSystemPath(ProviderInfo provider, string path)
        {
            bool isFileSystem = true;
            if (provider.ImplementingType != typeof(FileSystemProvider)) {
                // tell the caller that the item was not on the filesystem
                isFileSystem = false;
            }
            return isFileSystem;
        }

        /// <summary>
        /// return the portion of the filter string that identifies the HL7 Item to filter on
        /// </summary>
        /// <param name="filterString"></param>
        /// <returns></returns>
        public static string GetFilterItem(string filterString)
        {
            if (IsFilterValid(filterString)) {
                string[] tempString = (filterString).Split('=');
                return tempString[0];
            }
            else {
                return null;
            }
        }

        /// <summary>
        /// return the portion of the filter string that identifies the value to filter on
        /// </summary>
        /// <param name="filterString"></param>
        /// <returns></returns>
        public static string GetFilterValue(string filterString)
        {
            if (IsFilterValid(filterString)) {
                string[] tempString = (filterString.Split('='));
                if (tempString.Length > 1) {
                    return tempString[1];
                }
                else {
                    return null;
                }
            }
            else {
                return null;
            }

        }

        public static List<string> GetFilesFromPath(string path, bool expandWildcards)
        {
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
                    string filesDir = System.IO.Path.GetDirectoryName(path) ?? throw new DirectoryNotFoundException();
                    if (Directory.Exists(filesDir))
                    {
                        filePaths.AddRange(Directory.GetFiles(filesDir, System.IO.Path.GetFileName(path)));
                    }
                }
                else
                {
                    // no wildcards, so don't try to expand any * or ? symbols.                    
                    filePaths.Add(path);
                }
            }

            return filePaths;

        }
    }
}
