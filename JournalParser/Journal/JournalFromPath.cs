/*
 * Work from https://github.com/andydandy74
 * https://github.com/andydandy74/Journalysis
 * 
 * Adapted from Python to C# in order to use the the methods in revit addins and/or external programs
 *  
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;

using System.IO;

namespace ACPV.Utilities.JournalParser
{
    public class Utils
    {
        public Journal JournalFromPath(string journalPath)
        {
            DateTime startProcessing = DateTime.Now;
            IList<JournalLine> journalLines = new List<JournalLine>();
            int commandCount = 0;
            int jBlockCount = 0;
            int jVersion = 0;
            string jMachineName = "";
            string jUsername = "";
            string jOSVersion = "";
            string jPath = "";
            string jRelease = "";
            string jBuild = "";
            string jBranch = "";
            string jBIMBunnySessionId = "";
            int sysinfoItem;
            bool sysinfoStarted = false;
            string sysinfoType = "";
            var i = 1;
            var b = 0;
            try
            {
                IList<string> rawLines = new List<string>();

                using (FileStream fs = File.Open(journalPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                using (BufferedStream bs = new BufferedStream(fs))
                using (StreamReader sr = new StreamReader(bs))
                {
                    string s;
                    while ((s = sr.ReadLine()) != null)
                    {
                        rawLines.Add(s);
                    }
                }

                foreach (string line in rawLines)
                {

                   // Debug.WriteLine(i);
                    

                    var l = line.TrimStart().TrimEnd(Environment.NewLine.ToCharArray());
                    //Debug.WriteLine(l);
                    if (l.Length < 2)
                    {
                        continue;
                    }
                    else if (l.StartsWith("'C ") || l.StartsWith("'H ") || l.StartsWith("'E "))
                    {
                        // if it's a TimeStamp then blockcount goes up
                        b++;
                        journalLines.Add(new JournalTimeStamp(i, l, b));
                    }
                    else if (l.Contains(":< API_SUCCESS { "))
                    {
                        journalLines.Add(new JournalAPIMessage(i, l, b, false));
                    }
                    else if (l.Contains(":< API_SUCCESS { "))
                    {
                        journalLines.Add(new JournalAPIMessage(i, l, b, true));
                    }
                    else if (l.Contains(":: Delta VM: ") || l.StartsWith("' 0:< Initial VM: "))
                    {
                        journalLines.Add(new JournalMemoryMetrics(i, l, b));
                    }
                    else if (l.Contains(":< GUI Resource Usage GDI: "))
                    {
                        journalLines.Add(new JournalGUIResourceUsage(i, l, b));
                    }
                    else if (l.StartsWith("' [Jrn.BasicFileInfo]"))
                    {
                        journalLines.Add(new JournalBasicFileInfo(i, l, b));
                    }
                    else if (l.StartsWith("Jrn.Data "))
                    {
                        journalLines.Add(new JournalData(i, l, b));
                    }
                    else if (l.StartsWith("Jrn.Directive ")) journalLines.Add(new JournalDirective(i, l, b));
                    else if (l.StartsWith("Jrn.Command "))
                    {
                        journalLines.Add(new JournalCommand(i, l, b));
                        // We need to count commands so we can grab JournalSystemInformation lines
                        if (commandCount < 2) commandCount++;
                    }
                    else if (l.StartsWith("Jrn.Key ")) journalLines.Add(new JournalKeyboardEvent(i, l, b));
                    else if (l.StartsWith("Jrn.AddInEvent ")) journalLines.Add(new JournalAddinEvent(i, l, b));
                    else if (l.StartsWith("Jrn.Wheel ") || l.StartsWith("Jrn.MouseMove") || l.StartsWith("Jrn.LButtonUp") ||
                        l.StartsWith("Jrn.LButtonDown") || l.StartsWith("Jrn.LButtonDblClk") || l.StartsWith("Jrn.MButtonUp") ||
                        l.StartsWith("Jrn.MButtonDown") || l.StartsWith("Jrn.MButtonDblClk") || l.StartsWith("Jrn.RButtonUp") ||
                        l.StartsWith("Jrn.RButtonDown") || l.StartsWith("Jrn.RButtonDblClk") || l.StartsWith("Jrn.Scroll")) journalLines.Add(new JournalMouseEvent(i, l, b));
                    else if (l.StartsWith("Jrn.Activate") || l.StartsWith("Jrn.AppButtonEvent") || l.StartsWith("Jrn.Browser") ||
                        l.StartsWith("Jrn.CheckBox") || l.StartsWith("Jrn.Close") || l.StartsWith("Jrn.ComboBox") ||
                        l.StartsWith("Jrn.DropFiles") || l.StartsWith("Jrn.Edit") || l.StartsWith("Jrn.Grid") ||
                        l.StartsWith("Jrn.ListBox") || l.StartsWith("Jrn.Maximize") || l.StartsWith("Jrn.Minimize") ||
                        l.StartsWith("Jrn.PropertiesPalette") || l.StartsWith("Jrn.PushButton") || l.StartsWith("Jrn.RadioButton") ||
                        l.StartsWith("Jrn.RibbonEvent") || l.StartsWith("Jrn.SBTrayAction") || l.StartsWith("Jrn.Size") ||
                        l.StartsWith("Jrn.SliderCtrl") || l.StartsWith("Jrn.TabCtrl") || l.StartsWith("Jrn.TreeCtrl") ||
                        l.StartsWith("Jrn.WidgetEvent")) journalLines.Add(new JournalUIEvent(i, l, b));
                    else if (l.Contains(":< SLOG $")) journalLines.Add(new JournalWorksharingEvent(i, l, b));
                    // append linebreaks to previous line
                    else if (journalLines[i - 2].RawText.Last() == '_')
                    {
                        journalLines[i - 2].RawText = journalLines[i - 2].RawText.TrimEnd('_') + l;
                        //Debug.WriteLine(" || " + journalLines[i - 2].Number + " TYPE: " + journalLines[i - 2].Type + " RAW: " + journalLines[i - 2].RawText);
                        // non deve aggiungere una nuova riga, continua su quella precedente
                        continue;
                    }
                    // append linebreaks in commands
                    else if (l[0] == ',') journalLines[i - 2].RawText = (journalLines[i - 2].RawText + l).Replace("_,", ",");
                    else if (l[0] == '\'')
                    {
                        //Debug.WriteLine(l);
                        // append linebreaks in API Messages
                        //Debug.WriteLine("check");
                        //Debug.WriteLine(journalLines[i - 2].Type);
                        if (l[1] != ' ' && journalLines[i - 2].Type == JournalLineType.JournalAPIMessage && !(journalLines[i - 2].RawText.EndsWith("}")))
                        {
                            journalLines[i - 2].RawText = journalLines[i - 2].RawText + " " + l.Substring(1);
                            //Debug.WriteLine(" || " + journalLines[i - 2].Number + " TYPE: " + journalLines[i - 2].Type + " RAW: " + journalLines[i - 2].RawText);
                            continue;
                        }
                        else if (commandCount == 1 && sysinfoStarted)
                        {
                            if (l.Contains(":< PROCESSOR INFORMATION:"))
                            {
                                sysinfoType = "Processor";
                                journalLines.Add(new JournalComment(i, l, b));
                            }
                            else if (l.Contains(":< VIDEO CONTROLLER INFORMATION:"))
                            {
                                sysinfoType = "VideoController";
                                journalLines.Add(new JournalComment(i, l, b));
                            }
                            else if (l.Contains(":< PRINTER INFORMATION:"))
                            {
                                sysinfoType = "Printer";
                                journalLines.Add(new JournalComment(i, l, b));
                            }
                            else if (l.Contains(":< PRINTER CONFIGURATION INFORMATION:"))
                            {
                                sysinfoType = "PrinterConfiguration";
                                journalLines.Add(new JournalComment(i, l, b));
                            }
                            else if (l.Contains(" INFORMATION:"))
                            {
                                sysinfoType = "Unknown";
                                journalLines.Add(new JournalComment(i, l, b));
                            }
                            else if (l.Contains(":<    "))
                            {
                                string[] stringSeparator = new string[] { ":<    " };
                                if (l.Split(stringSeparator, StringSplitOptions.None).Last().StartsWith(" ")) journalLines.Add(new JournalComment(i, l, b));
                                else journalLines.Add(new JournalSystemInformation(i, l, b, sysinfoType));
                            }
                            else journalLines.Add(new JournalComment(i, l, b));
                        }
                        else
                        {
                            if (!sysinfoStarted)
                            {
                                if (l.Contains(":< OPERATING SYSTEM INFORMATION:"))
                                {
                                    sysinfoStarted = true;
                                    sysinfoType = "OperatingSystem";
                                }
                            }

                            journalLines.Add(new JournalComment(i, l, b));
                        }
                    }
                    else
                    {
                        journalLines.Add(new JournalLine(i, l, b, JournalLineType.JournalMiscCommand));
                    }

                    //Debug.WriteLine(" || " + journalLines[i - 1].Number + " TYPE: " + journalLines[i - 1].Type + " RAW: " + journalLines[i - 1].RawText);

                    i++;
                }
                jBlockCount = b;

                // Round 2: process raw multiline text and fill type-specific attributes
                bool machineNameFound = false;
                bool osVersionFound = false;
                sysinfoItem = 0;

                foreach (JournalLine jrnl in journalLines)
                {
                    if (jrnl.Type == JournalLineType.JournalAPIMessage)
                    {
                        Regex r = new Regex(@"\{ (?<message>.*?)\ }");
                        MatchCollection matchCollection = r.Matches(jrnl.RawText);
                        //Debug.WriteLine(jrnl.RawText.Split('{')[1].Split('}')[0].Trim());
                        if (matchCollection.Count > 0)
                        {
                            foreach (Match m in matchCollection)
                            {
                                GroupCollection groupCollection = m.Groups;
                                (jrnl as JournalAPIMessage).MessageText = groupCollection["message"].Value; 
                            }
                        }
                        else
                        {
                            (jrnl as JournalAPIMessage).MessageText = jrnl.RawText.Split('{')[1].Split('}')[0].Trim();
                        }
                        if ((jrnl as JournalAPIMessage).MessageText.StartsWith("Registered an external service")) (jrnl as JournalAPIMessage).MessageType = "RegisteredExternalService";
                        else if ((jrnl as JournalAPIMessage).MessageText.StartsWith("Registered an external server") || (jrnl as JournalAPIMessage).MessageText.StartsWith("An external server has been registered")) (jrnl as JournalAPIMessage).MessageType = "RegisteredExternalServer";
                        else if ((jrnl as JournalAPIMessage).MessageText.StartsWith("Starting External DB Application")) (jrnl as JournalAPIMessage).MessageType = "StartingExternalDBApp";
                        else if ((jrnl as JournalAPIMessage).MessageText.StartsWith("Starting External Application")) (jrnl as JournalAPIMessage).MessageType = "StartingExternalApp";
                        else if ((jrnl as JournalAPIMessage).MessageText.StartsWith("Registering")) (jrnl as JournalAPIMessage).MessageType = "RegisteringEvent";
                        else if ((jrnl as JournalAPIMessage).MessageText.StartsWith("Replacing command id")) (jrnl as JournalAPIMessage).MessageType = "ReplacingCommandID";
                        else if ((jrnl as JournalAPIMessage).MessageText.StartsWith("API registering command")) (jrnl as JournalAPIMessage).MessageType = "RegisteringCommandEvent";
                        else if ((jrnl as JournalAPIMessage).MessageText.StartsWith("Added pushbutton")) (jrnl as JournalAPIMessage).MessageType = "AddedPushbutton";
                        else if ((jrnl as JournalAPIMessage).MessageText.StartsWith("Unregistering")) (jrnl as JournalAPIMessage).MessageType = "UnregisteringEvent";
                        else if ((jrnl as JournalAPIMessage).MessageText.StartsWith("Restoring command id")) (jrnl as JournalAPIMessage).MessageType = "RestoringCommandID";
                        else if ((jrnl as JournalAPIMessage).MessageText.StartsWith("API unregistering command")) (jrnl as JournalAPIMessage).MessageType = "UnregisteringCommandEvent";
                        else if ((jrnl as JournalAPIMessage).MessageText.StartsWith("System.")) (jrnl as JournalAPIMessage).MessageType = "Exception";
                        else (jrnl as JournalAPIMessage).MessageType = "Unknown";
                    }
                    else if (jrnl.Type == JournalLineType.JournalDirective)
                    {
                        // Directive are these 4 cases
                        //
                        // Jrn.Directive "Version"  , "2019.000", "2.164"
                        // Jrn.Directive "U(char n° 15)sername"  , "capasso@citterio-viel.com"
                        // Jrn.Directive "CategoryDisciplineFilter" , 3
                        // Jrn.Directive "TabDisplayOptions"  , "StayOnModifyInProject", 0
                        //
                        string[] complexLine = new string[] { "\"  , " };
                        var d1 = jrnl.RawText.Split(complexLine, StringSplitOptions.None);
                        (jrnl as JournalDirective).Key = d1[0].Substring(15);
                        foreach (string d2 in d1[1].Split(','))
                        {
                            (jrnl as JournalDirective).Values.Add(d2.Trim().Replace('"', ' ').Trim());
                        }
                        // Add Revit version to journal metadata
                        if ((jrnl as JournalDirective).Key == "Version") jVersion = Int32.Parse((jrnl as JournalDirective).Values[0].Substring(0, 4));
                        // Add username to journal metadata
                        else if ((jrnl as JournalDirective).Key == "Username") jUsername = (jrnl as JournalDirective).Values[0];
                    }
                    else if (jrnl.Type == JournalLineType.JournalData)
                    {
                        string[] complexLine = new string[] { "\"  , " };
                        var d1 = jrnl.RawText.Split(complexLine, StringSplitOptions.None);
                        (jrnl as JournalData).Key = d1[0].Substring(10);
                        //Debug.WriteLine(" >> " + (jrnl as JournalData).Key);
                        foreach (string d2 in d1[1].Split(','))
                        {
                           // Debug.WriteLine(" >> " + d2.Trim().Replace('"', ' ').Trim());
                            (jrnl as JournalData).Values.Add(d2.Trim().Replace('"', ' ').Trim());
                        }
                    }
                    else if (jrnl.Type == JournalLineType.JournalWorksharingEvent)
                    {
                        string[] complexLine = new string[] { ":< SLOG " };
                        var ws = jrnl.RawText.Split(complexLine, StringSplitOptions.None).Last().Split(null as char[], StringSplitOptions.RemoveEmptyEntries);
                        (jrnl as JournalWorksharingEvent).SessionID = ws[0];
                        (jrnl as JournalWorksharingEvent).DateTime = DateTime.ParseExact(ws[1] + " " + ws[2], "yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
                        (jrnl as JournalWorksharingEvent).Text = String.Join(" ", ws.Skip(3).ToArray());
                    }
                    else if (jrnl.Type == JournalLineType.JournalSystemInformation)
                    {
                        string[] c1 = new string[] { ":<    " };
                        string[] c2 = new string[] { " : " };
                        var si = jrnl.RawText.Split(c1, StringSplitOptions.None).Last().Split(c2, StringSplitOptions.None);
                        (jrnl as JournalSystemInformation).Key = si[0];
                        if (si[0].Length > 1) (jrnl as JournalSystemInformation).Value = si[1];
                        else (jrnl as JournalSystemInformation).Value = "None";
                        if ((jrnl as JournalSystemInformation).SystemInformationType == "Processor" && (jrnl as JournalSystemInformation).Key == "AddressWidth") sysinfoItem++;
                        if ((jrnl as JournalSystemInformation).SystemInformationType == "VideoController" && (jrnl as JournalSystemInformation).Key == "AdapterCompatibility") sysinfoItem++;
                        if ((jrnl as JournalSystemInformation).SystemInformationType == "Printer" && (jrnl as JournalSystemInformation).Key == "Caption") sysinfoItem++;
                        if ((jrnl as JournalSystemInformation).SystemInformationType == "PrinterConfiguration" && (jrnl as JournalSystemInformation).Key == "Color") sysinfoItem++;
                        if (!osVersionFound)
                        {
                            if (jrnl.RawText.Contains("Caption :"))
                            {
                                jOSVersion = jrnl.RawText.Split(':').Last().Trim();
                                osVersionFound = true;
                            }
                        }
                        (jrnl as JournalSystemInformation).ItemNumber = sysinfoItem;
                    }
                    //  Jrn.Command "Internal" , "Cancel the current operation , ID_CANCEL_EDITOR" 
                    else if (jrnl.Type == JournalLineType.JournalCommand)
                    {
                        Regex r = new Regex(@"\""(.*?)\"" , \""(.*?)\""");
                        MatchCollection matchCollection = r.Matches(jrnl.RawText);
                        foreach (Match m in matchCollection)
                        {
                            GroupCollection groupCollection = m.Groups;
                            //Debug.WriteLine(groupCollection[2]);
                            (jrnl as JournalCommand).CommandType = groupCollection[1].Value;
                            (jrnl as JournalCommand).CommandDescription = groupCollection[2].Value.Split(',')[0];
                            (jrnl as JournalCommand).CommandID = groupCollection[2].Value.Split(',')[1].Trim();
                        }
                    }
                    else if (jrnl.Type == JournalLineType.JournalMouseEvent)
                    {
                        char[] separator = { ' ' };
                        var m1 = jrnl.RawText.Split(separator, 2);
                        (jrnl as JournalMouseEvent).MouseEventType = m1[0].Substring(4).Trim();
                        foreach (string m2 in m1[1].Split(','))
                        {
                            (jrnl as JournalMouseEvent).Data.Add(Int32.Parse(m2.Trim()));
                        }
                    }
                    else if (jrnl.Type == JournalLineType.JournalKeyboardEvent) (jrnl as JournalKeyboardEvent).Key = jrnl.RawText.Split('"')[1];
                    else if (jrnl.Type == JournalLineType.JournalBasicFileInfo)
                    {
                        /*
                                     elif line.Type == 'JournalBasicFileInfo':
                                         bfi = map(list, zip(*[x.split(":",1) for x in line.RawText[22:].split("Rvt.Attr.")[1:]]))
                                         bfidict = dict(zip(bfi[0], [x.strip() for x in bfi[1]]))
                                         if bfidict["Worksharing"] != "": line.Worksharing = bfidict["Worksharing"]
                                         if bfidict["CentralModelPath"] != "": line.CentralModelPath = bfidict["CentralModelPath"]
                                         if bfidict["LastSavePath"] != "": 
                                             line.LastSavePath = bfidict["LastSavePath"]
                                             line.FileName = bfidict["LastSavePath"].split("\\")[-1]
                                         if bfidict["LocaleWhenSaved"] != "": line.Locale = bfidict["LocaleWhenSaved"]
                         */
                    }
                    else if (jrnl.Type == JournalLineType.JournalGUIResourceUsage)
                    {
                        List<object> g2 = new List<object>();
                        foreach (string g1 in jrnl.RawText.Split(','))
                        {
                            g2.Add(Int32.Parse(g1.Trim().Split(null as char[], StringSplitOptions.RemoveEmptyEntries).Last()));
                        }
                        (jrnl as JournalGUIResourceUsage).Available = g2[0] as int?;
                        (jrnl as JournalGUIResourceUsage).Used = g2[1] as int?;
                        (jrnl as JournalGUIResourceUsage).User = g2[2] as string;
                    }
                    else if (jrnl.Type == JournalLineType.JournalUIEvent)
                    {
                        char[] separator = { ' ' };
                        var d1 = jrnl.RawText.Split(separator, 2);
                        (jrnl as JournalUIEvent).UIEventType = d1[0].Substring(4);
                        // if (!(jrnl as JournalUIEvent).UIEventType.Contains("Maximize") || !(jrnl as JournalUIEvent).UIEventType.Contains("Minimize") || !(jrnl as JournalUIEvent).UIEventType.Contains("Restore"))
                        string[] chk1 = { "Maximize", "Minimize", "Restore" };
                        if (!chk1.Any(s => s.Contains((jrnl as JournalUIEvent).UIEventType)))
                        {
                            
                            foreach (string d2 in d1[1].Split(','))
                            {
                                var d3 = d2.Trim().Replace('"', ' ').Trim();
                                Debug.WriteLine(" >> " + d3);
                                string[] chk2 = { "RibbonEvent", "SBTrayAction" };
                                if (chk2.Any(s => s.Contains((jrnl as JournalUIEvent).UIEventType)))
                                {
                                    foreach (string d4 in d3.Split(':'))
                                    {
                                        if (d4.Trim() != "") (jrnl as JournalUIEvent).Data.Add(d4);
                                    }
                                }
                                else if ((jrnl as JournalUIEvent).UIEventType == "Browser")
                                {
                                    string[] sep = { ">>" };
                                    foreach (string d4 in d3.Split(sep, StringSplitOptions.None))
                                    {
                                        if (d4.Trim() != "") (jrnl as JournalUIEvent).Data.Add(d4);
                                    }
                                }
                                else if (d3 != "") (jrnl as JournalUIEvent).Data.Add(d3);
                            }
                        }
                    }
                    else if (jrnl.Type == JournalLineType.JournalAddinEvent) (jrnl as JournalAddinEvent).MessageText = jrnl.RawText.Split('"')[3];
                    else if (jrnl.Type == JournalLineType.JournalTimeStamp)
                    {
                        (jrnl as JournalTimeStamp).TimeStampType = jrnl.RawText[1];
                        var tsl = jrnl.RawText.Split(';');
                        (jrnl as JournalTimeStamp).DateTime = (DateTime.Parse(tsl[0].Substring(3)));
                        (jrnl as JournalTimeStamp).Description = tsl[1].Substring(7).Trim();
                    }
                    else if (jrnl.Type == JournalLineType.JournalMemoryMetrics)
                    {
                        string[] m1 = new string[50];
                        char[] separator = { ':' };
                        // ' 0:< Initial VM: Avail 134213236 MB, Used 17 MB, Peak 17; RAM: Avail 46318 MB, Used 60 MB, Peak 17 
                        if (jrnl.RawText.Contains("Initial VM"))
                        {
                            m1 = jrnl.RawText.Split(separator, 3)[2].Replace(';', ' ').Split(null as char[], StringSplitOptions.RemoveEmptyEntries);
                        }
                        // ' 0:< ::0:: Delta VM: Avail -641 -> 134212376 MB, Used +24 -> 79 MB, Peak +22 -> 79 MB; RAM: Avail -26 -> 46166 MB, Used +35 -> 160 MB, Peak +35 -> 160 MB 
                        else
                        {
                            m1 = jrnl.RawText.Split(separator, 7)[6].Split(null as char[], StringSplitOptions.RemoveEmptyEntries);
                        }
                        List<int> m3 = new List<int>();
                        List<string> m4 = new List<string>();
                        string[] chkmm = { "Avail", "Used", "Peak" };
                        foreach (string m2 in m1)
                        {
                            if (int.TryParse(m2, out int n)) m3.Add(n);
                            else if (!chkmm.Any(s => s.Contains(m2))) m4.Add(m2);
                        }
                        (jrnl as JournalMemoryMetrics).VMAvailable = m3[0];
                        (jrnl as JournalMemoryMetrics).VMUsed = m3[1];
                        if (m3.Count() == 6)
                        {
                            (jrnl as JournalMemoryMetrics).VMPeak = m3[2];
                            (jrnl as JournalMemoryMetrics).RAMAvailable = m3[3];
                            (jrnl as JournalMemoryMetrics).RAMUsed = m3[4];
                            (jrnl as JournalMemoryMetrics).RAMPeak = m3[5];
                        }
                        else if (m3.Count() == 5)
                        {
                            if (m4[2] == "Avail")
                            {
                                (jrnl as JournalMemoryMetrics).VMPeak = 0;
                                (jrnl as JournalMemoryMetrics).RAMAvailable = m3[2];
                                (jrnl as JournalMemoryMetrics).RAMUsed = m3[3];
                                (jrnl as JournalMemoryMetrics).RAMPeak = m3[4];
                            }
                            else if (m4[2] == "Peak")
                            {
                                (jrnl as JournalMemoryMetrics).VMPeak = m3[2];
                                (jrnl as JournalMemoryMetrics).RAMAvailable = m3[3];
                                (jrnl as JournalMemoryMetrics).RAMUsed = m3[4];
                                (jrnl as JournalMemoryMetrics).RAMPeak = 0;
                            }
                        }
                        else if (m3.Count() == 4)
                        {
                            (jrnl as JournalMemoryMetrics).VMPeak = 0;
                            (jrnl as JournalMemoryMetrics).RAMAvailable = m3[2];
                            (jrnl as JournalMemoryMetrics).RAMUsed = m3[3];
                            (jrnl as JournalMemoryMetrics).RAMPeak = 0;
                        }
                    }
                    else if (jrnl.Type == JournalLineType.JournalComment)
                    {
                        if (jrnl.RawText.Contains("this journal =")) jPath = jrnl.RawText.Split('=')[1].Trim();
                        else if (jrnl.RawText.StartsWith("' Build:")) jBuild = jrnl.RawText.Split(':')[1].Trim();
                        else if (jrnl.RawText.StartsWith("' Branch:")) jBranch = jrnl.RawText.Split(':')[1].Trim();
                        else if (jrnl.RawText.StartsWith("' Release:")) jRelease = jrnl.RawText.Split(':')[1].Trim();
                        else if (jrnl.RawText.Contains("BIMBunny")) jBIMBunnySessionId = jrnl.RawText.Split('{')[1].Trim().TrimEnd('}');
                        else if (machineNameFound)
                        {
                            if (jrnl.RawText.Contains("Additional IP address/name found for host"))
                            {
                                jMachineName = jrnl.RawText.Split(':')[1].Split(null as char[], StringSplitOptions.RemoveEmptyEntries).Last();
                                machineNameFound = true;
                            }
                        }
                    }
                    //Debug.WriteLine(" || " + jrnl.Number + " TYPE: " + jrnl.Type + " RAW: " + jrnl.RawText);
                }
                Journal journal = new Journal(journalLines, jVersion,jUsername,jBlockCount,jPath,jBuild,jBranch,jMachineName,jOSVersion, jBIMBunnySessionId);
                foreach (JournalLine j in journal.JournalLines)
                {
                    j.Journal = journal;
                }

                // Compute total processing time of this node
                journal.ProcessingTime = DateTime.Now - startProcessing;
                return journal;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                Debug.WriteLine(ex.Message + " " + ex.Source + " " + ex.InnerException + " " + ex.TargetSite);
                return null;
            }
        }
    }
}
