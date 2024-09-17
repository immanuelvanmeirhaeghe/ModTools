using ModTools.Managers;
using UnityEngine;

namespace ModTools
{
    class PlayerExtended : Player
    {
        protected override void Start()
        {
            base.Start();
            new GameObject($"__{nameof(ModTools)}__").AddComponent<ModTools>();
            new GameObject($"__{nameof(StylingManager)}__").AddComponent<StylingManager>();
        }
    }
}
