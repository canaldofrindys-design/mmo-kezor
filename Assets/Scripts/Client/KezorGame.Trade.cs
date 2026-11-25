using UnityEngine;
using Kezor.Core.Net;

namespace Kezor.Client
{
    /// <summary>Troca entre jogadores: T sobre outro jogador pede a troca (ou /trade Nome). A janela fica em UI/Windows.cs.</summary>
    public sealed partial class KezorGame
    {
        private void TryTradeHotkey()
        {
            if (!InputK.KeyDown(KeyCode.T) || _hoverId == 0 || _hoverKind != EntityKind.Player) return;
            if (State.Entities.TryGetValue(_hoverId, out var e)) Send($"TRADE {e.Name}");
        }
    }
}
