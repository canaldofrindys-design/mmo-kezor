using System;
using System.Collections.Generic;
using System.Linq;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using Kezor.Core.World;

namespace Kezor.Core.Server
{
    /// <summary>
    /// Evento "Fenda da Geada Rubra" na Escarpa (nível 80+): de tempos em tempos uma fenda se abre no planalto central.
    /// Saem duas ondas de Estilhaços e depois o Colosso da Geada Rubra. Quem mais causou dano no evento inteiro
    /// ganha prêmio (runas e ouro) direto no inventário, mesmo que não tenha dado o último golpe.
    /// </summary>
    public sealed partial class GameServer
    {
        public const string RiftName = "Fenda da Geada Rubra";
        public const int RiftWaveSize = 8, RiftRewardedPlayers = 5;

        private float _riftTimer = -1;
        private bool _riftActive, _riftWarned;
        private int _riftWave;
        private double _riftEnds;
        private readonly HashSet<int> _riftMonsters = new HashSet<int>();
        private readonly Dictionary<int, long> _riftDamage = new Dictionary<int, long>();

        public bool RiftActive => _riftActive;
        public int RiftWave => _riftWave;

        private void TickRift(float dt)
        {
            if (!_maps.ContainsKey(MapDefinition.EscarpId)) return;
            if (_riftTimer < 0) _riftTimer = _opt.RiftFirstDelay;
            if (_riftActive)
            {
                _riftMonsters.RemoveWhere(id => !_monsters.ContainsKey(id));
                if (_time >= _riftEnds) { EndRift(null); return; }
                if (_riftMonsters.Count == 0)
                {
                    if (_riftWave < 2) SpawnRiftWave(_riftWave + 1);
                    else if (_riftWave == 2) SpawnRiftWave(3);
                }
                return;
            }
            _riftTimer -= dt;
            if (!_riftWarned && _riftTimer <= 30f && _riftTimer > 0.5f)
            {
                _riftWarned = true;
                SysAll($"[Evento] O gelo racha na Escarpa... a {RiftName} se abre em 30 segundos! (nível 80+, /warp ESCARPA)");
            }
            if (_riftTimer > 0) return;
            StartRift();
        }

        /// <summary>Abre a fenda agora (GM RIFT ou o relógio do evento).</summary>
        public void StartRift()
        {
            if (_riftActive || !_maps.ContainsKey(MapDefinition.EscarpId)) return;
            _riftActive = true; _riftWarned = false; _riftWave = 0;
            _riftDamage.Clear(); _riftMonsters.Clear();
            _riftEnds = _time + _opt.RiftDuration;
            SpawnRiftWave(1);
            SysAll($"[Evento] A {RiftName} se abriu no centro da Escarpa! Feche-a antes que o gelo tome tudo.");
            Broadcast("@EV EVENT;1;" + RiftName);
        }

        private void SpawnRiftWave(int wave)
        {
            _riftWave = wave;
            if (wave >= 3)
            {
                var boss = Spawn(MonsterCatalog.CrimsonFrostColossus, MapDefinition.EscarpId, MapDefinition.RiftX, MapDefinition.RiftZ);
                boss.WanderRadius = 4;
                _riftMonsters.Add(boss.Id);
                SysAll($"[Evento] O {boss.Name} saiu da fenda! Quem mais ferir os inimigos da fenda ganha o prêmio.");
                return;
            }
            for (int i = 0; i < RiftWaveSize; i++)
            {
                float a = i * (float)(Math.PI * 2 / RiftWaveSize) + wave * 0.4f;
                var m = Spawn(MonsterCatalog.RiftShard, MapDefinition.EscarpId,
                    MapDefinition.RiftX + (float)Math.Cos(a) * 6f, MapDefinition.RiftZ + (float)Math.Sin(a) * 6f);
                m.WanderRadius = 6;
                _riftMonsters.Add(m.Id);
            }
            if (wave > 1) SysAll($"[Evento] Segunda onda de Estilhaços saindo da {RiftName}!");
        }

        /// <summary>Chamado quando um monstro morre: soma o dano de cada jogador nos monstros da fenda.</summary>
        private void CountRiftKill(Monster m, Character owner)
        {
            if (!_riftActive || !_riftMonsters.Remove(m.Id)) return;
            foreach (var kv in m.DamageBy)
                _riftDamage[kv.Key] = (_riftDamage.TryGetValue(kv.Key, out var d) ? d : 0) + kv.Value;
            if (m.Def == MonsterCatalog.CrimsonFrostColossus) EndRift(owner);
        }

        /// <summary>Quanto cada jogador já contribuiu no evento atual (dano em monstros da fenda).</summary>
        public long RiftContribution(int characterId) => _riftDamage.TryGetValue(characterId, out var d) ? d : 0;

        private void EndRift(Character? killer)
        {
            foreach (var id in _riftMonsters) _monsters.Remove(id);
            _riftMonsters.Clear();
            _riftActive = false; _riftWave = 0;
            _riftTimer = _opt.RiftInterval;
            Broadcast("@EV EVENT;0;" + RiftName);
            if (killer == null)
            {
                SysAll($"[Evento] A {RiftName} se fechou sozinha... ninguém derrotou o Colosso desta vez.");
                _riftDamage.Clear();
                return;
            }
            SysAll($"[Evento] {killer.Name} derrubou o Colosso da Geada Rubra! A fenda se fechou.");
            int place = 0;
            foreach (var kv in _riftDamage.OrderByDescending(kv => kv.Value))
            {
                var c = CharById(kv.Key);
                if (c == null) continue;            // saiu do jogo antes do fim: sem prêmio
                place++;
                int runes = place == 1 ? 3 : place <= 3 ? 2 : 1;
                long gold = place == 1 ? 3_000_000 : place <= 3 ? 1_500_000 : 700_000;
                c.Gold += gold;
                Push(c, $"@EV GOLD;{gold}");
                for (int i = 0; i < runes; i++) GiveOrDrop(c, DropSystem.RandomRune(_rng));
                Sys(c, $"Prêmio da {RiftName}: {place}º lugar em dano ({kv.Value:N0}). +{gold:N0} de ouro e {runes} runa(s).");
                LogLine($"fenda: {place}º {c.Name} dano {kv.Value} ouro {gold} runas {runes}");
                if (place >= RiftRewardedPlayers) break;
            }
            _riftDamage.Clear();
        }

        /// <summary>Põe o item no inventário; se estiver cheio, deixa no chão aos pés do jogador (reservado para ele).</summary>
        private void GiveOrDrop(Character c, Item it)
        {
            if (c.Inventory.TryAdd(it, out _)) return;
            var g = new GroundItem
            {
                Id = _nextEntityId++, Item = it, MapId = c.MapId, X = c.X, Z = c.Z,
                OwnerId = c.Id, OwnerPartyId = 0, ReservedUntil = _time + 120, ExpiresAt = _time + 300
            };
            _ground[g.Id] = g;
            Sys(c, "Inventário cheio: o prêmio ficou no chão aos seus pés.");
        }
    }
}
