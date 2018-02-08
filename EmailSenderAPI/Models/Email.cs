using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmailSenderAPI.Models
{ 
    public class EmailObject
    {
        public string Recipient { get; set; }
        public string Cc { get; set; }
        public string Bcc { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
    }
}