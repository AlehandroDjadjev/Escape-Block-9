using EscapeBlock9.ProcGen.Data;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Validation
{
    public readonly struct TileAuthoringIssue
    {
        public TileAuthoringIssue(TileAuthoringSeverity severity, Object context, string message)
        {
            Severity = severity;
            Context = context;
            Message = message;
        }

        public TileAuthoringSeverity Severity { get; }
        public Object Context { get; }
        public string Message { get; }
    }
}
