using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace Addit.AK.WBD.DocumentGeneration
{
    /// <summary>
    /// Author: Bruno Hautzenberger
    /// Creation Date: 12.2010
    /// Implements functions to load XSL Templates and automatically convert RTF to XSL Template
    /// </summary>
    class TemplateLibrary
    {
        #region Singleton

        /// <summary>
        /// the singleton instance of this object
        /// </summary>
        private static TemplateLibrary instance;

        /// <summary>
        /// private constructor to avoid multiple instances
        /// </summary>
        private TemplateLibrary() { }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// returns the singleton instance of this object
        /// </summary>
        /// <returns>returns the singleton instance of this object</returns>
        public static TemplateLibrary getInstance()
        {
            if (instance == null)
                instance = new TemplateLibrary();

            return instance;
        }

        #endregion

        #region private members

        /// <summary>
        /// all templates that have allready been converted
        /// </summary>
        private List<Template> templates;

        /// <summary>
        /// path to rtf templates
        /// </summary>
        private string importPath;

        /// <summary>
        /// path to xsl templates
        /// </summary>
        private string templatePath;

        /// <summary>
        /// path to cache file
        /// </summary>
        private string cacheFile;

        #endregion

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// initializes the templatelibrary with all needed values
        /// </summary>
        /// <param name="importPath">path to rtf templates</param>
        /// <param name="templatePath">path to xsl templates</param>
        /// <param name="cacheFile">path to cachefile</param>
        public void loadLibrary(string importPath, string templatePath, string cacheFile)
        {
            if (!importPath.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
            {
                this.importPath = importPath + System.IO.Path.DirectorySeparatorChar;
            }
            else
            {
                this.importPath = importPath;
            }

            if (!templatePath.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
            {
                this.templatePath = templatePath + System.IO.Path.DirectorySeparatorChar;
            }
            else
            {
                this.templatePath = templatePath;
            }
            
            this.cacheFile = cacheFile;

            loadCache();
        }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// loads a template by its name (without extension)
        /// </summary>
        /// <param name="name">name of the template (without extension)</param>
        /// <returns>a template object</returns>
        public Template getTemplate(string name)
        {
            Template template;

            if (tryGetCachedTemplate(name, out template)) //try to load template from cache (List) 
            {
                if (template.checkSum == getMD5HashFromFile(importPath + name + ".rtf")) //check if template is still up to date
                {
                    return template;
                }
                else //template is outdated so we regenerate it
                {
                    string rtfContent = readFileContent(importPath + template.name + ".rtf");
                    string xslContent = XSLProcessor.rtfToXsl(rtfContent,template);

                    template.checkSum = getMD5HashFromFile(importPath + name + ".rtf");

                    writeFile(xslContent, templatePath + template.name);
                    saveCache();

                    return template;
                }
            }
            else //template not in cache. convert it and add it to cache
            {
                template = new Template(name, "NOT SET", templatePath + name + ".xml", null);

                string rtfContent = readFileContent(importPath + name + ".rtf");
                string xslContent = XSLProcessor.rtfToXsl(rtfContent, template);

                template.checkSum = getMD5HashFromFile(importPath + name + ".rtf");

                writeFile(xslContent, templatePath + name + ".xml");

                templates.Add(template);
                
                saveCache();

                return template;
            }
        }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// loads all templates saved ijn cache file
        /// </summary>
        private void loadCache()
        {
            templates = new List<Template>();

            if (System.IO.File.Exists(cacheFile))
            {
                TextReader tr = new StreamReader(cacheFile);

                System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(templates.GetType());
                templates = (List<Template>) x.Deserialize(tr);

                tr.Close();
            }
        }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// saves all converted templates to cache file
        /// </summary>
        private void saveCache()
        {
            TextWriter tw = new StreamWriter(cacheFile,false);

            System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(templates.GetType());
            x.Serialize(tw,templates);

            tw.Flush();
            tw.Close();
        }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// tries to load a template from cache
        /// </summary>
        /// <param name="name">name of the template (without extension)</param>
        /// <param name="template">out template - the loaded template</param>
        /// <returns>true if template exists in cache, false if not</returns>
        private bool tryGetCachedTemplate(string name,out Template template)
        {
            try
            {
                template = templates.First<Template>(a => a.name == name);
                return true;
            }
            catch
            {
                template = null;
                return false;
            }
        }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// reads a file's content
        /// </summary>
        /// <param name="path">path to file</param>
        /// <returns>the file content as string</returns>
        private string readFileContent(string path)
        {
            TextReader tr = null;
            String content = String.Empty;

            try{
                tr = new StreamReader(path);
                content = tr.ReadToEnd();
            }
            catch(Exception ex) 
            {
                throw ex;
            } 
            finally 
            {
                if(tr != null)
                {
                    tr.Close();
                }
            }

            return content;
        }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// writes something to a file
        /// </summary>
        /// <param name="content">the file's content</param>
        /// <param name="path">path to file</param>
        private void writeFile(string content, string path)
        {
            TextWriter tw = new StreamWriter(path);

            tw.Write(content);

            tw.Close();
        }

        /// <summary>
        /// Author: Bruno Hautzenberger
        /// Creation Date: 12.2010
        /// generates the MD5 Checksum of a file
        /// </summary>
        /// <param name="path">path to file</param>
        /// <returns>the MD5 Checksum of the given file</returns>
        private string getMD5HashFromFile(string path)
        {
            FileStream file = new FileStream(path, FileMode.Open);
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] retVal = md5.ComputeHash(file);
            file.Close();

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < retVal.Length; i++)
            {
                sb.Append(retVal[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
