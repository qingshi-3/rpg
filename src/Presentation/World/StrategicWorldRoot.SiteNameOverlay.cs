using System.Collections.Generic;
using Godot;
using Rpg.Definitions.World;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private void BuildSiteNameOverlay()
    {
        _worldSiteNameOverlay = new Control
        {
            Name = "WorldSiteNameOverlay",
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 120
        };
        SetFullRect(_worldSiteNameOverlay);
        // This overlay is the home for world annotations that must track map
        // coordinates but remain screen-space UI, starting with site names.
        AddChild(_worldSiteNameOverlay);
    }

    private void SyncSiteNameOverlay()
    {
        if (_worldSiteNameOverlay == null || Definition == null || State == null)
        {
            foreach (WorldSiteNameBadge badge in _worldSiteNameBadges.Values)
            {
                if (badge != null)
                {
                    badge.Visible = false;
                }
            }

            return;
        }

        HashSet<string> activeSiteIds = new(State.SiteStates.Keys);
        HashSet<string> visitedSiteIds = new();
        foreach (WorldSiteDefinition siteDefinition in Definition.SiteDefinitions)
        {
            if (siteDefinition == null)
            {
                continue;
            }

            WorldSiteNameBadge badge = EnsureSiteNameBadge(siteDefinition.Id);
            if (badge == null)
            {
                continue;
            }

            visitedSiteIds.Add(siteDefinition.Id);
            if (!activeSiteIds.Contains(siteDefinition.Id))
            {
                badge.Visible = false;
                continue;
            }

            bool selected = siteDefinition.Id == _selectedSiteId;
            bool hovered = siteDefinition.Id == _hoveredSiteId;
            WorldSiteNamePresentationMode presentationMode = ResolveSiteNamePresentationMode(siteDefinition, selected, hovered);
            badge.Bind(siteDefinition.DisplayName, selected, hovered, presentationMode);
            badge.SetScreenRect(GetSiteLabelRect(siteDefinition));
        }

        foreach (KeyValuePair<string, WorldSiteNameBadge> entry in _worldSiteNameBadges)
        {
            if (!visitedSiteIds.Contains(entry.Key) && entry.Value != null)
            {
                entry.Value.Visible = false;
            }
        }
    }

    private WorldSiteNameBadge EnsureSiteNameBadge(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId) || _worldSiteNameOverlay == null)
        {
            return null;
        }

        if (_worldSiteNameBadges.TryGetValue(siteId, out WorldSiteNameBadge badge) && badge != null)
        {
            return badge;
        }

        badge = GameUiSceneFactory.CreateWorldSiteNameBadge(nameof(StrategicWorldRoot));
        if (badge == null)
        {
            return null;
        }

        _worldSiteNameOverlay.AddChild(badge);
        _worldSiteNameBadges[siteId] = badge;
        return badge;
    }

    private static WorldSiteNamePresentationMode ResolveSiteNamePresentationMode(
        WorldSiteDefinition definition,
        bool selected,
        bool hovered)
    {
        if (definition == null)
        {
            return WorldSiteNamePresentationMode.Hidden;
        }

        if (selected || hovered)
        {
            return WorldSiteNamePresentationMode.Full;
        }

        // Placeholder for future zoom-aware label policies. For now the new overlay
        // always renders a fixed-size full label.
        return WorldSiteNamePresentationMode.Full;
    }
}
