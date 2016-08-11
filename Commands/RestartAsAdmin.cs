//------------------------------------------------------------------------------
// <copyright file="RestartAsAdmin.cs" company="Radacode">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using PowerArm.Extension.Managers;
using Process = System.Diagnostics.Process;

namespace PowerArm.Extension.Commands
{
    /// <summary>
    /// Command handler
    /// Courtesy of https://github.com/ilmax/vs-restart/
    /// </summary>
    internal sealed class RestartAsAdmin
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        public const int RadacodeId = 0x1021;

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

        /// <summary>
        /// Initializes a new instance of the <see cref="RestartAsAdmin"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private RestartAsAdmin(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            dte = (package as PowerArmPackage).DTE;

            statusbar = Package.GetGlobalService(typeof(SVsStatusbar)) as IVsStatusbar;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandId = new CommandID(CommandSet, CommandId);
                var menuItem = new OleMenuCommand(this.MenuItemCallback, menuCommandId);
                menuItem.BeforeQueryStatus += MenuItemOnBeforeQueryStatus;

                if (ElevationChecker.CanCheckElevation)
                    menuItem.Visible = !ElevationChecker.IsElevated(Process.GetCurrentProcess().Handle);
                else
                    menuItem.Visible = true;

                commandService.AddCommand(menuItem);
            }
        }

        private void MenuItemOnBeforeQueryStatus(object sender, EventArgs eventArgs)
        {
            OleMenuCommand item = (OleMenuCommand)sender;
            if (ElevationChecker.CanCheckElevation)
            {
                item.Visible = !ElevationChecker.IsElevated(Process.GetCurrentProcess().Handle);
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static RestartAsAdmin Instance
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
            Instance = new RestartAsAdmin(package);
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
            var dte = (package as PowerArmPackage).DTE;

            if (dte == null)
            {
                // Show some error message and return
                return;
            }

            Debug.Assert(dte != null);

            bool elevated = ((OleMenuCommand)sender).CommandID.ID == MenuId.RestartAsAdmin;

            new VisualStuioRestarter().Restart(dte, elevated);
        }
    }
}
