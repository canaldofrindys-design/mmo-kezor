using System;
using System.Collections.Generic;
using MuProto.Core.Characters;

namespace MuProto.Core.Server
{
    /// <summary>Porta de persistência. Implementar com SQLite/PostgreSQL em produção.</summary>
    public interface ICharacterRepository
    {
        Character? Load(string name);
        void Save(Character c);
    }

    public sealed class InMemoryCharacterRepository : ICharacterRepository
    {
        private readonly Dictionary<string, Character> _db = new Dictionary<string, Character>(StringComparer.OrdinalIgnoreCase);
        public Character? Load(string name) => _db.TryGetValue(name, out var c) ? c : null;
        public void Save(Character c) => _db[c.Name] = c;
    }
}
