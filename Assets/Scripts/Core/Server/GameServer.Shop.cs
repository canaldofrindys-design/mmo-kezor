using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using static Kezor.Core.Server.Protocol;

namespace Kezor.Core.Server
{
    /// <summary>
    /// Loja pessoal: na vila, o herói monta uma barraca com nome, põe itens da mochila à venda com preço em ouro e fica parado.
    /// A placa com o nome vai no snapshot. Compradores veem a vitrine (SHOP VIEW) e compram (SHOP BUY dono espaço preço):
    /// o servidor confere se o item ainda está no mesmo espaço, se o preço é o anunciado, ouro e espaço, e troca de uma vez.
    /// </summary>
    public sealed partial class GameServer
    {
        public const int ShopMaxListings = 16;
        public const float ShopViewRange = 6f;
        public const long ShopMaxPrice = 2_000_000_000;

        public sealed class PersonalShop
        {
            public string Title = "";
            public bool Open;
            public readonly List<(int slot, Item item, long price)> Listings = new List<(int, Item, long)>();
        }

        private readonly Dictionary<int, PersonalShop> _shops = new Dictionary<int, PersonalShop>();   // sessão -> loja
        public PersonalShop? ShopOfSession(int sessionId) => _shops.TryGetValue(sessionId, out var p) ? p : null;
        private string ShopTitle(Session s) => _shops.TryGetValue(s.Id, out var p) && p.Open ? p.Title : "";
        private bool ShopOpen(Session s) => _shops.TryGetValue(s.Id, out var p) && p.Open;

        private string ShopCmd(Session s, Character c, string[] p, string line)
        {
            if (p.Length < 2) return "ERR use SHOP OPEN <nome>|ADD <slot> <preço>|REMOVE <slot>|CLOSE|VIEW <jogador>|BUY <jogador> <slot> <preço>";
            var shop = _shops.TryGetValue(s.Id, out var existing) ? existing : null;
            switch (p[1].ToUpperInvariant())
            {
                case "OPEN":
                {
                    var map = MapOf(c.MapId);
                    if (!map.InSafeZone(c.X, c.Z)) return "ERR a loja só abre dentro da vila";
                    if (_tradeOf.ContainsKey(s.Id)) return "ERR termine a troca primeiro";
                    var title = Clean(line.Trim().Length > 9 ? line.Trim().Substring(9).Trim() : "");
                    if (title.Length == 0) title = $"Loja de {c.Name}";
                    if (title.Length > 24) title = title.Substring(0, 24);
                    shop ??= _shops[s.Id] = new PersonalShop();
                    if (shop.Listings.Count == 0) return "ERR ponha pelo menos um item à venda (SHOP ADD)";
                    shop.Title = title; shop.Open = true;
                    s.ClearIntent(); c.Dismount();
                    return $"OK loja \"{title}\" aberta";
                }
                case "ADD":
                {
                    int slot = Int(p, 2);
                    if (p.Length < 4 || !TryPL(p[3], out var price) || price <= 0 || price > ShopMaxPrice) return "ERR preço inválido";
                    var it = c.Inventory.Get(slot);
                    if (it == null) return "ERR slot vazio";
                    if (it.Def.Bound) return "ERR item vinculado não pode ser vendido";
                    shop ??= _shops[s.Id] = new PersonalShop();
                    if (shop.Listings.Any(l => l.slot == slot)) return "ERR item já está à venda";
                    if (shop.Listings.Count >= ShopMaxListings) return $"ERR no máximo {ShopMaxListings} itens";
                    shop.Listings.Add((slot, it, price));
                    return $"OK {it.DisplayName} à venda por {price:N0}";
                }
                case "REMOVE":
                {
                    if (shop == null) return "ERR nenhuma loja";
                    int slot = Int(p, 2);
                    if (shop.Listings.RemoveAll(l => l.slot == slot) == 0) return "ERR item não está à venda";
                    if (shop.Listings.Count == 0) shop.Open = false;
                    return "OK";
                }
                case "CLOSE":
                    if (shop == null || !shop.Open) return "ERR a loja não está aberta";
                    shop.Open = false;
                    return "OK loja fechada";
                case "MINE":
                    SendShop(s, s, c);
                    return "OK";
                case "VIEW":
                {
                    var owner = p.Length > 2 ? CharByName(p[2]) : null;
                    var os = owner != null ? SessionOf(owner.Id) : null;
                    if (owner == null || os == null || !ShopOpen(os)) return "ERR loja fechada";
                    if (owner.MapId != c.MapId || MathK.Dist(c.X, c.Z, owner.X, owner.Z) > ShopViewRange) return "ERR chegue perto da barraca";
                    SendShop(s, os, owner);
                    return "OK";
                }
                case "BUY": return ShopBuy(s, c, p);
                default: return "ERR subcomando desconhecido";
            }
        }

        private string ShopBuy(Session s, Character buyer, string[] p)
        {
            if (p.Length < 5) return "ERR use SHOP BUY <jogador> <slot> <preço>";
            var owner = CharByName(p[2]);
            var os = owner != null ? SessionOf(owner.Id) : null;
            if (owner == null || os == null || owner == buyer) return "ERR loja não encontrada";
            if (!_shops.TryGetValue(os.Id, out var shop) || !shop.Open) return "ERR loja fechada";
            if (owner.MapId != buyer.MapId || MathK.Dist(buyer.X, buyer.Z, owner.X, owner.Z) > ShopViewRange) return "ERR chegue perto da barraca";
            int slot = Int(p, 3);
            if (!TryPL(p[4], out var price)) return "ERR preço inválido";
            int idx = shop.Listings.FindIndex(l => l.slot == slot);
            if (idx < 0) return "ERR item já foi vendido";
            var (_, item, listed) = shop.Listings[idx];
            if (listed != price) return "ERR o preço mudou; abra a vitrine de novo";       // nada de trocar o preço na última hora
            if (!ReferenceEquals(owner.Inventory.Get(slot), item)) { shop.Listings.RemoveAt(idx); return "ERR item não está mais à venda"; }
            if (buyer.Gold < price) return "ERR ouro insuficiente";
            owner.Inventory.Remove(slot);
            if (!buyer.Inventory.TryAdd(item, out _)) { owner.Inventory.Put(slot, item); return "ERR mochila cheia"; }
            buyer.Gold -= price; owner.Gold += price;
            shop.Listings.RemoveAt(idx);
            buyer.Inventory.Touch(); owner.Inventory.Touch();
            Sys(os, $"{buyer.Name} comprou {item.DisplayName} por {price:N0} de ouro.");
            LogLine($"loja: {buyer.Name} comprou {item.DisplayName} de {owner.Name} por {price:N0}");
            if (shop.Listings.Count == 0) { shop.Open = false; Sys(os, "Tudo vendido: a loja fechou."); }
            SendShop(s, os, owner);
            SaveCharacter(s); SaveCharacter(os);
            return $"OK comprou {item.DisplayName} por {price:N0}";
        }

        private static bool CanStack(Inventory inv, Item it)
        {
            for (int i = 0; i < inv.Capacity; i++)
            {
                var x = inv.Get(i);
                if (x != null && x.Def == it.Def && x.Quantity + it.Quantity <= x.Def.MaxStack) return true;
            }
            return false;
        }

        /// <summary>@PSHOP dono|título|aberta|slot;preço;item...  (para o comprador ou para o próprio dono)</summary>
        private void SendShop(Session to, Session ownerSession, Character owner)
        {
            var shop = _shops.TryGetValue(ownerSession.Id, out var x) ? x : new PersonalShop();
            var sb = new StringBuilder("@PSHOP ").Append(Clean(owner.Name)).Append(ListSep).Append(Clean(shop.Title)).Append(ListSep).Append(shop.Open ? 1 : 0);
            foreach (var (slot, item, price) in shop.Listings)
                sb.Append(ListSep).Append(slot).Append(Sep).Append(price).Append(Sep).Append(ItemFields(item));
            to.Outbox.Add(sb.ToString());
        }

        /// <summary>Remove anúncios de itens que saíram do lugar; fecha a loja de quem saiu da vila ou caiu.</summary>
        private void TickShops()
        {
            foreach (var kv in _shops.ToList())
            {
                if (!_sessions.TryGetValue(kv.Key, out var s) || s.Character == null) { _shops.Remove(kv.Key); continue; }
                var c = s.Character; var shop = kv.Value;
                shop.Listings.RemoveAll(l => !ReferenceEquals(c.Inventory.Get(l.slot), l.item));
                if (shop.Open && (shop.Listings.Count == 0 || c.IsDead || !MapOf(c.MapId).InSafeZone(c.X, c.Z))) shop.Open = false;
            }
        }
    }
}
