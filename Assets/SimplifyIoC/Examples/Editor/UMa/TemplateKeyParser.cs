using UnityEngine;
using UnityEditor;
using System.IO;
//============================================================
//@author	JiphuTzu
//@create	2016/4/28
//@company	UMa
//
//@description:
//============================================================

namespace Tzu
{
    public class TemplateKeyParser : UnityEditor.AssetModificationProcessor
    {
        private static string AUTHOR = "JiphuTzu";
        private static string COMPANY = "UMa";
        public static void OnWillCreateAsset(string path)
        {
            //Debug.Log("TempleteKeyParser=>" + path);
            path = path.Replace(".meta", "");
            //不是.cs文件
            if (path.EndsWith(".cs") == false) return;
            //
            int index = Application.dataPath.LastIndexOf("Assets");
            //
            string fullPath = Application.dataPath.Substring(0, index) + path;
            //Debug.Log("full path => " + fullPath);
            index = GetStringIndex(path);
            if (index <= 0) return;
            
            string ns = COMPANY;
            int end = path.LastIndexOf("/");
            if (end > index)
            {
                path = path.Substring(index, end - index);
                ns = path.Replace("/", ".");
            }
            //
            string file = File.ReadAllText(fullPath);
            //
            file = file.Replace("#AUTHOR#", AUTHOR);//Environment.UserName);
            file = file.Replace("#CREATEDATE#", System.DateTime.Now.ToString("yyyyMMdd"));
            file = file.Replace("#COMPANY#", COMPANY);
            file = file.Replace("#NAMESPACE#", ns);
            //
            File.WriteAllText(fullPath, file);
            AssetDatabase.Refresh();
        }
        private static int GetStringIndex(string path)
        {
            int index = path.LastIndexOf("Scripts/");
            if (index > 0) return index + 8;
            index = path.LastIndexOf("Plugins/");
            if (index > 0) return index + 8;
            index = path.LastIndexOf("Editor/");
            if (index > 0 && path.IndexOf("TemplateKeyParser") < 0) return index + 7;
            return -1;
        }
    }
}