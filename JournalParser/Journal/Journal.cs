/*
 * Work from https://github.com/andydandy74
 * https://github.com/andydandy74/Journalysis
 * 
 * Adapted from Python to C# in order to use the the methods in revit addins and/or external programs
 *  
 */

using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ACPV.Utilities.JournalParser
{
    #region JournalLineType Enum
    public enum JournalLineType
    {
        JournalLine,
        JournalAddinEvent,
        JournalAPIMessage,
        JournalBasicFileInfo,
        JournalCommand,
        JournalComment,
        JournalData,
        JournalDirective,
        JournalGUIResourceUsage,
        JournalKeyboardEvent,
        JournalMemoryMetrics,
        JournalMiscCommand,
        JournalMouseEvent,
        JournalSystemInformation,
        JournalTimeStamp,
        JournalUIEvent,
        JournalWorksharingEvent
    }
    #endregion
    public class Journal
    {
        public IList<JournalLine> JournalLines { get; set; }
        public string Build { get; }
        public string Branch { get; }
        public string SessionId { get; }
        public string Release { get; }
        public int BlockCount { get; }
        public string Path { get; }
        public string Username { get; }
        public string MachineName { get; }
        public string OSVersion { get; }
        public int Version { get; }
        public TimeSpan ProcessingTime { get; set; }

        public int journalLinesCount
        {
            get
            {
                return JournalLines.Count();
            }
        }
        public int blockcount;

        public Journal()
        {
            JournalLines = new List<JournalLine>();
        }
        public Journal(IList<JournalLine> jLines)
        {
            JournalLines = jLines;
        }
        public Journal(IList<JournalLine> jLines, int version, string username, int blockcount, string path, string build, string branch, string machineName, string osVersion, string sessionId)
        {
            JournalLines = jLines;
            Version = version;
            Username = username;
            BlockCount = blockcount;
            Path = path;
            Build = build;
            Branch = branch;
            MachineName = machineName;
            OSVersion = osVersion;
            SessionId = sessionId;
        }

        /*public void Add(JournalLine journalLine)
        {
            journalLines.ToList().Add(journalLine);
         }*/
        public string GetLicenseInfo()
        {
            try
            {
                JournalLine firstLicenseLine = GetLinesByType(JournalLineType.JournalComment).Where(x => x.RawText.Contains("License mode:")).First();
                var licenseMode = firstLicenseLine.RawText.Split(':')[2].Trim();
                if (licenseMode == "Network") {
                    var serverline = firstLicenseLine.Next(firstLicenseLine);
                    var serverType = serverline.RawText.Split(':')[2].Trim();
                    var licenseStatus = serverline.Next(serverline).RawText.Split(':')[2].Trim();
                    return licenseMode + ":" + serverType + ":" + licenseStatus;
                 }
                else
                {
                    var licenseStatus = firstLicenseLine.Next(firstLicenseLine).RawText.Split(':')[2].Trim();
                    return licenseMode + ":" + licenseStatus;
                }
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
        public bool ContainsAPIErrors()
        {
            try
            {
                //if (this.GetLinesByTypeAndProperty(JournalLineType.JournalAPIMessage, "IsError", "true").Count() > 0) return true;
                //else return false;

                if (this.JournalLines.OfType<JournalAPIMessage>().Where(x => x.IsError).Count() > 0) return true;
                else return false;
            }
            catch
            {
                return false;
            }
        }
        public bool ContainsExceptions()
        {
            try
            {
                return this.GetLinesByType(JournalLineType.JournalTimeStamp).
                    Cast<JournalTimeStamp>().Any(x => x.Description.StartsWith("ExceptionCode"));
            }
            catch
            {
                return false;
            }
        }

        /*public IList<LoadedAssembly> GetLoadedAssemblies()
        {
            IList<LoadedAssembly> loadedAssemblies = new List<LoadedAssembly>();
            IList<JournalLine> apimsg = GetLinesByType(JournalLineType.JournalAPIMessage);
            foreach (JournalLine journalLine in apimsg)
            {

            }
        }*/
        public IList<JournalLine> GetLinesByType(JournalLineType journalLineType)
        {
            return JournalLines.Where(x => x.Type.Equals(journalLineType)).ToList();
        }
        public IList<JournalLine> GetLinesByTypes(IList<JournalLineType> journalLineTypes)
        {
            IList<JournalLine> jLines = new List<JournalLine>();
            foreach (JournalLineType jType in journalLineTypes)
            {
                jLines.Concat(JournalLines.Where(x => x.Type.Equals(jType)));
            }
            return jLines;
        }
        public IList<JournalLine> GetLinesByDateTime(DateTime? afromDate = null, DateTime? atoDate = null)
        {
            var fromDate = afromDate ?? DateTime.MinValue;
            var toDate = atoDate ?? DateTime.MaxValue;
            try
            {
                IList<JournalTimeStamp> ts = GetLinesByType(JournalLineType.JournalTimeStamp).Cast<JournalTimeStamp>().Where(x => x.DateTime > fromDate && x.DateTime < toDate).ToList();
                List<int> g = ts.Select(x => x.Block).Distinct().ToList();
                return GetLinesByBlocks(g);
            }
            catch
            {
                return null;
            }
        }
        // isn't it too much variable? means the property must be known ok in phyton but c# has lots of type
        /*public JournalLines GetLinesByTypeAndProperty(JournalLineType journalLineType,string property,string value)
        {
            try
            {
                //return journalLines.OfType<journalLineType>().GetType().GetProperty( Where(x => x.Type.Equals(journalLineType) && x.GetType;
                //return journalLines.Where(x => x.Type.Equals(journalLineType) && x.GetType().GetProperty(property).GetValue(value));
            }
            catch
            {
                return null;
            }
        }*/
        public TimeSpan GetSessionTime()
        {
            try
            {
                IList<JournalTimeStamp> ts = GetLinesByType(JournalLineType.JournalTimeStamp).Cast<JournalTimeStamp>().ToList();
                return ts.Last().DateTime - ts.First().DateTime;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }


        // Blocks: a group of journal lines between two JournalTimeStamp
        // except the Block 0 which start without JournalTimeStamp
        #region Operation on Blocks
        public DateTime GetDateTimeByBlock(int block)
        {
            try
            {
                // block 0, no timestamp associated
                if (block == 0) return DateTime.MinValue;
                else return GetLinesByType(JournalLineType.JournalTimeStamp).Cast<JournalTimeStamp>().Where(x => x.Block == block).First().DateTime;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        public IList<JournalLine> GetLinesByBlock(int block)
        {
            return JournalLines.Where(x => x.Block == block).ToList();
        }
        public IList<JournalLine> GetLinesByBlocks(List<int> blocks)
        {
            IList<JournalLine> jLines = new List<JournalLine>();
            foreach (int b in blocks)
            {
                jLines.Concat(JournalLines.Where(x => x.Block == b));
            }
            return jLines;
        }
        #endregion

        /// <summary>
        /// Compute how long it takes revit to display the starup screen
        /// in REVIT 2019.1 Jrn.Command has been changed from ID_STARTUP_PAGE to ID_REVIT_MODEL_BROWSER_OPEN
        /// </summary>
        /// <returns>DateTime</returns>
        public DateTime GetStartupTime()
        {
            try
            {
                DateTime first_ts = GetDateTimeByBlock(1);
                int fix;
                bool success = Int32.TryParse(Release.Split('.')[1], out fix);
                IList<int> startup1 = new List<int>();
                if (Version >= 2019 && success && fix >= 1)
                {
                    startup1.Concat(GetLinesByType(JournalLineType.JournalCommand).Cast<JournalCommand>()
                    .Where(x => x.CommandID == "ID_REVIT_MODEL_BROWSER_OPEN").Select(x => x.Block).ToList());
                }
                else
                {
                    startup1.Concat(GetLinesByType(JournalLineType.JournalCommand).Cast<JournalCommand>()
                    .Where(x => x.CommandID == "ID_STARTUP_PAGE").Select(x => x.Block).ToList());
                }

                if (startup1.Count() > 0)
                {
                    return GetDateTimeByBlock(startup1.First());
                }
                else
                {
                    // DynamoAutomation journal playback
                    IList<int> startup2 = GetLinesByType(JournalLineType.JournalCommand).Cast<JournalCommand>()
                        .Where(x => x.CommandID == "ID_FILE_MRU_FIRST").Select(x => x.Block).ToList();
                    if (startup2.Count() > 0)
                    {
                        return GetDateTimeByBlock(startup2.First());
                    }
                    else
                    {
                        return DateTime.MinValue;
                    }
                }
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
        public bool WasSessionTerminatedProperly()
        {
            return GetLinesByType(JournalLineType.JournalTimeStamp).Cast<JournalTimeStamp>().Any(x => x.Description.Equals("finished recording journal file"));
        }

    }

    public class JournalLine
    {
        public int Number { get; set; }
        public string RawText { get; set; }
        public int Block { get; set; }
        public Journal Journal { get; set; }
        public JournalLineType Type { get; set; }

        public JournalLine(int number, string rawText, int block, JournalLineType type)
        {
            Number = number;
            RawText = rawText;
            Block = block;
            Type = type;
        }
        public JournalLine()
        {

        }

        public JournalLine Next(JournalLine journalLine)
        {
            try
            {
                if (journalLine.Journal.JournalLines.IndexOf(journalLine) + 1 < journalLine.Journal.journalLinesCount) return journalLine.Journal.JournalLines[journalLine.Journal.JournalLines.IndexOf(journalLine) + 1];
                else return null;
            }
            catch
            {
                return null;
            }
        }
    }

    public class LoadedAssembly
    {
        public string Name { get; set; }
        public string Class { get; set; }
        public string Path { get; set; }
        public string Filename { get; set; }
        public string GUID { get; set; }
        public string Events { get; set; }

        public LoadedAssembly(string name, string cLass, string path, string fileName, string gUID, string events)
        {
            Name = name;
            Class = cLass;
            Path = path;
            Filename = fileName;
            GUID = gUID;
            Events = events;
        }
    }

    public class JournalLines : IEnumerable<JournalLine>
    {
        public List<JournalLine> Lines { get; set; }

        #region Implementation of IEnumerable
        IEnumerator IEnumerable.GetEnumerator()
        {
            return Lines.GetEnumerator();
        }
        public IEnumerator<JournalLine> GetEnumerator()
        {
            foreach (JournalLine jrnl in Lines)
                yield return jrnl;
        }


        #endregion
    }

    #region JournalLine Types

    // https://github.com/andydandy74/Journalysis/wiki/JournalLine-types

    public class JournalAddinEvent : JournalLine
    {
        public string MessageText { get; set; }
        public JournalAddinEvent(int number, string rawText, int block) : base(number, rawText, block, JournalLineType.JournalAddinEvent)
        {
        }
        public JournalAddinEvent(int number, string rawText, int block, string messageText) : base(number, rawText, block, JournalLineType.JournalAddinEvent)
        {
            MessageText = messageText;
        }
        public JournalAddinEvent()
        {
            Type = JournalLineType.JournalAddinEvent;
        }
    }
    public class JournalAPIMessage : JournalLine
    {
        public bool IsError { get; set; }
        public string MessageText { get; set; }
        public string MessageType { get; set; }
        public JournalAPIMessage(int number, string rawText, int block, bool isError, string messageText, string messageType)
           : base(number, rawText, block, JournalLineType.JournalAPIMessage)
        {
            IsError = isError;
            MessageText = messageText;
            MessageType = messageType;
        }
        public JournalAPIMessage(int number, string rawText, int block, bool isError)
           : base(number, rawText, block, JournalLineType.JournalAPIMessage)
        {
            IsError = isError;
        }
        public JournalAPIMessage()
        {
            Type = JournalLineType.JournalAPIMessage;
        }
    }
    public class JournalBasicFileInfo : JournalLine
    {
        public string Worksharing { get; set; }
        public string CentralModelPath { get; set; }
        public string LastSavePath { get; set; }
        public string Locale { get; set; }
        public string FileName { get; set; }
        public JournalBasicFileInfo(int number, string rawText, int block, string worksharing, string centralModelPath, string lastSavePath, string locale, string fileName)
            : base(number, rawText, block, JournalLineType.JournalBasicFileInfo)
        {
            Worksharing = worksharing;
            CentralModelPath = centralModelPath;
            LastSavePath = lastSavePath;
            Locale = locale;
            FileName = fileName;
        }
        public JournalBasicFileInfo(int number, string rawText, int block) : base(number, rawText, block, JournalLineType.JournalBasicFileInfo)
        {
        }
        public JournalBasicFileInfo()
        {
            Type = JournalLineType.JournalBasicFileInfo;
        }
    }
    public class JournalCommand : JournalLine
    {
        public string CommandType { get; set; }
        public string CommandDescription { get; set; }
        // By Journal Type and Property -> "CommandID" = "Value"
        public string CommandID { get; set; }
        public JournalCommand(int number, string rawText, int block, string commandType, string commandDescription, string commandID) 
            : base(number, rawText, block,JournalLineType.JournalCommand)
        {
            CommandType = commandType;
            CommandDescription = commandDescription;
            CommandID = commandID;
        }
        public JournalCommand(int number, string rawText, int block) : base(number, rawText, block, JournalLineType.JournalCommand)
        {
        }
        public JournalCommand() 
        {
            Type = JournalLineType.JournalCommand;
        }
    }
    public class JournalComment : JournalLine
    {
        public JournalComment(int number, string rawText, int block) : base(number, rawText, block, JournalLineType.JournalComment)
        {
        }
        public JournalComment()
        {
            Type = JournalLineType.JournalComment;
        }
    }
    public class JournalData : JournalLine
    {
        public string Key { get; set; }
        public IList<string> Values { get; set; }
        public JournalData(int number, string rawText, int block, string key, IList<string> values)
            : base(number, rawText, block, JournalLineType.JournalData)
        {
            Key = key;
            Values = values;
        }
        public JournalData(int number, string rawText, int block) : base(number, rawText, block, JournalLineType.JournalData)
        {
            Values = new List<string>();
        }
        public JournalData()
        {
            Values = new List<string>();
            Type = JournalLineType.JournalData;
        }
    }
    public class JournalDirective : JournalLine
    {
        public string Key { get; set; }
        public IList<string> Values { get; set; }
        public JournalDirective(int number, string rawText, int block, string key, IList<string> values)
            : base(number, rawText, block, JournalLineType.JournalDirective)
        {
            Key = key;
            Values = values;
        }
        public JournalDirective(int number, string rawText, int block) : base(number, rawText, block, JournalLineType.JournalDirective)
        {
            Values = new List<string>();
        }
        public JournalDirective()
        {
            Values = new List<string>();
            Type = JournalLineType.JournalDirective;
        }
    }
    public class JournalGUIResourceUsage : JournalLine
    {
        public int? Available { get; set; }
        public int? Used { get; set; }
        public string User { get; set; }
        public JournalGUIResourceUsage(int number, string rawText, int block, int available, int used, string user)
            : base(number, rawText, block, JournalLineType.JournalGUIResourceUsage)
        {
            Available = available;
            Used = used;
            User = user;
        }
        public JournalGUIResourceUsage(int number, string rawText, int block) : base(number, rawText, block, JournalLineType.JournalGUIResourceUsage)
        {
        }
        public JournalGUIResourceUsage()
        {
            Type = JournalLineType.JournalGUIResourceUsage;
        }
    }
    public class JournalKeyboardEvent : JournalLine
    {
        public string Key { get; set; }
        public JournalKeyboardEvent(int number, string rawText, int block) : base(number, rawText, block, JournalLineType.JournalKeyboardEvent)
        {
        }
        public JournalKeyboardEvent(int number, string rawText, int block, string key) : base(number, rawText, block, JournalLineType.JournalKeyboardEvent)
        {
            Key = key;
        }
        public JournalKeyboardEvent()
        {
            Type = JournalLineType.JournalKeyboardEvent;
        }
    }
    public class JournalMemoryMetrics : JournalLine
    {
        public int VMAvailable { get; set; }
        public int VMUsed { get; set; }
        public int VMPeak { get; set; }
        public int RAMAvailable { get; set; }
        public int RAMUsed { get; set; }
        public int RAMPeak { get; set; }
        public JournalMemoryMetrics(int number, string rawText, int block, int vmAvailable, int vmUsed, int vmPeak, int ramAvailable, int ramUsed, int ramPeak)
            : base(number, rawText, block, JournalLineType.JournalMemoryMetrics)
        {
            VMAvailable = vmAvailable;
            VMUsed = vmUsed;
            VMPeak = vmPeak;
            RAMAvailable = ramAvailable;
            RAMUsed = ramUsed;
            RAMPeak = ramPeak;
        }
        public JournalMemoryMetrics(int number, string rawText, int block) : base(number, rawText, block, JournalLineType.JournalMemoryMetrics)
        {
        }
        public JournalMemoryMetrics()
        {
            Type = JournalLineType.JournalMemoryMetrics;
        }
    }    
    public class JournalMouseEvent : JournalLine
    {
        public string MouseEventType { get; set; }
        public IList<int> Data { get; set; }
        public JournalMouseEvent(int number, string rawText, int block, string mouseEventType, IList<int> data)
            : base(number, rawText, block, JournalLineType.JournalMouseEvent)
        {
            MouseEventType = mouseEventType;
            Data = data;
        }
        public JournalMouseEvent(int number, string rawText, int block) : base(number, rawText, block, JournalLineType.JournalMouseEvent)
        {
            Data = new List<int>();
        }
        public JournalMouseEvent()
        {
            Data = new List<int>();
            Type = JournalLineType.JournalMouseEvent;
        }
    }
    
    public class JournalSystemInformation : JournalLine
    {
        public string SystemInformationType { get; set; }
        public int ItemNumber { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public JournalSystemInformation(int number, string rawText, int block, string systemInformationType, int itemNumber, string key, string value)
            : base(number, rawText, block, JournalLineType.JournalSystemInformation)
        {
            SystemInformationType = systemInformationType;
            ItemNumber = itemNumber;
            Key = key;
            Value = value;
        }
        public JournalSystemInformation(int number, string rawText, int block, string systemInformationType)
            : base(number, rawText, block, JournalLineType.JournalSystemInformation)
        {
            SystemInformationType = systemInformationType;
        }
        public JournalSystemInformation(int number, string rawText, int block) : base(number, rawText, block, JournalLineType.JournalSystemInformation)
        {
        }
        public JournalSystemInformation()
        {
            Type = JournalLineType.JournalSystemInformation;
        }
    }
    public class JournalTimeStamp : JournalLine
    {
        public char TimeStampType { get; set; }
        public string Description { get; set; }
        public DateTime DateTime { get; set; }
        public JournalTimeStamp(int number, string rawText, int block, char timeStampType, string description, DateTime dateTime)
           : base(number, rawText, block, JournalLineType.JournalTimeStamp)
        {
            TimeStampType = timeStampType;
            Description = description;
            DateTime = dateTime;
        }
        public JournalTimeStamp(int number, string rawText, int block) : base(number, rawText, block, JournalLineType.JournalTimeStamp)
        {
        }
        public JournalTimeStamp()
        {
            Type = JournalLineType.JournalTimeStamp;
        }
    }
    public class JournalUIEvent : JournalLine
    {
        public string UIEventType { get; set; }
        public IList<string> Data { get; set; }
        public JournalUIEvent(int number, string rawText, int block, string uiEventType, IList<string> data)
            : base(number, rawText, block, JournalLineType.JournalUIEvent)
        {
            UIEventType = uiEventType;
            Data = data;
        }
        public JournalUIEvent(int number, string rawText, int block) : base(number, rawText, block, JournalLineType.JournalUIEvent)
        {
            Data = new List<string>();
        }
        public JournalUIEvent()
        {
            Data = new List<string>();
            Type = JournalLineType.JournalUIEvent;
        }
    }
    public class JournalWorksharingEvent : JournalLine
    {
        public string SessionID { get; set; }
        public DateTime DateTime { get; set; }
        public string Text { get; set; }
        public JournalWorksharingEvent(int number, string rawText, int block, string sessionID, DateTime dateTime, string text)
            : base(number, rawText, block, JournalLineType.JournalWorksharingEvent)
        {
            SessionID = sessionID;
            DateTime = dateTime;
            Text = text;
        }
        public JournalWorksharingEvent(int number, string rawText, int block) : base(number, rawText, block, JournalLineType.JournalWorksharingEvent)
        {
        }
        public JournalWorksharingEvent()
        {
            Type = JournalLineType.JournalWorksharingEvent;
        }
    }
    #endregion
}
