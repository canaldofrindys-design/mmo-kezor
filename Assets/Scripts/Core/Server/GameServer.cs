using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kezor.Core.Characters;
using Kezor.Core.Combat;
using Kezor.Core.Items;
using Kezor.Core.World;
using static Kezor.Core.Server.Protocol;

namespace Kezor.Core.Server
{
    public sealed class ServerOptions
    {
        public int? Seed;
        /// <summary>Libera comandos GM para todas as contas (só para teste). Normalmente use <see cref="GmAccounts"/>.</summary>
        public bool AllowGm;
        /// <summary>Contas com comandos GM (como os GMs de um servidor online). O servidor deste PC marca a conta do dono.</summary>
        public readonly HashSet<string> GmAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Registro do servidor (entradas, saídas, GM, trocas). Null = sem registro. O servidor dedicado escreve no console.</summary>
        public Action<string>? Log;
        public float SnapshotInterval = 0.1f;
        public float ViewRange = 60f;   // AOI: a câmera mais alta mostra mais chão
        public float EventFirstDelay = 180f;
        public float EventInterval = 900f;
        public float EventDuration = 300f;
        /// <summary>Fenda da Geada Rubra (Escarpa): primeira abertura, intervalo entre aberturas e tempo para fechar (segundos).</summary>
        public float RiftFirstDelay = 900f, RiftInterval = 2400f, RiftDuration = 600f;
        /// <summary>Exige conta (REGISTER/AUTH) para criar e jogar. Todo servidor de verdade (dedicado e o deste PC) liga isto;
        /// desligado só nos testes automáticos, que criam heróis direto.</summary>
        public bool RequireAccounts;
        /// <summary>Salva todos os personagens e contas online a cada N segundos (0 = só ao desconectar).</summary>
        public float AutosaveSeconds = 60f;
        /// <summary>Iterações do PBKDF2 (menos nos testes).</summary>
        public int PasswordIterations = PasswordHasher.DefaultIterations;
        /// <summary>Nome e canal mostrados na lista de servidores do login (STATUS) e limite de jogadores no mundo.</summary>
        public string ServerName = "Kezor";
        public int Channel = 1;
        public int MaxPlayers = 500;
    }

    /// <summary>
    /// Servidor autoritativo de Kezor. Toda regra roda aqui; o cliente só envia intenções.
    /// Thread-safety: chamar Handle/Tick sempre da mesma thread (os hosts de rede enfileiram mensagens).
    /// </summary>
    public sealed partial class GameServer
    {
        public const long StartingGold = 20_000;
        public const float AttackCooldownSeconds = 0.6f;
        public const float PickRange = 2.5f;
        public const float RangeTolerance = 0.6f;
        public const float ReservedSeconds = 15f, GroundItemSeconds = 90f;

        private readonly IRandom _rng;
        private readonly ICharacterRepository _repo;
        private readonly IAccountRepository _accounts;
        private readonly Ranking _ranking = new Ranking();
        private readonly IRankingRepository? _rankRepo;
        public Ranking Ranking => _ranking;
        private double _autosaveAcc;
        private readonly ArcaneForge _forge;
        private readonly ServerOptions _opt;
        private readonly Dictionary<int, Session> _sessions = new Dictionary<int, Session>();
        private readonly Dictionary<int, Monster> _monsters = new Dictionary<int, Monster>();
        private readonly Dictionary<int, GroundItem> _ground = new Dictionary<int, GroundItem>();
        private readonly Dictionary<int, Party> _parties = new Dictionary<int, Party>();
        private readonly Dictionary<int, int> _partyOf = new Dictionary<int, int>(); // charId -> partyId
        private int _nextCharId = 1, _nextEntityId = 10_000, _nextPartyId = 1;
        private double _time, _snapshotAcc, _regenAcc;

        /// <summary>Mapa principal (Vale de Brasalume, onde fica a vila).</summary>
        public MapDefinition Map => _maps[MapDefinition.ValeId];
        private readonly Dictionary<int, MapDefinition> _maps;
        private readonly Dictionary<int, Pathfinder> _paths = new Dictionary<int, Pathfinder>();
        public IReadOnlyDictionary<int, MapDefinition> Maps => _maps;
        public MapDefinition MapOf(int id) => _maps.TryGetValue(id, out var m) ? m : Map;
        private Pathfinder PathOf(int mapId)
        {
            if (!_paths.TryGetValue(mapId, out var p)) _paths[mapId] = p = new Pathfinder(MapOf(mapId));
            return p;
        }
        public const float PortalCooldown = 2f;
        public double Time => _time;
        public IReadOnlyDictionary<int, Monster> Monsters => _monsters;
        public IReadOnlyDictionary<int, GroundItem> GroundItems => _ground;

        public sealed class Session
        {
            public int Id;
            public Character? Character;
            public double LastAttackTime = -999, LastPotionTime = -999, LastChatTime = -999;
            public readonly List<string> Outbox = new List<string>();
            // Intenções em andamento (executadas no Tick)
            public bool HasMove; public float MoveX, MoveZ;
            public int AutoTargetId;      // ataque básico contínuo
            public int PendingSkill = -1; // skill única a usar no alvo
            public int PendingUtility = -1; // buff/estandarte/eco pedido durante o intervalo do ataque: sai assim que der
            public int PendingPick;       // item no chão a pegar
            public readonly ComboTracker Combo = new ComboTracker();
            public readonly EmberFlow Flow = new EmberFlow();
            /// <summary>Recarga das skills com Cooldown (id -> quando volta).</summary>
            public readonly Dictionary<int, double> SkillReadyAt = new Dictionary<int, double>();
            public int SentInvVersion = -1;
            public string LastMe = "";
            public int PendingInviteFrom;
            /// <summary>Conta autenticada nesta conexão (null = convidado).</summary>
            public Account? Account;
            /// <summary>Caminho calculado pelo A* até o destino atual (null = linha reta).</summary>
            public List<(float x, float z)>? Path;
            public float PathTx = float.NaN, PathTz = float.NaN;
            public double PortalReadyAt;
            /// <summary>Baú aberto (perto da guardiã): recebe @VAULT quando muda.</summary>
            public bool VaultOpen;
            public int SentVaultVersion = int.MinValue;
            public int SentAscVersion = int.MinValue;
            public void ClearIntent() { HasMove = false; AutoTargetId = 0; PendingSkill = -1; PendingPick = 0; }
        }

        public GameServer(IRandom? rng = null, ICharacterRepository? repo = null, ServerOptions? options = null, IAccountRepository? accounts = null, IGuildRepository? guilds = null)
        {
            _opt = options ?? new ServerOptions();
            _rng = rng ?? new SystemRandom(_opt.Seed);
            _repo = repo ?? new InMemoryCharacterRepository();
            _accounts = accounts ?? (repo as IAccountRepository) ?? new InMemoryAccountRepository();
            InitGuilds(guilds ?? repo as IGuildRepository);
            _rankRepo = repo as IRankingRepository;
            if (_rankRepo != null) { try { _ranking.Load(_rankRepo.LoadRanking()); } catch (Exception e) { Console.Error.WriteLine("[Kezor] ranking: " + e.Message); } }
            _forge = new ArcaneForge(_rng);
            _maps = MapDefinition.CreateAll();
            _eventTimer = _opt.EventFirstDelay;
            foreach (var map in _maps.Values.OrderBy(mm => mm.Id))
                foreach (var g in map.Spawns)
                    for (int i = 0; i < g.Count; i++)
                    {
                        float x = g.X, z = g.Z;
                        for (int tries = 0; tries < 12; tries++)   // ponto andável dentro do raio (masmorras têm paredes)
                        {
                            float a = _rng.Range(0, 6.2831f), r = _rng.Range(0, g.Radius);
                            float cx = g.X + (float)Math.Cos(a) * r, cz = g.Z + (float)Math.Sin(a) * r;
                            if (!map.IsBlocked(cx, cz)) { x = cx; z = cz; break; }
                        }
                        Spawn(g.Monster, map.Id, x, z);
                    }
        }

        /// <summary>Cria um monstro no Vale (evento) ou no mapa dado.</summary>
        public Monster Spawn(MonsterDefinition def, float x, float z) => Spawn(def, MapDefinition.ValeId, x, z);

        public Monster Spawn(MonsterDefinition def, int mapId, float x, float z)
        {
            var map = MapOf(mapId);
            var m = new Monster(_nextEntityId++, def, map.ClampCoord(x), map.ClampCoord(z)) { MapId = map.Id };
            m.StateTimer = _rng.Range(0.5f, 4f);
            _monsters[m.Id] = m;
            return m;
        }

        // ------------------------------------------------------------ sessões
        public Session Connect(int sessionId)
        {
            var s = new Session { Id = sessionId };
            _sessions[sessionId] = s;
            return s;
        }

        public void Disconnect(int sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var s)) return;
            LeaveWorld(s);
            _sessions.Remove(sessionId);
            _guildInvite.Remove(sessionId);
        }

        public Character? GetCharacter(int sessionId) => _sessions.TryGetValue(sessionId, out var s) ? s.Character : null;
        public Session? GetSession(int sessionId) => _sessions.TryGetValue(sessionId, out var s) ? s : null;
        public IEnumerable<int> SessionIds => _sessions.Keys;

        /// <summary>Retira as mensagens pendentes de uma sessão (o host envia pela rede).</summary>
        public List<string>? DrainOutbox(int sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var s) || s.Outbox.Count == 0) return null;
            var l = new List<string>(s.Outbox);
            s.Outbox.Clear();
            return l;
        }

        private IEnumerable<Session> Online => _sessions.Values.Where(s => s.Character != null);
        private Session? SessionOf(int charId) => _sessions.Values.FirstOrDefault(s => s.Character != null && s.Character.Id == charId);
        private Character? CharById(int id) => SessionOf(id)?.Character;
        private Character? CharByName(string name) => Online.Select(s => s.Character!).FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        private void Push(Session s, string msg) => s.Outbox.Add(msg);
        private void Push(Character c, string msg) { var s = SessionOf(c.Id); if (s != null) s.Outbox.Add(msg); }
        private void Broadcast(string msg) { foreach (var s in Online) s.Outbox.Add(msg); }
        private void BroadcastNear(int mapId, float x, float z, string msg)
        {
            foreach (var s in Online)
                if (s.Character!.MapId == mapId && MathK.Dist(s.Character.X, s.Character.Z, x, z) <= _opt.ViewRange) s.Outbox.Add(msg);
        }
        private void Sys(Session s, string text) => Push(s, "@EV SYS;" + Clean(text));
        private void Sys(Character c, string text) => Push(c, "@EV SYS;" + Clean(text));
        private void SysAll(string text) => Broadcast("@EV SYS;" + Clean(text));

        // ------------------------------------------------------------ comandos
        /// <summary>Processa um comando e retorna a resposta. Mensagens extras vão para o Outbox da sessão.</summary>
        public string Handle(int sessionId, string line)
        {
            if (!_sessions.TryGetValue(sessionId, out var s)) s = Connect(sessionId);
            var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "ERR comando vazio";
            string cmd = parts[0].ToUpperInvariant();
            try
            {
                if (cmd == "STATUS") return Status();
                if (cmd == "CREATE") return Create(s, parts);
                if (cmd == "LOGIN") return Login(s, parts);
                if (cmd == "REGISTER") return Register(s, parts);
                if (cmd == "AUTH") return Auth(s, parts);
                if (cmd == "CHARS") return Chars(s);
                if (cmd == "PLAY") return Play(s, parts);
                if (cmd == "DELCHAR") return DeleteChar(s, parts);
                if (cmd == "PING") return "OK pong";
                if (cmd == "SELECT") return BackToSelect(s);
                if (cmd == "HELP") return "OK REGISTER <conta> <senha> | AUTH <conta> <senha> | CHARS | PLAY <nome> | CREATE <nome> <GUA|ARC|PAT> | LOGIN <nome> | MOVE <x> <z> | STOP | ATTACK <id> | SKILL <skillId> <id> | PICK <id> | " +
                                          "ADD <STR|AGI|VIT|ENE> <n> | STATS | LOOK | INV | EQUIP <slot> | UNEQUIP <RightHand|Helm..> | USE <slot> | RUNE <runa> <alvo> | " +
                                          "FORGEPREVIEW <s1,s2,..> | FORGE <s1,s2,..> | BUY <itemId> [qtd] | SELL <slot> | REPAIR | QUICK <0-3> <itemId> | USEQ <0-3> | REBIRTH | SAY <texto> | " +
                                          "PARTY INVITE <nome>|ACCEPT|LEAVE | MOUNT | WARP [destino] | VAULT | VPUT <slot> [qtd] [casa] | VTAKE <slot> [qtd] [casa] | IMOVE <de> <para> | VMOVE <de> <para> | VGOLDIN <n> | VGOLDOUT <n> | VCLOSE | TRADE <nome>|ACCEPT|DECLINE|ADD <slot>|REMOVE <slot>|GOLD <n>|LOCK|CONFIRM|CANCEL | GUILD CREATE|INVITE|ACCEPT|LEAVE|KICK|PROMOTE|DEMOTE|DISBAND|INFO|WAR <guilda>|WARACCEPT|WARDECLINE|WARINFO | GSAY <texto> | RANKING [GERAL|GUILDAS|GUA|ARC|PAT|LAM|COM|INV|PUN] | SHOP OPEN <nome>|ADD <slot> <preço>|REMOVE <slot>|CLOSE|VIEW <jogador>|BUY <jogador> <slot> <preço> | W <nome> <texto> | ONLINE | SELECT | PING" + (IsGm(s) ? " | GM KICK <nome> [motivo]|BAN <conta> <dias|perm> [motivo]|UNBAN <conta>|MUTE <nome> [min]|UNMUTE <nome>|INFO <nome>|GOTO <nome>|BRING <nome>|AVISO <texto>|LEVEL <n>|GOLD <n>|RUNES|EVENT|RIFT|HEAL|ITEM <id> [nível]" : "");
                var c = s.Character;
                if (c == null) return "ERR faça LOGIN ou CREATE primeiro";
                if ((cmd == "SAY" || cmd == "W" || cmd == "WHISPER" || cmd == "GSAY") && MutedError(s) is string muted) return muted;
                switch (cmd)
                {
                    case "MOVE": return ShopOpen(s) ? "ERR feche a loja para andar (SHOP CLOSE)" : Move(s, c, parts);
                    case "STOP": s.ClearIntent(); return "OK";
                    case "ATTACK": return ShopOpen(s) ? "ERR feche a loja primeiro" : Attack(s, c, parts);
                    case "SKILL": return ShopOpen(s) ? "ERR feche a loja primeiro" : Skill(s, c, parts);
                    case "PICK": return ShopOpen(s) ? "ERR feche a loja primeiro" : Pick(s, c, parts);
                    case "STATS": return Stats(c);
                    case "ADD": return AddPoint(c, parts);
                    case "LOOK": return Look(c);
                    case "INV": s.SentInvVersion = -1; return Inv(c);
                    case "EQUIP": return c.Equip(Int(parts, 1), out var e1) ? "OK equipado" : "ERR " + e1;
                    case "UNEQUIP": return Enum.TryParse<EquipSlot>(parts[1], true, out var es) && c.Unequip(es, out var e3) ? "OK" : "ERR não foi possível desequipar";
                    case "USE": return Use(s, c, parts);
                    case "QUICK": return Quick(c, parts);
                    case "ASC": return AscensionCmd(s, c, parts);
                    case "USEQ": return UseQuick(s, c, parts);
                    case "RUNE": return Rune(c, parts);
                    case "FORGEPREVIEW": return ForgePreview(c, parts);
                    case "FORGE": return Forge(c, parts);
                    case "BUY": return Buy(c, parts);
                    case "SELL": return Sell(c, parts);
                    case "REPAIR": return Repair(c);
                    case "REBIRTH": return c.TryRebirth(out var e2) ? $"OK renascimento #{c.Rebirths}, pontos livres {c.FreePoints}" : "ERR " + e2;
                    case "SAY": return Say(s, c, line.Trim().Substring(3).Trim());
                    case "PARTY": return PartyCmd(c, parts);
                    case "MOUNT": return MountCmd(c);
                    case "WARP": return Warp(s, c, parts);
                    case "TRADE": return TradeCmd(s, c, parts);
                    case "GUILD": return GuildCmd(s, c, parts);
                    case "SHOP": return ShopCmd(s, c, parts, line);
                    case "RANKING": return RankingCmd(s, c, parts);
                    case "GSAY": return GuildSay(c, line.Trim().Substring(4).Trim());
                    case "VAULT": return VaultOpen(s, c);
                    case "VCLOSE": s.VaultOpen = false; return "OK";
                    case "VPUT": return VaultPut(s, c, parts);
                    case "IMOVE": return c.Inventory.MoveOrSwap(Int(parts, 1), Int(parts, 2), out var sw) ? (sw >= 0 ? $"OK troca {sw}" : "OK") : "ERR não cabe aí";
                    case "DROP": return DropToGround(c, parts);
                    case "VMOVE": { var w = NearVault(c); if (w != "") return w; return c.Vault.Items.MoveOrSwap(Int(parts, 1), Int(parts, 2), out var vsw) ? (vsw >= 0 ? $"OK troca {vsw}" : "OK") : "ERR não cabe aí"; }
                    case "VTAKE": return VaultTake(s, c, parts);
                    case "VGOLDIN": return VaultGold(s, c, parts, true);
                    case "VGOLDOUT": return VaultGold(s, c, parts, false);
                    case "GM": return IsGm(s) ? Gm(s, c, parts) : "ERR comandos GM desativados para esta conta";
                    case "W": case "WHISPER": return Whisper(s, c, parts);
                    case "ONLINE": return OnlineList();
                    default: return "ERR comando desconhecido (HELP)";
                }
            }
            catch (FormatException) { return "ERR argumento inválido"; }
            catch (OverflowException) { return "ERR argumento inválido"; }
            catch (IndexOutOfRangeException) { return "ERR argumentos faltando"; }
            finally { if (s.Character != null && s.Character.Inventory.Version != s.SentInvVersion) SendInventory(s); }
        }

        private static int Int(string[] p, int i) => PI(p[i]);

        /// <summary>Jogadores no mundo agora (sessões com herói).</summary>
        public int OnlineCount => _sessions.Values.Count(x => x.Character != null);

        /// <summary>STATUS (funciona sem login): "OK status nome;canal;online;máximo;versão" para a lista de servidores.</summary>
        private string Status() => $"OK status {Clean(_opt.ServerName)};{_opt.Channel};{OnlineCount};{_opt.MaxPlayers};{Protocol.GameVersion}";

        private string Create(Session s, string[] p)
        {
            if (s.Character != null) return "ERR já conectado";
            if (_opt.RequireAccounts && s.Account == null) return "ERR entre na sua conta primeiro (AUTH ou REGISTER)";
            if (s.Account != null && s.Account.Characters.Count >= Account.MaxCharacters) return $"ERR limite de {Account.MaxCharacters} personagens por conta";
            string name = p[1];
            if (name.Length < 3 || name.Length > 12 || !name.All(char.IsLetterOrDigit)) return "ERR nome deve ter 3-12 letras/números";
            if (_repo.Load(name) != null) return "ERR nome em uso";
            if (!ClassDefinition.TryParse(p[2], out var cls)) return "ERR classe inválida (GUA, ARC ou PAT)";
            var c = new Character(_nextCharId++, name, cls) { Gold = StartingGold };
            var starter = cls switch
            {
                CharacterClass.Warden => ItemDatabase.IronBlade,
                CharacterClass.Arcanist => ItemDatabase.ApprenticeStaff,
                CharacterClass.Bladeweaver => ItemDatabase.RuneShortblade,
                CharacterClass.Commander => ItemDatabase.RecruitSpear,
                CharacterClass.Invoker => ItemDatabase.LesserEchoTome,
                CharacterClass.EmberFist => ItemDatabase.RawhideFists,
                _ => ItemDatabase.HuntingBow
            };
            c.Inventory.TryAdd(new Item(starter), out var slot); c.Equip(slot, out _);
            c.Inventory.TryAdd(new Item(ItemDatabase.LeatherArmor), out slot); c.Equip(slot, out _);
            c.Inventory.TryAdd(new Item(ItemDatabase.LeatherBoots), out slot); c.Equip(slot, out _);
            c.Inventory.TryAdd(new Item(ItemDatabase.HealthPotion) { Quantity = 15 }, out _);
            c.Inventory.TryAdd(new Item(ItemDatabase.ManaPotion) { Quantity = 10 }, out _);
            c.Hp = c.MaxHp;
            if (s.Account != null)
            {   // com conta: o personagem entra na lista e o jogador escolhe com PLAY
                var home = MapOf(c.MapId);             // já fica salvo na praça da cidade principal (onde nasce)
                c.X = home.RespawnX + _rng.Range(-2, 2); c.Z = home.RespawnZ + _rng.Range(-2, 2);
                c.AccountName = s.Account.Name;
                s.Account.Characters.Add(c.Name);
                _repo.Save(c);
                _accounts.Save(s.Account);
                return $"OK criado {c.Name} ({c.Def.DisplayName})";
            }
            _repo.Save(c);
            EnterWorld(s, c);
            return $"OK criado {c.Name} ({c.Def.DisplayName})";
        }

        // ------------------------------------------------------------ contas
        private static bool ValidAccountName(string n) => n.Length >= 3 && n.Length <= 16 && n.All(char.IsLetterOrDigit);

        private string Register(Session s, string[] p)
        {
            if (s.Character != null || s.Account != null) return "ERR já conectado";
            if (p.Length < 3) return "ERR use REGISTER <conta> <senha>";
            string name = p[1], pass = p[2];
            if (!ValidAccountName(name)) return "ERR a conta deve ter 3-16 letras/números";
            if (pass.Length < 6 || pass.Length > 64) return "ERR a senha deve ter 6-64 caracteres";
            if (_accounts.Load(name) != null) return "ERR conta já existe";
            var a = new Account { Name = name, PasswordHash = PasswordHasher.Hash(pass, _opt.PasswordIterations) };
            _accounts.Save(a);
            s.Account = a;
            LogLine($"conta criada: {a.Name} (sessão {s.Id})");
            return $"OK conta {a.Name} criada";
        }

        private string Auth(Session s, string[] p)
        {
            if (s.Character != null || s.Account != null) return "ERR já conectado";
            if (p.Length < 3) return "ERR use AUTH <conta> <senha>";
            var a = _accounts.Load(p[1]);
            if (a == null || !PasswordHasher.Verify(p[2], a.PasswordHash)) return "ERR conta ou senha incorretas";
            if (a.IsBanned(DateTime.UtcNow))
            {
                LogLine($"login recusado (conta banida): {a.Name} (sessão {s.Id})");
                return $"ERR esta conta está banida {Until(a.BannedUntil)}" + (a.BanReason.Length > 0 ? ": " + a.BanReason : "");
            }
            if (_sessions.Values.Any(o => o != s && o.Account != null && string.Equals(o.Account.Name, a.Name, StringComparison.OrdinalIgnoreCase)))
                return "ERR esta conta já está conectada";
            s.Account = a;
            LogLine($"login: {a.Name} (sessão {s.Id})");
            return "OK " + CharList(a);
        }

        /// <summary>
        /// Lista dos personagens da conta para a tela de seleção, separada por "|":
        /// nome:classe:nível:renascimentos:guilda:cargo:aparência (a aparência monta o modelo com o equipamento).
        /// </summary>
        private string CharList(Account a)
        {
            var parts = new List<string>();
            foreach (var n in a.Characters)
            {
                var c = _repo.Load(n);
                if (c == null) continue;
                var g = GuildOf(c.Name);
                parts.Add($"{c.Name}:{(int)c.Class}:{c.Level}:{c.Rebirths}:{g?.Name ?? ""}:{(g != null ? (int)g.RankOf(c.Name) : -1)}:{Appearance.Encode(c)}");
            }
            return "chars " + string.Join("|", parts);
        }

        /// <summary>DELCHAR nome confirmação: apaga um personagem da conta (a confirmação é o nome digitado de novo).</summary>
        private string DeleteChar(Session s, string[] p)
        {
            if (s.Character != null) return "ERR saia do mundo antes";
            if (s.Account == null) return "ERR entre na sua conta primeiro";
            if (p.Length < 3) return "ERR use DELCHAR <nome> <nome de novo>";
            var name = s.Account.Characters.FirstOrDefault(n => string.Equals(n, p[1], StringComparison.OrdinalIgnoreCase));
            if (name == null) return "ERR personagem não é desta conta";
            if (!string.Equals(p[2], name, StringComparison.Ordinal)) return "ERR a confirmação não confere (digite o nome exato)";
            var g = GuildOf(name);
            if (g != null && g.RankOf(name) == GuildRank.Leader) return "ERR o líder precisa passar a liderança ou dissolver a guilda antes";
            if (g != null) { g.Members.Remove(name); SaveGuild(g); }
            s.Account.Characters.Remove(name);
            _accounts.Save(s.Account);
            _repo.Delete(name);
            return $"OK {name} foi apagado";
        }

        private string Chars(Session s) => s.Account == null ? "ERR entre na sua conta primeiro" : "OK " + CharList(s.Account);

        private string Play(Session s, string[] p)
        {
            if (s.Character != null) return "ERR já está no mundo";
            if (s.Account == null) return "ERR entre na sua conta primeiro";
            var name = s.Account.Characters.FirstOrDefault(n => string.Equals(n, p[1], StringComparison.OrdinalIgnoreCase));
            if (name == null) return "ERR personagem não é desta conta";
            if (_sessions.Values.Any(o => o.Character != null && string.Equals(o.Character.Name, name, StringComparison.OrdinalIgnoreCase))) return "ERR já conectado";
            if (OnlineCount >= _opt.MaxPlayers) return "ERR servidor lotado, tente outro canal";
            var c = _repo.Load(name);
            if (c == null) return "ERR personagem não encontrado";
            c.Id = _nextCharId++;
            c.AccountName = s.Account.Name;
            c.Vault = s.Account.Vault;          // baú da conta, o mesmo para todos os personagens
            EnterWorld(s, c, keepPosition: true);
            LogLine($"entrou: {c.Name} nv {c.Level} (conta {s.Account.Name}) · {OnlineCount} online");
            return $"OK bem-vindo, {c.Name}";
        }

        /// <summary>Salva o personagem da sessão e a conta (com o baú).</summary>
        private void SaveCharacter(Session s)
        {
            if (s.Character == null) return;
            _ranking.Update(s.Character);
            try
            {
                _repo.Save(s.Character);
                if (s.Account != null) _accounts.Save(s.Account);
            }
            catch (Exception e) { Console.Error.WriteLine($"[Kezor] falha ao salvar {s.Character.Name}: {e.Message}"); }
        }

        /// <summary>Salva todos os jogadores online (autosave e desligamento do servidor).</summary>
        public void SaveAll()
        {
            foreach (var s in Online.ToList()) SaveCharacter(s);
            if (_rankRepo != null && _ranking.Dirty)
                try { _rankRepo.SaveRanking(_ranking.Entries); _ranking.MarkSaved(); } catch (Exception e) { Console.Error.WriteLine("[Kezor] ranking: " + e.Message); }
        }

        private string Login(Session s, string[] p)
        {
            if (s.Character != null) return "ERR já conectado";
            if (_opt.RequireAccounts) return "ERR este servidor usa contas (AUTH <conta> <senha>, depois PLAY <nome>)";
            var c = _repo.Load(p[1]);
            if (c == null) return "ERR personagem não existe";
            if (_sessions.Values.Any(o => o != s && o.Character == c)) return "ERR já conectado";
            EnterWorld(s, c, keepPosition: true);   // personagem salvo: volta onde saiu
            return $"OK bem-vindo de volta, {c.Name}";
        }

        private void EnterWorld(Session s, Character c, bool keepPosition = false)
        {
            s.Character = c;
            s.ClearIntent();
            var map = MapOf(c.MapId);
            c.MapId = map.Id;
            // Herói salvo volta exatamente onde saiu (se o lugar não existir mais, o ponto livre mais perto); novo nasce na praça
            if (keepPosition && (c.X > 0 || c.Z > 0)) SaveData.FixPosition(c, map.LayoutVersion);
            else { c.X = map.RespawnX + _rng.Range(-2, 2); c.Z = map.RespawnZ + _rng.Range(-2, 2); }
            s.SentInvVersion = -1; s.LastMe = ""; s.SentAscVersion = int.MinValue;
            s.PortalReadyAt = _time + PortalCooldown;
            _ranking.Update(c);
            Push(s, $"@WELCOME {c.Id} {map.Id}");
            Sys(s, $"Bem-vindo a {map.Name}! Clique no chão para andar, clique num monstro para atacar.");
            BroadcastExcept(s, $"@EV SYS;{c.Name} entrou no mundo.");
            var guild = GuildOf(c.Name);
            if (guild != null) { SendGuild(guild); var war = WarOf(guild.Name); if (war != null) SendWar(war); }
            else Push(s, "@GUILD NONE");
        }

        private void BroadcastExcept(Session except, string msg) { foreach (var o in Online) if (o != except) o.Outbox.Add(msg); }

        private string Move(Session s, Character c, string[] p)
        {
            if (!TryPF(p[1], out var x) || !TryPF(p[2], out var z)) return "ERR coordenadas inválidas";
            s.ClearIntent();
            var map = MapOf(c.MapId);
            s.HasMove = true; s.MoveX = map.ClampCoord(x); s.MoveZ = map.ClampCoord(z);
            return "OK";
        }

        private bool TryGetTarget(int id, out ICombatant? t)
        {
            if (_monsters.TryGetValue(id, out var m)) { t = m.IsDead || m.IsSummon ? null : m; return t != null; }
            var ch = CharById(id);
            t = ch != null && !ch.IsDead ? ch : null;
            return t != null;
        }

        private string CanAttackPlayer(Character a, Character b)
        {
            if (a == b) return "não pode atacar a si mesmo";
            if (a.MapId != b.MapId) return "alvo em outro mapa";
            var map = MapOf(a.MapId);
            if (map.InSafeZone(a.X, a.Z) || map.InSafeZone(b.X, b.Z)) return "PvP proibido na vila";
            if (_partyOf.TryGetValue(a.Id, out var pa) && _partyOf.TryGetValue(b.Id, out var pb) && pa == pb) return "não pode atacar o seu grupo";
            if (b.Level < 6) return "jogadores abaixo do nível 6 são protegidos";
            return "";
        }

        private string Attack(Session s, Character c, string[] p)
        {
            int id = Int(p, 1);
            if (!TryGetTarget(id, out var t) || t!.MapId != c.MapId) return "ERR alvo inválido";
            if (t is Character tc) { var why = CanAttackPlayer(c, tc); if (why != "") return "ERR " + why; }
            s.ClearIntent();
            s.AutoTargetId = id;
            c.Dismount(); // não se luta montado
            return "OK";
        }

        private string Skill(Session s, Character c, string[] p)
        {
            var skill = SkillCatalog.Find(Int(p, 1));
            if (skill == null) return "ERR skill inexistente";
            if ((skill.Classes & ClassDefinition.MaskOf(c.Class)) == 0) return "ERR sua classe não usa essa skill";
            if (c.Level < skill.ReqLevel || c.Stats.Energy < skill.ReqEnergy) return $"ERR requer nível {skill.ReqLevel}" + (skill.ReqEnergy > 0 ? $" e energia {skill.ReqEnergy}" : "");
            if (c.Mp < skill.ManaCost) return "ERR mana insuficiente";
            if (c.Vigor < Character.SkillVigorCost) return "ERR vigor insuficiente";
            if (!skill.NeedsTarget) { c.Dismount(); return UseUtilitySkill(s, c, skill); }
            int id = Int(p, 2);
            if (!TryGetTarget(id, out var t) || t!.MapId != c.MapId) return "ERR alvo inválido";
            if (t is Character tc) { var why = CanAttackPlayer(c, tc); if (why != "") return "ERR " + why; }
            s.HasMove = false; s.PendingPick = 0;
            s.AutoTargetId = id; s.PendingSkill = skill.Id;
            c.Dismount();
            return "OK";
        }

        private string Pick(Session s, Character c, string[] p)
        {
            int id = Int(p, 1);
            if (!_ground.ContainsKey(id)) return "ERR item não existe mais";
            s.ClearIntent();
            s.PendingPick = id;
            return "OK";
        }

        /// <summary>RANKING [GERAL|GUILDAS|GUA|ARC|PAT|LAM|COM|INV|PUN]: manda @RANK tipo|pos;nome;classe;nível;renasc;guilda|... (top 10) e responde a sua posição.</summary>
        private string RankingCmd(Session s, Character c, string[] p)
        {
            foreach (var o in Online) _ranking.Update(o.Character!);   // quem está online entra atualizado
            var kind = p.Length > 1 ? p[1].ToUpperInvariant() : "GERAL";
            var sb = new StringBuilder("@RANK ");
            if (kind == "GUILDAS" || kind == "GUILDS")
            {
                sb.Append("GUILDAS");
                int pos = 1;
                foreach (var g in _ranking.Guilds(_guilds.Values))
                    sb.Append(ListSep).Append(pos++).Append(Sep).Append(g.Name).Append(Sep).Append(g.Score).Append(Sep).Append(g.Members)
                      .Append(Sep).Append(g.Wins).Append(Sep).Append(g.Losses).Append(Sep).Append(g.Draws);
                Push(s, sb.ToString());
                return "OK ranking de guildas";
            }
            CharacterClass? cls = null;
            if (kind != "GERAL" && ClassDefinition.TryParse(kind, out var parsed)) cls = parsed;
            else if (kind != "GERAL") return "ERR use RANKING GERAL|GUILDAS|GUA|ARC|PAT|LAM|COM|INV|PUN";
            sb.Append(cls == null ? "GERAL" : kind);
            int i = 1;
            foreach (var e in _ranking.Top(10, cls))
                sb.Append(ListSep).Append(i++).Append(Sep).Append(e.Name).Append(Sep).Append((int)e.Class).Append(Sep).Append(e.Level)
                  .Append(Sep).Append(e.Rebirths).Append(Sep).Append(GuildOf(e.Name)?.Name ?? "");
            Push(s, sb.ToString());
            int me = _ranking.PositionOf(c.Name, cls);
            if (me <= 0) return cls != null && cls != c.Class ? $"OK ranking de {ClassDefinition.Get(cls.Value).DisplayName} (seu herói é de outra classe)" : "OK você ainda não está no ranking";
            return $"OK você está em {me}º lugar";
        }

        private static string MountCmd(Character c)
        {
            if (c.Mounted) { c.Dismount(); return "OK desmontou"; }
            return c.Mount(out var err) ? $"OK montou: {c.MountItem!.Def.Name} (+{c.MountItem.Def.MountSpeedPercent}% de velocidade)" : "ERR " + err;
        }

        // ------------------------------------------------------------ mascotes
        public const float PetCollectRange = 6f;
        public const int CollectorGoldBonusPercent = 10;
        private double _petAcc;

        /// <summary>Diabrete de Cinza: recolhe os itens do dono (ou livres) num raio de 6 m, sem o herói andar.</summary>
        private void TickPets(float dt)
        {
            _petAcc += dt;
            if (_petAcc < 0.5) return;
            _petAcc = 0;
            foreach (var s in Online)
            {
                var c = s.Character!;
                if (c.IsDead || c.ActivePet != PetType.Collector) continue;
                foreach (var g in _ground.Values.ToList())
                {
                    if (g.MapId != c.MapId || MathK.Dist(c.X, c.Z, g.X, g.Z) > PetCollectRange) continue;
                    bool mine = g.OwnerId == c.Id || (g.OwnerPartyId != 0 && _partyOf.TryGetValue(c.Id, out var pp) && pp == g.OwnerPartyId);
                    if (!mine && _time < g.ReservedUntil) continue;
                    if (!c.Inventory.TryAdd(g.Item, out _)) break;
                    _ground.Remove(g.Id);
                    Sys(s, $"Diabrete recolheu: {g.Item.DisplayName}");
                }
            }
        }

        // ------------------------------------------------------------ tick
        public void Tick(float dt)
        {
            if (dt <= 0) return;
            if (dt > 0.5f) dt = 0.5f;
            _time += dt;

            _regenAcc += dt;
            bool regen = _regenAcc >= 1.0;
            if (regen) _regenAcc -= 1.0;

            foreach (var s in _sessions.Values.ToList())
            {
                var c = s.Character;
                if (c == null) continue;
                if (regen) Regen(c);
                c.TickVitals(dt);
                TickPlayer(s, c, dt);
                if (s.Character == c && !c.IsDead) CheckPortals(s, c);
            }
            TickMonsters(dt);
            TickPets(dt);
            TickTrades();
            TickShops();
            TickBanners();
            TickWars();
            TickGround();
            TickEvent(dt);
            TickRift(dt);

            if (_opt.AutosaveSeconds > 0)
            {
                _autosaveAcc += dt;
                if (_autosaveAcc >= _opt.AutosaveSeconds) { _autosaveAcc = 0; SaveAll(); }
            }

            _snapshotAcc += dt;
            if (_snapshotAcc >= _opt.SnapshotInterval)
            {
                _snapshotAcc = 0;
                SendSnapshots();
            }
        }

        private void Regen(Character c)
        {
            bool safe = MapOf(c.MapId).InSafeZone(c.X, c.Z);
            c.Hp = Math.Min(c.MaxHp, c.Hp + Math.Max(1, c.MaxHp * (safe ? 5 : 1) / 100));
            c.Mp = Math.Min(c.MaxMp, c.Mp + Math.Max(1, c.MaxMp * (safe ? 8 : 3) / 100));
        }

        /// <summary>
        /// Anda em direção a (tx, tz). Se a linha reta estiver livre vai direto; senão segue o caminho do A*
        /// (recalculado quando o destino muda mais de 1 m). Retorna true ao chegar a stopAt do destino.
        /// </summary>
        private bool StepChar(Session s, Character c, float tx, float tz, float dt, float stopAt)
        {
            var map = MapOf(c.MapId);
            float dx = tx - c.X, dz = tz - c.Z;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d <= stopAt) { s.Path = null; return true; }
            float wx = tx, wz = tz;
            bool routed = false;
            if (!map.LineClear(c.X, c.Z, tx, tz))
            {
                if (s.Path == null || float.IsNaN(s.PathTx) || MathK.Dist(s.PathTx, s.PathTz, tx, tz) > 1f)
                {
                    s.Path = PathOf(c.MapId).Find(c.X, c.Z, tx, tz);
                    s.PathTx = tx; s.PathTz = tz;
                }
                while (s.Path != null && s.Path.Count > 1 && MathK.Dist(c.X, c.Z, s.Path[0].x, s.Path[0].z) < 0.25f) s.Path.RemoveAt(0);
                if (s.Path != null && s.Path.Count > 0)
                {
                    wx = s.Path[0].x; wz = s.Path[0].z;
                    routed = s.Path.Count > 1;
                }
            }
            else s.Path = null;
            float sx = wx - c.X, sz = wz - c.Z, sd = (float)Math.Sqrt(sx * sx + sz * sz);
            if (sd < 0.0001f) return true;
            // dentro da vila e das zonas seguras o herói caminha (sem montaria); fora delas, corre
            float speed = !c.Mounted && map.InSafeZone(c.X, c.Z) ? Math.Min(c.MoveSpeed, TownWalkSpeed) : c.MoveSpeed;
            float step = Math.Min(speed * dt, routed ? sd : Math.Max(0f, d - stopAt));
            float x = c.X, z = c.Z;
            if (!map.TryStep(ref x, ref z, sx / sd, sz / sd, step)) { s.Path = null; return true; } // encurralado: para
            if (Math.Abs(x - c.X) + Math.Abs(z - c.Z) > 0.0001f) c.Facing = (float)(Math.Atan2(x - c.X, z - c.Z) * 180.0 / Math.PI);
            c.X = x; c.Z = z;
            return !routed && d - step <= stopAt + 0.001f;
        }

        /// <summary>Velocidade de caminhada na vila (m/s).</summary>
        public const float TownWalkSpeed = 1.8f;   // passo de caminhada natural para o clipe de mocap (Anim/Heroi: Walk)

        /// <summary>Portais: pisar no círculo leva ao outro mapa (nível mínimo do destino; 2 s de espera após chegar).</summary>
        private void CheckPortals(Session s, Character c)
        {
            if (_time < s.PortalReadyAt) return;
            foreach (var p in MapOf(c.MapId).Portals)
            {
                if (MathK.Dist(c.X, c.Z, p.X, p.Z) > p.Radius) continue;
                var dest = MapOf(p.TargetMap);
                s.PortalReadyAt = _time + PortalCooldown;
                if (c.Level < dest.MinLevel) { Sys(s, $"{dest.Name} exige nível {dest.MinLevel}."); return; }
                Transfer(s, c, dest, p.TargetX, p.TargetZ);
                return;
            }
        }

        /// <summary>Leva o personagem para outro mapa (portal, /warp ou morte).</summary>
        private void Transfer(Session? s, Character c, MapDefinition dest, float x, float z)
        {
            c.MapId = dest.Id;
            c.X = dest.ClampCoord(x); c.Z = dest.ClampCoord(z);
            c.Dismount();
            foreach (var m in _monsters.Values)
                if (m.TargetId == c.Id) { m.State = MonsterState.Return; m.TargetId = 0; }
            if (s == null) return;
            s.ClearIntent(); s.Path = null;
            s.PortalReadyAt = _time + PortalCooldown;
            s.LastMe = "";
            Push(s, $"@WELCOME {c.Id} {dest.Id}");
            Sys(s, $"Você chegou a {dest.Name}.");
        }

        private void TickPlayer(Session s, Character c, float dt)
        {
            if (s.PendingUtility >= 0 && _time - s.LastAttackTime >= c.Def.AttackCooldown)
            {   // skill sem alvo que chegou no meio do golpe: lança agora, sem interromper o ataque automático
                var u = SkillCatalog.Find(s.PendingUtility);
                s.PendingUtility = -1;
                if (u != null) { var r = UseUtilitySkill(s, c, u); if (r.StartsWith("ERR")) Sys(s, r.Substring(4)); }
            }
            if (s.PendingPick != 0)
            {
                if (!_ground.TryGetValue(s.PendingPick, out var g)) { s.PendingPick = 0; return; }
                if (g.MapId != c.MapId) { s.PendingPick = 0; return; }
                if (StepChar(s, c, g.X, g.Z, dt, PickRange * 0.8f)) { DoPick(s, c, g); s.PendingPick = 0; }
                return;
            }
            if (s.AutoTargetId != 0)
            {
                if (!TryGetTarget(s.AutoTargetId, out var t)) { s.AutoTargetId = 0; s.PendingSkill = -1; return; }
                if (t is Character tc && CanAttackPlayer(c, tc) != "") { s.AutoTargetId = 0; s.PendingSkill = -1; return; }
                var skill = s.PendingSkill >= 0 ? SkillCatalog.Find(s.PendingSkill)! : SkillCatalog.BasicAttack;
                float range = SkillCatalog.RangeOf(skill, c.Class);
                float d = MathK.Dist(c.X, c.Z, t!.X, t.Z);
                if (t.MapId != c.MapId) { s.AutoTargetId = 0; s.PendingSkill = -1; return; }
                if (d > range) { StepChar(s, c, t.X, t.Z, dt, range * 0.9f); return; }
                if (_time - s.LastAttackTime < c.Def.AttackCooldown) return;
                if (c.Mp < skill.ManaCost) { Sys(s, "Mana insuficiente."); s.PendingSkill = -1; return; }
                if (skill.Id != 0 && c.Vigor < Character.SkillVigorCost) { Sys(s, "Vigor insuficiente."); s.PendingSkill = -1; return; }
                DoAttack(s, c, t, skill);
                s.PendingSkill = -1;
                return;
            }
            if (s.HasMove && StepChar(s, c, s.MoveX, s.MoveZ, dt, 0.05f)) s.HasMove = false;
        }

        private void DoAttack(Session s, Character c, ICombatant target, SkillDefinition skill)
        {
            s.LastAttackTime = _time;
            c.Mp -= skill.ManaCost;
            if (skill.Id != 0) c.SpendVigor(Character.SkillVigorCost);
            c.EnterCombat();
            var killed = new List<Monster>();

            void HitOne(ICombatant t, HitResult h, SkillDefinition sk)
            {
                BroadcastNear(t.MapId, t.X, t.Z, $"@EV HIT;{c.Id};{t.Id};{(int)h.Type};{h.Damage};{sk.Id}");
                if (t is Monster m)
                {
                    if (h.Damage > 0) m.DamageBy[c.Id] = (m.DamageBy.TryGetValue(c.Id, out var dd) ? dd : 0) + h.Damage;
                    if (m.State != MonsterState.Chase && !m.IsDead) { m.State = MonsterState.Chase; m.TargetId = c.Id; }
                    if (h.Killed && !killed.Contains(m)) killed.Add(m);
                }
                else if (t is Character v)
                {
                    v.WearEquipment(false, _rng);
                    if (h.Killed) OnPlayerKilled(v, c);
                }
            }

            for (int hitN = 0; hitN < Math.Max(1, skill.Hits) && !target.IsDead; hitN++)
            {
                var hit = CombatResolver.Resolve(c, target, skill, _rng);
                HitOne(target, hit, skill);
                if (hit.Type != HitType.Miss && target is Monster tm) ApplyCurses(c, tm, skill);
                // Fluxo de Brasa: cada acerto soma uma brasa; a 5ª solta o Golpe do Fluxo
                if (c.Class == CharacterClass.EmberFist && c.Level >= EmberFlow.MinLevel && hit.Type != HitType.Miss && !target.IsDead && s.Flow.Register(target.Id, _time))
                {
                    var fr = new HitResult { Type = HitType.Combo };
                    fr.Damage = CombatResolver.Apply(c.GetCombatStats(), target.GetCombatStats(), target, EmberFlow.FlowDamage(c.Stats), out fr.Killed);
                    HitOne(target, fr, skill);
                }
            }

            if (skill.AreaRadius > 0)
            {
                BroadcastNear(target.MapId, target.X, target.Z, $"@EV FX;{c.Id};{skill.Id};{F(target.X)};{F(target.Z)}");
                foreach (var m in _monsters.Values.ToList())
                {
                    if (m == target || m.IsDead || m.IsSummon || m.MapId != target.MapId) continue;
                    if (MathK.Dist(m.X, m.Z, target.X, target.Z) > skill.AreaRadius) continue;
                    var ah = CombatResolver.Resolve(c, m, skill, _rng);
                    HitOne(m, ah, skill);
                    if (ah.Type != HitType.Miss) ApplyCurses(c, m, skill);
                }
            }

            if (skill.ComboStarter && c.Level >= ComboTracker.MinLevel && s.Combo.Register(skill.Id, _time) && !target.IsDead)
            {
                var cr = new HitResult { Type = HitType.Combo };
                cr.Damage = CombatResolver.Apply(c.GetCombatStats(), target.GetCombatStats(), target, ComboTracker.ComboDamage(c.Stats), out cr.Killed);
                HitOne(target, cr, skill);
            }

            c.WearEquipment(true, _rng);
            foreach (var m in killed) OnMonsterKilled(m, c);
        }

        private void OnMonsterKilled(Monster m, Character killer)
        {
            BroadcastNear(m.MapId, m.X, m.Z, $"@EV DIE;{m.Id}");
            int ownerId = m.TopDamager();
            var owner = CharById(ownerId) ?? killer;

            // Experiência: dividida no grupo (proporcional ao nível) com bônus por membro.
            var recipients = new List<Character> { owner };
            if (_partyOf.TryGetValue(owner.Id, out var pid) && _parties.TryGetValue(pid, out var party))
            {
                recipients = party.Members.Select(CharById).Where(ch => ch != null && !ch.IsDead && ch.MapId == m.MapId &&
                    MathK.Dist(ch.X, ch.Z, m.X, m.Z) <= Party.ShareRange).Select(ch => ch!).ToList();
                if (!recipients.Contains(owner)) recipients.Add(owner);
            }
            float pool = ExpFor(owner.Level, m.Def) * Party.ExpBonus(recipients.Count);
            int sumLv = recipients.Sum(r => r.Level);
            foreach (var r in recipients)
            {
                long exp = Math.Max(1, (long)(pool * r.Level / sumLv));
                int ascBefore = r.Ascension.Level;
                int lv = r.GainExperience(exp);
                if (r.Ascension.Level > ascBefore)
                {
                    BroadcastNear(r.MapId, r.X, r.Z, $"@EV ASCLVL;{r.Id};{r.Ascension.Level}");
                    Sys(r, $"Ascensão {r.Ascension.Level}! +{r.Ascension.Level - ascBefore} ponto(s) na Árvore da Ascensão (tecla A).");
                }
                Push(r, $"@EV EXP;{exp}");
                if (lv > 0)
                {
                    _ranking.Update(r);
                    BroadcastNear(r.MapId, r.X, r.Z, $"@EV LVL;{r.Id};{r.Level}");
                    Sys(r, $"Subiu para o nível {r.Level}! +{lv * r.Def.PointsPerLevel} pontos de atributo (tecla C).");
                }
            }

            long gold = _rng.Next(m.Def.GoldDropMax / 4, m.Def.GoldDropMax + 1);
            if (owner.ActivePet == PetType.Collector) gold += gold * CollectorGoldBonusPercent / 100;
            owner.Gold += gold;
            Push(owner, $"@EV GOLD;{gold}");
            var ks = killer.GetCombatStats();
            if (ks.LifeOnKill > 0) killer.Hp = Math.Min(killer.MaxHp, killer.Hp + killer.MaxHp * ks.LifeOnKill / 8);

            foreach (var it in DropSystem.Roll(m.Def, _rng))
            {
                var g = new GroundItem
                {
                    Id = _nextEntityId++, Item = it, MapId = m.MapId,
                    X = MapOf(m.MapId).ClampCoord(m.X + _rng.Range(-1.2f, 1.2f)), Z = MapOf(m.MapId).ClampCoord(m.Z + _rng.Range(-1.2f, 1.2f)),
                    OwnerId = owner.Id, OwnerPartyId = _partyOf.TryGetValue(owner.Id, out var op) ? op : 0,
                    ReservedUntil = _time + ReservedSeconds, ExpiresAt = _time + GroundItemSeconds
                };
                _ground[g.Id] = g;
            }

            CountRiftKill(m, owner);
            if (m.Def.Boss) OnBossKilled(m, owner);
            if (m.Temporary) _monsters.Remove(m.Id);
            else m.RespawnTimer = m.Def.RespawnSeconds;
        }

        private void OnPlayerKilled(Character victim, Character? killer)
        {
            BroadcastNear(victim.MapId, victim.X, victim.Z, $"@EV DIE;{victim.Id}");
            if (killer != null)
            {
                if (!CountWarKill(killer, victim)) killer.Infamy++;   // abate de guerra não dá infâmia
                SysAll($"{killer.Name} derrotou {victim.Name} em combate!");
            }
            else Sys(victim, "Você caiu em combate e despertou na vila.");
            var vs = SessionOf(victim.Id);
            vs?.ClearIntent();
            vs?.Combo.Reset();
            vs?.Flow.Reset();
            victim.Dismount();
            victim.Hp = victim.MaxHp; victim.Mp = victim.MaxMp;
            // Desperta sempre na vila do Vale (de qualquer mapa)
            var vale = Map;
            if (victim.MapId != vale.Id) Transfer(vs, victim, vale, vale.RespawnX + _rng.Range(-2, 2), vale.RespawnZ + _rng.Range(-2, 2));
            else
            {
                victim.X = vale.RespawnX + _rng.Range(-2, 2); victim.Z = vale.RespawnZ + _rng.Range(-2, 2);
                foreach (var m in _monsters.Values)
                    if (m.TargetId == victim.Id) { m.State = MonsterState.Return; m.TargetId = 0; }
            }
        }

        /// <summary>Exp de monstro ajustada pela diferença de nível (penalidade se monstro muito fraco).</summary>
        public static long ExpFor(int playerLevel, MonsterDefinition m)
        {
            int diff = playerLevel - m.Level;
            if (diff <= 10) return m.Experience;
            return Math.Max(1, m.Experience * 10 / diff);
        }

        private void TickMonsters(float dt)
        {
            foreach (var m in _monsters.Values.ToList())
            {
                if (m.IsDead)
                {
                    m.RespawnTimer -= dt;
                    if (m.RespawnTimer <= 0) m.Respawn();
                    continue;
                }
                if (m.IsSummon) { TickSummon(m, dt); continue; }
                if (TickDot(m)) continue;
                m.AttackTimer -= dt;
                float px = m.X, pz = m.Z;
                var map = MapOf(m.MapId);
                float slow = m.SpeedFactor(_time);
                switch (m.State)
                {
                    case MonsterState.Chase:
                    {
                        var t = CharById(m.TargetId);
                        if (t == null || t.IsDead || t.MapId != m.MapId || map.InSafeZone(t.X, t.Z) || (!m.Temporary && MathK.Dist(m.X, m.Z, m.SpawnX, m.SpawnZ) > Monster.LeashRange))
                        { m.State = MonsterState.Return; m.TargetId = 0; break; }
                        float d = MathK.Dist(m.X, m.Z, t.X, t.Z);
                        if (d > m.Def.AttackRange) StepMonster(m, map, t.X, t.Z, m.Def.MoveSpeed * slow, dt, m.Def.AttackRange * 0.85f);
                        else if (m.AttackTimer <= 0)
                        {
                            m.AttackTimer = m.Def.AttackInterval;
                            var h = CombatResolver.Resolve(m, t, SkillCatalog.BasicAttack, _rng);
                            BroadcastNear(t.MapId, t.X, t.Z, $"@EV HIT;{m.Id};{t.Id};{(int)h.Type};{h.Damage};0");
                            t.WearEquipment(false, _rng);
                            if (h.Killed) OnPlayerKilled(t, null);
                        }
                        break;
                    }
                    case MonsterState.Return:
                        if (StepMonster(m, map, m.SpawnX, m.SpawnZ, m.Def.MoveSpeed * 1.4f * slow, dt, 0.3f))
                        { m.State = MonsterState.Idle; m.Hp = m.MaxHp; m.DamageBy.Clear(); m.StateTimer = _rng.Range(1, 3); m.Path = null; }
                        else if (MathK.Dist(px, pz, m.X, m.Z) < 0.0005f)
                        { m.X = m.SpawnX; m.Z = m.SpawnZ; m.Path = null; }   // preso no caminho de volta: some e reaparece no ponto de origem
                        break;
                    case MonsterState.Wander:
                        if (TryAggro(m)) break;
                        if (m.StepTowards(m.WanderX, m.WanderZ, m.Def.MoveSpeed * 0.45f * slow, dt))
                        { m.State = MonsterState.Idle; m.StateTimer = _rng.Range(2, 6); }
                        break;
                    default:
                        if (TryAggro(m)) break;
                        m.StateTimer -= dt;
                        if (m.StateTimer <= 0)
                        {
                            float a = _rng.Range(0, 6.2831f), r = _rng.Range(1, m.WanderRadius);
                            float wx = map.ClampCoord(m.SpawnX + (float)Math.Cos(a) * r), wz = map.ClampCoord(m.SpawnZ + (float)Math.Sin(a) * r);
                            if (map.LineClear(m.X, m.Z, wx, wz)) { m.WanderX = wx; m.WanderZ = wz; m.State = MonsterState.Wander; }
                            else m.StateTimer = _rng.Range(1, 3);
                        }
                        break;
                }
                if (map.IsBlocked(m.X, m.Z) && !map.IsBlocked(px, pz))
                {   // parede: desliza girando a direção; se não der, desiste de vagar
                    float x = px, z = pz, ddx = m.X - px, ddz = m.Z - pz, len = (float)Math.Sqrt(ddx * ddx + ddz * ddz);
                    m.X = px; m.Z = pz;
                    if (len > 0.0001f && map.TryStep(ref x, ref z, ddx / len, ddz / len, len)) { m.X = x; m.Z = z; }
                    else if (m.State == MonsterState.Wander) { m.State = MonsterState.Idle; m.StateTimer = _rng.Range(1, 3); }
                }
                if (map.InSafeZone(m.X, m.Z)) { m.X = px; m.Z = pz; if (m.State == MonsterState.Chase) { m.State = MonsterState.Return; m.TargetId = 0; } }
            }
        }

        /// <summary>
        /// Passo de monstro: linha reta se estiver livre; senão segue o A* do mapa (recalcula quando o alvo andou mais
        /// de 1,5 m ou a cada 1 s). Desliza nas paredes com TryStep. Retorna true ao chegar a stopAt do destino.
        /// </summary>
        private bool StepMonster(Monster m, MapDefinition map, float tx, float tz, float speed, float dt, float stopAt)
        {
            float d = MathK.Dist(m.X, m.Z, tx, tz);
            if (d <= stopAt) { m.Path = null; return true; }
            float wx = tx, wz = tz;
            bool routed = false;
            if (!map.LineClear(m.X, m.Z, tx, tz))
            {
                if (m.Path == null || float.IsNaN(m.PathTx) || MathK.Dist(m.PathTx, m.PathTz, tx, tz) > 1.5f || _time - m.PathAt > 1.0)
                {
                    m.Path = PathOf(m.MapId).Find(m.X, m.Z, tx, tz);
                    m.PathTx = tx; m.PathTz = tz; m.PathAt = _time;
                }
                while (m.Path != null && m.Path.Count > 1 && MathK.Dist(m.X, m.Z, m.Path[0].x, m.Path[0].z) < 0.25f) m.Path.RemoveAt(0);
                if (m.Path != null && m.Path.Count > 0) { wx = m.Path[0].x; wz = m.Path[0].z; routed = m.Path.Count > 1; }
            }
            else m.Path = null;
            float sx = wx - m.X, sz = wz - m.Z, sd = (float)Math.Sqrt(sx * sx + sz * sz);
            if (sd < 0.0001f) return true;
            float step = Math.Min(speed * dt, routed ? sd : Math.Max(0f, d - stopAt));
            float x = m.X, z = m.Z;
            if (!map.TryStep(ref x, ref z, sx / sd, sz / sd, step)) { m.Path = null; return false; }
            m.X = x; m.Z = z;
            return !routed && d - step <= stopAt + 0.001f;
        }

        private bool TryAggro(Monster m)
        {
            if (!m.Def.Aggressive) return false;
            Character? best = null; float bd = m.Def.AggroRange;
            foreach (var s in Online)
            {
                var c = s.Character!;
                if (c.IsDead || c.MapId != m.MapId || MapOf(c.MapId).InSafeZone(c.X, c.Z)) continue;
                float d = MathK.Dist(m.X, m.Z, c.X, c.Z);
                if (d < bd) { bd = d; best = c; }
            }
            if (best == null) return false;
            m.State = MonsterState.Chase; m.TargetId = best.Id;
            return true;
        }

        private void TickGround()
        {
            List<int>? rm = null;
            foreach (var g in _ground.Values) if (_time >= g.ExpiresAt) (rm ??= new List<int>()).Add(g.Id);
            if (rm != null) foreach (var id in rm) _ground.Remove(id);
        }

        private void DoPick(Session s, Character c, GroundItem g)
        {
            bool mine = g.OwnerId == c.Id || (g.OwnerPartyId != 0 && _partyOf.TryGetValue(c.Id, out var p) && p == g.OwnerPartyId);
            if (!mine && _time < g.ReservedUntil) { Sys(s, "Este item está reservado para outro jogador."); return; }
            if (!c.Inventory.TryAdd(g.Item, out _)) { Sys(s, "Inventário cheio."); return; }
            _ground.Remove(g.Id);
            Sys(s, $"Obteve: {g.Item.DisplayName}");
            if (Notable(g.Item)) LogLine($"item: {c.Name} pegou {g.Item.DisplayName} ({MapOf(c.MapId).Name})");
        }

        // ------------------------------------------------------------ snapshots
        private void SendSnapshots()
        {
            var ents = new List<(int map, float x, float z, string s)>();
            foreach (var o in Online)
            {
                var c = o.Character!;
                int party = _partyOf.TryGetValue(c.Id, out var pid) ? pid : 0;
                ents.Add((c.MapId, c.X, c.Z, $"P;{c.Id};{c.Name};{F(c.X)};{F(c.Z)};{c.Hp};{c.MaxHp};{c.Level};{(int)c.Class};{party};{c.Infamy};{WeaponId(c)};{Appearance.Encode(c)};{GuildOf(c.Name)?.Name ?? ""};{Clean(ShopTitle(o))};{(int)c.Facing}"));
            }
            foreach (var m in _monsters.Values)
                if (!m.IsDead) ents.Add((m.MapId, m.X, m.Z, $"M;{m.Id};{m.Def.Id};{F(m.X)};{F(m.Z)};{m.Hp};{m.MaxHp};{m.OwnerId}"));
            foreach (var b in _banners.Values)
                ents.Add((b.MapId, b.X, b.Z, $"B;{b.Id};{(int)b.Kind};{F(b.X)};{F(b.Z)};{Clean(b.OwnerName)};{F(b.Radius)};{b.Value}"));
            foreach (var g in _ground.Values)
                ents.Add((g.MapId, g.X, g.Z, $"D;{g.Id};{g.Item.Def.Id};{F(g.X)};{F(g.Z)};{g.Item.Level};{(int)g.Item.Rarity};{Clean(g.Item.DisplayName)}"));

            var sb = new StringBuilder();
            foreach (var s in Online)
            {
                var c = s.Character!;
                sb.Clear().Append("@S ").Append(F((float)_time));
                foreach (var e in ents)
                    if (e.map == c.MapId && Math.Abs(e.x - c.X) <= _opt.ViewRange && Math.Abs(e.z - c.Z) <= _opt.ViewRange) sb.Append(ListSep).Append(e.s);
                s.Outbox.Add(sb.ToString());
                var me = MeLine(s, c);
                if (me != s.LastMe) { s.Outbox.Add(me); s.LastMe = me; }
                if (c.Inventory.Version != s.SentInvVersion) SendInventory(s);
                if (c.Ascension.Version != s.SentAscVersion) SendAscension(s, c);
                if (s.VaultOpen)
                {
                    if (NearVault(c) != "") { s.VaultOpen = false; Push(s, "@VAULT CLOSE"); }
                    else if (c.Vault.Version != s.SentVaultVersion) SendVault(s);
                }
            }
        }

        private static int WeaponId(Character c) => c.Equipment.TryGetValue(EquipSlot.RightHand, out var w) ? w.Def.Id : 0;

        /// <summary>Buffs, auras e recargas como horário absoluto do servidor (a linha só muda quando algo muda):
        /// B tipo:valor:expira · A tipo:valor (aura de estandarte) · C skill:pronta.</summary>
        private string EffectsField(Session s, Character c)
        {
            var sb = new StringBuilder();
            foreach (var b in c.Buffs) sb.Append(sb.Length > 0 ? "," : "").Append('B').Append((int)b.kind).Append(':').Append(b.value).Append(':').Append(F((float)b.expires));
            if (c.AuraDamagePercent > 0) sb.Append(sb.Length > 0 ? "," : "").Append("A1:").Append(c.AuraDamagePercent);
            if (c.AuraDefense > 0) sb.Append(sb.Length > 0 ? "," : "").Append("A2:").Append(c.AuraDefense);
            if (c.AuraSpeedPercent > 0) sb.Append(sb.Length > 0 ? "," : "").Append("A3:").Append(c.AuraSpeedPercent);
            foreach (var kv in s.SkillReadyAt)
                if (kv.Value > _time) sb.Append(sb.Length > 0 ? "," : "").Append('C').Append(kv.Key).Append(':').Append(F((float)kv.Value));
            return sb.ToString();
        }

        private string MeLine(Session s, Character c)
        {
            var cs = c.GetCombatStats();
            int party = _partyOf.TryGetValue(c.Id, out var pid) ? pid : 0;
            return $"@ME {c.Id};{Clean(c.Name)};{(int)c.Class};{c.Level};{c.Experience};{Character.ExpToNext(c.Level)};{c.Hp};{c.MaxHp};{c.Mp};{c.MaxMp};{c.Gold};{c.FreePoints};" +
                   $"{c.Stats.Strength};{c.Stats.Agility};{c.Stats.Vitality};{c.Stats.Energy};{c.Rebirths};{cs.MinDamage};{cs.MaxDamage};{cs.MinMagic};{cs.MaxMagic};{cs.Defense};{cs.AttackRate};{cs.DefenseRate};{party};{c.Infamy};{F(c.X)};{F(c.Z)};{c.MapId};{EffectsField(s, c)};" +
                   $"{(int)c.Vigor};{c.MaxVigor};{(int)c.Shield};{c.MaxShield};{string.Join(",", c.QuickSlots)}";
        }

        public static string ItemFields(Item it) =>
            $"{it.Def.Id};{it.Level};{it.Quantity};{(it.Luck ? 1 : 0)};{(it.Skill ? 1 : 0)};{it.AdditionalOption};{(int)it.Ascended};{it.Durability}";

        private void SendVault(Session s)
        {
            var v = s.Character!.Vault;
            s.SentVaultVersion = v.Version;
            var sb = new StringBuilder("@VAULT ").Append(v.Gold);
            for (int i = 0; i < v.Items.Capacity; i++)
            {
                var it = v.Items.Get(i);
                if (it != null) sb.Append(ListSep).Append("I;").Append(i).Append(Sep).Append(ItemFields(it));
            }
            s.Outbox.Add(sb.ToString());
        }

        private void SendInventory(Session s)
        {
            var c = s.Character!;
            s.SentInvVersion = c.Inventory.Version;
            var sb = new StringBuilder("@INV ").Append(c.Gold);
            for (int i = 0; i < c.Inventory.Capacity; i++)
            {
                var it = c.Inventory.Get(i);
                if (it != null) sb.Append(ListSep).Append("I;").Append(i).Append(Sep).Append(ItemFields(it));
            }
            foreach (var kv in c.Equipment) sb.Append(ListSep).Append("E;").Append((int)kv.Key).Append(Sep).Append(ItemFields(kv.Value));
            s.Outbox.Add(sb.ToString());
        }
    }
}
