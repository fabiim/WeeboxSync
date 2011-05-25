using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using Microsoft.Http;
using System.Net;
using System.Xml.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using Ionic.Zip;
using Microsoft.Http.Headers;
using System.IO;
namespace WeeboxSync {
    public class ConnectionNotSet : Exception {


    }
    public class UnpriviligedUser : Exception {


    }
    public class NoServerAccess : Exception {


    }

    public class CoreAbstraction {
        public HttpClient _client;
        private ConnectionInfo _conInfo; 
        private bool _connection=false;

        public void SetConnection(ConnectionInfo con) {
            this._conInfo = con;
                _client = new HttpClient(con.address.ToString() );
                _client.TransportSettings.Credentials = new NetworkCredential(con.user.user, con.user.pass); 
                //TODO test connection , test user credentials 
                //TODO set proxy if any 
                //TODO check possible exceptions in those methods ; 
                _connection = true; 
            if (con.useProxy){
//                _client.TransportSettings.Proxy = new WebProxy("proxy.uminho.pt:3128");
            }
        }

        /**
         * Returns null if no scheme is returned
         */
        public List<Scheme> getSchemesFromServer(){
            //TODO - ALOT , exceptions , null values , empty values, server errors , validate xml input in these and look a like methods
            HttpResponseMessage resp = null;
                bool bo = true;
                while (bo){
                    try{
                        resp = _client.Get("manager/api?operation=getThesauri");
                        bo = false;
                    }
                    catch (HttpStageProcessingException e){
                        continue;
                    }
                }

            resp.EnsureStatusIsSuccessful();
            String schemes = resp.Content.ReadAsString();
            String[] planos = schemes.Split('\n');
            List<Scheme> lista = new List<Scheme>();
            for (int i = 0; i < planos.Length; i++) {
                if (planos[i] != ""){
                    Scheme s = getScheme(planos[i]);
                    if (s != null ) { // TODO - necessary? 
                        lista.Add(s);
                    }
                }
            }
            return (lista.Count != 0) ? lista : null; 
        }

        public  Scheme getScheme(string rootID){
            //TODO - check every , see getSchemes comments
             HttpResponseMessage resp = null;
                bool bo = true;
                while (bo){
                    try{
                resp  = _client.Get("manager/api?operation=getThesaurus&thesaurus=" + rootID);
                        bo = false;
                    }
                    catch (HttpStageProcessingException e){
                        continue;
                    }
                }

            
            resp.EnsureStatusIsSuccessful();
            Stream s = resp.Content.ReadAsStream();
            // TODO - validate xml answer. 
            XDocument classifications = XDocument.Load(s);
            XElement rootElement = classifications.Root;

            //TODO - check to see if this will return always the value we want for the root. 
            XElement raiz = rootElement.Element(_getSkoName("ConceptScheme"));

            Tag rootTag = new Tag(raiz.Value, raiz.Value, "Q2W1C42bT6");
            Scheme scheme = new Scheme(s.ToString(), "Q2W1C42bT6", rootTag);
            //<id,label> 
            IEnumerable<Tuple<string, string>> lista = _getFirstLevelChilds(rootElement);
            foreach (Tuple<String, String> tup in lista){
                string myPath = rootTag.Path + "\\" + tup.Item2;
                Tag t = new Tag(tup.Item2, myPath, tup.Item1);
                scheme.arvore.add(t, rootTag.Path, t.Path);
                scheme.arvoreByWeeboxIds.add(t, rootTag.WeeId, t.WeeId);
                _buildSubTree(rootElement, t, scheme);
            }

            return scheme;
        }

        public List<Bundle> GetAllBundles() {
            throw new System.Exception("Not implemented");
        }

        /// <summary>
        /// Gets all Bundles specified as owned by the user established in this class
        /// </summary>
        /// <returns>Null if none present</returns>
        public IEnumerable<String> GetAllBundlesList(){
            HttpResponseMessage resp = null;
                bool bo = true;
                while (bo){
                    try{
                        resp =   _client.Get("core/bundle/?operation=searchRetrieve&version=1.1&query=bundle.owner+=+%22" +
                                this._conInfo.user.user + "%22");
                        bo = false;
                    }
                    catch (HttpStageProcessingException e){
                        continue;
                    }
                }

            
            resp.EnsureStatusIsSuccessful();
            Stream s = resp.Content.ReadAsStream();
            // TODO - validate xml answer. 
            XDocument classifications = XDocument.Load(s);
            XElement rootElement = classifications.Root;

            List<String> lista = new List<string>();
            foreach (XElement record in rootElement.Descendants(_getLocName("recordData"))){
                String ss = record.Value;
                string pattern = "(<entry key=\"bundle.id\">)(\\w*)(</entry>)";
                MatchCollection matches = Regex.Matches(ss, pattern );
                foreach (Match  matche in matches){
                    if (matche.Groups.Count >= 1){
                        Group group = matche.Groups[2];
                        lista.Add(group.Value);
                        Console.WriteLine(group.Value);
                    }
                }
            }
            if (lista.Count >= 0) return lista;  else return null; 

        }

        public MetaData GetMetaFromBundle(String bundleid){
            try{

                HttpResponseMessage resp = null;
                bool bo = true;
                while (bo){
                    try{
                    resp =
                        _client.Get("core/bundle/" + bundleid + "?operation=retrieveBundleMetadata");
                        bo = false;
                    }
                    catch (HttpStageProcessingException e){
                        continue;
                    }
                }

                
                
                Stream s = resp.Content.ReadAsStream(); 
                XDocument meta = XDocument.Load(s);
                XElement rootElement = meta.Root;

                MetaData metaData = new MetaData(); 
                foreach (XElement e in rootElement.Descendants()) {
                    //TODO - clean up the house, maybe read the attributes one by one into real values and types
                    if (!e.Value.Equals("")) metaData.keyValueData.Add(e.FirstAttribute.Value , e.Value); 
                }
                return metaData; 
            }
            catch (ArgumentOutOfRangeException ) {
                return null;
            }
        }

        public MetaData GetAllMetaFromBundle(String bundleid){
            try{
            HttpResponseMessage resp = null;
                bool bo = true;
                while (bo){
                    try{
                        resp = _client.Get("core/bundle/" + bundleid + "?operation=retrieveAllMetadata");
                        bo = false;
                    }
                    catch (HttpStageProcessingException e){
                        continue;
                    }
                }
            

                resp.EnsureStatusIsSuccessful();

                Stream s = resp.Content.ReadAsStream();
                XDocument meta = XDocument.Load(s);
                XElement rootElement = meta.Root;

                MetaData metaData = new MetaData();
                foreach (XElement e in rootElement.Descendants()){
                    //TODO - clean up the house, maybe read the attributes one by one into real values and types
                    if (!e.Value.Equals("")) metaData.keyValueData.Add(e.FirstAttribute.Value, e.Value);
                }
                return metaData; 
            }
            catch (ArgumentOutOfRangeException ) {
                return null;
            }
        }

        public Bundle getBundle(string bundleId, string downloadPath){
            Bundle toBeBundle = new Bundle {meta = GetAllMetaFromBundle(bundleId), weeId = bundleId};
            List<String> tags = new List<String>();
            if (toBeBundle.meta.keyValueData.ContainsKey("user.0")){
                string toParse = "";
                toBeBundle.meta.keyValueData.TryGetValue("user.0", out toParse);
                String[] strs = Regex.Split(toParse, ";;");
                foreach (String st in strs){
                    String[] tagss = Regex.Split(st, "::");
                    tags.Add(tagss.Last<String>().Trim());
                }
                toBeBundle.weeTags = tags;
            }

            HttpResponseMessage resp = null;
                bool bo = true;
                while (bo){
                    try{
                        resp = 
                            _client.Get("core/bundle/" + bundleId + "?encodeFileName=true");
                        bo = false;
                    }
                    catch (HttpStageProcessingException e){
                        continue;
                    }
                }


             // resp.EnsureStatusIsSuccessful();
            //has files?

            if (toBeBundle.meta.keyValueData.ContainsKey("bundle.data.files.id")){
                int num_files;
                string toParse = "";
                bool val = toBeBundle.meta.keyValueData.TryGetValue("bundle.data.files.id", out toParse);
                num_files = toParse.Count(p => p == ',') + 1;

                //zip case
                if (num_files > 1){
                    Stream files = resp.Content.ReadAsStream();
                    string path = downloadPath + "\\" + bundleId + ".zip";

                    if (File.Exists(path)){
                        File.Delete(path);
                    }

                    FileStream f = File.OpenWrite(path);
                    files.CopyTo(f);
                    f.Flush();
                    f.Close();

                    List<Ficheiro> ficheiros = new List<Ficheiro>();

                    using (ZipFile zip = ZipFile.Read(path)){
                        //TODO - what if files exists? 
                            zip.ExtractAll(downloadPath);
                        ficheiros.AddRange(
                            zip.EntryFileNames.Select(entrada => new Ficheiro(downloadPath + "\\" + entrada, bundleId, true)));
                    }
                    File.Delete(path);
                }
                else{
                    //normal file case. 

                    if (resp.Headers.ContainsKey("encoded.filename")){
                        String s = resp.Headers["encoded.filename"];
                        Stream files = resp.Content.ReadAsStream();
                        string path = downloadPath + "\\" + s;

                        if (File.Exists(path)){
                            File.Delete(path);
                        }


                        FileStream f = File.OpenWrite(path);
                        files.CopyTo(f);
                        f.Flush();
                        f.Close();
                        List<Ficheiro> ficheiros = new List<Ficheiro>();
                        ficheiros.Add(new Ficheiro(path, bundleId, true));
                    }
                    else{
                        Console.WriteLine(("could not read file name"));
                    }
                }
            }
            return toBeBundle;
        }


        public List<Ficheiro> GetFicheiros(String bundleid) {
            throw new System.Exception("Not implemented");
        }
        public void GetFicheiro(String Ficheiroid, String bundleid) {
            throw new System.Exception("Not implemented");
        }
        public void PutFicheiro(String bundleid, Ficheiro Ficheiro) {
            throw new System.Exception("Not implemented");
        }
        public void RmFicheiroFromServer(String bundleId, Ficheiro Ficheiro) {
            throw new System.Exception("Not implemented");
        }
        public bool HasNewVersion(String bundleId) {
            throw new System.Exception("Not implemented");
        }
        public Bundle GetLatestVersionFromServer(String bundleid) {
            throw new System.Exception("Not implemented");
        }

        private static String _getRDFName(String s) { return "{http://www.w3.org/1999/02/22-rdf-syntax-ns#}" + s; }
        private static String _getSkoName(String s) { return "{http://www.w3.org/2004/02/skos/core#}" + s; }
        private static String _getLocName(String s){
            return "{http://www.loc.gov/zing/srw/}" + s;
        }

        private  void _buildSubTree(XElement rootElement, Tag parent, Scheme scheme) {
            IEnumerable<Tuple<string, string>> childs = _getChilds(rootElement, parent.WeeId);
            foreach (Tuple<String, String> tuplo in childs) {
                string path = parent.Path + "\\" + tuplo.Item2; // children path
                Tag children = new Tag(tuplo.Item2, path, tuplo.Item1);
                scheme.arvore.add(children, parent.Path, children.Path);
                scheme.arvoreByWeeboxIds.add(children, parent.WeeId, children.WeeId); 
                _buildSubTree(rootElement, children,  scheme);
            }
        }

        /**
         * el must have the type Concept
         */
        private  Tuple<String, String> _getIdAndLabelFromTag(XElement el) {
            //let's get his id and 
            String id, label;
            label = el.Element(_getSkoName("prefLabel")).Value;
            id = el.Attribute(_getRDFName("about")).Value;
            return new Tuple<String, String>(id, label);
        }

        private  IEnumerable<Tuple<string, string>> _getFirstLevelChilds(XElement root) {
            List<Tuple<String, String>> lista = new List<Tuple<String, String>>();

            foreach (XElement elem in root.Descendants(_getSkoName("Concept"))) {
                if (elem.Element(_getSkoName("broader")) == null) {
                    lista.Add(_getIdAndLabelFromTag(elem));
                }
            }
            return lista;
        }


        private  IEnumerable<Tuple<string, string>> _getChilds(XElement root, string parent) {
            List<Tuple<String, String>> lista = new List<Tuple<String, String>>();
            foreach (XElement con in root.Descendants(_getSkoName("Concept"))) {
                foreach(XElement elem in con.Descendants(_getSkoName("broader"))){
                    foreach (XAttribute x in elem.Attributes()){
                        if (x.Value.Equals(parent) ){
                            if (x.Name.LocalName.Equals("resource")) {
                                lista.Add(_getIdAndLabelFromTag(con)); 
                            }
                        }
                    }
                }
            }
           return lista;
        }

    }
}

