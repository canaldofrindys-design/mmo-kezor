using System.Collections;
using System.Linq;
using UnityEngine;
using Kezor.Core.Items;
using Kezor.Core.Net;

namespace Kezor.Client
{
    /// <summary>
    /// Atalho de teste (servidor deste PC, conta "dev" com GM): cria um herói, sobe de nível, distribui pontos e equipa
    /// itens numa chamada só. Usado pelos testes no Play e para tirar capturas; num servidor de fora o GM fica desligado.
    /// </summary>
    public sealed partial class KezorGame
    {
        public void DevSetup(string name, int classIndex, int level, string stats, int[] items, int itemLevel = 9)
            => StartCoroutine(DevSetupRoutine(name, classIndex, level, stats, items, itemLevel));

        private IEnumerator DevSetupRoutine(string name, int classIndex, int level, string stats, int[] items, int itemLevel)
        {
            if (_conn != null) Disconnect();
            yield return null;
            _name = name; _classIndex = classIndex;
            _helpShownOnce = true; _showHelp = false;
            if (!EnsureHost(Kezor.Core.Server.Protocol.DefaultPort, out _, out var hostErr)) { Debug.LogWarning("[Kezor] DevSetup: " + hostErr); yield break; }
            _host = "127.0.0.1"; _port = Kezor.Core.Server.Protocol.DefaultPort.ToString(); _serverLabel = "Servidor deste PC";
            for (int attempt = 0; attempt < 2 && _loginStage != LoginStage.Characters; attempt++)
            {   // primeiro tenta entrar; se a conta ainda não existe, cria
                _account = "dev"; _password = "kezor-dev"; _registering = attempt == 1;
                StartGame();
                for (int i = 0; i < 100 && _loginStage != LoginStage.Characters && (_conn != null || i < 2); i++) yield return new WaitForSeconds(0.05f);
            }
            if (_loginStage != LoginStage.Characters) { Debug.LogWarning("[Kezor] DevSetup: não entrou na conta dev: " + _loginError); yield break; }
            if (!_charRows.Exists(r => string.Equals(r.Name, name, System.StringComparison.OrdinalIgnoreCase)))
            {   // conta local: cria se ainda não existe (se a conta estiver cheia, apaga o último)
                if (_charRows.Count >= 5) { var last = _charRows[_charRows.Count - 1].Name; Send($"DELCHAR {last} {last}"); }
                Send($"CREATE {name} {Kezor.Core.Characters.ClassDefinition.All[classIndex].Code}");
            }
            Send($"PLAY {name}", r => { if (r.StartsWith("OK")) _loginStage = LoginStage.Form; });
            for (int i = 0; i < 100 && !InWorld; i++) yield return new WaitForSeconds(0.05f);
            Send($"GM LEVEL {level}");
            yield return new WaitForSeconds(0.2f);
            foreach (var part in stats.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=');
                if (kv.Length == 2) Send($"ADD {kv[0]} {kv[1]}");
            }
            foreach (var id in items) Send($"GM ITEM {id} {(id >= 2000 ? 0 : itemLevel)}");
            yield return new WaitForSeconds(0.5f);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = State.Inventory.Length - 1; i >= 0; i--)
                {
                    var it = State.Inventory[i];
                    if (it != null && it.Def.Slot != EquipSlot.None && items.Contains(it.Def.Id) && !State.Equipment.Values.Any(e => e.Def.Id == it.Def.Id)) Send($"EQUIP {i}");
                }
                yield return new WaitForSeconds(0.4f);
            }
            _showHelp = false;
        }

        /// <summary>Anda até o monstro mais próximo (para testes de combate).</summary>
        public int DevNearestMonster(bool walk = true)
        {
            EntityState? best = null; float bd = float.MaxValue;
            foreach (var e in State.Entities.Values)
                if (e.Kind == EntityKind.Monster && e.OwnerId == 0)
                {
                    float d = (e.X - State.Me.X) * (e.X - State.Me.X) + (e.Z - State.Me.Z) * (e.Z - State.Me.Z);
                    if (d < bd) { bd = d; best = e; }
                }
            if (best == null) return 0;
            if (walk) Send($"MOVE {Kezor.Core.Server.Protocol.F(best.X - 2f)} {Kezor.Core.Server.Protocol.F(best.Z)}");
            return best.Id;
        }
    }
}
