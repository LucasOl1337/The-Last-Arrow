using System.Diagnostics;

namespace ProjectPVP.Input
{
    public enum AiArenaTransportStatus
    {
        Success = 0,
        Timeout = 1,
        Disconnected = 2,
        InvalidRequest = 3,
        InvalidResponse = 4,
    }

    public readonly struct AiArenaTransportResult
    {
        public AiArenaTransportResult(AiArenaTransportStatus status, string responseJson, string error)
        {
            Status = status;
            ResponseJson = responseJson;
            Error = error;
        }

        public AiArenaTransportStatus Status { get; }
        public string ResponseJson { get; }
        public string Error { get; }
        public bool IsSuccess => Status == AiArenaTransportStatus.Success;
    }

    public sealed class AiArenaLocalTransport
    {
        public bool simulateTimeout;
        public bool simulateDisconnect;
        public bool simulateInvalidResponse;

        public AiArenaTransportResult RequestDecisionJson(string snapshotJson, int timeoutMs)
        {
            if (simulateDisconnect)
            {
                return new AiArenaTransportResult(AiArenaTransportStatus.Disconnected, string.Empty, "transport_disconnected");
            }

            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                return new AiArenaTransportResult(AiArenaTransportStatus.InvalidRequest, string.Empty, "empty_snapshot");
            }

            if (simulateTimeout)
            {
                return new AiArenaTransportResult(AiArenaTransportStatus.Timeout, string.Empty, "transport_timeout");
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            string responseJson = AiArenaHeuristicPolicy.DecideJson(snapshotJson);
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > timeoutMs)
            {
                return new AiArenaTransportResult(AiArenaTransportStatus.Timeout, string.Empty, "transport_timeout");
            }

            if (simulateInvalidResponse)
            {
                responseJson = "{invalid_json";
            }

            return new AiArenaTransportResult(AiArenaTransportStatus.Success, responseJson, string.Empty);
        }
    }
}
