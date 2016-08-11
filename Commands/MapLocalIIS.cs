﻿//------------------------------------------------------------------------------
// <copyright file="MapLocalIIS.cs" company="Radacode">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Web.Administration;
using Configuration = EnvDTE.Configuration;

namespace PowerArm.Extension.Commands
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class MapLocalIIS
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0300;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = Guids.PowerArmGroupGuid;

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        private readonly DTE dte;
        private IVsStatusbar statusbar;

        private string _mapLog;

        private bool _unloadedPresent;

        /// <summary>
        /// Initializes a new instance of the <see cref="MapLocalIIS"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private MapLocalIIS(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            dte = Package.GetGlobalService(typeof(SDTE)) as DTE;

            statusbar = Package.GetGlobalService(typeof(SVsStatusbar)) as IVsStatusbar;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandId = new CommandID(CommandSet, CommandId);
                var menuItem = new OleMenuCommand(this.MenuItemCallback, menuCommandId);
                menuItem.BeforeQueryStatus += MenuItemOnBeforeQueryStatus;

                commandService.AddCommand(menuItem);
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static MapLocalIIS Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new MapLocalIIS(package);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            this.CheckIfUnloadedFilesPresent();

            string message = _unloadedPresent
                ? "Unloaded projects are present"
                : "No unloaded project are in the solution. Nothing to map.";
            string title = "Map Local IIS";

            if (!_unloadedPresent)
            {
                VsShellUtilities.ShowMessageBox(
                    this.ServiceProvider,
                    message,
                    title,
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }

            _mapLog = string.Empty;

            Window win = dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput);
            OutputWindow ow = win.Object as OutputWindow;
            OutputWindowPane owP = ow.OutputWindowPanes.Item(ow.OutputWindowPanes.Count);

            var p = owP.TextDocument.StartPoint.CreateEditPoint();
            string s = p.GetText(owP.TextDocument.EndPoint);

            this.ProcessErrorMessage(s);
        }

        public void ProcessErrorMessage(string errorMessage)
        {
            //Trying to parse each line.
            var result = Regex.Split(errorMessage, "\r\n|\r|\n");

            foreach (var error in result)
            {
                try
                {
                    this.ProcessErrorEntry(error);
                }
                catch (Exception ex)
                {
                    VsShellUtilities.ShowMessageBox(
                        this.ServiceProvider,
                        "Log: " + Environment.NewLine + _mapLog 
                        + Environment.NewLine + 
                        "Error: " + Environment.NewLine + ex.Message,
                        "Error during mapping of the site",
                        OLEMSGICON.OLEMSGICON_INFO,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
            }
        }

        private void ProcessErrorEntry(string error)
        {
            if (!error.Contains("is configured to use IIS.  "))
                return;

            string re1 = "([a-z]:\\\\(?:[-\\w\\.\\d]+\\\\)*(?:[-\\w\\.\\d]+)?)";    // Windows Path 1
            string re2 = ".*?"; // Non-greedy match on filler
            string re3 = "((?:http|https)(?::\\/{2}[\\w]+)(?:[\\/|\\.]?)(?:[^\\s\"]*))";    // HTTP URL 1

            Regex r = new Regex(re1 + re2 + re3, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Match m = r.Match(error);
            if (m.Success)
            {
                String pathToProject = m.Groups[1].ToString();
                String url = m.Groups[2].ToString();

                Regex regexObj = new Regex(@"The Web Application Project (.+?) is configured to use IIS.");
                String projectName = regexObj.Match(error).Groups[1].Value;

                _mapLog = $"Parsed error successfully, parameters are {pathToProject}, {url}, {projectName}";

                this.MapInIIS(pathToProject, url, projectName);
            }
        }

        private void MapInIIS(string pathToProject, string url, string projectName)
        {
            if (url.EndsWith("'"))
                url = url.TrimEnd('\'');

            var uri = new Uri(url);

            ServerManager iisManager = new ServerManager();
            var site = iisManager.Sites.Add(projectName, uri.Scheme, $"*:{uri.Port}:{uri.DnsSafeHost}", Path.GetDirectoryName(pathToProject));
            iisManager.CommitChanges();

            var count = 0;

            while (!iisManager.Sites.Any(s => s.Name == projectName) && count < 20)
            {
                System.Threading.Thread.Sleep(100);
                count++;
            }

            _mapLog += Environment.NewLine + "Site added successfully.";

            try
            {
                site.Start();
                _mapLog += Environment.NewLine + "Site started";
            }
            catch (Exception ex)
            {
                _mapLog += Environment.NewLine + "Site could not be started; Try starting it manually.";
            }

            var hostsEntryExists = false;

            using(var reader = new StringReader(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers/etc/hosts")))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains(" " + uri.DnsSafeHost))
                    {
                        hostsEntryExists = true;
                        break;
                    }
                }
            }

            if (!hostsEntryExists)
            {
                using (
                    StreamWriter w =
                        File.AppendText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                            "drivers/etc/hosts")))
                {
                    w.WriteLine($"{w.NewLine}127.0.0.1 {uri.DnsSafeHost}");
                }
            }

            _mapLog += Environment.NewLine + "hosts written.";

            this.ReloadProject(projectName);
        }

        private void ReloadProject(string projectName)
        {
            string solutionName = Path.GetFileNameWithoutExtension(dte.Solution.FullName);

            dte.Windows.Item(EnvDTE.Constants.vsWindowKindSolutionExplorer).Activate();
            ((DTE2)dte).ToolWindows.SolutionExplorer.GetItem(solutionName + @"\" + projectName).Select(vsUISelectionType.vsUISelectionTypeSelect);

            _mapLog += Environment.NewLine + "About to run reload";

            dte.ExecuteCommand("Project.ReloadProject");

            _mapLog += Environment.NewLine + "Poject reloaded";
        }

        private void CheckIfUnloadedFilesPresent()
        {
            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                _unloadedPresent = false;

                if (
                    string.Compare(EnvDTE.Constants.vsProjectKindUnmodeled, project.Kind,
                        StringComparison.OrdinalIgnoreCase) == 0)
                {
                    _unloadedPresent = true;
                    break;
                }
            }
        }

        private void MenuItemOnBeforeQueryStatus(object sender, EventArgs eventArgs)
        {
            this.CheckIfUnloadedFilesPresent();

            var menuCommand = sender as OleMenuCommand;

            menuCommand.Enabled = _unloadedPresent;
        }
    }
}