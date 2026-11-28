using System;

namespace Kezor.Core.Items
{
    public struct UpgradeResult
    {
        public bool Applied;   // runa consumida
        public bool Success;
        public string Message;
        public static UpgradeResult Reject(string m) => new UpgradeResult { Applied = false, Success = false, Message = m };
    }

    /// <summary>
    /// Aplicação direta de runas (selecionar a runa e depois o item).
    /// Aurora (Bless do MU): +0..+5 -> +1 (100%).  Eclipse (Soul): +0..+8 -> +1 (50%, +25% com Sorte);
    /// falha: +7/+8 voltam a +0, abaixo disso cai 1 nível.
    /// Vital: opção adicional +4 por aplicação até +28 (50%); falha zera a opção.
    /// +9 em diante: apenas pela Forja Arcana. Cristal do Vórtice é catalisador da Forja.
    /// </summary>
    public static class RuneService
    {
        public const int EclipseBaseRate = 50;
        public const int EclipseLuckBonus = 25;
        public const int VitalRate = 50;

        public static string Describe(RuneType t) => t switch
        {
            RuneType.Dawn => "Aplique num equipamento +0~+5: +1 nível (100%).",
            RuneType.Eclipse => "Aplique num equipamento +0~+8: +1 nível (50%, 75% com Sorte). Falha tira 1 nível (+7 e +8 voltam a +0).",
            RuneType.Vital => "Aplique num equipamento: opção adicional +4 (50%). Falha remove a opção.",
            RuneType.Vortex => "Catalisador da Forja Arcana (refino +10~+15 e Arma do Vórtice).",
            _ => ""
        };

        public static UpgradeResult Apply(Item rune, Item target, IRandom rng)
        {
            if (!rune.Def.IsRune) return UpgradeResult.Reject("não é uma runa");
            if (!target.Def.IsUpgradable) return UpgradeResult.Reject("item não pode ser refinado");

            switch (rune.Def.Rune)
            {
                case RuneType.Dawn:
                    if (target.Level > 5) return UpgradeResult.Reject("Runa da Aurora só funciona de +0 a +5");
                    target.Level++;
                    return new UpgradeResult { Applied = true, Success = true, Message = $"sucesso: +{target.Level}" };

                case RuneType.Eclipse:
                    if (target.Level > 8) return UpgradeResult.Reject("Runa do Eclipse só funciona de +0 a +8");
                    int rate = EclipseBaseRate + (target.Luck ? EclipseLuckBonus : 0);
                    if (rng.Roll(rate))
                    {
                        target.Level++;
                        return new UpgradeResult { Applied = true, Success = true, Message = $"sucesso: +{target.Level}" };
                    }
                    target.Level = target.Level >= 7 ? 0 : Math.Max(0, target.Level - 1);
                    return new UpgradeResult { Applied = true, Success = false, Message = $"falhou: item voltou a +{target.Level}" };

                case RuneType.Vital:
                    if (target.AdditionalOption >= Item.MaxAdditionalOption) return UpgradeResult.Reject("opção já no máximo");
                    if (rng.Roll(VitalRate))
                    {
                        target.AdditionalOption++;
                        return new UpgradeResult { Applied = true, Success = true, Message = $"sucesso: opção +{target.AdditionalOption * 4}" };
                    }
                    target.AdditionalOption = 0;
                    return new UpgradeResult { Applied = true, Success = false, Message = "falhou: opção removida" };

                default:
                    return UpgradeResult.Reject("esta runa é usada na Forja Arcana");
            }
        }
    }
}
