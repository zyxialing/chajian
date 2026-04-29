using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
namespace ZFramework
{
    public class ZStringEditor
    {

        [MenuItem("ZFramework/Editor/2.Tool/文件首字母转大写（仅文件）")]
        static private void Capitalize()
        {
            Object[] m_objects = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);//选择的所有对象

            int index = 0;//序号

            foreach (Object item in m_objects)
            {

                //string m_name = item.name;
                if (Path.GetExtension(AssetDatabase.GetAssetPath(item)) != "")//判断路径是否为空
                {

                    string path = AssetDatabase.GetAssetPath(item);

                    AssetDatabase.RenameAsset(path, UpperCase(item.name));
                    index++;
                }

            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
         [MenuItem("ZFramework/Editor/2.Tool/文件首字母转小写（仅文件）")]
        static private void Capitalize2()
        {
            Object[] m_objects = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);//选择的所有对象

            int index = 0;//序号

            foreach (Object item in m_objects)
            {

                //string m_name = item.name;
                if (Path.GetExtension(AssetDatabase.GetAssetPath(item)) != "")//判断路径是否为空
                {

                    string path = AssetDatabase.GetAssetPath(item);

                    AssetDatabase.RenameAsset(path, LowerCase(item.name));
                    index++;
                }

            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        [MenuItem("ZFramework/Editor/2.Tool/删除存储")]
        static private void DeleteFile()
        {
            Storage.Instance.DeleteFile();
        }

        #region 扩展方法

        static private string UpperCase(string str)
        {

           string[] strs =  str.Split(' ');
            string newName = "";
            for (int i = 0; i < strs.Length; i++)
            {
                char[] ch = strs[i].ToCharArray();
                if (ch.Length <= 0) continue;
                if (ch[0] >= 'a' && ch[0] <= 'z')
                {
                    ch[0] = (char)(ch[0] - 32);
                }
                if (i == 0)
                {
                    newName += new string(ch);
                }
                else
                {
                    newName = newName + " " + new string(ch);
                }
            }
          
            return newName;
        }

        static private string LowerCase(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;

            string[] words = str.Split(' ');  // 按空格拆分单词
            StringBuilder newName = new StringBuilder();

            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    char firstChar = char.ToLower(words[i][0]); // 让首字母小写
                    string rest = words[i].Length > 1 ? words[i].Substring(1) : "";

                    if (i > 0)
                        newName.Append(" "); // 只有第一个单词不需要空格

                    newName.Append(firstChar).Append(rest);
                }
            }
            
            return newName.ToString();
        }

        #endregion
        #region test
        //[MenuItem("ZFramework/Editor/3.Tool/文件头追加单词data_")]
        //static private void addworld()
        //{
        //    Object[] m_objects = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);//选择的所有对象

        //    int index = 0;//序号

        //    foreach (Object item in m_objects)
        //    {

        //        //string m_name = item.name;
        //        if (Path.GetExtension(AssetDatabase.GetAssetPath(item)) != "")//判断路径是否为空
        //        {

        //            string path = AssetDatabase.GetAssetPath(item);

        //            if(!item.name.Contains("data_"))
        //            AssetDatabase.RenameAsset(path,"data_"+item.name);
        //            index++;
        //        }

        //    }

        //    AssetDatabase.SaveAssets();
        //    AssetDatabase.Refresh();


        //}
        //[MenuItem("ZFramework/Editor/3.Tool/文件头追加单词pic_")]
        //static private void addworldpic()
        //{
        //    Object[] m_objects = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);//选择的所有对象

        //    int index = 0;//序号

        //    foreach (Object item in m_objects)
        //    {

        //        //string m_name = item.name;
        //        if (Path.GetExtension(AssetDatabase.GetAssetPath(item)) != "")//判断路径是否为空
        //        {

        //            string path = AssetDatabase.GetAssetPath(item);

        //            if (!item.name.Contains("pic_"))
        //                AssetDatabase.RenameAsset(path, "pic_" + item.name);
        //            index++;
        //        }

        //    }

        //    AssetDatabase.SaveAssets();
        //    AssetDatabase.Refresh();


        //}
        //[MenuItem("ZFramework/Editor/3.Tool/给ui图改名为规则Point")]
        //static private void changePointName()
        //{
        //    Object[] m_objects = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);//选择的所有对象

        //    int index = 0;//序号
        //    int tempIndex = 0;//序号
        //    foreach (Object item in m_objects)
        //    {

        //        //string m_name = item.name;
        //        if (Path.GetExtension(AssetDatabase.GetAssetPath(item)) != "")//判断路径是否为空
        //        {
        //            string path = AssetDatabase.GetAssetPath(item);

        //            if (item.name.Contains("point"))
        //            {
        //                tempIndex = int.Parse(item.name.Replace("point", ""));
        //                if (tempIndex > index)
        //                {
        //                    index = tempIndex;
        //                }
        //            }
        //        }

        //    }

        //    foreach (Object item in m_objects)
        //    {

        //        //string m_name = item.name;
        //        if (Path.GetExtension(AssetDatabase.GetAssetPath(item)) != "")//判断路径是否为空
        //        {

        //            string path = AssetDatabase.GetAssetPath(item);

        //            if (!item.name.Contains("point"))
        //            {
        //                Debug.Log(item.name);
        //                index++;
        //                AssetDatabase.RenameAsset(path, "point" + index);

        //            }

        //        }

        //    }
        //    AssetDatabase.SaveAssets();
        //    AssetDatabase.Refresh();


        //}
        //[MenuItem("ZFramework/Editor/3.Tool/给ui图改名为规则bilinear")]
        //static private void changebilinearName()
        //{
        //    Object[] m_objects = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);//选择的所有对象

        //    int index = 0;//序号
        //    int tempIndex = 0;//序号
        //    foreach (Object item in m_objects)
        //    {

        //        //string m_name = item.name;
        //        if (Path.GetExtension(AssetDatabase.GetAssetPath(item)) != "")//判断路径是否为空
        //        {
        //            string path = AssetDatabase.GetAssetPath(item);

        //            if (item.name.Contains("bilinear"))
        //            {
        //                tempIndex = int.Parse(item.name.Replace("bilinear", ""));
        //                if (tempIndex > index)
        //                {
        //                    index = tempIndex;
        //                }
        //            }
        //        }

        //    }

        //    foreach (Object item in m_objects)
        //    {

        //        //string m_name = item.name;
        //        if (Path.GetExtension(AssetDatabase.GetAssetPath(item)) != "")//判断路径是否为空
        //        {

        //            string path = AssetDatabase.GetAssetPath(item);

        //            if (!item.name.Contains("bilinear"))
        //            {
        //                Debug.Log(item.name);
        //                index++;
        //                AssetDatabase.RenameAsset(path, "bilinear" + index);

        //            }

        //        }

        //    }
        //    AssetDatabase.SaveAssets();
        //    AssetDatabase.Refresh();


        //}
        #endregion
    }

}