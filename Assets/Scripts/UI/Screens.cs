namespace FallingWizard.UI
{
    public static class Screens
    {
        public static bool ModalOpen { get; private set; }

        public static void Claim() => ModalOpen = true;

        public static void Release() => ModalOpen = false;
    }
}
