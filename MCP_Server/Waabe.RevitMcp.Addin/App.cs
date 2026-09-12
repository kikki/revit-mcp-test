using System;
using Autodesk.Revit.UI;
using Waabe.RevitMcp.Addin.Bridge;

namespace Waabe.RevitMcp.Addin
{
    public sealed class App : IExternalApplication
    {
        private RevitRequestDispatcher _dispatcher;
        private LocalBridgeServer _server;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                int year;
                if (!int.TryParse(application.ControlledApplication.VersionNumber, out year))
                    throw new InvalidOperationException("Revit VersionNumber is not numeric.");
                if (year != 2023 && year != 2026)
                    throw new NotSupportedException("This playground supports Revit 2023 and Revit 2026 only.");

                _dispatcher = new RevitRequestDispatcher();
                _dispatcher.Attach(ExternalEvent.Create(_dispatcher));
                _server = new LocalBridgeServer(year, _dispatcher);
                _server.Start();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                application.ControlledApplication.WriteJournalComment(
                    "WAABE Revit MCP startup failed: " + ex, true);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try { _server?.Dispose(); } catch { }
            _server = null;
            _dispatcher = null;
            return Result.Succeeded;
        }
    }
}
