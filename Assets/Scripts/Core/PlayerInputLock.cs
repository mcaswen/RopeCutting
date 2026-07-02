using System.Collections.Generic;

namespace Core
{
    public static class PlayerInputLock
    {
        private static readonly HashSet<object> Owners = new HashSet<object>();

        public static bool IsLocked => Owners.Count > 0;

        public static void Lock(object owner)
        {
            if (owner == null) return;

            Owners.Add(owner);
        }

        public static void Unlock(object owner)
        {
            if (owner == null) return;

            Owners.Remove(owner);
        }

        public static void Clear()
        {
            Owners.Clear();
        }
    }
}
