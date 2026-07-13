#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.StrategicMap;

namespace Rpg.Application.StrategicMap;

public sealed class StrategicMapChunkLoadScheduler
{
    public const int MinimumConcurrentLoads = 1;
    public const int MaximumConcurrentLoads = 8;

    private readonly int _maxConcurrentLoads;
    private readonly List<StrategicMapChunkLoadRequest> _activeInRequestOrder = new();
    private readonly Dictionary<string, StrategicMapChunkLoadRequest> _activeByChunkId = new(StringComparer.Ordinal);
    private readonly HashSet<string> _activeResourcePaths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StrategicMapChunkLoadRequest> _residentByChunkId = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failedChunkIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failedResourcePaths = new(StringComparer.Ordinal);
    private IReadOnlyList<StrategicMapChunkLoadRequest> _desiredInCanonicalOrder = Array.Empty<StrategicMapChunkLoadRequest>();
    private Dictionary<string, StrategicMapChunkLoadRequest> _desiredByChunkId = new(StringComparer.Ordinal);

    public StrategicMapChunkLoadScheduler(int maxConcurrentLoads)
    {
        if (maxConcurrentLoads is < MinimumConcurrentLoads or > MaximumConcurrentLoads)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrentLoads),
                $"Strategic map concurrent chunk loads must be in range {MinimumConcurrentLoads}-{MaximumConcurrentLoads}.");
        }

        _maxConcurrentLoads = maxConcurrentLoads;
    }

    public IReadOnlyList<StrategicMapChunkLoadRequest> ActiveRequests => _activeInRequestOrder;
    public int ActiveCount => _activeInRequestOrder.Count;
    public int ResidentCount => _residentByChunkId.Count;
    public int FailedCount => _failedChunkIds.Count;

    public bool SetDesired(IReadOnlyList<StrategicMapChunkLoadRequest> desiredInCanonicalOrder)
    {
        ArgumentNullException.ThrowIfNull(desiredInCanonicalOrder);
        Dictionary<string, StrategicMapChunkLoadRequest> nextByChunkId = new(StringComparer.Ordinal);
        HashSet<string> nextResourcePaths = new(StringComparer.Ordinal);

        foreach (StrategicMapChunkLoadRequest request in desiredInCanonicalOrder)
        {
            if (request.Chunk == null || string.IsNullOrWhiteSpace(request.Chunk.ChunkId))
            {
                throw new InvalidOperationException("Strategic map chunk loading received an empty chunk id.");
            }
            if (string.IsNullOrWhiteSpace(request.ResourcePath))
            {
                throw new InvalidOperationException($"Strategic map chunk loading received an empty resource path chunkId={request.Chunk.ChunkId}.");
            }
            if (!nextByChunkId.TryAdd(request.Chunk.ChunkId, request))
            {
                throw new InvalidOperationException($"Strategic map chunk loading received duplicate chunkId={request.Chunk.ChunkId}.");
            }
            if (!nextResourcePaths.Add(request.ResourcePath))
            {
                throw new InvalidOperationException($"Strategic map chunk loading received duplicate resource path={request.ResourcePath}.");
            }
        }

        bool changed = !_desiredInCanonicalOrder.SequenceEqual(desiredInCanonicalOrder);
        _desiredInCanonicalOrder = desiredInCanonicalOrder.ToArray();
        _desiredByChunkId = nextByChunkId;
        return changed;
    }

    public IReadOnlyList<string> GetChunkIdsToUnload() => _residentByChunkId.Keys
        .Where(chunkId => !_desiredByChunkId.ContainsKey(chunkId))
        .OrderBy(chunkId => chunkId, StringComparer.Ordinal)
        .ToArray();

    public void MarkUnloaded(string chunkId)
    {
        _residentByChunkId.Remove(chunkId);
    }

    public IReadOnlyList<StrategicMapChunkLoadRequest> ReserveAvailableRequests()
    {
        int available = _maxConcurrentLoads - _activeInRequestOrder.Count;
        if (available <= 0)
        {
            return Array.Empty<StrategicMapChunkLoadRequest>();
        }

        List<StrategicMapChunkLoadRequest> reserved = new(available);
        foreach (StrategicMapChunkLoadRequest request in _desiredInCanonicalOrder)
        {
            string chunkId = request.Chunk.ChunkId;
            if (_activeByChunkId.ContainsKey(chunkId) ||
                _activeResourcePaths.Contains(request.ResourcePath) ||
                _residentByChunkId.ContainsKey(chunkId) ||
                _failedChunkIds.Contains(chunkId) ||
                _failedResourcePaths.Contains(request.ResourcePath))
            {
                continue;
            }

            _activeByChunkId.Add(chunkId, request);
            _activeResourcePaths.Add(request.ResourcePath);
            _activeInRequestOrder.Add(request);
            reserved.Add(request);
            if (reserved.Count == available)
            {
                break;
            }
        }

        return reserved;
    }

    public bool IsDesired(StrategicMapChunkLoadRequest request) =>
        _desiredByChunkId.TryGetValue(request.Chunk.ChunkId, out StrategicMapChunkLoadRequest? desired) &&
        string.Equals(desired.ResourcePath, request.ResourcePath, StringComparison.Ordinal);

    public StrategicMapChunkLoadCompletion Complete(StrategicMapChunkLoadRequest request, bool resourceIsUsable)
    {
        if (!_activeByChunkId.TryGetValue(request.Chunk.ChunkId, out StrategicMapChunkLoadRequest? active) ||
            !string.Equals(active.ResourcePath, request.ResourcePath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Strategic map chunk completion has no matching request chunkId={request.Chunk.ChunkId} path={request.ResourcePath}.");
        }

        _activeByChunkId.Remove(request.Chunk.ChunkId);
        _activeResourcePaths.Remove(request.ResourcePath);
        _activeInRequestOrder.Remove(request);

        if (!resourceIsUsable)
        {
            // Failure memory is scene-lifetime state; camera churn must never create retries or log spam.
            _failedChunkIds.Add(request.Chunk.ChunkId);
            _failedResourcePaths.Add(request.ResourcePath);
            return StrategicMapChunkLoadCompletion.Failed;
        }

        if (!IsDesired(request))
        {
            return StrategicMapChunkLoadCompletion.Stale;
        }

        _residentByChunkId[request.Chunk.ChunkId] = request;
        return StrategicMapChunkLoadCompletion.Resident;
    }
}
