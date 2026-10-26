using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace MFramework.Editor
{
    internal static class MFrameworkDocumentationMenu
    {
        private const string DocumentationMenu = "Tools/MFramework/打开 Documentation~ 目录";
        private const string SiteMenu = "Tools/MFramework/打开已生成的 DocFX 手册";

        [MenuItem(DocumentationMenu)]
        private static void RevealDocumentationFolder()
        {
            EditorUtility.RevealInFinder(GetDocumentationPath());
        }

        [MenuItem(SiteMenu)]
        private static void OpenGeneratedSite()
        {
            string indexPath = Path.Combine(GetDocumentationPath(), "_site", "index.html");
            if (!File.Exists(indexPath))
            {
                EditorUtility.DisplayDialog(
                    "MFramework",
                    "尚未生成 DocFX 站点。请先运行 Documentation~/build.sh。",
                    "确定");
                return;
            }

            Application.OpenURL(new Uri(indexPath).AbsoluteUri);
        }

        private static string GetDocumentationPath()
        {
            PackageManagerPackageInfo package =
                PackageManagerPackageInfo.FindForAssembly(Assembly.GetExecutingAssembly());
            if (package == null)
            {
                throw new InvalidOperationException("无法定位 com.mstudio.mframework 包目录。");
            }

            return Path.Combine(package.resolvedPath, "Documentation~");
        }
    }
}
