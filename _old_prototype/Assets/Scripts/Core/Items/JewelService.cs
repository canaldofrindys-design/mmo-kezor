namespace MuProto.Core.Items
{
    public struct UpgradeResult
    {
        public bool Applied;   // jóia consumida
        public bool Success;
        public string Message;
        public static UpgradeResult Reject(string m) => new UpgradeResult { Applied = false, Success = false, Message = m };
    }

    /// <summary>
    /// Aplicação direta de jóias (arrastar jóia sobre o item).
    /// Bless: +0..+5 -> +1 (100%).  Soul: +6..+8 -> +1 (50%, +25% com Luck); falha: +6 cai p/ +5, +7/+8 voltam a +0.
    /// Life: opção adicional +4 por aplicação até +28 (50%); falha zera a opção.
    /// +9 em diante: apenas pela Chaos Machine.
    /// </summary>
    public static class JewelService
    {
        public const int SoulBaseRate = 50;
        public const int SoulLuckBonus = 25;
        public const int LifeRate = 50;

        public static UpgradeResult Apply(Item jewel, Item target, IRandom rng)
        {
            if (!jewel.Def.IsJewel) return UpgradeResult.Reject("não é uma jóia");
            if (!target.Def.IsUpgradable) return UpgradeResult.Reject("item não pode ser refinado");

            switch (jewel.Def.Jewel)
            {
                case JewelType.Bless:
                    if (target.Level > 5) return UpgradeResult.Reject("Bless só funciona de +0 a +5");
                    target.Level++;
                    return new UpgradeResult { Applied = true, Success = true, Message = $"sucesso: +{target.Level}" };

                case JewelType.Soul:
                    if (target.Level < 6 || target.Level > 8) return UpgradeResult.Reject("Soul só funciona de +6 a +8");
                    int rate = SoulBaseRate + (target.Luck ? SoulLuckBonus : 0);
                    if (rng.Roll(rate))
                    {
                        target.Level++;
                        return new UpgradeResult { Applied = true, Success = true, Message = $"sucesso: +{target.Level}" };
                    }
                    target.Level = target.Level == 6 ? 5 : 0;
                    return new UpgradeResult { Applied = true, Success = false, Message = $"falhou: item voltou a +{target.Level}" };

                case JewelType.Life:
                    if (target.AdditionalOption >= Item.MaxAdditionalOption) return UpgradeResult.Reject("opção já no máximo");
                    if (rng.Roll(LifeRate))
                    {
                        target.AdditionalOption++;
                        return new UpgradeResult { Applied = true, Success = true, Message = $"sucesso: opção +{target.AdditionalOption * 4}" };
                    }
                    target.AdditionalOption = 0;
                    return new UpgradeResult { Applied = true, Success = false, Message = "falhou: opção removida" };

                default:
                    return UpgradeResult.Reject("esta jóia é usada na Chaos Machine");
            }
        }
    }
}
