using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FallingWizard.Menus
{
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
