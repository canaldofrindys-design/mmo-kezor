using System.Collections.Generic;

namespace Kezor.Core.Server
{
    /// <summary>Grupo de até 5 personagens. Divide experiência e libera loot reservado entre os membros.</summary>
    public sealed class Party
    {
        public const int MaxMembers = 5;
        public const float ShareRange = 25f;
        public int Id;
        public int LeaderId;
        public readonly List<int> Members = new List<int>();
        public bool IsFull => Members.Count >= MaxMembers;

        /// <summary>
        /// Experiência total do grupo em % da experiência de um monstro (MU clássico): grupo comum de 2/3/4/5 = 160/180/200/220%;
        /// grupo com 3+ classes todas diferentes = 230/270/300%. O total é dividido entre os membros pelo nível.
        /// </summary>
        public static int ExpPercent(int membersInRange, bool allDifferentClasses)
        {
            if (membersInRange <= 1) return 100;
            if (allDifferentClasses && membersInRange >= 3) return membersInRange switch { 3 => 230, 4 => 270, _ => 300 };
            return membersInRange switch { 2 => 160, 3 => 180, 4 => 200, _ => 220 };
        }
    }
}
