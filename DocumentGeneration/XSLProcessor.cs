using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.XPath;
using System.Xml.Xsl;
using System.Xml;

namespace Addit.AK.WBD.DocumentGeneration
{
    /// <summary>
    /// Author: Bruno Hautzenberger
    /// Creation Date: 12.2010
    /// Implements functions convert rtf to xsl template and render xsl
    /// </summary>
    class XSLProcessor
    {

        #region private members

        /// <summary>
        /// the xsl header for rtf templates
        /// </summary>
        private static string xslBodyTemplate = "<xsl:stylesheet xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\" version=\"1.0\">" +
                                                "<xsl:output method=\"text\" />" +
                                                "<xsl:template match=\"/\">" +
                                                "$BODY$" +
                                                "</xsl:template>" +
                                                "</xsl:stylesheet>";

        /// <summary>
        /// start marker of of rtf merge fields
        /// </summary>
        private static string fieldStartMarker = "<";

        /// <summary>
        /// end marker of of rtf merge fields
        /// </summary>
        private static string fieldEndtMarker = ">";

        #endregion

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// converts a rtf template to a xsl template
        /// </summary>
        /// <param name="rtf">file content of the rtf template</param>
        /// <param name="template">the template object</param>
        /// <returns>xsl file content for xsl template</returns>
        public static string rtfToXsl(string rtf, Template template)
        {
            template.datafileds = new List<string>();

            //iterate through fields
            int i = 0; //searchPosition
            int startMarker = 0;
            int endMarker = 0;
            bool EOF = false;
            string fieldName = "";
            string xslField = @"<xsl:value-of select='/data/F{0}'/>";

            while (!EOF)
            {
                //get position of next marker
                startMarker = rtf.IndexOf(fieldStartMarker, i);

                //if there are no new markers it's time to end this
                if (startMarker == -1)
                {
                    EOF = true;
                }
                else
                {
                    if (rtf.Substring(startMarker, 5) == "<xsl:" || rtf.Substring(startMarker, 5) == "<![CD") //field was already converted! Skip it!
                    {
                        i = startMarker + xslField.Length + 1;
                    }
                    else
                    {
                        //get endmarker
                        i = startMarker + 1;
                        endMarker = rtf.IndexOf(fieldEndtMarker, i);

                        fieldName = rtf.Substring(startMarker + 1, endMarker - startMarker - 1);

                        string newXslField = String.Format(xslField, fieldName);
                        
                        rtf = rtf.Replace(fieldStartMarker + fieldName + fieldEndtMarker, newXslField);

                        if (!template.datafileds.Contains(fieldName))
                        {
                            template.datafileds.Add(fieldName);
                        }

                        i = startMarker + newXslField.Length;
                    }
                }

            }

            rtf = rtf.Replace("> ", "><![CDATA[ ]]>"); //restore blanks
            rtf = rtf.Replace(">\n", "><![CDATA[ ]]>"); //restore blanks

            return xslBodyTemplate.Replace("$BODY$", rtf);
        }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// renders a rtf document with template and datasource
        /// </summary>
        /// <param name="xmlDataFile">path to Datasource</param>
        /// <param name="xslTemplateFile">path to xsl Template</param>
        /// <param name="outputFile">path to outputfile</param>
        public static void render(string xmlDataFile, string xslTemplateFile, string outputFile)
        {
            try
            {
                //load data document
                XPathDocument myXPathDoc = new XPathDocument(xmlDataFile);

                //load template
                XslTransform myXslTrans = new XslTransform();
                myXslTrans.Load(xslTemplateFile);

                //create the output stream
                //XmlTextWriter myWriter = new XmlTextWriter(outputFile, null);
                XmlTextWriter myWriter = new XmlTextWriter(outputFile, Encoding.UTF8);

                //Transform!
                myXslTrans.Transform(myXPathDoc, null, myWriter);

                //ok, close writer
                myWriter.Close();
            }
            catch (Exception e)
            {
                //go on little exception! Spread the word!
                throw e;
            }

        }
    }
}
