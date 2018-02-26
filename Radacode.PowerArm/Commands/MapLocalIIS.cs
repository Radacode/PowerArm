//------------------------------------------------------------------------------
// <copyright file="MapLocalIIS.cs" company="Radacode">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text.RegularExpressions;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Web.Administration;
using PowerArm.Extension.Managers;
using radacode.net.logger;
//using radacode.net.logger;
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
        private string _unloadedProject;

        private ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MapLocalIIS"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private MapLocalIIS(Package package, ILogger logger)
        {
            _logger = logger;

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

            _unloadedProject = String.Empty;
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
        public static void Initialize(Package package, ILogger logger)
        {
            Instance = new MapLocalIIS(package, logger);
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
            //_logger?.Log($"MenuItemCallback in {this.ToString()} started.");

            this.CheckIfUnloadedFilesPresent();

            string message = _unloadedPresent
                ? "Unloaded projects are present."
                : "No unloaded project are in the solution. Nothing to map.";

            //_logger?.Log(message);

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

            OutputWindowPane owP = null;

            if (ow.OutputWindowPanes != null)
            {
                for (uint i = 1; i <= ow.OutputWindowPanes.Count; i++)
                {
                    if (ow.OutputWindowPanes.Item(i).Name.Equals("Solution", StringComparison.CurrentCultureIgnoreCase))
                    {
                        owP = ow.OutputWindowPanes.Item(i);
                    }
                }
            }

            if (owP == null && string.IsNullOrEmpty(_unloadedProject)) return;
            if (owP == null && !string.IsNullOrEmpty(_unloadedProject))
            {
                this.ReloadProject(_unloadedProject);

                this.MenuItemCallback(sender, e);
                return;
            }

            var p = owP.TextDocument.StartPoint.CreateEditPoint();
            string s = p.GetText(owP.TextDocument.EndPoint);

            try
            {
                this.ProcessErrorMessage(s);
            }
            catch (Exception ex)
            {
                var preamble =
                    $"MenuItemCallback in {this.ToString()} encountered an error. The logic logged the following before failing: /n {_mapLog}";

                _logger?.Log(preamble);
                _logger?.Error($"The error is the following: {ex.Message}", ex.StackTrace);
            }

            _logger?.Log($"MenuItemCallback in {this.ToString()} finished.");
        }

        private void ProcessErrorMessage(string errorMessage)
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


            using (ServerManager iisManager = new ServerManager())
            {
                if (!iisManager.ApplicationPools.Any(p => p.Name == projectName))
                {
                    ApplicationPool newPool = iisManager.ApplicationPools.Add(projectName);
                    newPool.ManagedRuntimeVersion = "v4.0";
                    iisManager.CommitChanges();
                }

                var site = iisManager.Sites.Add(projectName, uri.Scheme, $"*:{uri.Port}:{uri.DnsSafeHost}", Path.GetDirectoryName(pathToProject));
                site.ApplicationDefaults.ApplicationPoolName = projectName;

                foreach (var item in site.Applications)
                {
                    item.ApplicationPoolName = projectName;
                }

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

            //TODO: Ask if user wants to set ApplicationPoolIdentitiy to NetworkService and allow it access to site folder.

            var shouldChangeAppPoolResult = VsShellUtilities.ShowMessageBox(
                this.ServiceProvider,
                $"Site {projectName} mapped in IIS. {Environment.NewLine}Would you like to change the created ApplicationPool's identitiy to NetworkService user and assign it read rights for the project folder?",
                "Change ApplicationPool Identitiy",
                OLEMSGICON.OLEMSGICON_QUERY,
                OLEMSGBUTTON.OLEMSGBUTTON_YESNO,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

            if (shouldChangeAppPoolResult == 6)
            {
                using (ServerManager iisManager = new ServerManager())
                {
                    var currentPool = iisManager.ApplicationPools.First(p => p.Name == projectName);
                    currentPool.ProcessModel.IdentityType = ProcessModelIdentityType.NetworkService;
                    iisManager.CommitChanges();
                }

                DirectorySecurity dir_security = Directory.GetAccessControl(pathToProject);

                FileSystemAccessRule full_access_rule = new FileSystemAccessRule("NetworkService",
                    FileSystemRights.FullControl, InheritanceFlags.ContainerInherit |
                                                  InheritanceFlags.ObjectInherit, PropagationFlags.None,
                    AccessControlType.Allow);

                dir_security.AddAccessRule(full_access_rule);

                Directory.SetAccessControl(pathToProject, dir_security);
            }

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
                    _unloadedProject = project.Name;
                    break;
                }
            }
        }

        private void MenuItemOnBeforeQueryStatus(object sender, EventArgs eventArgs)
        {
            this.CheckIfUnloadedFilesPresent();

            var isAdmin = false;

            var menuCommand = sender as OleMenuCommand;

            if (ElevationChecker.CanCheckElevation)
            {
                isAdmin = ElevationChecker.IsElevated(System.Diagnostics.Process.GetCurrentProcess().Handle);
            }

            menuCommand.Enabled = _unloadedPresent && isAdmin;
        }
    }
}
