using UnityEngine;

namespace KMA.UI.Kit
{
    /// Marks content art (sport icons, hearts, stars, portraits, badges, illustrations) that the style
    /// audits skip, together with its children. Panels and buttons never carry it.
    [DisallowMultipleComponent]
    public sealed class StyleExempt : MonoBehaviour { }
}
