//------------------------------------------------------------------------------
// <copyright file="CleanAll.cs" company="Radacode">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.IO;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace PowerArm.Extension.Commands
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class CleanAll
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        public const int RadacodeId = 0x1021;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("3c281d67-fe51-41e1-b138-b4385425efc5");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        private readonly DTE dte;
        private BuildEvents buildEvents;
        private IVsStatusbar statusbar;

        /// <summary>
        /// Initializes a new instance of the <see cref="CleanAll"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private CleanAll(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            dte = Package.GetGlobalService(typeof(SDTE)) as DTE;

            statusbar = Package.GetGlobalService(typeof(SVsStatusbar)) as IVsStatusbar;

            buildEvents = dte.Events.BuildEvents;
            buildEvents.OnBuildBegin += BuildEventsOnOnBuildBegin;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandId = new CommandID(CommandSet, CommandId);
                var menuItem = new OleMenuCommand(this.MenuItemCallback, menuCommandId);
                menuItem.BeforeQueryStatus += MenuItemOnBeforeQueryStatus;

                commandService.AddCommand(menuItem);
            }
        }

        private void BuildEventsOnOnBuildBegin(vsBuildScope scope, vsBuildAction action)
        {
            foreach (Project p in dte.Solution.Projects)
            {
                try
                {
                    Configuration conf = p.ConfigurationManager.ActiveConfiguration;
                    conf.Properties.Item("UseVSHostingProcess").Value = true;
                }
                catch { }
            }
        }

        private void MenuItemOnBeforeQueryStatus(object sender, EventArgs eventArgs)
        {
            var menuCommand = sender as OleMenuCommand;

            if (dte.Solution.Projects.Count > 0)
            {
                menuCommand.Enabled = true;
            }
            else
            {
                menuCommand.Enabled = false;
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static CleanAll Instance
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
            Instance = new CleanAll(package);
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
            foreach (Project p in dte.Solution.Projects)
            {
                Configuration conf = null;
                try
                {
                    conf = p.ConfigurationManager.ActiveConfiguration;
                    conf.Properties.Item("UseVSHostingProcess").Value = false;
                }
                catch { }

                DeleteDirectoryRecursive(Path.GetDirectoryName(p.FileName));
            }

            statusbar.SetText("Clean finished");
        }

        static void DeleteDirectoryRecursive(string path)
        {
            DirectoryInfo di = new DirectoryInfo(path);

            foreach (var dir in di.GetDirectories())
            {
                if (dir.Name == "bin" || dir.Name == "obj")
                {
                    try
                    {
                        dir.Delete(true);
                    }
                    catch { }
                }
                else
                {
                    DeleteDirectoryRecursive(dir.FullName);
                }
            }
        }
    }
}
