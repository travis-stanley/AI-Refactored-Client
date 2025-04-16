using EFT;
using Comfort.Common;

namespace AIRefactored.Core
{
    public static class GameWorldHandler
    {
        public static ClientGameWorld Get()
        {
            return Singleton<ClientGameWorld>.Instance;
        }

        public static string GetCurrentMapName()
        {
            return Get()?.MainPlayer?.Location ?? "unknown";
        }
    }
}
