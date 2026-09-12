using System.Runtime.Serialization;

namespace Waabe.RevitMcp.Addin.Bridge
{
    [DataContract]
    internal sealed class BridgeRequest
    {
        [DataMember(Name = "token")] public string Token { get; set; }
        [DataMember(Name = "method")] public string Method { get; set; }
    }

    [DataContract]
    internal sealed class BridgeResponse
    {
        [DataMember(Name = "ok")] public bool Ok { get; set; }
        [DataMember(Name = "data", EmitDefaultValue = false)] public DocumentInfo Data { get; set; }
        [DataMember(Name = "error", EmitDefaultValue = false)] public BridgeError Error { get; set; }

        public static BridgeResponse Success(DocumentInfo data) => new BridgeResponse { Ok = true, Data = data };
        public static BridgeResponse Failure(string code, string message) => new BridgeResponse
        {
            Ok = false,
            Error = new BridgeError { Code = code, Message = message }
        };
    }

    [DataContract]
    internal sealed class BridgeError
    {
        [DataMember(Name = "code")] public string Code { get; set; }
        [DataMember(Name = "message")] public string Message { get; set; }
    }

    [DataContract]
    internal sealed class DocumentInfo
    {
        [DataMember(Name = "revitVersion")] public string RevitVersion { get; set; }
        [DataMember(Name = "revitBuild")] public string RevitBuild { get; set; }
        [DataMember(Name = "documentOpen")] public bool DocumentOpen { get; set; }
        [DataMember(Name = "title", EmitDefaultValue = false)] public string Title { get; set; }
        [DataMember(Name = "path", EmitDefaultValue = false)] public string Path { get; set; }
        [DataMember(Name = "isFamilyDocument")] public bool IsFamilyDocument { get; set; }
        [DataMember(Name = "isWorkshared")] public bool IsWorkshared { get; set; }
        [DataMember(Name = "instanceElementCount")] public int InstanceElementCount { get; set; }
    }

    [DataContract]
    internal sealed class DiscoveryRecord
    {
        [DataMember(Name = "schema")] public string Schema { get; set; }
        [DataMember(Name = "revitYear")] public int RevitYear { get; set; }
        [DataMember(Name = "host")] public string Host { get; set; }
        [DataMember(Name = "port")] public int Port { get; set; }
        [DataMember(Name = "token")] public string Token { get; set; }
        [DataMember(Name = "processId")] public int ProcessId { get; set; }
    }
}
