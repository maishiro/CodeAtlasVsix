﻿using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.VCProjectEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeAtlasVSIX
{
    

    public class SolutionTraverser
    {
        public string GetSolutionPath()
        {
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            if (dte == null)
            {
                return "";
            }

            Solution solution = dte.Solution;
            return solution.FileName;
        }

        public void Traverse()
        {
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            if (dte == null)
            {
                return;
            }
            
            Solution solution = dte.Solution;
            TraverseSolution(solution);
        }

        void TraverseSolution(Solution solution)
        {
            if (solution == null)
            {
                return;
            }

            if(BeforeTraverseSolution(solution))
            {
                string solutionFile = solution.FileName;
                Projects projectList = solution.Projects;
                int projectCount = projectList.Count;
                foreach (var proj in projectList)
                {
                    var project = proj as Project;
                    if (project != null)
                    {
                        TraverseProject(project);
                    }
                }
                AfterTraverseSolution(solution);
            }
        }

        void TraverseProject(Project project)
        {
            if (project == null)
            {
                return;
            }

            if(BeforeTraverseProject(project))
            {
                ProjectItems projectItems = project.ProjectItems;
                if (projectItems != null)
                {
                    //var codeModel = project.CodeModel;
                    //var codeLanguage = codeModel.Language;

                    var items = projectItems.GetEnumerator();
                    while (items.MoveNext())
                    {
                        var item = items.Current as ProjectItem;
                        TraverseProjectItem(item);
                    }
                }
                AfterTraverseProject(project);
            }

        }

        void TraverseProjectItem(ProjectItem item)
        {
            if (item == null)
            {
                return;
            }

            if (BeforeTraverseProjectItem(item))
            {
                if (item.SubProject != null)
                {
                    TraverseProject(item.SubProject);
                }
                var projectItems = item.ProjectItems;
                if (projectItems != null)
                {
                    var items = projectItems.GetEnumerator();
                    while (items.MoveNext())
                    {
                        var currentItem = items.Current as ProjectItem;
                        TraverseProjectItem(currentItem);
                    }
                }
                AfterTraverseProjectItem(item);
            }
        }

        protected virtual bool BeforeTraverseSolution(Solution solution) { return true; }
        protected virtual bool BeforeTraverseProject(Project project) { return true; }
        protected virtual bool BeforeTraverseProjectItem(ProjectItem item) { return true; }

        protected virtual void AfterTraverseSolution(Solution solution) { }
        protected virtual void AfterTraverseProject(Project project) { }
        protected virtual void AfterTraverseProjectItem(ProjectItem item) { }
    }

    public class ProjectFileCollector : SolutionTraverser
    {
        class PathNode
        {
            public PathNode(string name) { m_name = name; }
            public string m_name;
            public Dictionary<string, PathNode> m_children = new Dictionary<string, PathNode>();
        }

        class ProjectInfo
        {
            public HashSet<string> m_includePath = new HashSet<string>();
            public HashSet<string> m_defines = new HashSet<string>();
            public string m_language = "";
        }

        public enum IncludeScope
        {
            INCLUDE_PROJECT_FOLDERS = 0,
            INCLUDE_OPEN_FOLDERS = 1,
            INCLUDE_NONE=2,
        };
        IncludeScope m_includeScope = IncludeScope.INCLUDE_PROJECT_FOLDERS;
        string m_solutionName = "";
        string m_solutionPath = "";
        bool m_onlySelectedProjects = false;
        HashSet<string> m_selectedProjID = new HashSet<string>();
        SortedSet<string> m_selectedProjName = new SortedSet<string>();
        List<string> m_fileList = new List<string>();
        HashSet<string> m_directoryList = new HashSet<string>();
        PathNode m_rootNode = new PathNode("root");
        // Dictionary<string, HashSet<string>> m_projectIncludePath = new Dictionary<string, HashSet<string>>();
        Dictionary<string, ProjectInfo> m_projectInfo = new Dictionary<string, ProjectInfo>();
        HashSet<string> m_customMacroList = new HashSet<string>();
        List<string> m_extensionList = new List<string> {
            ".c", ".cc", ".cxx", ".cpp", ".c++", ".inl",".h", ".hh", ".hxx", ".hpp", ".h++",".inc", 
            ".java", ".ii", ".ixx", ".ipp", ".i++", ".idl", ".ddl", ".odl",
            ".cs",
            ".d", ".php", ".php4", ".php5", ".phtml", ".m", ".markdown", ".md", ".mm", ".dox",
            ".py",
            ".f90", ".f", ".for",
            ".tcl",
            ".vhd", ".vhdl", ".ucf", ".qsf",
            ".as", ".js" };

        public ProjectFileCollector()
        {
        }

        public void SetCustomExtension(Dictionary<string, string> extDict)
        {
            foreach (var item in extDict)
            {
                string ext = item.Key;
                if (!ext.StartsWith("."))
                {
                    ext = "." + ext;
                }
                m_extensionList.Add(ext);
            }
        }

        public void SetCustomMacro(HashSet<string> macroSet)
        {
            if (macroSet != null)
            {
                m_customMacroList = macroSet;
            }
        }

        public void SetToSelectedProjects()
        {
            m_onlySelectedProjects = true;
            m_selectedProjID.Clear();
            m_selectedProjName.Clear();

            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            var activeProjects = dte.ActiveSolutionProjects as Array;
            if (activeProjects != null)
            {
                foreach (var item in activeProjects)
                {
                    var project = item as Project;
                    if (project != null)
                    {
                        m_selectedProjName.Add(project.Name);
                        SetProjectParentSelected(project);
                    }
                }
            }
        }

        public Dictionary<string, int> GetLanguageCount()
        {
            var res = new Dictionary<string, int>();
            foreach (var projectPair in m_projectInfo)
            {
                string lang = projectPair.Value.m_language;
                if (!res.ContainsKey(lang))
                {
                    res[lang] = 0;
                }
                res[lang] += 1;
            }
            return res;
        }

        public string GetMainLanguage()
        {
            string mainLanguage = "";
            int count = 0;
            var langDict = GetLanguageCount();
            foreach (var item in langDict)
            {
                if (item.Value > count)
                {
                    count = item.Value;
                    mainLanguage = item.Key;
                }
            }
            return mainLanguage;
        }

        void SetProjectParentSelected(Project project)
        {
            if (project == null)
            {
                return;
            }
            m_selectedProjID.Add(project.UniqueName);

            var parentItem = project.ParentProjectItem as ProjectItem;
            if (parentItem != null)
            {
                var containingProj = parentItem.ContainingProject;
                SetProjectParentSelected(containingProj);
            }
        }

        public List<string> GetSelectedProjectName()
        {
            return m_selectedProjName.ToList();
        }

        public List<string> GetDirectoryList()
        {
            return m_directoryList.ToList();
        }

        public List<string> GetAllIncludePath()
        {
            var res = new HashSet<string>();
            foreach (var projectPair in m_projectInfo)
            {
                var includeList = projectPair.Value.m_includePath.ToList();
                foreach (var include in includeList)
                {
                    res.Add(include);
                }
            }
            return res.ToList();
        }

        public List<string> GetAllDefines()
        {
            var res = new HashSet<string>();
            foreach (var projectPair in m_projectInfo)
            {
                var defineList = projectPair.Value.m_defines.ToList();
                foreach (var define in defineList)
                {
                    res.Add(define);
                }
            }

            res.UnionWith(m_customMacroList);
            return res.ToList();
        }

        public string GetSolutionPath()
        {
            return m_solutionPath;
        }

        public string GetSolutionFolder()
        {
            if (m_solutionPath == "")
            {
                return "";
            }
            return System.IO.Path.GetDirectoryName(m_solutionPath).Replace('\\','/');
        }

        public string GetSolutionName()
        {
            return m_solutionName;
        }

        public void SetIncludeScope(IncludeScope scope)
        {
            m_includeScope = scope;
        }

        protected override bool BeforeTraverseSolution(Solution solution)
        {
            m_solutionPath = solution.FileName;
            if (m_solutionPath != "")
            {
                m_solutionName = System.IO.Path.GetFileNameWithoutExtension(m_solutionPath);
            }

            if (m_includeScope == IncludeScope.INCLUDE_OPEN_FOLDERS)
            {
                var dte = solution.DTE;
                foreach (Document doc in dte.Documents)
                {
                    string fileName = doc.FullName;
                    m_fileList.Add(fileName);

                    var ext = System.IO.Path.GetExtension(fileName).ToLower();
                    foreach (var extension in m_extensionList)
                    {
                        if (ext == extension)
                        {
                            var directory = System.IO.Path.GetDirectoryName(fileName);
                            directory = directory.Replace('\\', '/');
                            m_directoryList.Add(directory);
                            break;
                        }
                    }
                }
            }
            return true;
        }

        protected override bool BeforeTraverseProject(Project project)
        {
            try
            {
                var projInfo = FindProjectInfo(project.UniqueName);

                // Skip unselected projects
                if (m_onlySelectedProjects)
                {
                    var projectID = project.UniqueName;
                    if (!m_selectedProjID.Contains(projectID))
                    {
                        return false;
                    }
                }

                Logger.Debug("Traversing Project:" + project.Name);
                var codeModel = project.CodeModel;
                if (codeModel != null)
                {
                    if (codeModel.Language == CodeModelLanguageConstants.vsCMLanguageCSharp)
                    {
                        projInfo.m_language = "csharp";
                    }
                    else if (codeModel.Language == CodeModelLanguageConstants.vsCMLanguageVC)
                    {
                        projInfo.m_language = "cpp";
                    }
                    else
                    {
                        projInfo.m_language = "";
                    }
                }
                //var configMgr = project.ConfigurationManager;
                //var config = configMgr.ActiveConfiguration as Configuration;

                var vcProject = project.Object as VCProject;
                Logger.Debug("check vc project");
                if (vcProject != null)
                {
                    var vccon = vcProject.ActiveConfiguration as VCConfiguration;
                    IVCRulePropertyStorage generalRule = (IVCRulePropertyStorage)vccon.Rules.Item("ConfigurationDirectories");
                    IVCRulePropertyStorage cppRule = (IVCRulePropertyStorage)vccon.Rules.Item("CL");

                    // Parsing include path
                    string addIncPath = cppRule.GetEvaluatedPropertyValue("AdditionalIncludeDirectories");
                    string incPath = generalRule.GetEvaluatedPropertyValue("IncludePath");
                    string allIncPath = incPath + ";" + addIncPath;
                    string[] pathList = allIncPath.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    var projectInc = new HashSet<string>();
                    var projectPath = Path.GetDirectoryName(project.FileName);
                    foreach (var item in pathList)
                    {
                        string path = item.Trim();
                        if (!path.Contains(":"))
                        {
                            // relative path
                            path = Path.Combine(projectPath, path);
                            path = Path.GetFullPath((new Uri(path)).LocalPath);
                        }
                        if (!Directory.Exists(path))
                        {
                            continue;
                        }
                        path = path.Replace('\\', '/').Trim();
                        projectInc.Add(path);
                        Logger.Debug("include path:" + path);
                    }
                    projInfo.m_includePath = projectInc;

                    // Parsing define
                    string defines = cppRule.GetEvaluatedPropertyValue("PreprocessorDefinitions");
                    string[] defineList = defines.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    var defineSet = new HashSet<string>();
                    foreach (var item in defineList)
                    {
                        defineSet.Add(item);
                    }
                    projInfo.m_defines = defineSet;
                }

            }
            catch
            {
                Logger.Debug("project error-------------");
            }
            return true;
        }

        ProjectInfo FindProjectInfo(string name)
        {
            if (!m_projectInfo.ContainsKey(name))
            {
                m_projectInfo[name] = new ProjectInfo();
            }
            return m_projectInfo[name];
        }

        protected override bool BeforeTraverseProjectItem(ProjectItem item)
        {
            string itemName = item.Name;
            string itemKind = item.Kind.ToUpper();
            if (itemKind == Constants.vsProjectItemKindPhysicalFolder)
            {
            }
            else if (itemKind == Constants.vsProjectItemKindPhysicalFile && m_includeScope == IncludeScope.INCLUDE_PROJECT_FOLDERS)
            {
                for (short i = 0; i < item.FileCount; i++)
                {
                    string fileName = item.FileNames[i];
                    m_fileList.Add(fileName);

                    var ext = System.IO.Path.GetExtension(fileName).ToLower();
                    foreach (var extension in m_extensionList)
                    {
                        if (ext == extension)
                        {
                            var directory = System.IO.Path.GetDirectoryName(fileName);
                            directory = directory.Replace('\\', '/');
                            m_directoryList.Add(directory);
                            break;
                        }
                    }
                }
            }
            return true;
        }
        
    }

    public class ProjectDB:SolutionTraverser
    {
        public class ProjectInfo
        {
            public HashSet<string> m_lowerProjects = new HashSet<string>();
            public HashSet<string> m_higherProjects = new HashSet<string>();

            public string m_name = "";
            public string m_path = "";
            public string m_vsUniqueName = "";
            public int m_itemCount = 0;
        }

        Dictionary<string, ProjectInfo> m_projectInfo = new Dictionary<string, ProjectInfo>();

        public ProjectInfo GetProjectInfo(string uname)
        {
            if (m_projectInfo.ContainsKey(uname))
            {
                return m_projectInfo[uname];
            }
            return null;
        }

        ProjectInfo FindProjectInfo(string name)
        {
            if (!m_projectInfo.ContainsKey(name))
            {
                m_projectInfo[name] = new ProjectInfo();
            }
            return m_projectInfo[name];
        }

        public List<string> GetAllProjects()
        {
            List<string> result = new List<string>();
            foreach (var item in m_projectInfo)
            {
                result.Add(item.Key);
            }
            return result;
        }

        static public List<string> GetSelectedProject()
        {
            List<string> result = new List<string>();
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            try
            {
                var activeProjects = dte.ActiveSolutionProjects as Array;
                if (activeProjects != null)
                {
                    foreach (var item in activeProjects)
                    {
                        var project = item as Project;
                        if (project != null)
                        {
                            result.Add(project.FullName);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return result;
        }

        protected override bool BeforeTraverseProject(Project project)
        {
            try
            {
                var projInfo = FindProjectInfo(project.FullName);
                projInfo.m_name = project.Name;
                projInfo.m_vsUniqueName = project.UniqueName;
                projInfo.m_path = project.FullName;
            }
            catch
            {
                Logger.Debug("project error-------------");
            }
            return true;
        }

        protected override bool BeforeTraverseProjectItem(ProjectItem projectItem)
        {
            var project = projectItem.ContainingProject;
            if (project != null && project.FullName != "")
            {
                var projectInfo = FindProjectInfo(project.FullName);
                projectInfo.m_itemCount++;
            }
            return true;
        }

        protected override void AfterTraverseSolution(Solution solution)
        {
            base.AfterTraverseSolution(solution);
            var build = solution.SolutionBuild;
            if (build != null && build.BuildDependencies != null)
            {
                foreach (var dp in build.BuildDependencies)
                {
                    var dependency = dp as BuildDependency;
                    if (dependency == null || dependency.Project == null)
                    {
                        continue;
                    }

                    var tarProj = dependency.Project;
                    object[] requiredProjects = dependency.RequiredProjects as object[];
                    if (m_projectInfo.ContainsKey(tarProj.FullName) && requiredProjects != null)
                    {
                        var tarProjData = m_projectInfo[tarProj.FullName];
                        for (int i = 0; i < requiredProjects.Length; i++)
                        {
                            var srcProj = requiredProjects[i] as Project;
                            if (srcProj == null || !m_projectInfo.ContainsKey(srcProj.FullName))
                            {
                                continue;
                            }
                            var srcProjData = m_projectInfo[srcProj.FullName];
                            tarProjData.m_lowerProjects.Add(srcProj.FullName);
                            srcProjData.m_higherProjects.Add(tarProj.FullName);
                        }
                    }
                }
            }
        }
    }

    public class ProjectCounter : SolutionTraverser
    {
        class ProjectInfo
        {
        }

        Dictionary<string, ProjectInfo> m_projectInfo = new Dictionary<string, ProjectInfo>();
        int m_projectItems = 0;
        int m_projects = 0;

        public ProjectCounter()
        {
        }

        protected override bool BeforeTraverseProject(Project project)
        {
            m_projects += 1;
            return true;
        }

        protected override bool BeforeTraverseProjectItem(ProjectItem item)
        {
            m_projectItems += 1;
            return true;
        }

        public int GetTotalProjectItems()
        {
            return m_projectItems;
        }

        public int GetTotalProjects()
        {
            return m_projects;
        }


    }
}
