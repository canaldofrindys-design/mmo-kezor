using System;

namespace Kezor.Core.Items
{
    /// <summary>
    /// Baú (armazém) da cidade: grade 8×15 com empilhamento igual ao inventário, mais ouro guardado.
    /// Por enquanto fica no personagem; com as contas (etapa 3) passa a ser da conta e é salvo junto.
    /// </summary>
    public sealed class Vault
    {
        public const int Columns = 8, Rows = 15, Capacity = Columns * Rows;
        public const long MaxGold = 2_000_000_000;
        public Inventory Items { get; } = new Inventory(Capacity);
        public long Gold { get; private set; }
        /// <summary>Muda a cada operação (o servidor reenvia o @VAULT quando muda).</summary>
        public int Version => Items.Version * 7919 + (int)(Gold % 7919);

        public bool CanStore(Item it, out string error)
        {
            error = "";
            if (it.Def.Bound) { error = "item vinculado não pode ir para o baú"; return false; }
            return true;
        }

        public bool AddGold(long amount, out string error)
        {
            error = "";
            if (amount <= 0) { error = "quantia inválida"; return false; }
            if (Gold + amount > MaxGold) { error = $"o baú guarda no máximo {MaxGold:N0} de ouro"; return false; }
            Gold += amount; Items.Touch();
            return true;
        }

        public bool TakeGold(long amount, out string error)
        {
            error = "";
            if (amount <= 0) { error = "quantia inválida"; return false; }
            if (amount > Gold) { error = "ouro insuficiente no baú"; return false; }
            Gold -= amount; Items.Touch();
            return true;
        }

        /// <summary>Restaura o ouro (persistência).</summary>
        public void RestoreGold(long gold) => Gold = Math.Max(0, Math.Min(MaxGold, gold));
    }
}
