﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using PowerArm.Extension.Commands;
using radacode.net.logger;

namespace PowerArm.Extension
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    //[ProvideService((typeof(Logger)), IsAsyncQueryable = true)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists)]
    [ProvideAutoLoad(UIContextGuids80.NoSolution)]
    [Guid(Guids.PackageId)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed partial class PowerArmPackage : AsyncPackage, IVsSolutionLoadManager
    {
        private uint _solutionEventsCoockie;

        private DteInitializer _dteInitializer;

        private ILogger _logger;
        private string _loggerLogin = "6a702fdf-3416-42db-add2-a5fb3a6558eb";
        private string _loggerPassword = "c{7aG(#t";
        private string _loggerAudienceId = "b10a7516218e45c8bc5fa9dff32d156d";

        public DTE DTE { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerArm.Extension.PowerArmPackage"/> class.
        /// </summary>
        public PowerArmPackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
        }

        private void InitializeDTE()
        {
            try
            {
                DTE = (DTE)GetService(typeof(DTE));
            }
            catch (Exception)
            {
                DTE = null;
            }

            if (DTE == null)
            {
                IVsShell shellService = (IVsShell)this.GetService(typeof(IVsShell));
                _dteInitializer = new DteInitializer(shellService, InitializeDTE);
            }
            else
            {
                _dteInitializer = null;
            }
        }

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            this.AddService(typeof(Logger), CreateLogger);

            await base.InitializeAsync(cancellationToken, progress);
            if (Environment.MachineName.Contains("LT-258157"))
                _logger = await this.GetServiceAsync(typeof(Logger)) as ILogger;
            _logger?.Log(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            CleanAll.Initialize(this, _logger);
            MapLocalIIS.Initialize(this, _logger);
            RestartAsAdmin.Initialize(this, _logger);
            InstallLocalIIS.Initialize(this, _logger);
            base.Initialize();

            InitializeDTE();

            var solution = GetService(typeof(SVsSolution)) as IVsSolution;
            if (null != solution)
            {
                solution.AdviseSolutionEvents(this, out _solutionEventsCoockie);
            }

            _logger?.Log("All initializations complete.");
        }

        private async Task<object> CreateLogger(IAsyncServiceContainer container, CancellationToken cancellationtoken, Type servicetype, IProgress<ServiceProgressData> progress)
        {
            Logger logger = null;
            await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    logger = new Logger(_loggerLogin, _loggerPassword, _loggerLogin, _loggerAudienceId);
                }
                catch
                {
                    logger = null;
                }
            });

            return logger;
        }

        #endregion

        public int OnDisconnect()
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeOpenProject(ref Guid guidProjectID, ref Guid guidProjectType, string pszFileName,
            IVsSolutionLoadManagerSupport pSLMgrSupport)
        {
            return VSConstants.S_OK;
        }
    }

    // Courtesy of http://www.mztools.com/articles/2013/MZ2013029.aspx
    internal class DteInitializer : IVsShellPropertyEvents
    {
        private readonly IVsShell _shellService;
        private uint _cookie;
        private readonly Action _callback;

        internal DteInitializer(IVsShell shellService, Action callback)
        {
            int hr;

            _shellService = shellService;
            _callback = callback;

            // Set an event handler to detect when the IDE is fully initialized
            hr = _shellService.AdviseShellPropertyChanges(this, out _cookie);

            ErrorHandler.ThrowOnFailure(hr);
        }

        int IVsShellPropertyEvents.OnShellPropertyChange(int propid, object var)
        {
            if (propid == (int)__VSSPROPID.VSSPROPID_Zombie)
            {
                var isZombie = (bool)var;

                if (!isZombie)
                {
                    // Release the event handler to detect when the IDE is fully initialized
                    var hr = _shellService.UnadviseShellPropertyChanges(_cookie);

                    ErrorHandler.ThrowOnFailure(hr);

                    _cookie = 0;

                    _callback();
                }
            }

            return VSConstants.S_OK;
        }
    }
}
