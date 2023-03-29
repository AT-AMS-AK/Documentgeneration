using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace Addit.AK.WBD.DocumentGeneration
{
    [ServiceContract]
    public interface IDocumentGeneration
    {
        [OperationContract]
        Response doPrint(string sessionToken, string user, string template, string printer, string bereich, string bezirk, string status, string ablehnung, string sort, string von, string bis, string ant_ikey, int wbd_bdl);

        [OperationContract]
        Response doSimplePrint(string sessionToken, string printer, string fileName);

        [OperationContract]
        Response doPrintWithValues(string sessionToken, string printer, string templateName, Dictionary<string, string> values, List<string> list);
    }


    [DataContract]
    public class Response
    {
        int responseCode = 0;
        string responseMsg = "";
        string exeptionMsg = "";

        [DataMember]
        public int ResponseCode
        {
            get { return responseCode; }
            set { responseCode = value; }
        }

        [DataMember]
        public string ResponseMsg
        {
            get { return responseMsg; }
            set { responseMsg = value; }
        }

        [DataMember]
        public string ExeptionMsg
        {
            get { return exeptionMsg; }
            set { exeptionMsg = value; }
        }
    }
}
