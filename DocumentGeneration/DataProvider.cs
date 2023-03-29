using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Addit.AK.WBD.DAL;
using Oracle.DataAccess.Client;
using System.Data;
using System.IO;

namespace Addit.AK.WBD.DocumentGeneration
{
    /// <summary>
    /// Author: Bruno Hautzenberger
    /// Creation Date: 12.2010
    /// Implements functions to generate a XSL DataSource and functions to load data from DB
    /// </summary>
    class DataProvider
    {
        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// generates a xsl datasource with given token,value pairs
        /// </summary>
        /// <param name="keys">Dict of Token,Value</param>
        /// <param name="path">the directory to write datasource file to</param>
        /// <returns>the path of the new Datasource</returns>
        public static string generateDatasource(Dictionary<String, String> keys, string path)
        {
            string datasourceFile = path + "DS" + DateTime.Now.Ticks.ToString() + ".xml";

            XmlTextWriter xWriter = new XmlTextWriter(datasourceFile, Encoding.UTF8);

            xWriter.WriteStartDocument(); //start new XML Document

            xWriter.WriteStartElement("data"); //open root node

            foreach (string key in keys.Keys) //write data nodes
            {
                if (key.Trim().Length > 0)
                {
                    xWriter.WriteStartElement("F" + key);

                    string theValue = " ";
                    if (keys[key] != null && keys[key] != "")
                    {
                        theValue = keys[key];
                    }

                    //xWriter.WriteString(keys[key]);
                    xWriter.WriteString(theValue);
                    xWriter.WriteEndElement();
                }
            }

            xWriter.WriteEndElement(); //close root node

            xWriter.WriteEndDocument(); //close XML Document

            xWriter.Close(); //close and flush writer!

            return datasourceFile;
        }

        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// gets all needed keys from DB. (for each document matching these cretirias)
        /// </summary>
        /// <param name="dal">a connected DAL_Oracle Object</param>
        /// <param name="userid">user by id</param>
        /// <param name="templateid">template by id</param>
        /// <param name="bereich">Bereich by Id</param>
        /// <param name="Bezirk">Bezirk by Id</param>
        /// <param name="status">status byId</param>
        /// <param name="printer">the printer as string</param>
        /// <param name="ablehnung">J/N</param>+
        /// <param name="sort">??</param>
        /// <param name="fromDate">von as String FORMAT</param>
        /// <param name="toDate">bis as String FORMAT</param>
        /// <param name="ant_ikey">ant_ikey as string</param>
        /// <param name="tokens">all tokens in this template</param>
        /// <returns>Dataset with two tables. T1 = TokenKeys, T2 = key-token-mapping</returns>
        public static DataSet getKeyValues(DAL_Oracle dal, string printer, string userid, string bereich, string bezirk, string status, string templateId, string sort, string ablehnung, string fromDate, string toDate, string antIkey, string[] tokens, int wbd_bdl)
        {
            OracleCommand cmd = new OracleCommand("WORKFLOW.nwbd_Seriendruck.Get_Key_Values");
            cmd.CommandType = CommandType.StoredProcedure;


            OracleParameter ret = new OracleParameter();
            ret.OracleDbType = OracleDbType.Int32;
            ret.Direction = ParameterDirection.ReturnValue;
            ret.Value = 0;
            cmd.Parameters.Add(ret);

            OracleParameter P1 = new OracleParameter();
            P1.OracleDbType = OracleDbType.Varchar2;
            P1.Direction = ParameterDirection.Input;
            P1.ParameterName = "var_Drucker";
            //P1.Value = "1";
            P1.Value = printer;
            cmd.Parameters.Add(P1);

            OracleParameter P2 = new OracleParameter();
            P2.OracleDbType = OracleDbType.Varchar2;
            P2.Direction = ParameterDirection.Input;
            P2.ParameterName = "var_UserNr";
            //P2.Value = "2";
            P2.Value = userid;
            cmd.Parameters.Add(P2);

            OracleParameter P3 = new OracleParameter();
            P3.OracleDbType = OracleDbType.Varchar2;
            P3.Direction = ParameterDirection.Input;
            P3.ParameterName = "var_Bereich";
            //P3.Value = "2";
            P3.Value = bereich;
            cmd.Parameters.Add(P3);

            OracleParameter P4 = new OracleParameter();
            P4.OracleDbType = OracleDbType.Varchar2;
            P4.Direction = ParameterDirection.Input;
            P4.ParameterName = "var_Bezirk";
            //P4.Value = "-1";
            P4.Value = bezirk;
            cmd.Parameters.Add(P4);

            OracleParameter P5 = new OracleParameter();
            P5.OracleDbType = OracleDbType.Varchar2;
            P5.Direction = ParameterDirection.Input;
            P5.ParameterName = "var_Status";
            //P5.Value = "3";
            P5.Value = status;
            cmd.Parameters.Add(P5);

            OracleParameter P6 = new OracleParameter();
            P6.OracleDbType = OracleDbType.Varchar2;
            P6.Direction = ParameterDirection.Input;
            P6.ParameterName = "var_Vorlage";
            //P6.Value = "14";
            P6.Value = templateId;
            cmd.Parameters.Add(P6);

            OracleParameter P7 = new OracleParameter();
            P7.OracleDbType = OracleDbType.Varchar2;
            P7.Direction = ParameterDirection.Input;
            P7.ParameterName = "var_Ablehnung";
            //P7.Value = "";
            P7.Value = ablehnung;
            cmd.Parameters.Add(P7);

            OracleParameter P8 = new OracleParameter();
            P8.OracleDbType = OracleDbType.Varchar2;
            P8.Direction = ParameterDirection.Input;
            P8.ParameterName = "var_Sort";
            //P8.Value = "0";
            P8.Value = sort;
            cmd.Parameters.Add(P8);

            OracleParameter P9 = new OracleParameter();
            P9.OracleDbType = OracleDbType.Varchar2;
            P9.Direction = ParameterDirection.Input;
            P9.ParameterName = "var_vonDatum";
            //P9.Value = "";
            P9.Value = fromDate;
            cmd.Parameters.Add(P9);


            OracleParameter PA = new OracleParameter();
            PA.OracleDbType = OracleDbType.Varchar2;
            PA.Direction = ParameterDirection.Input;
            PA.ParameterName = "var_bisDatum";
            //PA.Value = "";
            PA.Value = toDate;
            cmd.Parameters.Add(PA);

            OracleParameter PB = new OracleParameter();
            PB.OracleDbType = OracleDbType.Varchar2;
            PB.Direction = ParameterDirection.Input;
            PB.ParameterName = "var_Antikey";
            //PB.Value = "-1";
            PB.Value = antIkey;
            cmd.Parameters.Add(PB);

            OracleParameter PD = new OracleParameter();
            PD.OracleDbType = OracleDbType.Varchar2;
            PD.Direction = ParameterDirection.Input;
            PD.ParameterName = "var_wbd_bdl";
            //PB.Value = "-1";
            PD.Value = wbd_bdl.ToString();
            cmd.Parameters.Add(PD);

            /*OracleParameter ISH = new OracleParameter();
            ISH.OracleDbType = OracleDbType.Varchar2;
            ISH.Direction = ParameterDirection.Input;
            ISH.ParameterName = "var_ish";

            ISH.Value = ish.ToString();
            cmd.Parameters.Add(ISH);*/

            OracleParameter PC = new OracleParameter();
            PC.OracleDbType = OracleDbType.Varchar2;
            PC.CollectionType = OracleCollectionType.PLSQLAssociativeArray;
            PC.Direction = ParameterDirection.Input;
            PC.ParameterName = "aso_key_pair";
            PC.Value = tokens;

            cmd.Parameters.Add(PC);

            OracleParameter p1_ref = new OracleParameter();
            p1_ref.OracleDbType = OracleDbType.RefCursor;
            p1_ref.Direction = ParameterDirection.Output;
            p1_ref.ParameterName = "out_cursor1";
            cmd.Parameters.Add(p1_ref);

            OracleParameter p2_ref = new OracleParameter();
            p2_ref.OracleDbType = OracleDbType.RefCursor;
            p2_ref.Direction = ParameterDirection.Output;
            p2_ref.ParameterName = "out_cursor2";
            cmd.Parameters.Add(p2_ref);

            return dal.executeQuery(cmd);
        }

        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// gets the values for each token
        /// </summary>
        /// <param name="dal">a connected DAL_Oracle Object</param>
        /// <param name="userid">user by id</param>
        /// <param name="tokenKeys">all tokens in this template including the right keys to load their values as token;key</param>
        /// <returns>a Response Object</returns>
        public static Dictionary<string, string> getTokenValues(DAL_Oracle dal, string userid, string[] tokenKeys)
        {
            OracleCommand cmd = new OracleCommand("WORKFLOW.nwbd_Seriendruck.Get_Token_Values");
            cmd.CommandType = CommandType.StoredProcedure;

            OracleParameter ret = new OracleParameter();
            ret.OracleDbType = OracleDbType.Int32;
            ret.Direction = ParameterDirection.ReturnValue;
            ret.Value = 0;
            cmd.Parameters.Add(ret);

            OracleParameter User = new OracleParameter();
            User.OracleDbType = OracleDbType.Int32;
            User.Direction = ParameterDirection.Input;
            User.ParameterName = "var_UserNr";
            User.Value = userid;
            cmd.Parameters.Add(User);

            OracleParameter p3_ref = new OracleParameter();
            p3_ref.OracleDbType = OracleDbType.Varchar2;
            p3_ref.CollectionType = OracleCollectionType.PLSQLAssociativeArray;
            p3_ref.Direction = ParameterDirection.Input;
            p3_ref.ParameterName = "aso_key_pair";
            p3_ref.Value = tokenKeys;
            cmd.Parameters.Add(p3_ref);

            OracleParameter p1_ref = new OracleParameter();
            p1_ref.OracleDbType = OracleDbType.RefCursor;
            p1_ref.Direction = ParameterDirection.Output;
            p1_ref.ParameterName = "out_cursor";
            cmd.Parameters.Add(p1_ref);

            DataTable dt = dal.executeQuery(cmd).Tables[0];

            Dictionary<string, string> tokenValues = new Dictionary<string, string>();
            foreach (DataRow row in dt.Rows)
            {
                tokenValues[row["ser_tokbez"].ToString()] = row["ser_tokval"].ToString();
            }

            return tokenValues;
        }

        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// gets the name of a template by its id
        /// </summary>
        /// <param name="dal">a connected DAL_Oracle Object</param>
        /// <param name="templateid">template by id</param>
        /// <returns>template name</returns>
        public static string getTemplateNameById(DAL_Oracle dal, string templateId)
        {
            OracleCommand cmd = new OracleCommand(String.Format("select prv_file_c from wbd_printvorlagen_c where prv_ikey_c = {0}", templateId));

            DataSet ds = dal.executeQuery(cmd);
            DataTable dt = ds.Tables[0];

            return dt.Rows[0][0].ToString();
        }

        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// all templates that could occur in this batch
        /// </summary>
        /// <param name="dal">a connected DAL_Oracle Object</param>
        /// <param name="templateid">template by id</param>
        /// <returns>template names</returns>
        public static Dictionary<string, string> getPossibleTemplateNames(DAL_Oracle dal, string templateId)
        {
            OracleCommand cmd = new OracleCommand(String.Format("select prv_file_c, prv_ms1, prv_msb, prv_m3ms1, prv_m3ms2, prv_m3ms3, prv_m3msb, prv_printable from wbd_printvorlagen_c where prv_ikey_c = {0}", templateId));

            DataSet ds = dal.executeQuery(cmd);
            DataTable dt = ds.Tables[0];

            Dictionary<string, string> templateNames = new Dictionary<string, string>();
            //if (dt.Rows[0]["prv_printable"].ToString() == String.Empty)
            //{
                templateNames["base"] = dt.Rows[0]["prv_file_c"].ToString().Trim().Split('.')[0];
                templateNames["ms1"] = dt.Rows[0]["prv_ms1"].ToString().Trim().Split('.')[0];
                templateNames["msb"] = dt.Rows[0]["prv_msb"].ToString().Trim().Split('.')[0];
                templateNames["m3ms1"] = dt.Rows[0]["prv_m3ms1"].ToString().Trim().Split('.')[0];
                templateNames["m3ms2"] = dt.Rows[0]["prv_m3ms2"].ToString().Trim().Split('.')[0];
                templateNames["m3ms3"] = dt.Rows[0]["prv_m3ms3"].ToString().Trim().Split('.')[0];
                templateNames["m3msb"] = dt.Rows[0]["prv_m3msb"].ToString().Trim().Split('.')[0];
                templateNames["printable"] = dt.Rows[0]["prv_printable"].ToString().Trim().Split('.')[0];
            //}

            return templateNames;
        }


        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 28.02.2011
        ///loads the content of a signature file as string
        /// </summary>
        /// <param name="signatureFile">the fileInfo Object of the signature</param>
        /// <returns>the crypted Signature content</returns>
        public static string loadSignatureContent(FileInfo signatureFile)
        {
            TextReader tr = null;
            String content = String.Empty;

            try
            {
                tr = new StreamReader(signatureFile.FullName);
                content = tr.ReadToEnd();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (tr != null)
                {
                    tr.Close();
                }
            }

            return content;
        }


        public static bool merge(string srcFile, string dstFile)
        {
            try
            {
                using (StreamReader src = File.OpenText(srcFile))
                {
                    using (StreamReader dest = File.OpenText(dstFile))
                    {
                        string srcContent = src.ReadToEnd();
                        // in the source file, we obtain indices of the first and the last paragraphs
                        int srcFirst = srcContent.IndexOf(@"\par");
                        int srcSecond = srcContent.LastIndexOf(@"\par") - 1;
                        if ((srcFirst < 0) || (srcSecond < 0))
                            return false;

                        // processing the destination file to get the index at which we insert the migrating content
                        string destContent = dest.ReadToEnd();
                        int destFirst = destContent.LastIndexOf("}");
                        if (destFirst < 0)
                            return false;
                        dest.Close();
                        string mirgatingContent = srcContent.Substring(srcFirst, srcSecond - srcFirst + 1);

                        // ensures that there's a blank line between the old content and the new one
                        if (mirgatingContent.StartsWith(@"\pard"))
                            mirgatingContent = @"\par\par" + mirgatingContent.Remove(0, 5);
                        destContent = destContent.Insert(destFirst, mirgatingContent);
                        File.WriteAllText(dstFile, destContent);
                        return true;
                    }
                }
            }
            catch
            {
                // something wrong happened
                return false;
            }

        }
    }

}