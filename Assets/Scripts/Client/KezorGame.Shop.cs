using Kezor.Core.Net;

namespace Kezor.Client
{
    /// <summary>
    /// Loja pessoal. Tecla B monta a própria barraca; clicar num jogador com placa de loja abre a vitrine dele.
    /// As janelas ficam em UI/Windows.cs; o servidor confere preço, item e espaço.
    /// </summary>
    public sealed partial class KezorGame
    {
        internal bool _showMyShop;
        internal string _shopTitleText = "", _shopPriceText = "10000";
        internal bool MyShopEditing => _showMyShop;

        /// <summary>Clique em jogador com placa de loja: abre a vitrine. Retorna true se tratou o clique.</summary>
        internal bool TryOpenShopOf(int entityId)
        {
            if (!State.Entities.TryGetValue(entityId, out var e) || e.Kind != EntityKind.Player || e.Shop == "") return false;
            Send($"SHOP VIEW {e.Name}");
            return true;
        }
    }
}
