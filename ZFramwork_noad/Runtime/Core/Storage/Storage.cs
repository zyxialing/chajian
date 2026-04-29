using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;
using System;

    public class Storage : Singleton<Storage>
   {
       private Dictionary<string, string> _AllFileData;

        private const string splitStr = "#";

        private const string endLine = "\r\n";

        private string filePath;

        private string filePathTmp;

        private const string fileName = "userdata.dat";

        private const string fileNameTmp = "userdataTmp.dat";

        private bool threadSaveFlag = false;

        private bool threadExitFlag = false;

        private object saveLock = new object();

        private const string dataStartKey = "AAAAAAA";

        private const string dataEndKey = "zzzzzzz";

        private const string dataStartKeyVal = "startData";

        private const string dataEndKeyVal = "endData";

        byte[] Encryptkey = new UTF8Encoding().GetBytes("goodjob");

        bool ifEncrypt = true;

        private Storage()
        {
            _AllFileData = new Dictionary<string, string>();

            filePath = Application.persistentDataPath + "/" + fileName;

            filePathTmp = Application.persistentDataPath + "/" + fileNameTmp;

            LoadFileData();

            Thread thread = new Thread(ThreadSaveRun);

            thread.Start();
        }

        void LoadFileData()
        {
            string dataStr = loadFileToString(filePath);
            if (dataStr == null)
            {
                _AllFileData.Clear();
                _AllFileData.Add(dataStartKey, dataStartKeyVal);             
                return;
            }   
            parseKeyAndValue(dataStr);
        }

        private string loadFileToString(string tmpPath)
        {
            if (File.Exists(tmpPath) == false)
            {
                ZLogUtil.LogError("loadFileToStrinig : NO FIEL:" + tmpPath);
                return null;
            }

            FileStream fs = new FileStream(tmpPath, FileMode.Open);
            byte[] bytes = new byte[fs.Length];
            fs.Read(bytes, 0, bytes.Length);
            fs.Close();
            fs.Dispose();

            Decrypt(bytes);

            return new UTF8Encoding().GetString(bytes);
        }

        void parseKeyAndValue(string dataStr)
        {
            string startKeyVal = dataStartKey + splitStr + dataStartKeyVal;
            string endKeyVal = dataEndKey + splitStr + dataEndKeyVal;
            if (dataStr.Contains(startKeyVal) && dataStr.Contains(endKeyVal))
            {
                string deleteEndKey = endLine + endKeyVal;
                string content = dataStr.Replace(deleteEndKey, "");
                string[] line = content.Split(new string[] { endLine }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string keyAndValue in line)
                {
                    string[] data = keyAndValue.Split(new string[] { splitStr }, StringSplitOptions.RemoveEmptyEntries);
                    if (data!=null&& data.Length>=2)
                   {
         
                    _AllFileData.Add(data[0], data[1]);
                   }
                    else
                    {
                      ZLogUtil.LogError("parseKeyAndValue key or value null");
                    }
                }
            }
            else
            {
                _AllFileData.Clear();
                _AllFileData.Add(dataStartKey, dataStartKeyVal);
            }
        }

        public int GetIntForKey(string key,int defValue = 0)
        {
            if (_AllFileData.ContainsKey(key))
            {
                int value = defValue;
                if(int.TryParse(_AllFileData[key], out value))
                {
                 return value;
                }
                else
                {
                 ZLogUtil.LogError("存储key被占用:" + key + "数据:" + _AllFileData[key]);
                 return defValue;
                }
            }
            else
            {
              return defValue;
            }
        }
        public string GetStringForKey(string key,string defValue ="")
       {
            if (_AllFileData.ContainsKey(key))
            {
              return (_AllFileData[key]);
            }
            else 
            {
              return defValue;
            }
        }
        public float  GetFloatForKey(string key,float defVaule = 0f)
        {
            if (_AllFileData.ContainsKey(key))
            {
                float value = defVaule;
                if (float.TryParse(_AllFileData[key], out value))
                {
                    return value;
                }
                else
                {
                    ZLogUtil.LogError("存储key被占用:" + key + "数据:" + _AllFileData[key]);
                    return defVaule;
                }
            }
            else
            {
                return defVaule;
            }
        }
        public bool GetBoolForKey(string key,bool defVaule = false)
        {
            if (_AllFileData.ContainsKey(key))
            {
                bool value = defVaule;
                if (bool.TryParse(_AllFileData[key], out value))
                {
                    return value;
                }
                else
                {
                    ZLogUtil.LogError("存储key被占用:" + key + "数据:" + _AllFileData[key]);
                    return defVaule;
                }
            }
            else
            {
                return defVaule;
            }
        }

        public void SetIntForKey(string key, int vaule = 0)
        {
            lock (saveLock)
            {
                _AllFileData[key] = vaule.ToString();
                threadSaveFlag = true;
            }

        }
        public void SetStringForKey(string key, string vaule = "")
        {
            lock (saveLock)
            {
                _AllFileData[key] = vaule;
                threadSaveFlag = true;
            }
        }
        public void SetFloatForKey(string key, float vaule = 0f)
        {
            lock (saveLock)
            {
                _AllFileData[key] = vaule.ToString();
                threadSaveFlag = true;
            }
        }
        public void SetBoolForKey(string key, bool vaule = false)
        {
            lock (saveLock)
            {
                _AllFileData[key] = vaule.ToString();
                threadSaveFlag = true;
            }
        }

        string SerializeToString(Dictionary<string, string> saveKeyAndVal)
        {
            if (saveKeyAndVal.Count == 0)
                return null;

            saveKeyAndVal[dataStartKey] = dataStartKeyVal;
            saveKeyAndVal.Add(dataEndKey,dataEndKeyVal);
      
            StringBuilder dataStr = new StringBuilder(512);
            int count = 0;
            int maxCount = saveKeyAndVal.Count;
            foreach (KeyValuePair<string, string> kv in saveKeyAndVal)
            {
                count++;
                dataStr.Append(kv.Key);
                dataStr.Append("#");
                dataStr.Append(kv.Value);
                if (count < maxCount)
                    dataStr.Append(endLine);
            }
            return dataStr.ToString();
        }

        private string createMD5Hash(string input)
        {
            MD5 md5 = MD5.Create();
            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("X2")); 
            }
            return sb.ToString();
        }

        private bool compareDataMD5(string orData)
        {
          string tmpData =  loadFileToString(filePathTmp);

            if (tmpData == null || orData == null)
                return false;

           string tmpMD5 = createMD5Hash(tmpData);
           string orMD5  = createMD5Hash(orData);

            if (tmpMD5 == orMD5)
                return true;

            return false;
        }

        public void SaveFileData(string originData)
        {
            FileStream fs = new FileStream(filePathTmp, FileMode.OpenOrCreate);
            byte[] bytes = new UTF8Encoding().GetBytes(originData);
            
            Encrypt(bytes);

            fs.Write(bytes, 0, bytes.Length);
            fs.Close();
            fs.Dispose();

            if (compareDataMD5(originData))
            {
                File.Copy(filePathTmp, filePath, true);
                File.Delete(filePathTmp);
            }
        }

        void ThreadSaveRun()
        {
            while (!threadExitFlag)
            {
                Dictionary<string, string> saveKeyAndVal = null;
                lock (saveLock)
                {
                    if (threadSaveFlag)
                    {
                        threadSaveFlag = false;
                        saveKeyAndVal = new Dictionary<string, string>(_AllFileData);                  
                    }
                }
                if (saveKeyAndVal != null)
                {
                    string originData = SerializeToString(saveKeyAndVal);
                    if (!string.IsNullOrEmpty(originData))
                        SaveFileData(originData);            
                }
                Thread.Sleep(500);
            }
        }

        byte[] Encrypt(byte[] data)
        {
            if (!ifEncrypt)
                return null;

            int index = 0;
            for (int i = 0; i < data.Length;i++)
            {                
               data[i] = (byte)(data[i] ^ Encryptkey[index]);
               index++;
               if (index >= Encryptkey.Length)
                    index = 0;
            }
            return data;
        }

        byte[] Decrypt(byte[] data)
        {
            if (!ifEncrypt)
                return null;

            int index = 0;
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(data[i] ^ Encryptkey[index]);
                index++;
                if (index >= Encryptkey.Length)
                    index = 0;
            }
            return data;
        }

        public void DeleteFile()
        {
            threadExitFlag = true;
            File.Delete(filePath);
        }

        public void DeleteForKey(string key)
        {
            lock (saveLock)
            {
                if (_AllFileData.ContainsKey(key))
                {
                    _AllFileData.Remove(key);
                    threadSaveFlag = true;
                }
            }
        }

}

