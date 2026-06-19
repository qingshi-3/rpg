using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.World.Sites;

namespace Rpg.Presentation.Battle;

public partial class BattleGridHighlightOverlay
{
    private WorldSiteRoot FindWorldSiteRoot()
    {
        Node current = this;

        while (current != null)
        {
            if (current is WorldSiteRoot siteRoot)
            {
                return siteRoot;
            }

            current = current.GetParent();
        }

        return null;
    }
}
