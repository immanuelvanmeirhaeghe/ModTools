using UnityEngine;

namespace ModTools
{
    class PlayerExtended : Player
    {
        protected override void Start()
        {
            base.Start();
            new GameObject($"__{nameof(ModTools)}__").AddComponent<ModTools>();
        }
    }
}
