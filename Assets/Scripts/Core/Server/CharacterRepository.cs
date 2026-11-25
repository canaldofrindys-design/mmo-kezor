using System;
using System.Collections.Generic;
using Kezor.Core.Characters;

namespace Kezor.Core.Server
{
    /// <summary>Porta de persistência. Implementar com SQLite/PostgreSQL em produção.</summary>
    public interface ICharacterRepository
    {
        Character? Load(string name);
        void Save(Character c);
        /// <summary>Apaga o personagem salvo (usado pelo DELCHAR da tela de seleção).</summary>
        void Delete(string name) { }
    }

    public sealed class InMemoryCharacterRepository : ICharacterRepository
    {
        private readonly Dictionary<string, Character> _db = new Dictionary<string, Character>(StringComparer.OrdinalIgnoreCase);
        public Character? Load(string name) => _db.TryGetValue(name, out var c) ? c : null;
        public void Save(Character c) => _db[c.Name] = c;
        public void Delete(string name) => _db.Remove(name);
    }
}
