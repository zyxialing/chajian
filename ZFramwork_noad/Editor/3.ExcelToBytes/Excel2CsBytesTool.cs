using System.IO;
using Excel;
using System.Data;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

/// <summary>
/// Excel生成bytes和cs工具
/// </summary>
public class Excel2CsBytesTool
{
    static string ExcelDataPath = Application.dataPath + "/../ExcelData";//源Excel文件夹,xlsx格式
    static string BytesDataPath = "Assets/Game/AssetDynamic/ConfigBytes";//生成的bytes文件夹
    static string CsClassPath = "Assets/Game/Scripts/ConfigClass";//生成的c#脚本文件夹
    static string XmlDataPath = ExcelDataPath + "/tempXmlData";//生成的xml(临时)文件夹..
    static string AllCsHead = "all";//序列化结构体的数组类.类名前缀
    static string CsExcelMgrPath = "Assets/Game/Scripts/ConfigClass/Excel";//生成的bytes文件夹


    public static char ArrayTypeSplitChar = '-';//数组类型值拆分符: int[] 1#2#34 string[] 你好#再见 bool[] true#false ...
    static bool IsDeleteXmlInFinish = false;//生成bytes后是否删除中间文件xml
    [MenuItem("ZFramework/Editor/3.Excel/转Csharp")]
    static void Excel2Cs()
    {
        if (Directory.Exists(CsClassPath))
        {
            Directory.Delete(CsClassPath, true);
        }
        Directory.CreateDirectory(CsClassPath);
        if (Directory.Exists(XmlDataPath))
        {
            Directory.Delete(XmlDataPath, true);
        }


        AssetDatabase.Refresh();
        Directory.CreateDirectory(XmlDataPath);
        Excel2CsOrXml(true);
   
    }
    [MenuItem("ZFramework/Editor/3.Excel/Bind")]
    static void BindExcelMgr()
    {
        if (!Directory.Exists(CsExcelMgrPath))
        {
            Directory.CreateDirectory(CsExcelMgrPath);
        }
        ExcelMgrCs();
    }
    [MenuItem("ZFramework/Editor/3.Excel/转Bytes(等待上一步编译后)")]
    static void Excel2Xml2Bytes()
    {
        if (Directory.Exists(BytesDataPath))
        {
            Directory.Delete(BytesDataPath, true);
        }
        Directory.CreateDirectory(BytesDataPath);


        AssetDatabase.Refresh();
        //生成中间文件xml
        Excel2CsOrXml(false);
        //生成bytes
        WriteBytes();
        AssetDatabase.Refresh();
    }

    static void Init()
    {

    }

    static void WriteCs(string className, string[] names, string[] types, string[] descs)
    {
        try
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("using System;");
            stringBuilder.AppendLine("using System.Collections.Generic;");
            stringBuilder.AppendLine("using System.IO;");
            stringBuilder.AppendLine("using System.Runtime.Serialization.Formatters.Binary;");
            stringBuilder.AppendLine("using System.Xml.Serialization;");
            stringBuilder.AppendLine("using UnityEngine;");
            stringBuilder.AppendLine("using ZFramework;");
            stringBuilder.Append("\n");
            stringBuilder.Append("namespace Table {\n");
            stringBuilder.AppendLine("    [Serializable]");
            stringBuilder.AppendLine("    public class " + className+ " : IConfig");
            stringBuilder.AppendLine("    {");
            for (int i = 0; i < names.Length; i++)
            {
                descs[i] = descs[i].Replace("\n", "\n        ///");
                stringBuilder.AppendLine("        /// <summary>");
                stringBuilder.AppendLine("        /// " + descs[i]);
                stringBuilder.AppendLine("        /// </summary>");

                string type = types[i];
                switch (type)
                {
                    case "int":
                        AppendInt(stringBuilder,names[i],type);
                        break;
                    case "long":
                        AppendLong(stringBuilder, names[i], type);
                        break;
                    case "float":
                        AppendFloat(stringBuilder, names[i], type);
                        break;
                    case "bool":
                        AppendBool(stringBuilder, names[i], type);
                        break;
                    case "string":
                        AppendString(stringBuilder, names[i], type);
                        break;
                    case "serv2":
                        AppendSerVector2(stringBuilder, names[i], type);
                        break;
                    case "serv3":
                        AppendSerVector3(stringBuilder, names[i], type);
                        break;
                    case "serv4":
                        AppendSerVector4(stringBuilder, names[i], type);
                        break;
                    case "stringArray":
                        AppendstringArray(stringBuilder, names[i], type);
                        break;
                    case "intArray":
                        AppendintArray(stringBuilder, names[i], type);
                        break;
                    case "floatArray":
                        AppendfloatArray(stringBuilder, names[i], type);
                        break;
                    case "boolArray":
                        AppendboolArray(stringBuilder, names[i], type);
                        break;
                    case "longArray":
                        AppendlongArray(stringBuilder, names[i], type);
                        break;

                }
                stringBuilder.Append("\n");
            }
            stringBuilder.AppendLine($"        public List<T> LoadBytes<T>()");
            stringBuilder.AppendLine("        {");
            stringBuilder.AppendLine($"            string bytesPath = \"Assets/Game/AssetDynamic/ConfigBytes/{className}\";");
            stringBuilder.AppendLine("             TextAsset asset = TextAssetUtils.GetTextAsset(bytesPath);");
            stringBuilder.AppendLine($"            List<T> {className}s = DeserializeData<T>(asset);");
            stringBuilder.AppendLine($"            return {className}s;");
            stringBuilder.AppendLine("        }");
            stringBuilder.AppendLine("");
            stringBuilder.AppendLine($"         private List<T> DeserializeData<T>(UnityEngine.TextAsset textAsset)");
            stringBuilder.AppendLine("        {");
            stringBuilder.AppendLine("            using (MemoryStream ms = new MemoryStream(textAsset.bytes))");
            stringBuilder.AppendLine("            {");
            stringBuilder.AppendLine("                BinaryFormatter formatter = new BinaryFormatter();");
            stringBuilder.AppendLine($"                all{className} table = formatter.Deserialize(ms) as all{className};");
            stringBuilder.AppendLine($"                Config<{className}>.AddExcelToDic(typeof(T).Name, table.{className}s);");
            stringBuilder.AppendLine($"                return table.{className}s  as List<T>;");
            stringBuilder.AppendLine("            }");
            stringBuilder.AppendLine("        }");
            stringBuilder.AppendLine("    }");
            stringBuilder.Append("\n");
            stringBuilder.AppendLine("    [Serializable]");
            stringBuilder.AppendLine("    public class " + AllCsHead + className);
            stringBuilder.AppendLine("    {");
            stringBuilder.AppendLine("        public List<" + className + "> " + className + "s;");
            stringBuilder.AppendLine("    }");
            stringBuilder.AppendLine("}");

            string csPath = CsClassPath + "/" + className + ".cs";
            if (File.Exists(csPath))
            {
                File.Delete(csPath);
            }
            using (StreamWriter sw = new StreamWriter(csPath))
            {
                sw.Write(stringBuilder);
                Debug.Log("生成:" + csPath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("写入CS失败:" + e.Message);
            throw;
        }
    }

    static void ExcelMgrCs()
    {
        string[] excelPaths = Directory.GetFiles(CsClassPath, "*.cs");
        List<string> csNames = new List<string>();
        foreach (var item in excelPaths)
        {
            csNames.Add(new DirectoryInfo(item).Name.Replace(".cs", ""));
        }


        try
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("//脚本自动生成，不要进行修改");
            stringBuilder.AppendLine("using System.Collections.Generic;");
            stringBuilder.AppendLine("using Table;");
            stringBuilder.Append("\n");
            stringBuilder.AppendLine("public class ExcelData");
            stringBuilder.AppendLine("{");
            foreach (var item in csNames)
            {
                stringBuilder.AppendLine($"    public Dictionary<int, {item}> {item}Map = new Dictionary<int, {item}>();");
                stringBuilder.AppendLine($"    public List<{item}> {item}List = new List<{item}>();");
            }

            stringBuilder.Append("\n");
            stringBuilder.AppendLine("    public void InitData<T>(List<T> list)");
            stringBuilder.AppendLine("    {");
            stringBuilder.AppendLine("        switch (typeof(T).Name)");
            stringBuilder.AppendLine("        {");
            foreach (var item in csNames)
            {
                stringBuilder.AppendLine($"            case \"{item}\":");
                stringBuilder.AppendLine($"                {item}List = list as List<{item}>;");
                stringBuilder.AppendLine($"                for (int i = 0; i < {item}List.Count; i++)");
                stringBuilder.AppendLine("                {");
                stringBuilder.AppendLine($"                    {item}Map.Add({item}List[i].id, {item}List[i]);");
                stringBuilder.AppendLine("                }");
                stringBuilder.AppendLine("                break;");
            }
            stringBuilder.AppendLine("        }");
            stringBuilder.AppendLine("    }");
            stringBuilder.AppendLine("}");

            string csPath = CsExcelMgrPath + "/ExcelData.cs";
            if (File.Exists(csPath))
            {
                File.Delete(csPath);
            }
            using (StreamWriter sw = new StreamWriter(csPath))
            {
                sw.Write(stringBuilder);
                Debug.Log("生成:" + csPath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("写入CS失败:" + e.Message);
            throw;
        }

        try
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("//脚本自动生成，不要进行修改");
            stringBuilder.AppendLine("using UnityEngine;");
            stringBuilder.AppendLine("using Table;");
            stringBuilder.Append("\n");
            stringBuilder.AppendLine("public class ExcelLoader : MonoBehaviour");
            stringBuilder.AppendLine("{");
            stringBuilder.AppendLine("    public void LoadExcel()");
            stringBuilder.AppendLine("    {");
            foreach (var item in csNames)
            {
                stringBuilder.AppendLine($"        ExcelConfig.Instance.InitExcelData(Config<{item}>.InitConfig());");
            }
            stringBuilder.AppendLine("    }");
            stringBuilder.AppendLine("}");

            string csPath = CsExcelMgrPath + "/ExcelLoader.cs";
            if (File.Exists(csPath))
            {
                File.Delete(csPath);
            }
            using (StreamWriter sw = new StreamWriter(csPath))
            {
                sw.Write(stringBuilder);
                Debug.Log("生成:" + csPath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("写入CS失败:" + e.Message);
            throw;
        }

        try
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("//脚本自动生成，不要进行修改");
            stringBuilder.AppendLine("using System.Collections.Generic;");
            stringBuilder.AppendLine("using UnityEngine;");
            stringBuilder.AppendLine("using Table;");
            stringBuilder.Append("\n");
            stringBuilder.AppendLine("public class ExcelConfig : Singleton<ExcelConfig>");
            stringBuilder.AppendLine("{");
            stringBuilder.AppendLine("    ExcelData excelData = new ExcelData();");
            stringBuilder.AppendLine("    private ExcelConfig()");
            stringBuilder.AppendLine("    {");
            stringBuilder.AppendLine("    }");
            stringBuilder.Append("\n");
            stringBuilder.AppendLine("    public ExcelData GetExcelData()");
            stringBuilder.AppendLine("    {");
            stringBuilder.AppendLine("        return excelData;");
            stringBuilder.AppendLine("    }");
            stringBuilder.Append("\n");
            stringBuilder.AppendLine("    public void InitExcelData<T>(List<T> list)");
            stringBuilder.AppendLine("    {");
            stringBuilder.AppendLine("        excelData.InitData(list);");
            stringBuilder.AppendLine("    }");
            stringBuilder.Append("\n");
            stringBuilder.AppendLine("    public void LoadAllExcel()");
            stringBuilder.AppendLine("    {");
            stringBuilder.AppendLine("        ExcelLoader excelLoader = new GameObject(\"ExcelLoader\").AddComponent<ExcelLoader>();");
            stringBuilder.AppendLine("        excelLoader.LoadExcel();");
            stringBuilder.AppendLine("    }");
            stringBuilder.Append("\n");
            stringBuilder.AppendLine("    #region 外部调用");
            foreach (var item in csNames)
            {
                stringBuilder.Append($"    public static {item} Get_{item}(int id)");
                stringBuilder.AppendLine("    {");
                stringBuilder.AppendLine($"      return  Instance.GetExcelData().{item}Map[id];");
                stringBuilder.AppendLine("    }");
            }
            stringBuilder.AppendLine("    #endregion");
            stringBuilder.Append("}");



            string csPath = CsExcelMgrPath + "/ExcelConfig.cs";
            if (File.Exists(csPath))
            {
                File.Delete(csPath);
            }
            using (StreamWriter sw = new StreamWriter(csPath))
            {
                sw.Write(stringBuilder);
                Debug.Log("生成:" + csPath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("写入CS失败:" + e.Message);
            throw;
        }
        AssetDatabase.Refresh();
    }

    static void WriteXml(string className, string[] names, string[] types, List<string[]> datasList)
    {
        try
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            stringBuilder.AppendLine("<" + AllCsHead + className + ">");
            stringBuilder.AppendLine("<" + className + "s>");
            for (int d = 0; d < datasList.Count; d++)
            {
                stringBuilder.Append("\t<" + className + " ");
                //单行数据
                string[] datas = datasList[d];
                //填充属性节点
                for (int c = 0; c < datas.Length; c++)
                {
                    string type = types[c];
                    if (!type.Contains("[]"))
                    {
                        string name = names[c];
                        string value = datas[c];
                        stringBuilder.Append(name + "=\"" + value + "\"" + (c == datas.Length - 1 ? "" : " "));
                    }
                }
                stringBuilder.Append(">\n");
                //填充子元素节点(数组类型字段)
                for (int c = 0; c < datas.Length; c++)
                {
                    string type = types[c];
                    if (type.Contains("[]"))
                    {
                        string name = names[c];
                        string value = datas[c];
                        string[] values = value.Split(ArrayTypeSplitChar);
                        stringBuilder.AppendLine("\t\t<" + name + ">");
                        for (int v = 0; v < values.Length; v++)
                        {
                            stringBuilder.AppendLine("\t\t\t<item>" + values[v] + "</item>");
                        }
                        stringBuilder.AppendLine("\t\t</" + name + ">");
                    }
                }
                stringBuilder.AppendLine("\t</" + className + ">");
            }
            stringBuilder.AppendLine("</" + className + "s>");
            stringBuilder.AppendLine("</" + AllCsHead + className + ">");

            string xmlPath = XmlDataPath + "/" + className + ".xml";
            if (File.Exists(xmlPath))
            {
                File.Delete(xmlPath);
            }
            using (StreamWriter sw = new StreamWriter(xmlPath))
            {
                sw.Write(stringBuilder);
                Debug.Log("生成文件:" + xmlPath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("写入Xml失败:" + e.Message);
        }
    }

    static void Excel2CsOrXml(bool isCs)
    {
        if (!Directory.Exists(ExcelDataPath))
        {
            Directory.CreateDirectory(ExcelDataPath);
        }
        string[] excelPaths = Directory.GetFiles(ExcelDataPath, "*.xlsx");
        if (excelPaths.Length == 0)
        {
            ZLogUtil.LogError(ExcelDataPath + ":不存在任何表");
        }
        for (int e = 0; e < excelPaths.Length; e++)
        {
            //0.读Excel
            string className;//类型名
            string[] names;//字段名
            string[] types;//字段类型
            string[] descs;//字段描述
            List<string[]> datasList;//数据

            try
            {
                string excelPath = excelPaths[e];//excel路径  
                className = Path.GetFileNameWithoutExtension(excelPath).ToLower();
                FileStream fileStream = File.Open(excelPath, FileMode.Open, FileAccess.Read);
                IExcelDataReader excelDataReader = ExcelReaderFactory.CreateOpenXmlReader(fileStream);
                // 表格数据全部读取到result里
                DataSet result = excelDataReader.AsDataSet();
                // 获取表格列数
                int columns = result.Tables[0].Columns.Count;
                // 获取表格行数
                int rows = result.Tables[0].Rows.Count;
                // 根据行列依次读取表格中的每个数据
                names = new string[columns];
                types = new string[columns];
                descs = new string[columns];
                datasList = new List<string[]>();
                for (int r = 0; r < rows; r++)
                {
                    string[] curRowData = new string[columns];
                    for (int c = 0; c < columns; c++)
                    {
                        //解析：获取第一个表格中指定行指定列的数据
                        string value = result.Tables[0].Rows[r][c].ToString();

                        //清除前两行的变量名、变量类型 首尾空格
                        if (r < 2)
                        {
                            value = value.TrimStart(' ').TrimEnd(' ');
                        }

                        curRowData[c] = value;
                    }
                    //解析：第一行类变量名
                    if (r == 0)
                    {
                        names = curRowData;
                    }//解析：第二行类变量类型
                    else if (r == 1)
                    {
                        types = curRowData;
                    }//解析：第三行类变量描述
                    else if (r == 2)
                    {
                        descs = curRowData;
                    }//解析：第三行开始是数据
                    else
                    {
                        datasList.Add(curRowData);
                    }
                }
            }
            catch (System.Exception exc)
            {
                Debug.LogError("请关闭Excel:" + exc.Message);
                return;
            }

            if (isCs)
            {
                //写Cs
                WriteCs(className, names, types, descs);
            }
            else
            {
                //写Xml
                WriteXml(className, names, types, datasList);
            }
        }

    }

    static void WriteBytes()
    {
        string csAssemblyPath = Application.dataPath + "/../Library/ScriptAssemblies/Assembly-CSharp.dll";
        Assembly assembly = Assembly.LoadFile(csAssemblyPath);
        if (assembly != null)
        {
            Type[] types = assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type.Namespace == "Table" && type.Name.Contains(AllCsHead))
                {
                    string className = type.Name.Replace(AllCsHead, "");

                    //读取xml数据
                    string xmlPath = XmlDataPath + "/" + className + ".xml";
                    if (!File.Exists(xmlPath))
                    {
                        Debug.LogError("Xml文件读取失败:" + xmlPath);
                        continue;
                    }
                    object table;
                    using (TextReader reader = new StreamReader(xmlPath))
                    {
                        //读取xml实例化table: all+classname
                        //object table = assembly.CreateInstance("Table." + type.Name);
                        ZLogUtil.Log(className);
                        XmlSerializer xmlSerializer = new XmlSerializer(type);
                        table = xmlSerializer.Deserialize(reader);
                    }
                    
                    //obj序列化二进制
                    string bytesPath = BytesDataPath + "/" + className + ".bytes";
                    if (File.Exists(bytesPath))
                    {
                        File.Delete(bytesPath);
                    }
                    using (FileStream fileStream = new FileStream(bytesPath, FileMode.Create))
                    {
                        BinaryFormatter binaryFormatter = new BinaryFormatter();
                        binaryFormatter.Serialize(fileStream, table);
                        Debug.Log("生成:" + bytesPath);
                    }

                    if (IsDeleteXmlInFinish)
                    {
                        File.Delete(xmlPath);
                        Debug.Log("删除:" + bytesPath);
                    }
                }
            }
        }

        if (IsDeleteXmlInFinish)
        {
            Directory.Delete(XmlDataPath);
            Debug.Log("删除:" + XmlDataPath);
        }
    }

    static void AppendInt(StringBuilder stringBuilder, string name,string type)
    {
        stringBuilder.AppendLine($"        [XmlIgnore]");
        stringBuilder.AppendLine($"        public int {name};");
        stringBuilder.AppendLine($"        [XmlAttribute(\"{name}\")]");
        stringBuilder.AppendLine($"        public string _{name} {{");
        stringBuilder.AppendLine($"            get {{ return {name}.ToString(); }}");
        stringBuilder.AppendLine($"            set {{ if (string.IsNullOrEmpty(value)) {name} = 0; else {name} = int.Parse(value); }}");
        stringBuilder.AppendLine($"        }}");
    }
    static void AppendLong(StringBuilder stringBuilder, string name,string type)
    {
        stringBuilder.AppendLine($"        [XmlIgnore]");
        stringBuilder.AppendLine($"        public long {name};");
        stringBuilder.AppendLine($"        [XmlAttribute(\"{name}\")]");
        stringBuilder.AppendLine($"        public string _{name} {{");
        stringBuilder.AppendLine($"            get {{ return {name}.ToString(); }}");
        stringBuilder.AppendLine($"            set {{ if (string.IsNullOrEmpty(value)) {name} = 0; else {name} = long.Parse(value); }}");
        stringBuilder.AppendLine($"        }}");
    }
    static void AppendFloat(StringBuilder stringBuilder, string name,string type)
    {
        stringBuilder.AppendLine($"        [XmlIgnore]");
        stringBuilder.AppendLine($"        public float {name};");
        stringBuilder.AppendLine($"        [XmlAttribute(\"{name}\")]");
        stringBuilder.AppendLine($"        public string _{name} {{");
        stringBuilder.AppendLine($"            get {{ return {name}.ToString(); }}");
        stringBuilder.AppendLine($"            set {{ if (string.IsNullOrEmpty(value)) {name} = 0; else {name} = float.Parse(value); }}");
        stringBuilder.AppendLine($"        }}");
    }
    static void AppendBool(StringBuilder stringBuilder, string name,string type)
    {
        stringBuilder.AppendLine($"        [XmlIgnore]");
        stringBuilder.AppendLine($"        public bool {name};");
        stringBuilder.AppendLine($"        [XmlAttribute(\"{name}\")]");
        stringBuilder.AppendLine($"        public string _{name} {{");
        stringBuilder.AppendLine($"            get {{ return {name}.ToString(); }}");
        stringBuilder.AppendLine($"            set {{ if (string.IsNullOrEmpty(value)) {name} = false; else {name} = bool.Parse(value); }}");
        stringBuilder.AppendLine($"        }}");
    }
    static void AppendString(StringBuilder stringBuilder, string name,string type)
    {
        stringBuilder.AppendLine($"        [XmlIgnore]");
        stringBuilder.AppendLine($"        public string {name};");
        stringBuilder.AppendLine($"        [XmlAttribute(\"{name}\")]");
        stringBuilder.AppendLine($"        public string _{name} {{");
        stringBuilder.AppendLine($"            get {{ return {name}.ToString(); }}");
        stringBuilder.AppendLine($"            set {{ if (string.IsNullOrEmpty(value)) {name} = \"\"; else {name} = value; }}");
        stringBuilder.AppendLine($"        }}");
    }
    static void AppendSerVector2(StringBuilder stringBuilder, string name, string type)
    {
        stringBuilder.AppendLine($"        [XmlIgnore]");
        stringBuilder.AppendLine($"        public SerV2 {name};");
        stringBuilder.AppendLine($"        [XmlAttribute(\"{name}\")]");
        stringBuilder.AppendLine($"        public string _{name} {{");
        stringBuilder.AppendLine($"            get {{ return {name}.ToString(); }}");
        stringBuilder.AppendLine($"            set {{ if (string.IsNullOrEmpty(value)) {name} = new SerV2(); else {name} = ZStringUtil.StringToSerV2(value); }}");
        stringBuilder.AppendLine($"        }}");
    }
    static void AppendSerVector3(StringBuilder stringBuilder, string name, string type)
    {
        stringBuilder.AppendLine($"        [XmlIgnore]");
        stringBuilder.AppendLine($"        public SerV3 {name};");
        stringBuilder.AppendLine($"        [XmlAttribute(\"{name}\")]");
        stringBuilder.AppendLine($"        public string _{name} {{");
        stringBuilder.AppendLine($"            get {{ return {name}.ToString(); }}");
        stringBuilder.AppendLine($"            set {{ if (string.IsNullOrEmpty(value)) {name} = new SerV3(); else {name} = ZStringUtil.StringToSerV3(value); }}");
        stringBuilder.AppendLine($"        }}");
    }
    static void AppendSerVector4(StringBuilder stringBuilder, string name, string type)
    {
        stringBuilder.AppendLine($"        [XmlIgnore]");
        stringBuilder.AppendLine($"        public SerV4 {name};");
        stringBuilder.AppendLine($"        [XmlAttribute(\"{name}\")]");
        stringBuilder.AppendLine($"        public string _{name} {{");
        stringBuilder.AppendLine($"            get {{ return {name}.ToString(); }}");
        stringBuilder.AppendLine($"            set {{ if (string.IsNullOrEmpty(value)) {name} = new SerV4(); else {name} = ZStringUtil.StringToSerV4(value); }}");
        stringBuilder.AppendLine($"        }}");
    }
    static void AppendstringArray(StringBuilder stringBuilder, string name, string type)
    {
        stringBuilder.AppendLine($"        [XmlIgnore]");
        stringBuilder.AppendLine($"        public List<string> {name};");
        stringBuilder.AppendLine($"        [XmlAttribute(\"{name}\")]");
        stringBuilder.AppendLine($"        public string _{name} {{");
        stringBuilder.AppendLine($"            get {{ return {name}.ToString(); }}");
        stringBuilder.AppendLine($"            set{{ if (string.IsNullOrEmpty(value)) {name} = new List<string>();else {name} = ZStringUtil.ArrayStringToList(value.Split('{ArrayTypeSplitChar}'));}}");
        stringBuilder.AppendLine($"        }}");
    }
    static void AppendintArray(StringBuilder stringBuilder, string name, string type)
    {
        stringBuilder.AppendLine($"        [XmlIgnore]");
        stringBuilder.AppendLine($"        public List<int> {name};");
        stringBuilder.AppendLine($"        [XmlAttribute(\"{name}\")]");
        stringBuilder.AppendLine($"        public string _{name} {{");
        stringBuilder.AppendLine($"            get {{ return {name}.ToString(); }}");
        stringBuilder.AppendLine($"            set{{ if (string.IsNullOrEmpty(value)) {name} = new List<int>();else {name} = ZStringUtil.ArrayStringToIntList(value.Split('{ArrayTypeSplitChar}'));}}");
        stringBuilder.AppendLine($"        }}");
    }
    static void AppendfloatArray(StringBuilder stringBuilder, string name, string type)
    {
        stringBuilder.AppendLine($"        [XmlIgnore]");
        stringBuilder.AppendLine($"        public List<float> {name};");
        stringBuilder.AppendLine($"        [XmlAttribute(\"{name}\")]");
        stringBuilder.AppendLine($"        public string _{name} {{");
        stringBuilder.AppendLine($"            get {{ return {name}.ToString(); }}");
        stringBuilder.AppendLine($"            set{{ if (string.IsNullOrEmpty(value)) {name} = new List<float>();else {name} = ZStringUtil.ArrayStringToFloatList(value.Split('{ArrayTypeSplitChar}'));}}");
        stringBuilder.AppendLine($"        }}");
    }
    static void AppendboolArray(StringBuilder stringBuilder, string name, string type)
    {
        stringBuilder.AppendLine($"        [XmlIgnore]");
        stringBuilder.AppendLine($"        public List<bool> {name};");
        stringBuilder.AppendLine($"        [XmlAttribute(\"{name}\")]");
        stringBuilder.AppendLine($"        public string _{name} {{");
        stringBuilder.AppendLine($"            get {{ return {name}.ToString(); }}");
        stringBuilder.AppendLine($"            set{{ if (string.IsNullOrEmpty(value)) {name} = new List<bool>();else {name} = ZStringUtil.ArrayStringToBoolList(value.Split('{ArrayTypeSplitChar}'));}}");
        stringBuilder.AppendLine($"        }}");
    }
    static void AppendlongArray(StringBuilder stringBuilder, string name, string type)
    {
        stringBuilder.AppendLine($"        [XmlIgnore]");
        stringBuilder.AppendLine($"        public List<long> {name};");
        stringBuilder.AppendLine($"        [XmlAttribute(\"{name}\")]");
        stringBuilder.AppendLine($"        public string _{name} {{");
        stringBuilder.AppendLine($"            get {{ return {name}.ToString(); }}");
        stringBuilder.AppendLine($"            set{{ if (string.IsNullOrEmpty(value)) {name} = new List<long>();else {name} = ZStringUtil.ArrayStringToBoolList(value.Split('{ArrayTypeSplitChar}'));}}");
        stringBuilder.AppendLine($"        }}");
    }
}
