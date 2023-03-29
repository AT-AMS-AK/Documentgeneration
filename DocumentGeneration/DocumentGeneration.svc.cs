using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using Addit.AK.WBD.DAL;
using Addit.AK.Util;
using System.Data;

using DocumentGeneration.AuthService;

using DocumentGeneration.LoggingService;

using LogResp = DocumentGeneration.LoggingService.Response;

using LogSource = DocumentGeneration.LoggingService.Source;

using AuthResponse = DocumentGeneration.AuthService.Response;

using System.IO;
using System.Diagnostics;
using Microsoft.Office.Interop.Word;
using System.Security.Principal;


namespace Addit.AK.WBD.DocumentGeneration
{
    /// <summary>
    /// Author: Bruno Hautzenberger
    /// Creation Date: 12.2010
    /// Implements functions to generate Documents and print them
    /// </summary>
    public class DocumentGeneration : IDocumentGeneration
    {
        #region private members

        private bool saveWindowClosed=false;



        private string printable;

        /// <summary>
        /// object for loading templates and converting them to xslt
        /// </summary>
        private TemplateLibrary templateLibrary;

        /// <summary>
        /// directory for temporary files
        /// </summary>
        private string tempDir = System.Web.Configuration.WebConfigurationManager.AppSettings.Get("tempDir");
        private string gemeinsamMit = System.Web.Configuration.WebConfigurationManager.AppSettings.Get("GemeinsamMit");

        /// <summary>
        /// oracle data access object
        /// </summary>
        private static DAL_Oracle dal;

        /// <summary>
        /// Encryption Key for signatures
        /// </summary>
        private static string sigKey = "myS1gn4tur3K3y!!!!";

        /// <summary>
        /// encryption key for config values
        /// </summary>
        private static string cryptoKey = "8h15Tw45d3RFr3ddyS46t1chF1nD3J442b3sS3r3st4ttE4t4u5chtM1tAS0nd3RZ31Ch3nJ3tztN0ch!$!((sup1K3y)";

        #endregion

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// initializes a new DAL_Oracle Object if needed
        /// </summary>
        /// <returns>a DAL_Oracle object that is connected to a DB</returns>
        private DAL_Oracle getDAL()
        {
            if (dal == null)
            {
                dal = DAL_Oracle.getInstance();
                dal.Connect(Encryptor.DecryptString(System.Web.Configuration.WebConfigurationManager.AppSettings.Get("ConnectionString"), cryptoKey), Int32.Parse(System.Web.Configuration.WebConfigurationManager.AppSettings.Get("DBConnectionPoolsize")));
            }

            return dal;
        }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// initializes a new TemplateLibrary Object if needed
        /// </summary>
        /// <returns>an initialized TemplateLibrary object</returns> 
        private TemplateLibrary getTemplateLibrary()
        {
            if(templateLibrary == null)
            {
                string importDir = System.Web.Configuration.WebConfigurationManager.AppSettings.Get("importDir");
                string templateDir = System.Web.Configuration.WebConfigurationManager.AppSettings.Get("templateDir");
                string cacheFile = System.Web.Configuration.WebConfigurationManager.AppSettings.Get("cacheFile");

                templateLibrary = TemplateLibrary.getInstance();
                templateLibrary.loadLibrary(importDir, templateDir, cacheFile);
            }

            return templateLibrary;
        }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// tries to delete a file, if file is still in use or does not exist anymore it does not throw an exception. (not needed a this point, clean up should be done seperatly)
        /// </summary>
        /// <param name="path">path to file as String</param>
        private void tryDeleteFile(string path)
        {
            try
            {
                System.IO.File.Delete(path);
            }
            catch 
            {
                //logging
                log(LogType.WARNING, String.Format("Failed to delete File {0}", path), -1);
            }
        }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// generates and prints documents for each row in keyList, with template t and to given printer
        /// </summary>
        /// <param name="user">id of executing user</param>
        /// <param name="t">the template object for this print</param>
        /// <param name="printer">the printer as string</param>
        /// <param name="keyList">List of Dict TOKEN,VALUE (Values for each Document)</param>
        /// <returns>a Response Object</returns>
        private Response doPrint(string user, List<Template> templates, string printer, List<Dictionary<string, string>> keyList, Dictionary<string, string> signatures, string von, string bis, string ctrlList1, string ctrlList2)
        {
            Response resp = new Response();
            String datasourceFile;

            StreamWriter printDoc = null;




            //write the darlehens Nummern into a file, in case printing does not work...
            var darlehensNummern = new List<string>();
            foreach (Dictionary<string, string> keys in keyList)
            {
                var darlehenNummber = keys["WBD_DARLEHNR"];
                darlehensNummern.Add(darlehenNummber);
            }

            string darlehenPath = tempDir + "Darlehensnummern_" + DateTime.Now.ToString("yyyyMMddHHmmssffff") + ".txt";
            if (keyList.Count != 0) File.WriteAllLines(darlehenPath, darlehensNummern);






            //open word
            Microsoft.Office.Interop.Word.Application app = null;
            Microsoft.Office.Interop.Word.Document adoc = null;
            object missing = System.Reflection.Missing.Value;
           
            try 
            {
                app = new Microsoft.Office.Interop.Word.Application();
                app.DisplayAlerts = Microsoft.Office.Interop.Word.WdAlertLevel.wdAlertsNone;
               
                adoc = app.Documents.Add(ref missing, ref missing, ref missing, ref missing);
            } 
            catch(Exception ex) 
            {
                //printDoc.Close();
                resp.ResponseCode = 410;
                resp.ResponseMsg = "Fehler beim Starten von Word!";
                resp.ExeptionMsg = ex.Message;

                //logging
                resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrint: {0} - EX: {1}", resp.ResponseMsg, ex.Message),Int32.Parse(user)));

                return resp;
            }

            // copy markers for concat
            object start = 0;
            object end = 0; 


            //zeilen für kontrolliste
            List<string> signatedDocs = new List<string>();

            Random rand = new Random();

            //old pribnt doc
            //string printDocPath = tempDir + "printDoc" + DateTime.Now.Ticks.ToString() + rand.NextDouble().ToString() + ".rtf";

            ////generate new print doc
            //try
            //{
            //    System.Text.Encoding encOutput = null;
            //    encOutput = System.Text.Encoding.Default;
            //    printDoc = new StreamWriter(printDocPath, false, encOutput);
            //}
            //catch (Exception ex)
            //{
            //    resp.ResponseCode = 402;
            //    resp.ResponseMsg = "Fehler beim Erstellen des Print Dokuments!";
            //    resp.ExeptionMsg = ex.Message;

            //    //logging
            //    resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrint: {0} - EX: {1}", resp.ResponseMsg, ex.Message), Int32.Parse(user)));

            //    return resp;
            //}

           
            int last = keyList.Count - 1;




            for(int count = 0; count < keyList.Count; count++){


                //foreach(Dictionary<string, string> keys in keyList){



                Dictionary<string, string> keys = keyList[count];
                Template t = templates[count];

                //kill first \par of UPs
                //try
                //{
                //    int firstUPIndex = Int32.MaxValue;
                //    string keyToRemovePar = "";

                //    StreamReader tr = File.OpenText(t.path);
                //    string tmp = tr.ReadToEnd();
                //    tr.Close();

                //    foreach (string key in keys.Keys)
                //    {
                //        if (key.StartsWith("WBD_URGENZ") && keys[key].Length > 5)
                //        {
                //            int idx = tmp.IndexOf(key);
                //            if (idx < firstUPIndex)
                //            {
                //                firstUPIndex = idx;
                //                keyToRemovePar = key;
                //            }
                //        }
                //    }

                //    if (keyToRemovePar != "")
                //    {
                //        keys[keyToRemovePar] = keys[keyToRemovePar].Substring(5);
                //    }
                //}
                //catch (Exception ex)
                //{
                //    printDoc.Close();
                //    resp.ResponseCode = 401;
                //    resp.ResponseMsg = "Fehler beim Formatieren der Urgenzpunkte!";
                //    resp.ExeptionMsg = ex.Message;

                //    //logging
                //    resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrint: {0} - EX: {1}", resp.ResponseMsg, ex.Message), Int32.Parse(user)));

                //    return resp;
                //}

                try{
                    datasourceFile = DataProvider.generateDatasource(keys, tempDir);
                } 
                catch(Exception ex) 
                {
                    //printDoc.Close();
                    resp.ResponseCode = 401;
                    resp.ResponseMsg = "Fehler beim Erstellen der Datenquelle!";
                    resp.ExeptionMsg = ex.Message;

                    //logging
                    resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrint: {0} - EX: {1}", resp.ResponseMsg, ex.Message),Int32.Parse(user)));

                    return resp;
                }

                String docFile = String.Format("{0}genDok{1}{2}.rtf", tempDir, user,DateTime.Now.Ticks);

                try
                {
                    XSLProcessor.render(datasourceFile, t.path, docFile);
                }
                catch (Exception ex)
                {
                    //printDoc.Close();
                    resp.ResponseCode = 402;
                    resp.ResponseMsg = "Fehler beim Befüllen des Dokuments!";

                    //logging
                    resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrint: {0} - EX: {1}", resp.ResponseMsg, ex.Message),Int32.Parse(user)));

                    resp.ExeptionMsg = ex.Message;
                    return resp;
                }
                finally
                {
                    tryDeleteFile(datasourceFile); //clean up datasource
                }

                try
                {
                    StreamReader re = File.OpenText(docFile);

                    string content = re.ReadToEnd();

                    content = content.Trim();

                    //unterschriften prüfen und einfügen
                    bool sig = false;
                    foreach (string signature in signatures.Keys.ToArray<string>())
                    {
                        if (content.IndexOf(String.Format("[#{0}#]", signature)) > -1)
                        {
                            content = content.Replace(String.Format("[#{0}#]", signature), signatures[signature]);

                            //liste für kontrolliste
                            if (sig == false)
                            {
                                signatedDocs.Add(String.Format("{0} {1} {2} {3}", keys["WBD_DARLEHNR"].PadRight(17), keys["AST_ZUNAME"].PadRight(24), keys["AST_VORNAME"].PadRight(23), keys["WBD_D_BETR_R00"]));
                                sig = true;
                            }
                        }
                    }

                    re.Close();



                    //concat new
                    string printDocPath = tempDir + "printDoc" + rand.Next() + ".rtf";

                    //generate new print doc
                    try
                    {
                        System.Text.Encoding encOutput = null;
                        encOutput = System.Text.Encoding.Default;
                        printDoc = new StreamWriter(printDocPath, false, encOutput);
                        printDoc.Write(content);
                        printDoc.Close();
                    }
                    catch (Exception ex)
                    {
                        resp.ResponseCode = 402;
                        resp.ResponseMsg = "Fehler beim Erstellen des Print Dokuments!";
                        resp.ExeptionMsg = ex.Message;

                        //logging
                        resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrint: {0} - EX: {1}", resp.ResponseMsg, ex.Message), Int32.Parse(user)));

                        return resp;
                    }


                    try
                    {
                        //printOutWithWordApp(printDocPath, "");

                        // create a range object which starts at 0
                        Range rng = adoc.Range(ref start, ref missing);

                       

                        // insert a file
                        //log(LogType.ERROR, "Beforeprintdocpath: " + printDocPath,0);

                        adoc.Range().InsertFile(printDocPath); //MiRo
                       
                      //  rng.InsertFile(printDocPath, ref missing, ref missing, ref missing, ref missing);
                       // log(LogType.ERROR, "nachprinPath: " + printDocPath, 0);
                        // now make start to point to the end of the content of the first document

                        if (count < keyList.Count - 1)
                        {
                            start = app.ActiveDocument.Content.End - 1;
                            rng = adoc.Range(ref start, ref missing);
                            rng.InsertBreak(1);
                          //  log(LogType.ERROR, "nachprintpath_INIF: " + start.ToString(), 0);
                        }
               


                


                   
                        log(LogType.ERROR, String.Format("amEnde....printer: {0}.....path: {1} ", app.ActivePrinter, adoc.Path), 0);
                        //logging 
                   

                        start = app.ActiveDocument.Content.End - 1;
                    }
                    catch (Exception ex)
                    {
                        resp.ResponseCode = 402;
                        resp.ResponseMsg = "Fehler beim Anhängen des Print Dokuments! Darlehensnummern wurden in " + darlehenPath + " gespeichert" ;
                        resp.ExeptionMsg = ex.Message;

                        WindowsIdentity wi = WindowsIdentity.GetCurrent();

                        //logging 
                        resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrint: {0} - EX: {1} - File: {2} - WI: {3} - pnt: {4}", resp.ResponseMsg, ex.Message, printDocPath,wi.Name, app.ActivePrinter), Int32.Parse(user)));

                        return resp;
                    }
                    

                    ////////////////////////

                    //old concat
                    //if (count > 0)
                    //{
                    //    content = content.Substring(content.IndexOf(@"\pard") + 5);
                    //}

                    //if (count != last)
                    //{
                    //    content = content.Substring(0, content.Length - 1);
                    //}

                    //printDoc.WriteLine(content);

                    //if (count != last)
                    //{
                    //    printDoc.Write(@"\page");
                    //}

                    //re.Close();

                    tryDeleteFile(docFile);

                    //print neu
                    printOutWithWordApp(printDocPath, printer);

                    tryDeleteFile(printDocPath);
                }
                catch (Exception ex)
                {
                    try
                    {
                        printDoc.Close();
                    }
                    catch { }

                    resp.ResponseCode = 402;
                    resp.ResponseMsg = "Fehler beim Schreiben des Print Dokuments!";

                    //logging
                    resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrint: {0} - EX: {1}", resp.ResponseMsg, ex.Message),Int32.Parse(user)));

                    resp.ExeptionMsg = ex.Message;
                    return resp;
                }
            }

            //printDoc.Close();

            try
            {
                //printOutWithWord(app, printer);

                object f = false;
                app.Quit(ref f, ref missing, ref missing);
                //printOutWithWordPad(printDocPath, printer);
            }
            catch (Exception ex)
            {
                try
                {
                    app.Quit(ref missing, ref missing, ref missing);
                }
                catch { }

                resp.ResponseCode = 403;
                resp.ResponseMsg = "Fehler beim Drucken des Dokuments!";
                resp.ExeptionMsg = ex.Message;

                //logging
                resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrint: {0} - EX: {1}", resp.ResponseMsg, ex.Message), Int32.Parse(user)));

                return resp;
            }

            try
            {
                if (signatedDocs.Count > 0)
                {
                    generateControlList(signatedDocs, von, bis, ctrlList1, ctrlList2, printer);
                }
            }
            catch (Exception ex)
            {
                resp.ResponseCode = 410;
                resp.ResponseMsg = "Fehler beim Generieren der Kontrolliste!";
                resp.ExeptionMsg = ex.Message;

                //logging
                resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrint: {0} - EX: {1}", resp.ResponseMsg, ex.Message),Int32.Parse(user)));

                return resp;
            }

            resp.ResponseCode = 0;
            resp.ResponseMsg = "OK";
            return resp;
        }

        private void generateControlList(List<String> signatedDocs, string von, string bis, string ctrlList1, string ctrlList2, string printer)
        {
            //generate new print doc
            try
            {
                System.Text.Encoding encOutput = null;
                encOutput = System.Text.Encoding.Default;
                String path = tempDir + "ctrllist" + DateTime.Now.Ticks.ToString() + ".rtf";
                StreamWriter printDoc = new StreamWriter(path, false, encOutput);

                String content;

                if (signatedDocs.Count > 1)
                {
                    content = ctrlList1;
                }
                else
                {
                    content = ctrlList2;
                }

                try
                {
                    von = String.Format("{0}.{1}.{2}", von.Substring(6, 2), von.Substring(4, 2), von.Substring(0, 4));
                }
                catch
                {
                    von = "";
                }

                try
                {
                    bis = String.Format("{0}.{1}.{2}", bis.Substring(6, 2), bis.Substring(4, 2), bis.Substring(0, 4));
                }
                catch
                {
                    bis = "";
                }

                content = content.Replace("<BEREICH>", "WBD");
                content = content.Replace("<DATUM>", String.Format("{0}.{1}.{2}", DateTime.Now.Day,DateTime.Now.Month,DateTime.Now.Year));
                content = content.Replace("<VON>", von);
                content = content.Replace("<BIS>", bis);
                content = content.Replace("<COUNTER>", signatedDocs.Count.ToString());

                StringBuilder ctrlListString = new StringBuilder();
                for(int i = 0; i < signatedDocs.Count; i++)
                {
                    ctrlListString.Append(signatedDocs[i]);
                    ctrlListString.Append(@"\par");
                }

                content = content.Replace("<LIST>", ctrlListString.ToString());

                printDoc.WriteLine(content);

                printDoc.Close();

                printOutWithWordApp(path, printer);
                //printOutWithWordPad(path, printer);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 03.2011
        /// Prints a RTF with Worpad
        /// </summary>
        /// <param name="printDocPath">path of rtf to printn</param>
        /// <param name="user">printers system name</param> revi
        private void printOutWithWordPad(string printDocPath, string printer)
        {
            String command = System.Web.Configuration.WebConfigurationManager.AppSettings.Get("printingApp");
            System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(command);

            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.UseShellExecute = false;

            procStartInfo.Arguments = String.Format("/pt {0} {1}", printDocPath, printer);

            procStartInfo.CreateNoWindow = true;

            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = procStartInfo;
            proc.Start();
        }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 03.2011
        /// Prints a RTF with MS Word 2010
        /// </summary>
        /// <param name="printDocPath">path of rtf to printn</param>
        /// <param name="user">printers system name</param>
        private void printOutWithWord(Microsoft.Office.Interop.Word.Application app, string printer)
        {
            // Create an Application object
            //Microsoft.Office.Interop.Word.Application app = new Microsoft.Office.Interop.Word.Application();

            //app.DisplayAlerts = Microsoft.Office.Interop.Word.WdAlertLevel.wdAlertsNone;

            // Open the document to print...
            //object filename = printDocPath;
            object missingValue = System.Reflection.Missing.Value;

            //// Using OpenOld so as to be compatible with other versions of Word
            //Microsoft.Office.Interop.Word.Document document = app.Documents.OpenOld(ref filename,
            //ref missingValue, ref missingValue,
            //ref missingValue, ref missingValue, ref missingValue,
            //    ref missingValue, ref missingValue, ref missingValue, ref missingValue);

            // Set the active printer
            app.ActivePrinter = printer;

            object myTrue = true; // Print in background
            object myFalse = false;

            // Using PrintOutOld to be version independent
            app.ActiveDocument.PrintOutOld(ref myTrue,
            ref myFalse, ref missingValue, ref missingValue, ref missingValue,
                missingValue, ref missingValue,
            ref missingValue, ref missingValue, ref missingValue, ref myFalse,
                ref missingValue, ref missingValue);

            // Make sure all of the documents are gone from the queue
            while (app.BackgroundPrintingStatus > 0)
            {
                System.Threading.Thread.Sleep(250);
            }

            //document.Close(ref missingValue, ref missingValue, ref missingValue);

            //app.Quit(ref missingValue, ref missingValue, ref missingValue);

            //tryDeleteFile(printDocPath);
        }


       

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 03.2011
        /// Prints a RTF with MS Word 2010
        /// </summary>
        /// <param name="printDocPath">path of rtf to printn</param>
        /// <param name="user">printers system name</param>
        private void printOutWithWordApp(string printDocPath, string printer)
        {
            Microsoft.Office.Interop.Word.Application app = null;
            Microsoft.Office.Interop.Word.Document document = null;

            // Open the document to print...
            object filename = printDocPath;
            object missingValue = System.Reflection.Missing.Value;

            object myTrue = true; // Print in background
            object myFalse = false;

            try
            {
                //Create an Application object
                app = new Microsoft.Office.Interop.Word.Application();

                app.DisplayAlerts = Microsoft.Office.Interop.Word.WdAlertLevel.wdAlertsNone;

                // Using OpenOld so as to be compatible with other versions of Word
                document = app.Documents.OpenOld(ref filename,
                ref missingValue, ref missingValue,
                ref missingValue, ref missingValue, ref missingValue,
                    ref missingValue, ref missingValue, ref missingValue, ref missingValue);




                // Set the active printer
                log(LogType.INFO, String.Format("WORD ACTIVE PRINTER: {0} NEW PRINTER {1}", app.ActivePrinter, printer), -1);

                if (printer.Trim() != app.ActivePrinter.Trim())
                {
                    app.ActivePrinter = printer.Trim();
                }

                log(LogType.INFO, String.Format("WORD NEW PRINTER: {0}", app.ActivePrinter), -1);

                log(LogType.INFO, String.Format("PRINTING DOCUMENT: {0}", printDocPath), -1);

                // Using PrintOutOld to be version independent
                app.ActiveDocument.PrintOutOld(ref myTrue,
                ref myFalse, ref missingValue, ref missingValue, ref missingValue,
                    missingValue, ref missingValue,
                ref missingValue, ref missingValue, ref missingValue, ref myFalse,
                    ref missingValue, ref missingValue);

                // Make sure all of the documents are gone from the queue
                while (app.BackgroundPrintingStatus > 0)
                {
                    System.Threading.Thread.Sleep(250);
                }

               
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                try
                {
                    document.Close(ref myFalse, ref missingValue, ref missingValue);
                }
                catch { }

                try {
                    object f = false;
                    app.Quit(ref f, ref missingValue, ref missingValue);
                }
                catch { }

                tryDeleteFile(printDocPath);
            }
        }

        private ApplicationEvents4_DocumentBeforeCloseEventHandler userClosed()
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// Print Method called from Service Client
        /// </summary>
        /// <param name="sessionToken">the session token of this user session</param>
        /// <param name="user">user by id</param>
        /// <param name="template">template by id</param>
        /// <param name="bereich">Bereich by Id</param>
        /// <param name="Bezirk">Bezirk by Id</param>
        /// <param name="status">status byId</param>
        /// <param name="printer">the printer as string</param>
        /// <param name="ablehnung">J/N</param>
        /// <param name="sort">0,1</param>
        /// <param name="von">von as String yyyymmdd</param>
        /// <param name="bis">bis as String yyyymmdd</param>
        /// <param name="ant_ikey">ant_ikey as string</param>
        /// <returns>a Response Object</returns>
        public Response doPrint(string sessionToken, string user, string template, string printer, string bereich, 
            string bezirk, string status, string ablehnung, string sort, string von, string bis, string ant_ikey, int wbd_bdl)
        {
            Response resp = new Response();

            #region AUTHENTICATION

            //AUTHENTICATION
            AuthServiceClient authService = new AuthServiceClient();
            User serviceUser = new User();
            AuthResponse authResponse = authService.getUser(out serviceUser, sessionToken);

            if (authResponse.ResponseCode != 0) //something is wrong with this token
            {
                resp.ResponseCode = authResponse.ResponseCode;
                resp.ResponseMsg = authResponse.ResponseMsg;
                return resp;
            }

            if (!serviceUser.CanRead && !serviceUser.CanWrite) //this method needs read permission!
            {
                resp.ResponseCode = 500;
                resp.ResponseMsg = "Permission denied!";
                return resp;
            }
            //END AUTHENTICATION

            #endregion

            String ctrlList1 = "";
            String ctrlList2 = "";

            #region signatures

            //load all signatures
            Dictionary<string, string> signatures = new Dictionary<string, string>(); 
            try
            {
                DirectoryInfo di = new DirectoryInfo(System.Web.Configuration.WebConfigurationManager.AppSettings.Get("SignatureDirectory"));
                FileInfo[] sigFiles = di.GetFiles("*.dat");

                foreach (FileInfo fi in sigFiles)
                {
                    string signature = Encryptor.DecryptString(DataProvider.loadSignatureContent(fi), sigKey);
                    signatures[fi.Name.Substring(0, fi.Name.Length - 4)] = signature;
                }
            }
            catch (Exception ex)
            {
                resp.ResponseCode = 407;
                resp.ResponseMsg = String.Format("Fehler beim Laden der Unterschriften!");
                resp.ExeptionMsg = ex.Message;
                return resp;
            }

            #endregion

            //all tokens
            List<string> tokens = new List<string>();

            //add standard token
            // KJ 23-02-2016 Serientermine
            tokens.Add("ALG_BEZST_PLZ");
            // HB
            tokens.Add("WBD_DARLEHNR");
            tokens.Add("AST_ZUNAME");
            tokens.Add("AST_VORNAME");
            tokens.Add("WBD_D_BETR_R00");


            //all posible tamplates
            Dictionary<string,Template> templates = new Dictionary<string,Template>();

            //load all possible templates
            try
            {

           

             
                Dictionary<string,string> templateNames = DataProvider.getPossibleTemplateNames(getDAL(), template);
             
                if (templateNames.LastOrDefault(x => x.Key == "printable").Value.ToString() != String.Empty)
                {
                    printable = templateNames.LastOrDefault(x => x.Key == "printable").Value.ToString();

     
                }


                
                foreach (String tKey in templateNames.Keys)
                {

                  //  log(LogType.ERROR, "tkey" + tKey, serviceUser.UserId);
                    if (templateNames[tKey].Trim().Length > 0 && !templateNames[tKey].ToString().Equals("nope"))
                    {
                        Template possibleT = getTemplateLibrary().getTemplate(templateNames[tKey]);
                        templates[tKey] = possibleT;
                        tokens.AddRange(possibleT.datafileds);


              
                      

                    }
                }
            }
            catch (Exception ex)
            {
                resp.ResponseCode = 404;
                resp.ResponseMsg = String.Format("Fehler beim Laden der Vorlagen!", template);
                resp.ExeptionMsg = ex.Message;

                //logging
                resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrint: {0} - EX: {1}", resp.ResponseMsg, ex.Message),getUserId(sessionToken)));

                return resp;
            }

            //load control lists
            try
            {
                StreamReader re = File.OpenText(System.Web.Configuration.WebConfigurationManager.AppSettings.Get("importDir") + System.Web.Configuration.WebConfigurationManager.AppSettings.Get("ctrlList1"));
                ctrlList1 = re.ReadToEnd();
                re.Close();

                re = File.OpenText(System.Web.Configuration.WebConfigurationManager.AppSettings.Get("importDir") + System.Web.Configuration.WebConfigurationManager.AppSettings.Get("ctrlList2"));
                ctrlList2 = re.ReadToEnd();
                re.Close();
            }
            catch (Exception ex)
            {
                resp.ResponseCode = 409;
                resp.ResponseMsg = "Fehler beim Laden der Kontrolllisten!";
                resp.ExeptionMsg = ex.Message;

                //logging
                resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrint: {0} - EX: {1}", resp.ResponseMsg, ex.Message), getUserId(sessionToken)));

                return resp;
            }

            //load keys
            DataSet keyValues;
            List<string[]> tokenKeyLists = new List<string[]>();
            List<Template> templateList = new List<Template>();
            try
            {
                //TODO OLD! keyValues = DataProvider.getKeyValues(getDAL(), printer, user, bereich, bezirk, status, template, sort, ablehnung, von, bis, ant_ikey, t.datafileds.ToArray<string>());
                log(LogType.INFO, "Getting keys for document",serviceUser.UserId);
                keyValues = DataProvider.getKeyValues(getDAL(), printer, user, bereich, bezirk, status, template, sort, ablehnung, von, bis, ant_ikey, tokens.ToArray<string>(), wbd_bdl);
                log(LogType.INFO, String.Format("Retrieved {0} rows of keys", keyValues.Tables[1].Rows.Count), serviceUser.UserId);


                //log(LogType.ERROR, "key count " + keyValues.Tables[1].Rows.Count, serviceUser.UserId);
                //generate token/key mapping
                Dictionary<string, string> keyMapping = new Dictionary<string, string>();

                foreach (DataRow row in keyValues.Tables[1].Rows)
                {
                    keyMapping[row[0].ToString()] = row[1].ToString();
                }

                //generate token/key array for each row
                foreach (DataRow row in keyValues.Tables[0].Rows)
                {
                    string[] tokenKeyList = new string[keyMapping.Count];

                    int i = 0;
                    int urgCount = 0;
                    foreach (string token in keyMapping.Keys) //collect urgenzen
                    {

                    
                       // log(LogType.ERROR, "token " + token, 0);
                        if (token.StartsWith("WBD_URGENZ")) //URGENZ TOKEN
                        {
                            string newKey = "-1"; //if urg not in keys -1 is passed

                            string[] urgs = row[keyMapping[token]].ToString().Split(';');

                            if (urgCount < urgs.Length)
                            {
                                newKey = urgs[urgCount];
                            }
                            urgCount++;

                            tokenKeyList[i] = string.Format("<{0}>;<{1}>", token, newKey);
                        } 
                        else 
                        {
                            tokenKeyList[i] = string.Format("<{0}>;<{1}>", token, row[keyMapping[token]].ToString());
                        }
                        i++;
                        
                    }

                    //wenn scheidung UND Tilgung, ma1 oder ma1 wird nur das mitschuldner schreiben gedruckt. Laut Fr.Schmautz 20.07.2011
                    if (!(row["WBD_SCHEIDUNG"].ToString() == "J" && (template == "14" || template == "15" || template == "6")))
                    {
                        //add base template
                        tokenKeyLists.Add(tokenKeyList);
                        templateList.Add(templates["base"]);
                    }

                    //select template
                    #region template selection

                   

                    //if tilgung
                    if (row["WBD_SCHEIDUNG"].ToString() == "J" && row["MS1_KEY"].ToString() != "-1" && template == "6")
                    {
                        if (templates.Keys.Contains<string>("ms1"))
                        {
                            tokenKeyLists.Add(tokenKeyList);
                            templateList.Add(templates["ms1"]);
                        }
                        else
                        {
                            resp.ResponseCode = 400;
                            resp.ResponseMsg = String.Format("Fehler beim Laden des Templates MS1!");
                            resp.ExeptionMsg = "";
                            return resp;
                        }

                    }

                    if (row["WBD_SCHEIDUNG"].ToString() == "J" && row["MSB_KEY"].ToString() != "-1" && template == "6")
                    {
                        if (templates.Keys.Contains<string>("msb"))
                        {
                            tokenKeyLists.Add(tokenKeyList);
                            templateList.Add(templates["msb"]);
                        }
                        else
                        {
                            resp.ResponseCode = 400;
                            resp.ResponseMsg = String.Format("Fehler beim Laden des Templates MSB!");
                            resp.ExeptionMsg = "";
                            return resp;

                        }
                    }

                    //wenn scheidung UND ms1_key oder msb-key exist
                    if (row["WBD_SCHEIDUNG"].ToString() == "J" && row["MS1_KEY"].ToString() != "-1" && (template == "14" || template == "15"))
                    {
                        if (templates.Keys.Contains<string>("ms1"))
                        {
                            tokenKeyLists.Add(tokenKeyList);
                            templateList.Add(templates["ms1"]);
                        }
                        else
                        {
                            resp.ResponseCode = 400;
                            resp.ResponseMsg = String.Format("Fehler beim Laden des Templates MS1!");
                            resp.ExeptionMsg = "";
                            return resp;

                        }

                    }
                    else if (row["WBD_SCHEIDUNG"].ToString() == "J" && row["MSB_KEY"].ToString() != "-1")
                    {
                        if (templates.Keys.Contains<string>("msb"))
                        {
                            tokenKeyLists.Add(tokenKeyList);
                            templateList.Add(templates["msb"]);
                        }
                        else
                        {
                            resp.ResponseCode = 400;
                            resp.ResponseMsg = String.Format("Fehler beim Laden des Templates MSB!");
                            resp.ExeptionMsg = "";
                            return resp;
                        }
                    }
                    else
                    {
                        //Wird am Anfang immer hinzugefügt templateList.Add(templates["base"]);
                    }

                    //wenn template gleich val von config 201
                    if (template == "16")
                    {

                        if (row["MSB_KEY"].ToString() != "-1")
                        {
                            if (templates.Keys.Contains<string>("m3msb"))
                            {
                                tokenKeyLists.Add(tokenKeyList);
                                templateList.Add(templates["m3msb"]);
                            }
                            else
                            {
                                resp.ResponseCode = 400;
                                resp.ResponseMsg = String.Format("Fehler beim Laden des Templates M3MSB!");
                                resp.ExeptionMsg = "";
                                return resp;
                            }
                        }

                        if (row["MS1_KEY"].ToString() != "-1")
                        {
                            if (templates.Keys.Contains<string>("m3ms1"))
                            {
                                tokenKeyLists.Add(tokenKeyList);
                                templateList.Add(templates["m3ms1"]);
                            }
                            else
                            {
                                resp.ResponseCode = 400;
                                resp.ResponseMsg = String.Format("Fehler beim Laden des Templates M3MS1!");
                                resp.ExeptionMsg = "";
                                return resp;
                            }
                        }

                        if (row["MS2_KEY"].ToString() != "-1")
                        {
                            if (templates.Keys.Contains<string>("m3ms2"))
                            {
                                tokenKeyLists.Add(tokenKeyList);
                                templateList.Add(templates["m3ms2"]);
                            }
                            else
                            {
                                resp.ResponseCode = 400;
                                resp.ResponseMsg = String.Format("Fehler beim Laden des Templates M3MS2!");
                                resp.ExeptionMsg = "";
                                return resp;
                            }
                        }

                        if (row["MS3_KEY"].ToString() != "-1")
                        {
                            if (templates.Keys.Contains<string>("m3ms3"))
                            {
                                tokenKeyLists.Add(tokenKeyList);
                                templateList.Add(templates["m3ms3"]);
                            }
                            else
                            {
                                resp.ResponseCode = 400;
                                resp.ResponseMsg = String.Format("Fehler beim Laden des Templates M3MS3!");
                                resp.ExeptionMsg = "";
                                return resp;
                            }
                        }
                    }
                    #endregion
                }

            }
            catch (Exception ex)
            {
                resp.ResponseCode = 405;
                resp.ResponseMsg = "Fehler beim Laden der Keys!";
                resp.ExeptionMsg = ex.Message;

                //logging
                resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrint: {0} - EX: {1}", resp.ResponseMsg, ex.Message), getUserId(sessionToken)));

                return resp;
            }

            //load token values
            List<Dictionary<string, string>> tokenValues = new List<Dictionary<string, string>>();
            try
            {
                int docCount = 1;
                foreach (string[] keyList in tokenKeyLists)
                {
                    Dictionary<string, string> tokvals = DataProvider.getTokenValues(getDAL(), user, keyList);
                    //
                    // key value available
                    // 23-02-2016 by KJ
                    //
                    string ms = null;
                    try{ms = tokvals.FirstOrDefault(x => x.Key == "MS1_ZUNAME_N").Value;}
                    catch{}
                    if ( ms != null)
                    {
                        if ((tokvals["MS1_ZUNAME_N"] == String.Empty) && (tokvals["MS1_TITEL_N"] == String.Empty) && (tokvals["MS1_VORNAME_N"] == String.Empty))
                        { }
                        else
                        {
                            if (tokvals["MS1_TITEL_N"] != String.Empty)
                            {
                                tokvals["MS1_TITEL_N"] = string.Format("{0}{1} ", gemeinsamMit, tokvals["MS1_TITEL_N"]);
                                tokvals["MS1_VORNAME_N"] = string.Format("{0} ", tokvals["MS1_VORNAME_N"]);
                            }
                            else
                            {
                                tokvals["MS1_VORNAME_N"] = string.Format("{0}{1} ", gemeinsamMit, tokvals["MS1_VORNAME_N"]);
                            }
                        }
                    }

                     try{ms = tokvals.FirstOrDefault(x => x.Key == "MS2_ZUNAME_N").Value;}
                     catch{}
                     if (ms != null)
                     {
                         if ((tokvals["MS2_ZUNAME_N"] == String.Empty) && (tokvals["MS2_TITEL_N"] == String.Empty) && (tokvals["MS2_VORNAME_N"] == String.Empty))
                         { }
                         else
                         {
                             if (tokvals["MS2_TITEL_N"] != String.Empty)
                             {
                                 tokvals["MS2_TITEL_N"] = string.Format(", {0} ", tokvals["MS2_TITEL_N"]);
                                 tokvals["MS2_VORNAME_N"] = string.Format("{0} ", tokvals["MS2_VORNAME_N"]);
                             }
                             else
                             {
                                 tokvals["MS2_VORNAME_N"] = string.Format(", {0} ", tokvals["MS2_VORNAME_N"]);
                             }
                         }
                     }
                     
                     try {ms = tokvals.FirstOrDefault(x => x.Key == "MS3_ZUNAME_N").Value; }
                     catch { }
                     if (ms != null)
                     {
                         if ((tokvals["MS3_ZUNAME_N"] == String.Empty) && (tokvals["MS3_TITEL_N"] == String.Empty) && (tokvals["MS3_VORNAME_N"] == String.Empty))
                         { }
                         else
                         {
                             if (tokvals["MS3_TITEL_N"] != String.Empty)
                             {
                                 tokvals["MS3_TITEL_N"] = string.Format(", {0} ", tokvals["MS3_TITEL_N"]);
                                 tokvals["MS3_VORNAME_N"] = string.Format("{0} ", tokvals["MS3_VORNAME_N"]);
                             }
                             else
                             {
                                 tokvals["MS3_VORNAME_N"] = string.Format(", {0} ", tokvals["MS3_VORNAME_N"]);
                             }
                         }
                     }
                    //
                    // 24-02-2016 by KJ
                    //
                    log(LogType.INFO, String.Format("Getting values for document {0} of {1}", docCount, tokenKeyLists.Count),serviceUser.UserId);
                    //for (int i = 0; i < templateList.Count; i++)
                    //{
                        tokenValues.Add(tokvals);
                    //}
                    log(LogType.INFO, String.Format("Retrieved values for document {0} of {1}", docCount, tokenKeyLists.Count), serviceUser.UserId);
                    docCount++;
                }

            }
            catch (Exception ex)
            {
                resp.ResponseCode = 406;
                resp.ResponseMsg = "Fehler beim Laden der Tokenvalues!";
                resp.ExeptionMsg = ex.Message;

                //logging
                resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrint: {0} - EX: {1}", resp.ResponseMsg, ex.Message), getUserId(sessionToken)));

                return resp;
            }

            //return doPrint(user, templateList, printer, tokenValues, signatures, von, bis, ctrlList1, ctrlList2);
            if (printable != null)
            {
                resp.ResponseCode = 0;
            }
            else
            {

                log(LogType.ERROR, String.Format("printer: {0}", printer), serviceUser.UserId);
                resp = doPrint(user, templateList, printer, tokenValues, signatures, von, bis, ctrlList1, ctrlList2);
            }
            

            if (resp.ResponseCode == 0)
            {
                resp.ResponseMsg = keyValues.Tables[0].Rows.Count.ToString();
                resp.ResponseCode = 0;
                resp.ExeptionMsg = "";
            }

            return resp;

            //for (int k = 45; k < tokenValues.Count; k++)
            //{
            //    List<Dictionary<string, string>> aDoc = new List<Dictionary<string, string>>();
            //    aDoc.Add(tokenValues[k]);
            //    resp = doPrint(user, templateList, printer, aDoc, signatures, von, bis, ctrlList1, ctrlList2);
                
            //    if (resp.ResponseCode != 0)
            //    {
            //        return resp;
            //    }

            //    DateTime startWait = DateTime.Now;
            //    DateTime waiting = DateTime.Now;
            //    while(Process.GetProcessesByName("wordpad").Length > 4 )
            //    {
            //        waiting = DateTime.Now;
            //        if ((waiting - startWait).Minutes > 2)
            //        {
            //            resp.ResponseCode = 410;
            //            resp.ResponseMsg = "Timeout beim Drucken!";
            //            resp.ExeptionMsg = "";
            //            return resp;
            //        }
            //    }
            //}

            //resp.ResponseCode = 0;
            //resp.ResponseMsg = "Ok";
            //return resp;
        }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 03.2011
        /// Prints a single (non generated) Document from tmp directory with wordpad.
        /// </summary>
        /// <param name="sessionToken">the session token of this user session</param>
        /// <param name="printer">the printer as string</param>
        /// <param name="fileName">The file that should be printed. (name with extension only) as String</param>
        /// <returns>a Response Object</returns>
        public Response doSimplePrint(string sessionToken, string printer, string fileName)
        {
            Response resp = new Response();




            getDAL();

            #region Authentication

            //AUTHENTICATION
            AuthServiceClient authService = new AuthServiceClient();
            User serviceUser = new User();
            AuthResponse authResponse = authService.getUser(out serviceUser, sessionToken);

            if (authResponse.ResponseCode != 0) //something is wrong with this token
            {
                resp.ResponseCode = authResponse.ResponseCode;
                resp.ResponseMsg = authResponse.ResponseMsg;
                return resp;
            }

            if (!serviceUser.CanRead && !serviceUser.CanWrite) //this method needs read permission!
            {
                resp.ResponseCode = 500;
                resp.ResponseMsg = "Permission denied!";
                return resp;
            }
            //END AUTHENTICATION

            #endregion Authentication

            string printDocPath = tempDir + fileName;
            string printDocPath2 = "C:\\wbd\\Data\\SEPA\\" + fileName;

            if (!File.Exists(printDocPath)) //check if file exists
            {
                if (!File.Exists(printDocPath2))
                {
                    resp.ResponseCode = 408;
                    resp.ResponseMsg = String.Format("Dokument {0} nicht gefunden!", fileName);

                    //logging
                    resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doSimplePrint: {0}", resp.ResponseMsg), getUserId(sessionToken)));

                    return resp;
                }
                else
                {
                    printDocPath = printDocPath2;
                }
                
            }

            try
            {
                printOutWithWordApp(printDocPath, printer);
                //printOutWithWordPad(printDocPath, printer);
            }
            catch (Exception ex)
            {
                resp.ResponseCode = 403;
                resp.ResponseMsg = "Fehler beim Drucken des Dokuments!";
                resp.ExeptionMsg = ex.Message;

                //logging
                resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doSimplePrint: {0} - EX: {1}", resp.ResponseMsg, ex.Message), getUserId(sessionToken)));

                return resp;
            }

            resp.ResponseCode = 0;
            resp.ResponseMsg = "Ok";
            return resp;
        }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 04.2011
        /// Prints a single Document and fills the given template with the given token values and the list token withe lines in list
        /// </summary>
        /// <param name="sessionToken">the session token of this user session</param>
        /// <param name="printer">the printer as string</param>
        /// <param name="fileName">The file that should be printed. (name with extension only) as String</param>
        /// <returns>a Response Object</returns>
        public Response doPrintWithValues(string sessionToken, string printer, string templateName, Dictionary<string,string> values, List<string> list)
        {
            Response resp = new Response();

            #region Authentication

            //AUTHENTICATION
            AuthServiceClient authService = new AuthServiceClient();
            User serviceUser = new User();
            AuthResponse authResponse = authService.getUser(out serviceUser, sessionToken);

            if (authResponse.ResponseCode != 0) //something is wrong with this token
            {
                resp.ResponseCode = authResponse.ResponseCode;
                resp.ResponseMsg = authResponse.ResponseMsg;
                return resp;
            }

            if (!serviceUser.CanRead && !serviceUser.CanWrite) //this method needs read permission!
            {
                resp.ResponseCode = 500;
                resp.ResponseMsg = "Permission denied!";
                return resp;
            }
            //END AUTHENTICATION

            #endregion Authentication


            if (printer == null || printer == "")
            {
                log(LogType.ERROR, "no printer", 404);
                throw new Exception("No printer defined");
            }



            string printDocPath = tempDir + "printDoc" + DateTime.Now.Ticks.ToString() + ".rtf";
            StreamWriter printDoc = null;

            Template template;
            try
            {
                template = getTemplateLibrary().getTemplate(templateName);
            }
            catch(Exception ex)
            {
                resp.ResponseCode = 400;
                resp.ResponseMsg = "Fehler beim Fehler beim Laden des Templates!";
                resp.ExeptionMsg = ex.Message;

                //logging
                resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrintWithValues: {0} - EX: {1}", resp.ResponseMsg, ex.Message), getUserId(sessionToken)));

                return resp;
            }

            try
            {
                System.Text.Encoding encOutput = null;
                encOutput = System.Text.Encoding.Default;
                printDoc = new StreamWriter(printDocPath, false, encOutput);
            }
            catch (Exception ex)
            {
                resp.ResponseCode = 402;
                resp.ResponseMsg = "Fehler beim Erstellen des Print Dokuments!";
                resp.ExeptionMsg = ex.Message;

                //logging
                resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrintWithValues: {0} - EX: {1}", resp.ResponseMsg, ex.Message), getUserId(sessionToken)));

                return resp;
            }


            try
            {
                StreamReader re = File.OpenText(System.Web.Configuration.WebConfigurationManager.AppSettings.Get("importDir") + template.name + ".rtf");

                string content = re.ReadToEnd();

                re.Close();

                content = content.Trim();

                //tokens befüllen
                foreach (string key in values.Keys)
                {
                    content = content.Replace(String.Format("<{0}>", key), values[key]);
                }

                //list befüllen
                StringBuilder theList = new StringBuilder();
                for (int i = 0; i < list.Count; i++)
                {
                    theList.Append(list[i]);
                    theList.Append(@"\par ");
                }

                content = content.Replace("<LIST>",theList.ToString());

                printDoc.WriteLine(content);

                printDoc.Close();
            }
            catch (Exception ex)
            {
                resp.ResponseCode = 402;
                resp.ResponseMsg = "Fehler beim Schreiben des Print Dokuments!";

                //logging
                resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrintWithValues: {0} - EX: {1}", resp.ResponseMsg, ex.Message), getUserId(sessionToken)));

                resp.ExeptionMsg = ex.Message;
                return resp;
            }

            try
            {
                printOutWithWordApp(printDocPath, printer);
                //printOutWithWordPad(printDocPath, printer);
            }
            catch (Exception ex)
            {
                resp.ResponseCode = 403;
                resp.ResponseMsg = "Fehler beim Drucken des Dokuments!";
                resp.ExeptionMsg = ex.Message;

                //logging
                resp = checkLogResponse(resp, log(LogType.ERROR, String.Format("Error in method doPrintWithValues: {0} - EX: {1}", resp.ResponseMsg, ex.Message),getUserId(sessionToken)));

                return resp;
            }

            resp.ResponseCode = 0;
            resp.ResponseMsg = "Ok";
            return resp;
        }



        #region logging

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 03.2011
        /// Logs a message
        /// </summary>
        /// <returns>a Log Response</returns>
        private LogResp log(LogType logtype, string msg, int userid)
        {
            LoggingClient logger = new LoggingClient();
            return logger.log(logtype, LogSource.DOCUMENT_GENERATION_SERVICE, SHORTCODE.DG, userid, "", msg);
        }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 03.2011
        /// Checks if a log was successfull. If log failed the response object if filled with detailed information about that!
        /// </summary>
        /// <returns>a Log Response</returns>
        private Response checkLogResponse(Response resp, LogResp logresp)
        {
            if (logresp.ResponseCode != 0) //if something went wrong while logging write that to response!
            {
                resp.ResponseCode = logresp.ResponseCode;
                resp.ResponseMsg = String.Format("{0} - Failed to log: {1}", logresp.ResponseMsg, resp.ResponseMsg);
                resp.ExeptionMsg = logresp.ExeptionMsg;
            }

            return resp;
        }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 04.2011
        /// Loads the userid of a user by its session token
        /// </summary>
        /// <returns>userid</returns>
        private int getUserId(string token)
        {
            AuthServiceClient authService = new AuthServiceClient();
            User serviceUser = new User();
            AuthResponse authResponse = authService.getUser(out serviceUser, token);

            if (authResponse.ResponseCode != 0) //something is wrong with this token
            {
                return serviceUser.UserId;
            }
            else
            {
                return -1;
            }
        }

        #endregion
    }
}
