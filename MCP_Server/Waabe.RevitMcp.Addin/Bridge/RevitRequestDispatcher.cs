using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Waabe.RevitMcp.Addin.Bridge
{
    internal sealed class RevitRequestDispatcher : IExternalEventHandler
    {
        private readonly ConcurrentQueue<PendingRequest> _queue = new ConcurrentQueue<PendingRequest>();
        private ExternalEvent _externalEvent;

        public void Attach(ExternalEvent externalEvent) => _externalEvent = externalEvent;

        public Task<BridgeResponse> Enqueue(string method)
        {
            var completion = new TaskCompletionSource<BridgeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Enqueue(new PendingRequest(method, completion));
            var result = _externalEvent.Raise();
            if (result != ExternalEventRequest.Accepted && result != ExternalEventRequest.Pending)
                completion.TrySetResult(BridgeResponse.Failure("REVIT_BUSY", "Revit did not accept the ExternalEvent request."));
            return completion.Task;
        }

        public void Execute(UIApplication application)
        {
            PendingRequest request;
            while (_queue.TryDequeue(out request))
            {
                try
                {
                    if (!string.Equals(request.Method, "get_document_info", StringComparison.Ordinal))
                    {
                        request.Completion.TrySetResult(BridgeResponse.Failure("METHOD_NOT_FOUND", "Unknown read-only method."));
                        continue;
                    }

                    var uiDocument = application.ActiveUIDocument;
                    var document = uiDocument == null ? null : uiDocument.Document;
                    var info = new DocumentInfo
                    {
                        RevitVersion = application.Application.VersionNumber,
                        RevitBuild = application.Application.VersionBuild,
                        DocumentOpen = document != null,
                        Title = document?.Title,
                        Path = document?.PathName,
                        IsFamilyDocument = document != null && document.IsFamilyDocument,
                        IsWorkshared = document != null && document.IsWorkshared,
                        InstanceElementCount = document == null
                            ? 0
                            : new FilteredElementCollector(document).WhereElementIsNotElementType().GetElementCount()
                    };
                    request.Completion.TrySetResult(BridgeResponse.Success(info));
                }
                catch (Exception ex)
                {
                    request.Completion.TrySetResult(BridgeResponse.Failure("REVIT_API_ERROR", ex.Message));
                }
            }
        }

        public string GetName() => "WAABE Revit MCP read-only dispatcher";

        private sealed class PendingRequest
        {
            public PendingRequest(string method, TaskCompletionSource<BridgeResponse> completion)
            {
                Method = method;
                Completion = completion;
            }
            public string Method { get; }
            public TaskCompletionSource<BridgeResponse> Completion { get; }
        }
    }
}
