using System.Collections.Generic;
using System.Linq;

namespace Rpg.Domain.World;

public sealed class ResourceStore
{
    public Dictionary<string, int> Amounts { get; set; } = new();
    public List<ResourceReservation> Reservations { get; set; } = new();

    public int GetAmount(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return 0;
        }

        return Amounts.TryGetValue(resourceId, out int value) ? value : 0;
    }

    public int GetReserved(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return 0;
        }

        return Reservations
            .Where(reservation => reservation.ResourceId == resourceId)
            .Sum(reservation => reservation.Amount);
    }

    public int GetAvailable(string resourceId)
    {
        return System.Math.Max(0, GetAmount(resourceId) - GetReserved(resourceId));
    }

    public void Set(string resourceId, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return;
        }

        Amounts[resourceId] = System.Math.Max(0, amount);
    }

    public void Add(string resourceId, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || amount == 0)
        {
            return;
        }

        Set(resourceId, GetAmount(resourceId) + amount);
    }

    public bool CanSpend(string resourceId, int amount, bool useAvailable = true)
    {
        if (amount <= 0)
        {
            return true;
        }

        int current = useAvailable ? GetAvailable(resourceId) : GetAmount(resourceId);
        return current >= amount;
    }

    public bool Spend(string resourceId, int amount, bool useAvailable = true)
    {
        if (!CanSpend(resourceId, amount, useAvailable))
        {
            return false;
        }

        Add(resourceId, -amount);
        return true;
    }

    public bool Reserve(string resourceId, int amount, string sourceId, string sourceKind)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (!CanSpend(resourceId, amount))
        {
            return false;
        }

        Reservations.Add(new ResourceReservation(resourceId, amount, sourceId, sourceKind));
        return true;
    }

    public int ReleaseReservations(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return 0;
        }

        int released = Reservations
            .Where(reservation => reservation.SourceId == sourceId)
            .Sum(reservation => reservation.Amount);

        Reservations = Reservations
            .Where(reservation => reservation.SourceId != sourceId)
            .ToList();
        return released;
    }

    public int ReleaseReservationsBySite(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            return 0;
        }

        int released = Reservations
            .Where(reservation => reservation.SourceId.StartsWith(siteId + ":", System.StringComparison.Ordinal))
            .Sum(reservation => reservation.Amount);

        Reservations = Reservations
            .Where(reservation => !reservation.SourceId.StartsWith(siteId + ":", System.StringComparison.Ordinal))
            .ToList();
        return released;
    }
}
