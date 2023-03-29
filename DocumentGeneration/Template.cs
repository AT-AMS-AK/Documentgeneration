using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Addit.AK.WBD.DocumentGeneration
{
    public class Template
    {
        #region public members

        public string name;
        public string checkSum;
        public string path;

        public List<String> datafileds;

        #endregion

        public Template() { }

        public Template(string name, string checkSum, string path, List<String> datafileds)
        {
            this.name = name;
            this.checkSum = checkSum;
            this.path = path;
            this.datafileds = datafileds;
        }
    }
}
