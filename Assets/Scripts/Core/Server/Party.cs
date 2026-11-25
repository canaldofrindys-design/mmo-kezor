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

        /// <summary>Bônus total de experiência por tamanho do grupo: +15% por membro extra no alcance.</summary>
        public static float ExpBonus(int membersInRange) => 1f + 0.15f * (membersInRange - 1);
    }
}
