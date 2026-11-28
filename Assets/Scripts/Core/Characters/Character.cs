using System;
using System.Collections.Generic;
using Kezor.Core.Combat;
using Kezor.Core.Items;

namespace Kezor.Core.Characters
{
    public sealed class Character : ICombatant, IShieldBearer
    {
        public const int MaxLevel = 400;
        public const int MaxRebirths = 50;
        public const int RebirthBonusPoints = 500;
        public const int MaxStatValue = 32767;
        public const int InventorySize = 64, InventoryColumns = 8;   // mochila em grade 8×8

        public int Id { get; internal set; }
        /// <summary>Conta dona do personagem (vazio = convidado, modo local/testes).</summary>
        public string AccountName { get; set; } = "";
        public string Name { get; }
        public CharacterClass Class { get; }
        public ClassDefinition Def => ClassDefinition.Get(Class);
        public int Level { get; private set; } = 1;
        public long Experience { get; private set; }
        public int Rebirths { get; private set; }
        public int FreePoints { get; private set; }
        public Attributes Stats;
        public long Gold { get; set; }
        public int Hp { get; set; }
        public int Mp { get; set; }
        /// <summary>Contador de abates de jogadores (PvP). Exibido como nome em vermelho.</summary>
        public int Infamy { get; set; }

        // Posição no mapa (plano XZ, unidades do mundo)
        public float X { get; set; }
        public float Z { get; set; }
        /// <summary>Direção para onde o herói olha (graus, 0 = +Z). Salva com a posição, para relogar virado igual.</summary>
        public float Facing { get; set; }
        public int MapId { get; set; } = 1;

        public Inventory Inventory { get; } = new Inventory(InventorySize, InventoryColumns);
        /// <summary>Baú da cidade (vai para a conta na etapa de contas).</summary>
        public Vault Vault { get; set; } = new Vault();
        public Dictionary<EquipSlot, Item> Equipment { get; } = new Dictionary<EquipSlot, Item>();
        /// <summary>Árvore da Ascensão (nível 400): permanente, sobrevive ao renascimento.</summary>
        public AscensionState Ascension { get; } = new AscensionState();

        public Character(int id, string name, CharacterClass cls)
        {
            Id = id; Name = name; Class = cls;
            Stats = Def.BaseStats;
            Hp = MaxHp; Mp = MaxMp;
            Vigor = MaxVigor;
        }

        // ----- Vigor e Escudo (HUD v4) -----
        /// <summary>Custo de Vigor de cada skill (o ataque básico não gasta).</summary>
        public const int SkillVigorCost = 6;
        /// <summary>Intervalo de recuperação de mana e AG (MU: a cada tique de 3 s).</summary>
        public const float RecoveryInterval = 3f;
        /// <summary>Mana recuperada por tique: 3,7% do máximo (MU).</summary>
        public const float ManaRecovery = 0.037f;
        /// <summary>AG recuperado por tique além da fração da classe: 2, e +3 na zona segura (MU).</summary>
        public const int AgRecoveryAbsolute = 2, AgRecoverySafeZone = 3;
        /// <summary>Segundos sem atacar nem apanhar para sair de combate.</summary>
        public const float CombatSeconds = 5f;
        /// <summary>Escudo (SD, MU): só segura golpes de jogadores; 90% do golpe vai no escudo e 10% na vida.
        /// Recarrega só na zona segura, depois de 6 s sem apanhar.</summary>
        public const int ShieldRatioPercent = 90;
        public const float ShieldDelaySeconds = 6f, ShieldRegenPerSecond = 0.10f;

        /// <summary>Vigor (AG do MU): fôlego para skills. Sem vigor suficiente a skill não sai (só o ataque básico).</summary>
        public float Vigor { get; set; }
        public int MaxVigor
        {
            get
            {
                var d = Def;
                return Math.Max(1, (int)(d.AgEne * Stats.Energy + d.AgVit * Stats.Vitality + d.AgAgi * Stats.Agility + d.AgStr * Stats.Strength))
                       + Ascension.Total(AscEffect.Vigor);
            }
        }
        /// <summary>Escudo (SD): só absorve golpes de jogadores (PvP). Começa vazio e enche na zona segura.</summary>
        public float Shield { get; set; }
        /// <summary>SD máximo (MU): 1,2·(FOR+AGI+VIT+ENE) + defesa + Nv²/30.</summary>
        public int MaxShield
        {
            get
            {
                int sd = (int)(1.2f * (Stats.Strength + Stats.Agility + Stats.Vitality + Stats.Energy)) + GetCombatStats().Defense + Level * Level / 30;
                return sd + sd * Ascension.Total(AscEffect.ShieldPct) / 100;
            }
        }
        /// <summary>Na zona segura (o servidor atualiza a cada tique): o SD só recarrega aqui e o AG volta mais rápido.</summary>
        public bool InSafeZone { get; set; }
        /// <summary>Tempo restante em combate (atacou ou apanhou há pouco).</summary>
        public float CombatTimer { get; set; }
        /// <summary>Tempo até o Escudo voltar a carregar.</summary>
        public float ShieldDelay { get; set; }
        public bool InCombat => CombatTimer > 0f;

        /// <summary>Atalhos de consumíveis Q, W, E, R (id do item; 0 = vazio). Salvos com o personagem.</summary>
        public readonly int[] QuickSlots = { 2000, 2002, 2001, 0 };
        public const int QuickSlotCount = 4;

        public int AbsorbShield(int damage, bool fromPlayer)
        {
            CombatTimer = CombatSeconds;
            ShieldDelay = ShieldDelaySeconds;
            if (!fromPlayer || Shield <= 0f || damage <= 0) return damage;
            int toShield = damage * ShieldRatioPercent / 100;
            int toHp = damage - toShield;
            if (toShield > Shield) { toHp += toShield - (int)Shield; toShield = (int)Shield; }
            Shield -= toShield;
            return toHp;
        }

        /// <summary>Marca combate (quando o herói ataca).</summary>
        public void EnterCombat() => CombatTimer = CombatSeconds;

        /// <summary>Gasta o vigor de uma skill (false se não houver).</summary>
        public bool SpendVigor(int amount)
        {
            if (Vigor < amount) return false;
            Vigor -= amount;
            return true;
        }

        private float _recoveryAcc;

        /// <summary>
        /// Passa o tempo. Como no MU: a vida não volta sozinha; mana volta 3,7% do máximo e o AG a fração da classe + 2 (+3 na
        /// zona segura) a cada 3 s; o escudo só recarrega na zona segura, depois de 6 s sem apanhar.
        /// </summary>
        public void TickVitals(float dt)
        {
            if (dt <= 0f) return;
            if (CombatTimer > 0f) CombatTimer = Math.Max(0f, CombatTimer - dt);
            if (IsDead) { Shield = 0f; return; }
            _recoveryAcc += dt;
            while (_recoveryAcc >= RecoveryInterval)
            {
                _recoveryAcc -= RecoveryInterval;
                int maxMp = MaxMp, maxAg = MaxVigor;
                Mp = Math.Min(maxMp, Mp + Math.Max(1, (int)(maxMp * ManaRecovery)));
                Vigor = Math.Min(maxAg, Vigor + maxAg * Def.AgRecovery + AgRecoveryAbsolute + (InSafeZone ? AgRecoverySafeZone : 0));
            }
            if (ShieldDelay > 0f) { ShieldDelay = Math.Max(0f, ShieldDelay - dt); return; }
            if (!InSafeZone) return;
            int maxSd = MaxShield;
            Shield = Math.Min(maxSd, Shield + maxSd * ShieldRegenPerSecond * dt);
        }

        // ----- PK (estados do MU) -----
        /// <summary>Cada grau de PK dura 3 h de jogo; depois cai um grau.</summary>
        public const int PkStageSeconds = 3 * 3600;
        /// <summary>Grau de PK: 0 normal, 1 aviso (laranja), 2 assassino 1º grau, 3 assassino 2º grau (vermelho).</summary>
        public int PkLevel => Math.Min(Infamy, 3);
        /// <summary>Assassino (2+ mortes): matá-lo não dá PK.</summary>
        public bool IsMurderer => Infamy >= 2;
        /// <summary>Segundos que faltam para cair um grau de PK.</summary>
        public int PkSeconds { get; set; }
        private float _pkAcc;

        /// <summary>Registra a morte de um jogador inocente: sobe um grau (até o 3º) e o tempo recomeça; no 3º grau o tempo soma.</summary>
        public void AddPlayerKill()
        {
            if (Infamy < 3) { Infamy++; PkSeconds = Math.Max(PkSeconds, PkStageSeconds); }
            else PkSeconds += PkStageSeconds;
        }

        /// <summary>Conta o tempo do PK (só com o herói online) e baixa um grau quando acaba.</summary>
        public void TickPk(float dt)
        {
            if (Infamy <= 0) { PkSeconds = 0; _pkAcc = 0; return; }
            _pkAcc += dt;
            int whole = (int)_pkAcc;
            if (whole <= 0) return;
            _pkAcc -= whole;
            PkSeconds -= whole;
            if (PkSeconds > 0) return;
            Infamy = Math.Max(0, Math.Min(Infamy, 3) - 1);
            PkSeconds = Infamy > 0 ? PkStageSeconds + PkSeconds : 0;
        }

        /// <summary>Restaura o progresso salvo (persistência).</summary>
        public void RestoreProgress(int level, long experience, int rebirths, int freePoints)
        {
            Level = Math.Max(1, Math.Min(MaxLevel, level));
            Experience = Math.Max(0, experience);
            Rebirths = Math.Max(0, Math.Min(MaxRebirths, rebirths));
            FreePoints = Math.Max(0, freePoints);
        }

        public bool IsDead => Hp <= 0;
        public int MaxHp
        {
            get
            {
                int hp = (int)(Def.HpConst + Def.HpPerLevel * Level + Def.HpPerVitality * Stats.Vitality);
                foreach (var it in Equipment.Values)
                    if (it.Durability > 0 && (it.Ascended & AscendedOption.MaxHpPlus) != 0) hp += hp * 4 / 100;
                hp += hp * Ascension.Total(AscEffect.HpPct) / 100;
                foreach (var it in Equipment.Values) if (it.Def.IsJewel) hp += it.Def.JewelHp;
                return hp;
            }
        }
        public int MaxMp
        {
            get
            {
                int mp = (int)(Def.MpConst + Def.MpPerLevel * Level + Def.MpPerEnergy * (Stats.Energy - Def.MpEnergyOffset));
                foreach (var it in Equipment.Values)
                    if (it.Durability > 0 && (it.Ascended & AscendedOption.MaxMpPlus) != 0) mp += mp * 4 / 100;
                mp += mp * Ascension.Total(AscEffect.MpPct) / 100;
                foreach (var it in Equipment.Values) if (it.Def.IsJewel) mp += it.Def.JewelMp;
                return mp;
            }
        }

        /// <summary>Slot onde um item entra: anéis vão no primeiro dedo livre (ou trocam o Ring1 se os dois estiverem ocupados).</summary>
        public EquipSlot TargetSlot(ItemDefinition d)
        {
            if (d.Kind != ItemKind.Ring) return d.Slot;
            if (!Equipment.ContainsKey(EquipSlot.Ring1)) return EquipSlot.Ring1;
            if (!Equipment.ContainsKey(EquipSlot.Ring2)) return EquipSlot.Ring2;
            return EquipSlot.Ring1;
        }
        /// <summary>Montado na montaria equipada (validado pelo servidor; desmonta ao atacar ou tirar a montaria).</summary>
        public bool Mounted { get; private set; }
        public Item? MountItem => Equipment.TryGetValue(EquipSlot.Mount, out var m) ? m : null;
        /// <summary>Conjunto completo equipado (bônus ativo), ou None.</summary>
        public ArmorSet ActiveSet => ArmorSets.Complete(Equipment);
        public PetType ActivePet => Equipment.TryGetValue(EquipSlot.Pet, out var p) ? p.Def.Pet : PetType.None;
        public float MoveSpeed => Def.MoveSpeed * (Mounted && MountItem != null ? 1f + MountItem.Def.MountSpeedPercent / 100f : 1f)
                                  * (1f + (BuffSpeedPercent + AuraSpeedPercent + Ascension.Total(AscEffect.SpeedPct)) / 100f);

        // ----- Buffs (skills em si mesmo) e auras (estandartes do Comandante) -----
        /// <summary>Buffs ativos: tipo, valor e quando expiram (tempo do servidor). O servidor remove os vencidos.</summary>
        public readonly List<(Kezor.Core.Combat.BuffKind kind, int value, double expires)> Buffs = new List<(Kezor.Core.Combat.BuffKind, int, double)>();
        /// <summary>Bônus de estandarte recalculados pelo servidor a cada tick (o maior de cada tipo por perto).</summary>
        public int AuraDamagePercent, AuraDefense, AuraSpeedPercent;
        private int BuffSum(Kezor.Core.Combat.BuffKind k) { int s = 0; foreach (var b in Buffs) if (b.kind == k) s += b.value; return s; }
        public int BuffDamagePercent => BuffSum(Kezor.Core.Combat.BuffKind.Damage);
        public int BuffSpeedPercent => BuffSum(Kezor.Core.Combat.BuffKind.Speed);
        public int BuffDefense => BuffSum(Kezor.Core.Combat.BuffKind.Defense);

        public void AddBuff(Kezor.Core.Combat.BuffKind kind, int value, double expires)
        {
            Buffs.RemoveAll(b => b.kind == kind);   // reaplicar renova (não acumula)
            Buffs.Add((kind, value, expires));
        }

        public void ExpireBuffs(double now) => Buffs.RemoveAll(b => b.expires <= now);

        public bool Mount(out string error)
        {
            error = "";
            if (IsDead) { error = "você está caído"; return false; }
            if (MountItem == null) { error = "nenhuma montaria equipada"; return false; }
            Mounted = true;
            return true;
        }

        public void Dismount() => Mounted = false;

        /// <summary>Experiência total para chegar ao nível (tabela do MU): 10·(N+8)·(N−1)², e do 256 em diante
        /// soma 1000·(N−247)·(N−256)².</summary>
        public static long TotalExpForLevel(int level)
        {
            if (level <= 1) return 0;
            long n = level - 1;
            long exp = 10L * (level + 8) * n * n;
            if (level > 256) { long m = level - 256; exp += 1000L * (level - 247) * m * m; }
            return exp;
        }

        /// <summary>Experiência que falta do nível atual para o próximo (o personagem guarda a experiência dentro do nível).</summary>
        public static long ExpToNext(int level) => TotalExpForLevel(level + 1) - TotalExpForLevel(level);

        /// <summary>Concede experiência e retorna quantos níveis foram ganhos.</summary>
        public int GainExperience(long amount)
        {
            if (amount <= 0) return 0;
            if (Level >= MaxLevel) { Ascension.Unlocked = true; Ascension.Gain(amount); return 0; }   // no 400 a experiência vira Ascensão
            Experience += amount;
            int gained = 0;
            while (Level < MaxLevel && Experience >= ExpToNext(Level))
            {
                Experience -= ExpToNext(Level);
                Level++; gained++;
                FreePoints += Def.PointsPerLevel;
            }
            if (Level >= MaxLevel) { Experience = 0; if (!Ascension.Unlocked) { Ascension.Unlocked = true; Ascension.Touch(); } }
            if (gained > 0) { Hp = MaxHp; Mp = MaxMp; }
            return gained;
        }

        public bool AddPoints(StatType stat, int amount, out string error)
        {
            error = "";
            if (amount <= 0) { error = "quantidade inválida"; return false; }
            if (amount > FreePoints) { error = "pontos insuficientes"; return false; }
            if (Stats.Get(stat) + amount > MaxStatValue) { error = "atributo no limite"; return false; }
            Stats.Add(stat, amount);
            FreePoints -= amount;
            return true;
        }

        public static long RebirthGoldCost(int rebirths) => 1_000_000L * (rebirths + 1);

        /// <summary>Renascimento: volta ao nível 1 com atributos base e pontos bônus acumulados.</summary>
        public bool TryRebirth(out string error)
        {
            error = "";
            if (Level < MaxLevel) { error = $"requer nível {MaxLevel}"; return false; }
            if (Rebirths >= MaxRebirths) { error = "limite de renascimentos"; return false; }
            long cost = RebirthGoldCost(Rebirths);
            if (Gold < cost) { error = $"ouro insuficiente ({cost})"; return false; }
            Gold -= cost;
            Rebirths++;
            Level = 1; Experience = 0;
            Stats = Def.BaseStats;
            FreePoints = Rebirths * RebirthBonusPoints;
            Hp = MaxHp; Mp = MaxMp;
            return true;
        }

        // ----- Equipamento -----
        public bool CanEquip(Item item, out string error)
        {
            error = "";
            var d = item.Def;
            if (d.Slot == EquipSlot.None) { error = "item não equipável"; return false; }
            if ((d.AllowedClasses & ClassDefinition.MaskOf(Class)) == 0) { error = "classe não pode usar"; return false; }
            if (Stats.Strength < item.RequiredStrength) { error = $"força requerida {item.RequiredStrength}"; return false; }
            if (Stats.Agility < item.RequiredAgility) { error = $"agilidade requerida {item.RequiredAgility}"; return false; }
            if (Stats.Energy < d.ReqEnergy) { error = $"energia requerida {d.ReqEnergy}"; return false; }
            if (Level < d.ReqLevel) { error = $"nível requerido {d.ReqLevel}"; return false; }
            return true;
        }

        public bool Equip(int inventorySlot, out string error)
        {
            var item = Inventory.Get(inventorySlot);
            if (item == null) { error = "slot vazio"; return false; }
            if (!CanEquip(item, out error)) return false;
            var target = TargetSlot(item.Def);
            Inventory.Remove(inventorySlot);
            if (Equipment.TryGetValue(target, out var old) && !Inventory.Put(inventorySlot, old) && !Inventory.TryAdd(old, out _))
            {   // o item que sai não cabe na mochila (tamanhos diferentes): desfaz
                Inventory.Put(inventorySlot, item);
                error = "sem espaço na mochila para o item trocado";
                return false;
            }
            Equipment[target] = item;
            Hp = Math.Min(Hp, MaxHp); Mp = Math.Min(Mp, MaxMp);
            return true;
        }

        public bool Unequip(EquipSlot slot, out string error)
        {
            error = "";
            if (!Equipment.TryGetValue(slot, out var item)) { error = "nada equipado"; return false; }
            if (!Inventory.TryAdd(item, out _)) { error = "inventário cheio"; return false; }
            Equipment.Remove(slot);
            if (slot == EquipSlot.Mount) Mounted = false;
            Hp = Math.Min(Hp, MaxHp); Mp = Math.Min(Mp, MaxMp);
            return true;
        }

        private static bool Usable(Item i) => i.Durability > 0;

        // ----- Status de combate derivados -----
        public CombatStats GetCombatStats()
        {
            var d = Def;
            int str = d.StrAgiCombined ? Stats.Strength + Stats.Agility : Stats.Strength;
            var s = new CombatStats
            {
                MinDamage = str / d.StrDivMin,
                MaxDamage = str / d.StrDivMax,
                Defense = Stats.Agility / d.AgiDivDefense,
                AttackRate = (int)(d.ArPerLevel * Level + d.ArPerAgi * Stats.Agility) + Stats.Strength / d.ArStrDiv,
                DefenseRate = Stats.Agility / d.AgiDivDefenseRate,
                AttackSpeed = Stats.Agility / (d.CastsMagic ? d.MagicSpeedDiv : d.AttackSpeedDiv),
                SkillMultiplier = d.SkillMultiplier + d.SkillMultiplierPerEnergy * Stats.Energy,
                IsPlayer = true,
                Level = Level
            };
            if (d.AgiDamageDivMin > 0)
            {
                s.MinDamage += Stats.Agility / d.AgiDamageDivMin;
                s.MaxDamage += Stats.Agility / d.AgiDamageDivMax;
            }
            if (d.EnePhysDivMin > 0)
            {
                s.MinDamage += Stats.Energy / d.EnePhysDivMin;
                s.MaxDamage += Stats.Energy / d.EnePhysDivMax;
            }
            if (d.VitPhysDivMin > 0)
            {
                s.MinDamage += Stats.Vitality / d.VitPhysDivMin;
                s.MaxDamage += Stats.Vitality / d.VitPhysDivMax;
            }
            if (d.EneDivMin > 0)
            {
                s.MinMagic = Stats.Energy / d.EneDivMin;
                s.MaxMagic = Stats.Energy / d.EneDivMax;
            }
            int magicPct = 0, dmgPct = 0;
            bool defRateExc = false;
            foreach (var kv in Equipment)
            {
                var it = kv.Value;
                if (!Usable(it)) continue;
                s.MinDamage += it.MinDamage; s.MaxDamage += it.MaxDamage;
                s.Defense += it.Defense;
                magicPct += it.MagicPowerPercent;
                if (it.Luck) s.CriticalChancePercent += 5;
                var ex = it.Ascended;
                if ((ex & AscendedOption.SurgeChance) != 0) s.SurgeChancePercent += 10;
                if ((ex & AscendedOption.DamagePerLevel) != 0) { s.MinDamage += Level / 20; s.MaxDamage += Level / 20; if (s.MaxMagic > 0) { s.MinMagic += Level / 20; s.MaxMagic += Level / 20; } }
                if ((ex & AscendedOption.DamagePercent) != 0) dmgPct += 2;
                if ((ex & AscendedOption.AttackSpeed) != 0) s.AttackSpeed += 7;
                if ((ex & AscendedOption.DefenseRate) != 0) defRateExc = true;
                if ((ex & AscendedOption.DamageReduction) != 0) s.DamageReductionPercent += 4;
                if ((ex & AscendedOption.Reflect) != 0) s.ReflectPercent += 5;
                if ((ex & AscendedOption.LifeOnKill) != 0) s.LifeOnKill += 1;
                if ((ex & AscendedOption.ManaOnKill) != 0) s.ManaOnKill += 1;
                if ((ex & AscendedOption.ZenBonus) != 0) s.ZenBonusPercent += 40;
                if (it.Def.IsWings) { s.DamageBonusPercent += it.WingDamage; s.DamageReductionPercent += it.WingAbsorb; }
                if (it.Def.Pet == PetType.Guardian) { s.Defense += Item.GuardianPetDefense; s.DamageReductionPercent += Item.GuardianPetReduction; }
                if (it.Def.IsJewel) { s.DamageReductionPercent += it.Def.JewelResistPercent; s.DamageBonusPercent += it.Def.JewelDamagePercent; }
            }
            // Árvore da Ascensão
            var asc = Ascension;
            if (asc.Ranks.Count > 0)
            {
                s.Defense += asc.Total(AscEffect.Defense);
                s.DamageReductionPercent += asc.Total(AscEffect.ReductionPct);
                s.DamageBonusPercent += asc.Total(AscEffect.DamagePct);
                s.CriticalChancePercent += asc.Total(AscEffect.CritPct);
                s.AttackRate += s.AttackRate * asc.Total(AscEffect.AttackRatePct) / 100;
                s.DefenseRate += s.DefenseRate * asc.Total(AscEffect.DefenseRatePct) / 100;
                s.LifeOnKill += asc.Total(AscEffect.LifeOnKill);
                magicPct += asc.Total(AscEffect.MagicPct);
            }
            // buffs e auras
            s.DamageBonusPercent += BuffDamagePercent + AuraDamagePercent;
            int auraDef = BuffDefense + AuraDefense;
            if (auraDef > 0) { s.Defense += auraDef; s.DefenseRate += s.DefenseRate * auraDef / 200; }
            var set = ArmorSets.Complete(Equipment);
            if (set != ArmorSet.None)
            {
                var b = ArmorSets.Bonus(set);
                s.Defense += b.Defense;
                s.DefenseRate += s.DefenseRate * b.DefenseRatePercent / 100;
                s.DamageReductionPercent += b.ReductionPercent;
                s.DamageBonusPercent += b.DamagePercent;
                magicPct += b.MagicPercent;
            }
            if (s.MaxMagic > 0 && magicPct > 0)
            {
                s.MinMagic += s.MinMagic * magicPct / 100;
                s.MaxMagic += s.MaxMagic * magicPct / 100;
            }
            if (dmgPct > 0)
            {   // opção excelente "dano +2%" (sobre o dano base)
                s.MinDamage += s.MinDamage * dmgPct / 100; s.MaxDamage += s.MaxDamage * dmgPct / 100;
                s.MinMagic += s.MinMagic * dmgPct / 100; s.MaxMagic += s.MaxMagic * dmgPct / 100;
            }
            if (defRateExc) s.DefenseRate += s.DefenseRate / 10;
            // MU: a defesa final é metade da soma (AGI + itens + bônus)
            s.DefenseBase = s.Defense;
            s.Defense /= 2;
            if (s.MaxDamage < s.MinDamage) s.MaxDamage = s.MinDamage;
            return s;
        }

        /// <summary>Velocidade de ataque (MU): AGI/divisor da classe + opção excelente (+7).</summary>
        public int AttackSpeed => GetCombatStats().AttackSpeed;

        /// <summary>Intervalo mínimo entre golpes (s), pela velocidade de ataque: 0,8 × 50/(50+velocidade), no mínimo 0,2 s.</summary>
        public float AttackCooldown => AttackIntervalFor(AttackSpeed);
        public static float AttackIntervalFor(int speed) => Math.Max(0.2f, 0.8f * 50f / (50f + Math.Max(0, speed)));

        /// <summary>Desgaste ao atacar/ser atingido.</summary>
        public void WearEquipment(bool attacking, IRandom rng)
        {
            foreach (var kv in Equipment)
            {
                if (kv.Key == EquipSlot.Wings || kv.Key == EquipSlot.Mount || kv.Key == EquipSlot.Pet || kv.Value.Def.IsJewel) continue; // não se desgastam
                bool isWeapon = kv.Key == EquipSlot.RightHand;
                if (isWeapon != attacking) continue;
                if (kv.Value.Durability > 0 && rng.Roll(attacking ? 4 : 2)) { kv.Value.Durability--; Inventory.Touch(); }
            }
        }
    }
}
