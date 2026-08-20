using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FallingWizard.Menus
{
    // Puts the highlight on a button when a menu opens. Without it a controller has nothing
    // selected and the menu looks frozen.
    static class MenuFocus
    {
        public static void Set(Selectable target)
        {
            if (target == null || EventSystem.current == null)
                return;

            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }
    }
}
