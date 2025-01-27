﻿/* Filename:    RemoveHL7Identifiers.cs
 * 
 * Author:      Rob Holme (rob@holme.com.au) 
 * 
 * Date:        07/03/2016 - Initial Version
 *              23/07/2016 - Added GetMLLPFramedMessage()
 *              30/07/2016 - Re-factored GetHL7Item() to remove a redundant code, improve range checking.
 *                           Implemented base class HL7Item
 *				22/09/2016 - Fixed issue where HL7Message.ToString() was adding an additional blank line
 * 
 * Notes:       Implements a class to store and manipulate a HL7 v2 message.
 * 
 */

namespace HL7Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    public class HL7Item
    {
        //
        public virtual void Mask(char maskCharacter = '*')
        {
        }

        public virtual void SetValueFromString(string value)
        {
        }
    }

    /// <summary>
    /// Class representing a HL7 sub component
    /// </summary>
    public class SubComponent : HL7Item
    {
        private string subComponentValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubComponent"/> class.
        /// </summary>
        /// <param name="SubComponentValue">The value to assign to the sub component object</param>
        public SubComponent(string SubComponentValue)
        {
            this.subComponentValue = SubComponentValue;
        }

        /// <summary>
        /// Set the subcomponent value based on a string provided. No checking to confirm the string is valid.
        /// </summary>
        /// <param name="value"></param>
        public override void SetValueFromString(string value)
        {
            this.subComponentValue = value;
        }

        /// <summary>
        /// Gets or sets the value of the sub component
        /// </summary>
        public SubComponent Value
        {
            get { return this; }
            set { this.subComponentValue = Value.subComponentValue; }
        }

        /// <summary>
        /// convert value to string
        /// </summary>
        /// <returns>Returns the SubComponent value as a string</returns>
        public override string ToString()
        {
            return this.subComponentValue;
        }

        /// <summary>
        /// Mask out the text with a mask character
        /// </summary>
        /// <param name="maskCharacter">The character to use as the mask</param>
        public override void Mask(char maskCharacter = '*')
        {
            // ignore null fields (""), nothing identifiable to mask.
            if (this.subComponentValue != "\"\"") {
                string maskString = new string(maskCharacter, this.subComponentValue.Length);
                this.subComponentValue = maskString;
            }
        }
    }

    /// <summary>
    /// Class representing a HL7 component
    /// </summary>
    public class Component : HL7Item
    {
        private List<SubComponent> subComponents = new List<SubComponent>();
        private char subComponentDelimiter;

        /// <summary>
        /// Initialises a new instance of the <see cref="Component"/> class.
        /// </summary>
        /// <param name="ComponentValue"></param>
        /// <param name="SubComponentDelimiter">The character used as the delimiter for sub components. Defaults to "&"</param>
        public Component(string ComponentValue, char SubComponentDelimiter = '&')
        {
            this.subComponentDelimiter = SubComponentDelimiter; // the character used to delimit sub components in the HL7 message

            // split the string into sub components, then save
            string[] splitSubComponents = ComponentValue.Split(this.subComponentDelimiter);
            foreach (string subComponent in splitSubComponents) {
                this.subComponents.Add(new SubComponent(subComponent));
            }
        }

        /// <summary>
        /// Gets the list of SubComponents 
        /// </summary>
        public List<SubComponent> SubComponents
        {
            get { return this.subComponents; }
        }

        /// <summary>
        /// Gets or sets the list of SubComponents that make up a Component object.
        /// </summary>
        public Component Value
        {
            get { return this; }
            set { this.subComponents = Value.subComponents; }
        }

        /// <summary>
        /// Set the value of a Component based on the string provided. No checks are done to confirm if the string is valid.
        /// </summary>
        /// <param name="value"></param>
        public override void SetValueFromString(string value)
        {
            Component newComponent = new Component(value, this.subComponentDelimiter);
            this.subComponents = newComponent.subComponents;
        }

        /// <summary>
        /// Return the sub component if in range, else return null. Index starts at 1
        /// </summary>
        /// <param name="ID">ID represents the position of a single SubComponent in the list of SubComponents (starts at 1)</param>
        /// <returns>Returns a SubComponent object</returns>
        public SubComponent GetSubComponent(int ID)
        {
            if ((ID > 0) && (ID <= this.subComponents.Count)) {
                return this.subComponents[ID - 1];
            }
            else {
                return null;
            }
        }

        /// <summary>
        /// Mask out the component value
        /// </summary>
        /// <param name="MaskCharacter">The character to use to as the mask</param>
        public override void Mask(char MaskCharacter = '*')
        {
            foreach (SubComponent item in this.subComponents) {
                item.Mask(MaskCharacter);
            }
        }

        /// <summary>
        /// Set the SubComponent value at a specific index
        /// </summary>
        /// <param name="ID">The index representing the item in the list of SubComponents that is set</param>
        /// <param name="SubComponentValue">The value to assign to the SubComponent</param>
        public void SetSubComponent(int ID, SubComponent SubComponentValue)
        {
            if ((ID > 0) && (ID <= this.subComponents.Count)) {
                this.subComponents[ID - 1] = SubComponentValue;
            }

            // if the sub component is out of the range of current subcomponents, add in empty sub components until the range is large enough
            if (ID > this.subComponents.Count) {
                while (ID > this.subComponents.Count + 1) {
                    this.subComponents.Add(new SubComponent(string.Empty));
                }
                this.subComponents.Add(SubComponentValue);
            }
        }

        /// <summary>
        /// set the sub component at a specific index using a string value
        /// </summary>
        /// <param name="ID">The index (position) of the item to set the value of (index starts at 1)</param>
        /// <param name="SubComponentStringValue">The value to assign the SubComponent</param>
        public void SetSubComponent(int ID, string SubComponentStringValue)
        {
            if ((ID > 0) && (ID <= this.subComponents.Count)) {
                SubComponent tempSubComponent = new SubComponent(SubComponentStringValue);
                this.subComponents[ID - 1] = tempSubComponent;
            }

            // if the sub component is out of the range of current subcomponents, add in empty sub components until the range is large enough
            if (ID > this.subComponents.Count) {
                while (ID > this.subComponents.Count + 1) {
                    this.subComponents.Add(new SubComponent(string.Empty));
                }
                this.subComponents.Add(new SubComponent(SubComponentStringValue));
            }
        }

        /// <summary>
        /// Convert the component value to a string
        /// </summary>
        /// <returns>Returns a string containing the vale of the component</returns>
        public override string ToString()
        {
            if (this.subComponents.Count == 0) {
                return "";
            }
            else if (this.subComponents.Count == 1) {
                return this.subComponents[0].ToString();
            }
            else {
                string returnString = this.subComponents[0].ToString();
                for (int i = 1; i < this.subComponents.Count; i++) {
                    returnString += subComponentDelimiter + this.subComponents[i].ToString();
                }
                return returnString;
            }
        }
    }

    /// <summary>
    /// private class representing a single HL7 field item
    /// </summary>
    public class FieldItem : HL7Item
    {
        private List<Component> components = new List<Component>();
        private char componentDelimiter;
        private char subComponentDelimiter;

        /// <summary>
        /// Initializes a new instance of the <see cref="FieldItem"/> class.
        /// </summary>
        /// <param name="FieldValue">The value to assign the FieldItem</param>
        /// <param name="ComponentDelimiter">The character to use as the component delimiter. Defaults to '^'</param>
        /// <param name="SubComponentDelimiter">The character to use as the sub component delimiter. Defaults to '&'</param>
        public FieldItem(string FieldValue, char ComponentDelimiter = '^', char SubComponentDelimiter = '&')
        {
            // set the HL7 delimiter characters
            this.componentDelimiter = ComponentDelimiter;
            this.subComponentDelimiter = SubComponentDelimiter;

            // split the string into components, then add to component list
            string[] splitSubComponents = FieldValue.Split(this.componentDelimiter);
            foreach (string field in splitSubComponents) {
                this.components.Add(new Component(field, this.subComponentDelimiter));
            }
        }

        /// <summary>
        /// Gets the list of components that make up a field item
        /// </summary>
        public List<Component> Components
        {
            get { return this.components; }
        }

        /// <summary>
        /// Gets or set the FieldItem value
        /// </summary>
        public FieldItem Value
        {
            get { return this; }
            set { this.components = Value.components; }
        }

        /// <summary>
        /// Set the value of a Component based on the string provided. No checks are done to confirm if the string is valid.
        /// </summary>
        /// <param name="value"></param>
        public override void SetValueFromString(string value)
        {
            FieldItem newField = new FieldItem(value, this.componentDelimiter, this.subComponentDelimiter);
            this.components = newField.components;
        }

        /// <summary>
        /// Convert the field value to a string
        /// </summary>
        /// <returns>Returns a string of the field value</returns>
        public override string ToString()
        {
            if (this.components.Count == 0) {
                return string.Empty;
            }
            else if (this.components.Count == 1) {
                return this.components[0].ToString();
            }
            else {
                string returnString = this.components[0].ToString();
                for (int i = 1; i < this.components.Count; i++) {
                    returnString += this.componentDelimiter + this.components[i].ToString();
                }
                return returnString;
            }
        }

        /// <summary>
        /// Mask out the field value
        /// </summary>
        /// <param name="MaskCharacter">This defines the character to use as a mask. Defaults to '*'</param>
        public override void Mask(char MaskCharacter = '*')
        {
            foreach (Component item in this.Components) {
                item.Mask(MaskCharacter);
            }
        }
    }

    /// <summary>
    /// Class representing a HL7 field item, or list of field items
    /// </summary>
    public class Field : HL7Item
    {
        private List<FieldItem> fieldItems = new List<FieldItem>();
        private char fieldRepeatDelimiter;
        private char componentDelimiter;
        private char subComponentDelimiter;

        /// <summary>
        /// Initializes a new instance of the <see cref="Field"/> class.
        /// </summary>
        /// <param name="FieldValue">String containing the field value</param>
        /// <param name="FieldRepeatDelimiter">The repeat delimiter, defaults to '~'</param>
        /// <param name="ComponentDelimiter">The component delimiter, defaults to '^'</param>
        /// <param name="SubComponentDelimter">The sub component  delimiter, defaults to '&'</param>
        public Field(string FieldValue, char FieldRepeatDelimiter = '~', char ComponentDelimiter = '^', char SubComponentDelimter = '&')
        {
            // set the HL7 delimiter characters
            this.fieldRepeatDelimiter = FieldRepeatDelimiter;
            this.componentDelimiter = ComponentDelimiter;
            this.subComponentDelimiter = SubComponentDelimter;

            // split repeating field values, add each value to the list of SinglField objects
            string[] splitFields = FieldValue.Split(fieldRepeatDelimiter);
            foreach (string fieldItem in splitFields) {
                this.fieldItems.Add(new FieldItem(fieldItem, this.componentDelimiter, this.subComponentDelimiter));
            }
        }

        /// <summary>
        /// convert the field values to a string
        /// </summary>
        /// <returns>Returns a the field value as a string</returns>
        public override string ToString()
        {
            if (this.fieldItems.Count == 0) {
                return string.Empty;
            }
            else if (this.fieldItems.Count == 1) {
                return this.fieldItems[0].ToString();
            }
            else {
                string returnString = this.fieldItems[0].ToString();
                for (int i = 1; i < this.fieldItems.Count; i++) {
                    returnString += this.fieldRepeatDelimiter + this.fieldItems[i].ToString();
                }
                return returnString;
            }
        }

        /// <summary>
        /// Gets or sets the value of the Field object
        /// </summary>
        public Field Value
        {
            get { return this; }
            set { this.fieldItems = Value.fieldItems; }
        }

        /// <summary>
        /// Set the value of a Field object based on the string provided. No checks are done to confirm if the string is valid.
        /// </summary>
        /// <param name="value"></param>
        public override void SetValueFromString(string value)
        {
            Field newField = new Field(value, this.fieldRepeatDelimiter, this.componentDelimiter, this.subComponentDelimiter);
            this.fieldItems = newField.fieldItems;
        }

        /// <summary>
        /// Gets the list of FieldItems that make up a Field object
        /// </summary>
        public List<FieldItem> FieldItems
        {
            get { return this.fieldItems; }
        }

        /// <summary>
        /// Mask out the field value
        /// </summary>
        /// <param name="MaskCharacter">The character to use as the mask, defaults to '*'</param>
        public override void Mask(char MaskCharacter = '*')
        {
            foreach (FieldItem item in this.FieldItems) {
                item.Mask(MaskCharacter);
            }
        }
    }


    /// <summary>
    /// Class representing a HL7 message segment
    /// </summary>
    class Segment : HL7Item
    {
        private List<Field> fields = new List<Field>();
        private char fieldDelimeter;
        private char fieldRepeatDelimter;
        private char componentDelimeter;
        private char subComponentDelimiter;
        private string segmentName;

        /// <summary>
        /// Initializes a new instance of the <see cref="Segment"/> class. Create a list of fields representing the segment based on a string containing the segment
        /// </summary>
        /// <param name="SegmentValue">A string representing the value of the segment</param>
        /// <param name="FieldDelimeter">The character used as the field delimiter. Defaults to '|'</param>
        /// <param name="FieldRepeatDelimeter">The character used as the field repeat delimiter. Defaults to '~'</param>
        /// <param name="ComponentDelimeter">The character used as the component delimiter. Defaults to '^'</param>
        /// <param name="SubComponentDelimter">The character used as the sub component delimiter. Defaults to '&'</param>
        public Segment(string SegmentValue, char FieldDelimeter = '|', char FieldRepeatDelimeter = '~', char ComponentDelimeter = '^', char SubComponentDelimter = '&')
        {
            this.fieldDelimeter = FieldDelimeter;
            this.fieldRepeatDelimter = FieldRepeatDelimeter;
            this.componentDelimeter = ComponentDelimeter;
            this.subComponentDelimiter = SubComponentDelimter;

            // split repeating field values, add each value to the list of SinglField objects
            string[] splitSegment = SegmentValue.Split(FieldDelimeter);
            this.segmentName = splitSegment[0];
            // special case for message header, the field delimiter is MSH-1
            if (this.segmentName == "MSH") {
                this.fields.Add(new Field(this.fieldDelimeter.ToString(), this.fieldRepeatDelimter, this.componentDelimeter, this.subComponentDelimiter));

            }
            // now add the remaining fields for all segment types
            for (int i = 1; i < splitSegment.Length; i++) {
                this.fields.Add(new Field(splitSegment[i], this.fieldRepeatDelimter, this.componentDelimeter, this.subComponentDelimiter));
            }
        }

        /// <summary>
        /// Gets or sets the value of the Segment
        /// </summary>
        public Segment Value
        {
            get { return this; }
            set { this.fields = Value.fields; }
        }

        /// <summary>
        /// Set the value of a Segment object based on the string provided. No checks are done to confirm if the string is valid.
        /// </summary>
        /// <param name="value"></param>
        public override void SetValueFromString(string value)
        {
            Segment newSegment = new Segment(value, this.fieldDelimeter, this.fieldRepeatDelimter, this.componentDelimeter, this.subComponentDelimiter);
            this.fields = newSegment.fields;
        }

        /// <summary>
        /// Gets or sets a list of Fields that the segment comprises of
        /// </summary>
        public List<Field> Fields
        {
            get { return this.fields; }
            set { this.fields = Fields; }
        }

        /// <summary>
        /// Gets the name of the segment
        /// </summary>
        public string Name
        {
            get { return this.segmentName; }
        }

        /// <summary>
        /// Converts the value of a Segment object to a sting
        /// </summary>
        /// <returns>Returns a copy of the segment value as a string</returns>
        public override string ToString()
        {
            if (this.fields.Count == 0) {
                return "";
            }

            string returnString = "";
            returnString = this.segmentName;
            // the MSH segment is different because MSH-1 is the field delimiter
            if (this.segmentName == "MSH") {
                for (int i = 1; i < this.fields.Count; i++) {
                    returnString += fieldDelimeter + this.fields[i].ToString();
                }
            }
            else {
                for (int i = 0; i < this.fields.Count; i++) {
                    returnString += fieldDelimeter + this.fields[i].ToString();
                }
            }
            return returnString;
        }

        /// <summary>
        /// Mask out the field value
        /// </summary>
        /// <param name="MaskCharacter">The character to use as the mask, defaults to '*'</param>
        public override void Mask(char MaskCharacter = '*')
        {
            foreach (Field item in this.Fields) {
                item.Mask(MaskCharacter);
            }
        }
    }

    /// <summary>
    /// Class representing a HL7 message. Items are treated as text only, no strict HL7 message schema
    /// </summary>
    public class HL7Message
    {
        private List<Segment> segments = new List<Segment>();
        private char fieldDelimeter;
        private char fieldRepeatDelimeter;
        private char componentDelimeter;
        private char subComponentDelimeter;

        /// <summary>
        /// Initializes a new instance of the <see cref="HL7Message"/> class.
        /// </summary>
        /// <param name="Message">A string containing the full message text used to create the HL7Message object</param>
        public HL7Message(string Message)
        {
            // If the file has been edited in a text editor, <CR><LF> may have been inserted at the end of each line. HL7 segments should only be delimited by <CR> characters, so replace <CR><LF> with <CR>
            string crlf = ((char)0x0D).ToString() + ((char)0x0A).ToString();
            string cr = ((char)0x0D).ToString();
			string lf = ((char)0x0A).ToString();

			// strip windows style or linux style line endings, replace with CRLF or LF with CR only.
            Message = Message.Replace(crlf, cr);
			Message = Message.Replace(lf, cr);
            
			string[] segmentStrings = Message.Split((char)0x0D);
            // set the field, component, sub component and repeat delimiters
            int startPos = Message.IndexOf("MSH");
            if (startPos >= 0) {
                startPos = startPos + 2;
                this.fieldDelimeter = Message[startPos + 1];
                this.componentDelimeter = Message[startPos + 2];
                this.fieldRepeatDelimeter = Message[startPos + 3];
                this.subComponentDelimeter = Message[startPos + 5];
            }
            // throw an exception if a MSH segment is not included in the message. 
            else {
                throw new ArgumentException("MSH segment not present.");
            }
            // add each segment
            foreach (string segmentItem in segmentStrings) {
                this.segments.Add(new Segment(segmentItem, this.fieldDelimeter, this.fieldRepeatDelimeter, this.componentDelimeter, this.subComponentDelimeter));
            }

        }


        /// <summary>
        /// The filename containing the item returned
        /// </summary>
        public char ComponentDelimeter
        {
            get { return this.componentDelimeter; }
        }

        /// <summary>
        /// The filename containing the item returned
        /// </summary>
        public char SubComponentDelimeter
        {
            get { return this.subComponentDelimeter; }
        }

        /// <summary>
        /// The filename containing the item returned
        /// </summary>
        public char FieldDelimeter
        {
            get { return this.fieldDelimeter; }
        }

        /// <summary>
        /// The filename containing the item returned
        /// </summary>
        public char FieldRepeatDelimeter
        {
            get { return this.fieldRepeatDelimeter; }
        }

        /// <summary>
        /// Gets a list of all Segments from the message
        /// </summary>
        //public List<Segment> Segments
        //{
        //    get { return this.segments; }
        //}

        /// <summary>
        /// Gets or sets the segments contained in the message
        /// </summary>
        public HL7Message Value
        {
            get { return this; }
            set { this.segments = Value.segments; }
        }


        /// <summary>
        /// Return the HL7 message contents as a string.
        /// </summary>
        /// <returns>returns a copy of the HL7Message as a string</returns>
        public override string ToString()
        {
            if (this.segments.Count == 0) {
                return "";
            }
            else {
                string returnString = "";
                foreach (Segment item in this.segments) {
                    // only add segments that contain content - ignore blank lines at the end of files etc.
                    if (item.ToString().Length > 0) {
                        returnString += item.ToString() + (char)0x0D;
                    }
                }
                return returnString;
            }
        }

        /// <summary>
        /// De Identify message fields.
        /// All PID fields except PID-1, PID-2, PID-3
        /// All NK1 fields except NK1-1, NK1-3
        /// All IN1 fields 
        /// All IN2 fields 
        /// </summary>
        /// <param name="MaskCharacter">The character used to mask the values, defaults to '*'</param>
        public void DeIdentify(char MaskCharacter = '*')
        {
            List<Segment> PIDSegments = this.GetSegment("PID");
            List<Segment> NK1Segments = this.GetSegment("NK1");
            List<Segment> IN1Segments = this.GetSegment("IN1");
            List<Segment> IN2Segments = this.GetSegment("IN2");

            // mask PID segments
            foreach (Segment segmentItem in PIDSegments) {
                // ignore PID-1, PID-2, PID-3
                for (int i = 3; i < segmentItem.Fields.Count; i++) {
                    segmentItem.Fields[i].Mask(MaskCharacter);
                }
            }

            // mask NK1 segments
            foreach (Segment segmentItem in NK1Segments) {
                // ignore NK1-1
                for (int i = 1; i < segmentItem.Fields.Count; i++) {
                    // ignore NK-3
                    if (i != 2) {
                        segmentItem.Fields[i].Mask(MaskCharacter);
                    }
                }
            }

            // mask IN1 segments
            foreach (Segment segmentItem in IN1Segments) {
                // mask all fields
                for (int i = 0; i < segmentItem.Fields.Count; i++) {
                    segmentItem.Fields[i].Mask(MaskCharacter);
                }
            }

            // mask IN1 segments
            foreach (Segment segmentItem in IN2Segments) {
                // mask all fields
                for (int i = 0; i < segmentItem.Fields.Count; i++) {
                    segmentItem.Fields[i].Mask(MaskCharacter);
                }
            }

        }

        /// <summary>
        /// Return the segment name and indexes of the field, component and subcomponent based on a text string identifying the HL7 item (e.g. PID-3.1). If values are not specified they will be set to 0.
        /// </summary>
        /// <param name="HL7ItemPosition">A text string identifying the HL7 item (e.g. PID-3.1)</param>
        /// <param name="Segment">The three letter segment name</param>
        /// <param name="SegmentRepeat">The segment repeat number, set to 0 if no segment repeat requested</param>
        /// <param name="Field">The field number</param>
        /// <param name="FieldRepeat">The field repeat number, set to 0 if no specific repeat requested</param>
        /// <param name="Component">The component number</param>
        /// <param name="SubComponent">The subcomponent number.</param>
        private void GetItemPosition(string HL7LocationString, ref string SegmentName, ref int SegmentRepeatNumber, ref int FieldNumber, ref int FieldRepeatNumber, ref int ComponentNumber, ref int SubComponentNumber)
        {
            SegmentName = string.Empty;
            FieldNumber = 0;
            ComponentNumber = 0;
            SubComponentNumber = 0;
            SegmentRepeatNumber = 0;
            FieldRepeatNumber = 0;

            //  use regular expressions to determine if the item location string is valid.
            if (Regex.IsMatch(HL7LocationString, "^[A-Z]{2}([A-Z]|[0-9])([[]([1-9]|[1-9][0-9])[]])?(([-][0-9]{1,3}([[]([1-9]|[1-9][0-9])[]])?[.][0-9]{1,3}[.][0-9]{1,3})|([-][0-9]{1,3}([[]([1-9]|[1-9][0-9])[]])?[.][0-9]{1,3})|([-][0-9]{1,3}([[]([1-9]|[1-9][0-9])[]])?))?$", RegexOptions.IgnoreCase)) // segment([repeat])? or segment([repeat)?-field([repeat])? or segment([repeat)?-field([repeat])?.component or segment([repeat)?-field([repeat])?.component.subcomponent 
            {

                // Obtain the segment repeat number if specified
                Match checkRepeatingSegmentNumber = System.Text.RegularExpressions.Regex.Match(HL7LocationString, "^[A-Z]{2}([A-Z]|[0-9])[[][1-9]{1,3}[]]", RegexOptions.IgnoreCase);
                if (checkRepeatingSegmentNumber.Success == true) {
                    string tmpStr = checkRepeatingSegmentNumber.Value.Split('[')[1];
                    SegmentRepeatNumber = Int32.Parse(tmpStr.Split(']')[0]);

                }

                // Obtain the field repeat number if specified
                Match checkRepeatingFieldNumber = System.Text.RegularExpressions.Regex.Match(HL7LocationString, "[-][0-9]{1,3}[[]([1-9]|[1-9][0-9])[]]", RegexOptions.IgnoreCase);
                if (checkRepeatingFieldNumber.Success == true) {
                    string tmpStr = checkRepeatingFieldNumber.Value.Split('[')[1];
                    FieldRepeatNumber = Int32.Parse(tmpStr.Split(']')[0]);
                }

                // retrieve the field, component and sub component values. If they don't exist, set to 0
                string[] tempString = HL7LocationString.Split('-');
                SegmentName = tempString[0].Substring(0, 3); // the segment name
                if (tempString.Count() > 1) // confirm values other than the segment were provided.
                {
                    string[] tempString2 = tempString[1].Split('.');
                    if (tempString2.Count() >= 1) // field exists, possibly more. Set the field value.
                    {
                        FieldNumber = Int32.Parse(tempString2[0].Split('[')[0]); // if the field contains a repeat number, ignore the repeat value and braces
                    }
                    if (tempString2.Count() >= 2) // field and component, possibly more. Set the component value
                    {
                        ComponentNumber = Int32.Parse(tempString2[1]);
                    }
                    if (tempString2.Count() == 3) // field, component and sub component exist. Set the value of the subComponent.
                    {
                        SubComponentNumber = Int32.Parse(tempString2[2]);
                    }
                }
            }
        }

        /// <summary>
        /// Mask the nominated HL7Item
        /// </summary>
        /// <param name="HL7LocationString">A string identifying the location of the HL7 item e.g. PID-3.1</param>
        /// <param name="MaskChar">The character to mask out the item with, defaults to '*'</param>
        public void MaskHL7Item(string HL7LocationString, char MaskChar = '*')
        {
            List<HL7Item> items = this.GetHL7Item(HL7LocationString);
            foreach (HL7Item item in items) {
                item.Mask(MaskChar);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="HL7LocationString">A string representing the location on the item within the message. e.g. PID-3.1, MSH-4, PID-13[1].1</param>
        /// <returns>Returns a copy of the nominated HL7 item as a string</returns>
        public List<HL7Item> GetHL7Item(string HL7LocationString)
        {
            string segmentName = "";
            int fieldNumber = 0;
            int componentNumber = 0;
            int subcomponentNumber = 0;
            int segmentRepeatNumber = 0;
            int fieldRepeatNumber = 0;
            List<HL7Item> returnHL7Item = new List<HL7Item>();

            // get the indexes of the item being requested based on the string provided by the caller
            this.GetItemPosition(HL7LocationString, ref segmentName, ref segmentRepeatNumber, ref fieldNumber, ref fieldRepeatNumber, ref componentNumber, ref subcomponentNumber);

            // get the list of segments the item(s) belong to
            List<Segment> segmentList = this.GetSegment(segmentName, segmentRepeatNumber);

            // Subcomponent value requested
            if (subcomponentNumber != 0) {
                List<FieldItem> fieldItems = this.GetField(segmentList, fieldRepeatNumber, fieldNumber);
                List<Component> componentItems = this.GetComponent(fieldItems, componentNumber);
                List<SubComponent> subComponentItems = this.GetSubcomponent(componentItems, subcomponentNumber);
                foreach (SubComponent item in subComponentItems) {
                    returnHL7Item.Add(item);
                }

            }

            // Component value requested
            else if (componentNumber != 0) {
                List<FieldItem> fieldItems = this.GetField(segmentList, fieldRepeatNumber, fieldNumber);
                List<Component> componentItems = this.GetComponent(fieldItems, componentNumber);
                foreach (Component item in componentItems) {
                    returnHL7Item.Add(item);
                }
            }

            // Field value requested
            else if (fieldNumber != 0) {
                List<FieldItem> fieldItems = this.GetField(segmentList, fieldRepeatNumber, fieldNumber);
                foreach (FieldItem item in fieldItems) {
                    returnHL7Item.Add(item);
                }
            }

            // Segment value requested
            else if (segmentName != null) {
                foreach (Segment item in segmentList) {
                    returnHL7Item.Add(item);
                }
            }

            // return the result to the caller
            return returnHL7Item;
        }



        /// <summary>
        /// Return the value for the corresponding HL7 item. HL7LocationString is formatted as Segment-Field.Component.SubComponent e.g. PID-3 or PID-5.1.1
        /// </summary>
        /// <param name="HL7LocationString">A string representing the location on the item within the message. e.g. PID-3.1, MSH-4, PID-13[1].1</param>
        /// <returns>Returns a copy of the nominated HL7 item as a string</returns>
        public string[] GetHL7ItemValue(string HL7LocationString)
        {
            string segmentName = "";
            int fieldNumber = 0;
            int componentNumber = 0;
            int subcomponentNumber = 0;
            int segmentRepeatNumber = 0;
            int fieldRepeatNumber = 0;
            List<string> returnString = new List<string>();

            // get the indexes of the item being requested based on the string provided by the caller
            this.GetItemPosition(HL7LocationString, ref segmentName, ref segmentRepeatNumber, ref fieldNumber, ref fieldRepeatNumber, ref componentNumber, ref subcomponentNumber);

            // get the list of segments the item(s) belong to
            List<Segment> segmentList = this.GetSegment(segmentName, segmentRepeatNumber);

            // Subcomponent value requested
            if (subcomponentNumber != 0) {
                List<FieldItem> fieldItems = this.GetField(segmentList, fieldRepeatNumber, fieldNumber);
                List<Component> componentItems = this.GetComponent(fieldItems, componentNumber);
                List<SubComponent> subComponentItems = this.GetSubcomponent(componentItems, subcomponentNumber);
                foreach (SubComponent item in subComponentItems) {
                    returnString.Add(item.ToString());
                }

            }

            // Component value requested
            else if (componentNumber != 0) {
                List<FieldItem> fieldItems = this.GetField(segmentList, fieldRepeatNumber, fieldNumber);
                List<Component> componentItems = this.GetComponent(fieldItems, componentNumber);
                foreach (Component item in componentItems) {
                    returnString.Add(item.ToString());
                }
            }

            // Field value requested
            else if (fieldNumber != 0) {
                List<FieldItem> fieldItems = this.GetField(segmentList, fieldRepeatNumber, fieldNumber);
                foreach (FieldItem item in fieldItems) {
                    returnString.Add(item.ToString());
                }
            }

            // Segment value requested
            else if (segmentName != null) {
                foreach (Segment item in segmentList) {
                    returnString.Add(item.ToString());
                }
            }

            // return the result to the caller
            return returnString.ToArray();
        }

        /// <summary>
        /// Return the HL7 message with MLLP framing added
        /// </summary>
        /// <returns>Returns the message contents as a string framed in the MLLP control characters</returns>
        public string GetMLLPFramedMessage()
        {
            StringBuilder mllpMessage = new StringBuilder();
            mllpMessage.Append((char)0x0B);
            mllpMessage.Append(this.ToString());
            mllpMessage.Append((char)0x1C);
            mllpMessage.Append((char)0x0D);
            return mllpMessage.ToString();
        }

        /// <summary>
        /// Returns a list of one or more SubComponent objects based on the item location provided
        /// </summary>
        /// <param name="ComponentItemList">A list of one or more Components to search</param>
        /// <param name="SubcomponentNumber">Identifies the index of the subcomponent to return (index starts from 1)</param>
        /// <returns>Returns a list of SubComponent objects containing the Subcomponents matching the query</returns>
        private List<SubComponent> GetSubcomponent(List<Component> ComponentItemList, int SubcomponentNumber)
        {
            List<SubComponent> subcomponentList = new List<SubComponent>();

            // return the subcomponent from each component in the list
            foreach (Component componentItem in ComponentItemList) {
                // confirm the subcomponent requested exists in the list of subcomponent
                if (SubcomponentNumber <= componentItem.SubComponents.Count()) {
                    subcomponentList.Add(componentItem.SubComponents[SubcomponentNumber - 1]);
                }
				// if the item isn't found return an empty value (absence of an item assumes an empty value)
				else {
					subcomponentList.Add(new SubComponent("")); 
				}
            }
            return subcomponentList;
        }

        /// <summary>
        /// Returns a list of one or more component objects based on the item location provided
        /// </summary>
        /// <param name="FieldItemList">A list of one or more FieldItem objects to search</param>
        /// <param name="ComponentNumber">Identifies the index of the component to search (index starts from 1)</param>
        /// <returns>Returns a list of Component objects containing the Components matching the query</returns>
        private List<Component> GetComponent(List<FieldItem> FieldItemList, int ComponentNumber)
        {
            List<Component> returnComponents = new List<Component>();

            foreach (FieldItem fieldItem in FieldItemList) {
                // confirm the component requested is in range
                if (ComponentNumber <= fieldItem.Components.Count()) {
                    returnComponents.Add(fieldItem.Components[ComponentNumber - 1]);
                }
				// if the item isn't found return an empty value (absence of an item assumes an empty value)
				else {
					returnComponents.Add(new Component(""));
				}
            }
            return returnComponents;
        }

        /// <summary>
        /// Returns a list of one or more FieldItems objects based on the item location provided
        /// </summary>
        /// <param name="SegmentItemList">A list of one or more segments to search</param>
        /// <param name="FieldRepeat">Identifies a specific occurrence of a repeating field. Set to 0 to search all field repeat occurrences</param>
        /// <param name="FieldNumber">Identifies the index of the field to search (index starts from 1)</param>
        /// <returns>Returns a list of Field objects containing the Fields matching the query</returns>
        private List<FieldItem> GetField(List<Segment> SegmentItemList, int FieldRepeat, int FieldNumber)
        {
            List<FieldItem> returnFields = new List<FieldItem>();

            // a specific field repeat was requested, only return matching field values within the specific field occurrence
            if (FieldRepeat != 0) {
                foreach (Segment segmentItem in SegmentItemList) {
                    if (FieldNumber <= segmentItem.Fields.Count()) {
                        if (FieldRepeat <= segmentItem.Fields[FieldNumber - 1].FieldItems.Count()) {
                            returnFields.Add(segmentItem.Fields[FieldNumber - 1].FieldItems[FieldRepeat - 1]);
                        }
						// if the item isn't found return an empty value (absence of an item assumes an empty value)
						else {
							returnFields.Add(new FieldItem(""));
						}
                    }
                }
            }

            // no repeating field item specified, return matching field values from field occurrences for this single segment
            else {
                foreach (Segment segmentItem in SegmentItemList) {
                    // check the range of the FieldNumber index. 
                    if (FieldNumber <= segmentItem.Fields.Count()) {
                        // return all repeats of the FieldItem requested 
                        foreach (FieldItem field in segmentItem.Fields[FieldNumber - 1].FieldItems) {
                            returnFields.Add(field);
                        }
                    }
					// if the item isn't found return an empty value (absence of an item assumes an empty value)
					else {
						returnFields.Add(new FieldItem(""));
					}
                }
            }
            return returnFields;
        }

        /// <summary>
        /// Returns a list of one or more Segment objects based on the item location provided
        /// </summary>
        /// <param name="SegmentName">The three letter segment name</param>
        /// <param name="SegmentRepeat">Identifies a specific occurrence of a repeating segment. Set to 0 to search all occurrences</param>
        /// <returns>Returns a list of Segment objects containing the segments matching the query</returns>
        private List<Segment> GetSegment(string SegmentName, int SegmentRepeat = 0)
        {
            List<Segment> returnSegment = new List<Segment>();
            List<Segment> segmentList = new List<Segment>();

            // create a list of all Segments that match the name requested
            foreach (Segment segmentItem in this.segments) {
                if (segmentItem.Name.ToUpper() == SegmentName.ToUpper()) {
                    segmentList.Add(segmentItem);
                }
            }

            // segment repeat identified, only return the single segment
            if (SegmentRepeat != 0) {
                // make sure the segment repeat requested is in range (i.e. it is contained in the message)
                if (SegmentRepeat <= segmentList.Count()) {
                    returnSegment.Add(segmentList[SegmentRepeat - 1]);
                }
            }

            // no segment repeat identified, so return all matching segments.
            else {
                returnSegment = segmentList;
            }

            return returnSegment;
        }

    }
}


