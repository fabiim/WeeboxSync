using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace WeeboxSync {

    public class Bundle {
        public string weeId { get; set; }
        public string localId { get; set;  }
        public List<String> weeTags { get; set; }
        public MetaData meta { get ; set ; }
        public IEnumerable<Ficheiro> filesPath { get; set;  }

        public Bundle() {
            this.weeId = "";
            this.localId = "";
            this.weeTags= new List<string>();
            this.meta = new MetaData();
            //this.filesPath = new IEnumerable<Ficheiro>();
        }
        /*public Bundle() { 
        
        }*/

        

    }

}