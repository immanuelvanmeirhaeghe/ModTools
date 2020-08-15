using System.Linq;
using UnityEngine;

namespace ModTools
{
    class ConstructionGhostManagerExtended : ConstructionGhostManager
    {
        protected override void Update()
        {
                if ( (ModTools.Get().IsLocalOrHost || ModTools.Get().IsModActiveForMultiplayer) && ModTools.Get().UseOptionF8  && Input.GetKeyDown(KeyCode.F8))
                {
                    foreach (ConstructionGhost m_Unfinished in m_AllGhosts.Where(
                                              m_Ghost => m_Ghost.gameObject.activeSelf
                                                                       && m_Ghost.GetState() != ConstructionGhost.GhostState.Ready))
                    {
                        m_Unfinished.SetState(ConstructionGhost.GhostState.Ready);
                    }
                }
                base.Update();
        }
    }
}
