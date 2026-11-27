using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Combat;
using Kezor.Core.Items;
using Kezor.Core.Net;
using Kezor.Core.Server;
using Kezor.Core.World;

namespace Kezor.Client
{
    /// <summary>
    /// Ponto de entrada do cliente. Cria-se sozinho ao apertar Play em qualquer cena (ou pode ser colocado numa cena).
    /// Só multiplayer: lista de servidores -> conta e senha -> seleção de personagem -> mundo dividido com os outros jogadores.
    /// Sempre por TCP, inclusive no "Servidor deste PC" (o jogo abre um servidor de verdade aqui e entra nele como qualquer
    /// outro jogador). Toda regra de jogo está no servidor; aqui só há visual, entrada do jogador e HUD.
    /// </summary>
    public sealed partial class KezorGame : MonoBehaviour
    {
        public static KezorGame? Instance { get; private set; }
        /// <summary>Id do herói local (a vista dele gira mais rápido para responder ao clique).</summary>
        public static int MyIdForViews => Instance != null ? Instance.State.MyId : 0;
        public static bool PointerOverUi;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (Application.isBatchMode || Instance != null) return;
            if (FindAnyObjectByType<ServerBootstrap>() != null) return;
            new GameObject("Kezor").AddComponent<KezorGame>();
        }

        // ---------------------------------------------------------------- estado
        public readonly ClientState State = new ClientState();
        private IClientConnection? _conn;
        private float _nextPing, _pingSentAt = -1;
        /// <summary>Latência medida pelo PING (ms); -1 enquanto não mediu.</summary>
        public int PingMs { get; private set; } = -1;
        private readonly Queue<Action<string>?> _replyHandlers = new Queue<Action<string>?>();

        private Camera _cam = null!;
        private IsoCamera _iso = null!;
        private Effects _fx = null!;
        private CombatAudio _sfx = null!;
        private readonly Dictionary<int, EntityView> _views = new Dictionary<int, EntityView>();
        private readonly Dictionary<int, float> _diedAt = new Dictionary<int, float>();
        private readonly List<(NpcDefinition def, EntityView view)> _npcs = new List<(NpcDefinition, EntityView)>();
        private MapDefinition _map = MapDefinition.Get(MapDefinition.ValeId);
        private GameObject? _worldRoot;
        private int _builtMapId = -1;
        private bool _loadingShown;

        // login
        internal string _name = "";
        internal int _classIndex;
        /// <summary>Nome do servidor e canal onde o jogador entrou (para a interface).</summary>
        internal string _serverLabel = "";
        internal string _host = "127.0.0.1", _port = Protocol.DefaultPort.ToString();
        internal string _loginError = "";
        internal bool _connecting;
        private float _connectStarted;
        public bool InWorld => State.InWorld && _conn != null;

        // entrada
        private int _hoverId; private EntityKind _hoverKind; private NpcDefinition? _hoverNpc;
        private int _selectedTarget;
        private int _skillSlot;
        private bool _holdMove; private float _nextMoveSend; private Vector3 _lastMoveSent;
        private NpcDefinition? _pendingNpc;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            _name = "Heroi" + UnityEngine.Random.Range(100, 999);

            _cam = Camera.main;
            if (_cam == null) { var cg = new GameObject("Main Camera") { tag = "MainCamera" }; _cam = cg.AddComponent<Camera>(); cg.AddComponent<AudioListener>(); }
            _cam.fieldOfView = 35f; _cam.nearClipPlane = 0.3f; _cam.farClipPlane = 220f;
            _iso = _cam.gameObject.GetComponent<IsoCamera>();
            if (_iso == null) _iso = _cam.gameObject.AddComponent<IsoCamera>();
            KezorBloom.LoadPrefs();
            KezorBloom.Attach(_cam);
            _iso.OrbitCenter = new Vector3(_map.SafeX, 3f, _map.SafeZ);
#if UNITY_6000_6_OR_NEWER
            foreach (var l in FindObjectsByType<Light>()) l.enabled = false;
#else
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.enabled = false;
#endif
            // (a luz vem do shader)

            _fx = new GameObject("Efeitos").AddComponent<Effects>();
            _sfx = CombatAudio.Ensure();
            UI.UiRoot.Create(this);
            LoadMap(MapDefinition.ValeId);
        }

        /// <summary>Troca de mapa (portal, /warp, morte): refaz o mundo, a luz/névoa e os NPCs do mapa novo.</summary>
        private void LoadMap(int mapId)
        {
            _map = MapDefinition.Get(mapId);
            _builtMapId = _map.Id;
            if (_worldRoot != null) Destroy(_worldRoot);
            foreach (var (_, view) in _npcs) if (view != null) Destroy(view.gameObject);
            _npcs.Clear();
            foreach (var v in _views.Values) if (v != null) Destroy(v.gameObject);
            _views.Clear(); _diedAt.Clear(); _selectedTarget = 0; _pendingNpc = null;
            _worldRoot = WorldBuilder.Build(_map);
            KezorLook.ApplyEnvironment(_cam, _map.Id);
            _iso.OrbitCenter = _map.SafeRadius > 0 ? new Vector3(_map.SafeX, 3f, _map.SafeZ) : new Vector3(_map.RespawnX, 3f, _map.RespawnZ);
            foreach (var n in _map.Npcs)
            {
                var rig = Models.Npc(n.Name, n.Role);
                var v = EntityView.Create(rig, -n.Id, EntityKind.Player, new Vector3(n.X, WorldBuilder.Height(n.X, n.Z), n.Z));
                v.transform.rotation = Quaternion.LookRotation(new Vector3(_map.SafeX - n.X, 0, _map.SafeZ - n.Z));
                _npcs.Add((n, v));
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Disconnect();
            StopHost();
        }

        private void OnApplicationQuit() { Disconnect(); StopHost(); }

        // ---------------------------------------------------------------- conexão
        // volta automática depois de uma queda (sem pedir a senha de novo)
        private string _resumeToken = "", _resumeAccount = "", _resumeHero = "";
        private bool _resuming;
        private int _reconnectAttempt;
        private float _reconnectAt = -1;
        private static readonly float[] ReconnectDelays = { 1f, 2f, 4f, 6f, 8f, 10f };
        /// <summary>Tentando voltar sozinho para o servidor depois de uma queda.</summary>
        public bool Reconnecting => _reconnectAt >= 0 || _resuming;

        /// <summary>Conecta por TCP no servidor escolhido (_host/_port) e, ao conectar, entra na conta (AUTH ou REGISTER).</summary>
        internal void StartGame() => StartGame(false);

        private void StartGame(bool resume)
        {
            _loginError = "";
            if (!resume) { _resumeToken = ""; _resumeHero = ""; _reconnectAttempt = 0; }
            _resuming = resume;
            _reconnectAt = -1;
            Disconnect();
            State.MyId = 0;
            if (!int.TryParse(_port, out var port) || port < 1 || port > 65535) { _loginError = "Porta inválida"; return; }
            if (_host.Trim().Length == 0) { _loginError = "Endereço do servidor vazio"; return; }
            _conn = new TcpClientConnection(_host.Trim(), port);
            _connecting = true; _connectStarted = Time.time;
            PingMs = -1; _pingSentAt = -1; _nextPing = Time.unscaledTime + 5f;
        }

        /// <summary>Sai do mundo e volta para a seleção de personagem sem desconectar (o servidor salva o herói).</summary>
        internal void BackToCharacterSelect()
        {
            if (!InWorld) return;
            Send("SELECT", r =>
            {
                if (r.StartsWith("ERR")) { State.AddLog("err", r.Substring(4)); return; }
                _resumeHero = "";
                LeaveWorldLocally();
                ParseCharList(r);
            });
        }

        /// <summary>Volta para a lista de servidores (desconecta).</summary>
        internal void BackToServerList()
        {
            _resumeToken = ""; _resumeHero = ""; _resuming = false; _reconnectAt = -1;
            Disconnect();
            _loginStage = LoginStage.Form;
        }

        private void LeaveWorldLocally()
        {
            CloseAll();
            foreach (var v in _views.Values) if (v != null) Destroy(v.gameObject);
            _views.Clear(); _diedAt.Clear(); _selectedTarget = 0; _pendingNpc = null;
            State.Entities.Clear();
            State.MyId = 0;
            State.VaultOpen = false;
        }

        internal void Disconnect()
        {
            _conn?.Dispose(); _conn = null;
            _connecting = false;
            _replyHandlers.Clear();
            foreach (var v in _views.Values) if (v != null) Destroy(v.gameObject);
            _views.Clear();
            State.Entities.Clear();
            State.MyId = 0;
            State.MapId = MapDefinition.ValeId;
            State.VaultOpen = false;
        }

        public void Send(string cmd, Action<string>? onReply = null)
        {
            if (_conn == null) return;
            _replyHandlers.Enqueue(onReply);
            _conn.Send(cmd);
        }

        // ---------------------------------------------------------------- loop
        private void Update()
        {
            PumpHost();
            UpdatePointer();
            if (_cam != null && _cam.enabled) LocalLights.Upload(_iso != null && _iso.Target != null ? _iso.Target.position : _cam.transform.position + _cam.transform.forward * 30f);
            float dt = Time.deltaTime;
            State.LocalTime += dt;

            if (_reconnectAt >= 0 && Time.unscaledTime >= _reconnectAt)
            {
                StartGame(true);
                _loginError = $"Conexão perdida. Voltando para o servidor ({_reconnectAttempt}/{ReconnectDelays.Length})...";
            }

            if (_conn != null)
            {
                if (_connecting && _conn.Connected)
                {
                    _connecting = false;
                    OnConnected();
                }
                else if (_connecting && Time.time - _connectStarted > 6f)
                {
                    var why = _conn.ByeReason != "" ? _conn.ByeReason : _conn.Error;
                    _connecting = false; Disconnect();
                    if (!_resuming || !ScheduleReconnect()) _loginError = "Sem resposta do servidor: " + why;
                    return;
                }
                else if (!_connecting && !_conn.Connected)
                {
                    DrainIncoming();   // o que chegou antes de cair (aviso de expulsão, por exemplo)
                    var bye = _conn?.ByeReason ?? ""; var err = _conn?.Error ?? "";
                    Disconnect();
                    if (bye != "") { _resumeToken = ""; _resuming = false; _loginError = "Desconectado: " + bye; }
                    else if (!ScheduleReconnect()) _loginError = "Conexão perdida: " + err;
                    return;
                }

                _conn.Update(dt);
                if (!_connecting && Time.unscaledTime >= _nextPing)
                {   // "ainda estou aqui" (o servidor derruba conexões mudas) e mede a latência
                    _nextPing = Time.unscaledTime + 10f;
                    _pingSentAt = Time.unscaledTime;
                    Send("PING", _ => { if (_pingSentAt >= 0) PingMs = Mathf.RoundToInt((Time.unscaledTime - _pingSentAt) * 1000f); _pingSentAt = -1; });
                }
                DrainIncoming();
            }
            if (InWorld && State.Entities.TryGetValue(State.MyId, out var meNow) && meNow.Name != "") _resumeHero = meNow.Name;

            if (State.MapId != _builtMapId)
            {   // troca de mapa: mostra a tela de carregamento num quadro e monta o mapa no seguinte
                var ui = UI.UiRoot.Instance;
                if (ui != null && !_loadingShown) { _loadingShown = true; ui.ShowLoading(MapDefinition.Get(State.MapId)); }
                else { _loadingShown = false; LoadMap(State.MapId); ui?.LoadingDone(); }
            }
            if (InWorld)
            {
                ProcessEvents();
                SyncViews();
                HandleInput();
            }
            else
            {
                _iso.Target = null; // tela de entrada: a câmera orbita a vila
            }
        }

        private void DrainIncoming()
        {
            while (_conn != null && _conn.TryReceive(out var line))
            {
                if (line.StartsWith("@TOKEN ")) { _resumeToken = line.Substring(7).Trim(); _resumeAccount = _account.Trim(); continue; }
                State.Process(line);
                if (line.Length > 0 && line[0] != '@' && _replyHandlers.Count > 0) _replyHandlers.Dequeue()?.Invoke(line);
            }
        }

        /// <summary>Conectou: confere a versão do jogo e entra na conta (ou volta para ela depois de uma queda).</summary>
        private void OnConnected()
        {
            Send($"HELLO {Protocol.GameVersion}", r =>
            {
                if (!r.StartsWith("ERR versão")) return;   // servidor antigo sem HELLO responde outro ERR: segue o login
                _loginError = r.Substring(4); _resumeToken = ""; _resuming = false; _reconnectAt = -1;
                Disconnect();
            });
            if (!_resuming) { SendAccountLogin(); return; }
            Send($"RESUME {_resumeAccount} {_resumeToken}", r =>
            {
                _resuming = false;
                if (r.StartsWith("ERR"))
                {
                    _resumeToken = ""; _loginStage = LoginStage.Form;
                    _loginError = "A conexão caiu e não deu para voltar sozinho: " + r.Substring(4);
                    Disconnect();
                    return;
                }
                _reconnectAttempt = 0;
                ParseCharList(r);
                if (_resumeHero == "") return;   // estava na tela de personagens
                var hero = _resumeHero;
                Send($"PLAY {hero}", rep =>
                {
                    if (rep.StartsWith("ERR")) { _loginError = rep.Substring(4); return; }
                    _loginStage = LoginStage.Form;
                    State.AddLog("sys", "Conexão recuperada.");
                });
            });
        }

        /// <summary>Marca a próxima tentativa de voltar. False se não há como voltar (sem código) ou as tentativas acabaram.</summary>
        private bool ScheduleReconnect()
        {
            _resuming = false;
            if (_resumeToken == "" || _reconnectAttempt >= ReconnectDelays.Length) { _resumeToken = ""; _reconnectAttempt = 0; return false; }
            _reconnectAt = Time.unscaledTime + ReconnectDelays[_reconnectAttempt];
            _reconnectAttempt++;
            _loginError = $"Conexão perdida. Tentando voltar em {ReconnectDelays[_reconnectAttempt - 1]:0} s ({_reconnectAttempt}/{ReconnectDelays.Length})...";
            return true;
        }

        private Vector3 GroundPos(float x, float z) => new Vector3(x, WorldBuilder.Height(x, z), z);

        private void SyncViews()
        {
            foreach (var e in State.Entities.Values)
            {
                EntityView? old = null;
                if (_views.TryGetValue(e.Id, out var v))
                {
                    if (e.Kind == EntityKind.Player && v.Look != e.Look) { old = v; _views.Remove(e.Id); }   // equipamento mudou: remonta o visual
                    else { v.Target = GroundPos(e.X, e.Z); continue; }
                }
                if (_diedAt.TryGetValue(e.Id, out var t) && Time.time - t < 1.3f) continue;
                Rig rig = e.Kind switch
                {
                    EntityKind.Player => Models.Player(e.Name, (CharacterClass)e.TypeId, e.Appearance),
                    EntityKind.Monster => Models.Monster(e.TypeId, MonsterCatalog.Get(e.TypeId)?.Scale ?? 1f),
                    EntityKind.Banner => Models.Banner(e.TypeId, e.Radius),
                    _ => Models.Drop(ItemDatabase.Get(e.TypeId) ?? ItemDatabase.HealthPotion, e.Rarity),
                };
                var nv = EntityView.Create(rig, e.Id, e.Kind, GroundPos(e.X, e.Z));
                if (e.Kind == EntityKind.Player) nv.transform.rotation = Quaternion.Euler(0, e.Facing, 0);   // nasce virado como saiu
                nv.TypeId = e.TypeId; nv.WeaponId = e.WeaponId; nv.Look = e.Look;
                if (old != null)
                {   // mesma pessoa, roupa nova: herda posição interpolada, direção e animação (não pula nem gira)
                    nv.InheritFrom(old);
                    nv.Target = GroundPos(e.X, e.Z);
                    Destroy(old.gameObject);
                }
                _views[e.Id] = nv;
                if (e.Id == State.MyId)
                {   // a câmera segue o mesmo herói; só "encaixa" o foco quando a vista nasce de novo (entrada, mapa)
                    _iso.Target = nv.transform;
                    if (old == null) _iso.Snap();
                }
            }
            List<int>? gone = null;
            foreach (var kv in _views) if (!State.Entities.ContainsKey(kv.Key)) (gone ??= new List<int>()).Add(kv.Key);
            if (gone != null) foreach (var id in gone) { if (_views[id] != null && !_views[id].Dying) Destroy(_views[id].gameObject); _views.Remove(id); }
            if (_iso.Target == null && _views.TryGetValue(State.MyId, out var me)) _iso.Target = me.transform;
        }

        private static System.Collections.IEnumerator Later(float delay, Action act) { yield return new WaitForSeconds(delay); act(); }

        /// <summary>Cor das marcas de garra conforme a família do monstro.</summary>
        private static Color ClawColor(int defId) => CombatAudio.VoiceOf(defId) switch
        {
            MonsterVoice.Fire => KezorLook.Hex("#ff7a2a"),
            MonsterVoice.Crystal => KezorLook.Hex("#9fe6ff"),
            MonsterVoice.Spirit => KezorLook.Hex("#c59bff"),
            MonsterVoice.Slime => KezorLook.Hex("#9be36a"),
            MonsterVoice.Undead => KezorLook.Hex("#d8e0c8"),
            _ => KezorLook.Hex("#ff5040"),
        };

        private EntityView? View(int id) => _views.TryGetValue(id, out var v) ? v : null;
        private Vector3 TopOf(EntityView v) => v.transform.position + Vector3.up * v.Height;

        private void ProcessEvents()
        {
            CombatAudio.Focus = _iso != null ? _iso.Target : null;
            while (State.Events.Count > 0)
            {
                var ev = State.Events.Dequeue();
                try
                {
                    switch (ev.Type)
                    {
                        case "HIT":
                        {
                            var a = View(ev.I(0)); var t = View(ev.I(1));
                            var type = (HitType)ev.I(2); int dmg = ev.I(3); int skillId = ev.I(4);
                            CharacterClass? atkClass = null;
                            if (a != null && a.Kind == EntityKind.Player && State.Entities.TryGetValue(a.Id, out var ac)) atkClass = (CharacterClass)ac.TypeId;
                            float impactDelay = CombatAudio.ImpactDelay(atkClass, type);
                            if (a != null && t != null)
                            {
                                if (type != HitType.Combo) a.PlayAttack(t.transform.position);
                                if (atkClass is CharacterClass hc && type != HitType.Combo) _sfx.HeroSwing(hc, skillId, a.transform.position);
                                if (a.Kind == EntityKind.Monster)
                                {   // bote do monstro: grito de ataque já, garra e impacto quando a mordida encosta
                                    impactDelay = 0.12f;
                                    _sfx.Monster(a.Id, a.TypeId, MonsterSfx.Attack, a.transform.position);
                                    if (type != HitType.Miss) StartCoroutine(Later(impactDelay, () =>
                                    {
                                        if (t == null || a == null) return;
                                        var def = MonsterCatalog.Get(a.TypeId);
                                        _fx.ClawSlash(t.transform.position + Vector3.up * t.Height * 0.55f, a.transform.position, ClawColor(a.TypeId), Mathf.Clamp(def?.Scale ?? 1f, 0.8f, 2.4f));
                                    }));
                                }
                                if (a.Kind == EntityKind.Player && State.Entities.TryGetValue(a.Id, out var ae))
                                {
                                    var cls = (CharacterClass)ae.TypeId;
                                    if (cls == CharacterClass.Ranger && type != HitType.Combo) _fx.Projectile(a.transform.position + Vector3.up * 1.3f, t.transform.position + Vector3.up * t.Height * 0.6f, true, KezorLook.Hex("#e8d9a8"));
                                    if (cls == CharacterClass.Invoker && type != HitType.Combo) _fx.Projectile(a.transform.position + Vector3.up * 1.4f, t.transform.position + Vector3.up * t.Height * 0.6f, false, KezorLook.Hex("#8fe8ff"));
                                    if (cls == CharacterClass.Bladeweaver && skillId != 0) _fx.AreaRing(t.transform.position, 0.9f, KezorLook.Hex("#8fd0ff"));
                                    if (cls == CharacterClass.EmberFist && type == HitType.Combo) { _fx.AreaRing(t.transform.position, 1.4f, KezorLook.Hex("#ff6a1a")); _fx.Text(TopOf(t) + Vector3.up * 0.4f, "FLUXO!", KezorLook.Hex("#ff8a3a"), 1.3f); }
                                    if (cls == CharacterClass.Arcanist) _fx.Projectile(a.transform.position + Vector3.up * 1.6f, t.transform.position + Vector3.up * t.Height * 0.6f, false, skillId == 4 ? KezorLook.Hex("#ff8a3a") : KezorLook.Hex("#a98bff"));
                                }
                            }
                            if (t != null)
                            {
                                MonsterVoice? tv = t.Kind == EntityKind.Monster ? CombatAudio.VoiceOf(t.TypeId) : (MonsterVoice?)null;
                                _sfx.Impact(t.transform.position, type, atkClass, tv, ev.I(1) == State.MyId, impactDelay);
                                if (t.Kind == EntityKind.Monster && type != HitType.Miss) _sfx.Monster(t.Id, t.TypeId, MonsterSfx.Hurt, t.transform.position);
                                if (type != HitType.Miss) t.PlayHit();
                                _fx.Damage(TopOf(t), type, dmg, ev.I(1) == State.MyId);
                            }
                            break;
                        }
                        case "FX":
                        {
                            var sk = SkillCatalog.Find(ev.I(1));
                            if (sk == null) break;
                            var col = sk.Magic ? KezorLook.Hex("#ff7a2a") : (sk.Classes == ClassMask.Ranger ? KezorLook.Hex("#e8d9a8") : KezorLook.Hex("#7fb2ff"));
                            _fx.AreaRing(GroundPos(ev.Fl(2), ev.Fl(3)), sk.AreaRadius, col);
                            _sfx.Play("area", GroundPos(ev.Fl(2), ev.Fl(3)), 0.7f);
                            break;
                        }
                        case "CAST":
                        {   // skill sem alvo: buff, estandarte ou invocação
                            var sk = SkillCatalog.Find(ev.I(1));
                            var v = View(ev.I(0));
                            if (sk == null) break;
                            var pos = GroundPos(ev.Fl(2), ev.Fl(3));
                            Color col = sk.Kind switch
                            {
                                SkillKind.Banner => Models.BannerColor((int)sk.Buff),
                                SkillKind.Summon => KezorLook.Hex("#b98bff"),
                                _ => sk.Classes == ClassMask.EmberFist ? KezorLook.Hex("#ff6a1a") : KezorLook.Hex("#8fd0ff"),
                            };
                            _fx.AreaRing(pos, sk.Kind == SkillKind.Banner ? sk.AuraRadius : 1.6f, col);
                            _sfx.Play(sk.Kind == SkillKind.Banner ? "cast_banner" : sk.Kind == SkillKind.Summon ? "spell_summon" : "cast_buff", pos, 0.65f);
                            if (v != null) { v.PlayAttack(v.transform.position + v.transform.forward); _fx.Text(TopOf(v) + Vector3.up * 0.3f, sk.Name, col, 1.0f); }
                            break;
                        }
                        case "DIE":
                        {
                            int id = ev.I(0);
                            _diedAt[id] = Time.time;
                            if (_views.TryGetValue(id, out var v))
                            {
                                if (v.Kind == EntityKind.Monster) _sfx.Monster(v.Id, v.TypeId, MonsterSfx.Death, v.transform.position);
                                else if (v.Kind == EntityKind.Player) _sfx.Play("death_me", v.transform.position, id == State.MyId ? 0.9f : 0.6f);
                                v.PlayDeath(); _views.Remove(id);
                            }
                            if (_selectedTarget == id) _selectedTarget = 0;
                            break;
                        }
                        case "LVL":
                        {
                            var v = View(ev.I(0));
                            if (v != null) { _fx.LevelUp(v.transform.position); _fx.Text(TopOf(v) + Vector3.up * 0.5f, $"NÍVEL {ev.I(1)}!", new Color(1f, 0.85f, 0.3f), 1.6f); _sfx.Play("levelup", v.transform.position, 0.7f); }
                            break;
                        }
                        case "AGGRO":
                        {   // monstro viu alguém e partiu para cima: grito de alerta e um "!" se o alvo sou eu
                            var m = View(ev.I(0));
                            if (m == null || m.Kind != EntityKind.Monster) break;
                            _sfx.Monster(m.Id, m.TypeId, MonsterSfx.Aggro, m.transform.position);
                            if (ev.I(1) == State.MyId) _fx.Text(TopOf(m) + Vector3.up * 0.3f, "!", new Color(1f, 0.3f, 0.2f), 1.8f);
                            break;
                        }
                        case "EXP": { var v = View(State.MyId); if (v != null) _fx.Text(TopOf(v), $"+{ev.F[0]} exp", new Color(0.95f, 0.9f, 0.5f), 0.9f); break; }
                        case "GOLD": { var v = View(State.MyId); if (v != null) _fx.Text(TopOf(v) + Vector3.up * 0.3f, $"+{ev.F[0]} ouro", new Color(1f, 0.8f, 0.2f), 0.9f); break; }
                    }
                }
                catch (Exception e) { Debug.LogWarning("[Kezor] evento inválido " + ev.Type + ": " + e.Message); }
            }
        }

        // ---------------------------------------------------------------- entrada
        private bool TryGround(Vector3 mouse, out Vector3 hit)
        {
            var ray = _cam.ScreenPointToRay(mouse);
            hit = Vector3.zero;
            // camada "Ground" (terreno e lajes com MeshCollider): raio direto; sem ela, marcha pela função de altura
            if (WorldBuilder.GroundMask != 0 && Physics.Raycast(ray, out var rh, 400f, WorldBuilder.GroundMask, QueryTriggerInteraction.Ignore))
            {
                hit = rh.point;
                return true;
            }
            float prev = 0;
            for (float t = 1f; t < 250f; t += 0.75f)
            {
                var p = ray.GetPoint(t);
                if (p.y <= WorldBuilder.Height(p.x, p.z))
                {
                    float lo = prev, hi = t;
                    for (int i = 0; i < 8; i++)
                    {
                        float mid = (lo + hi) * 0.5f; var q = ray.GetPoint(mid);
                        if (q.y <= WorldBuilder.Height(q.x, q.z)) hi = mid; else lo = mid;
                    }
                    hit = ray.GetPoint(hi);
                    return true;
                }
                prev = t;
            }
            return false;
        }

        private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a; float l = ab.sqrMagnitude;
            float t = l < 0.0001f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l);
            return Vector2.Distance(p, a + ab * t);
        }

        private void UpdateHover()
        {
            _hoverId = 0; _hoverNpc = null;
            Vector2 m = InputK.MousePosition;
            float best = 30f;
            foreach (var kv in _views)
            {
                var v = kv.Value;
                if (v == null || v.Dying || kv.Key == State.MyId || v.Kind == EntityKind.Banner) continue;
                if (State.Entities.TryGetValue(kv.Key, out var ve) && ve.OwnerId != 0) continue;   // ecos não são alvo
                var a = _cam.WorldToScreenPoint(v.transform.position);
                var b = _cam.WorldToScreenPoint(v.transform.position + Vector3.up * Mathf.Max(0.6f, v.Height));
                if (a.z < 0) continue;
                float d = SegDist(m, a, b) - (v.Kind == EntityKind.Drop ? 0f : 6f);
                if (d < best) { best = d; _hoverId = kv.Key; _hoverKind = v.Kind; }
            }
            foreach (var (def, view) in _npcs)
            {
                var a = _cam.WorldToScreenPoint(view.transform.position);
                var b = _cam.WorldToScreenPoint(view.transform.position + Vector3.up * 2f);
                if (a.z < 0) continue;
                float d = SegDist(m, a, b);
                if (d < best) { best = d; _hoverId = 0; _hoverNpc = def; }
            }
        }

        private void HandleInput()
        {
            UpdateHover();
            if (_chatOpen) return;

            var me = State.Me;
            if (_pendingNpc != null && MathK.Dist(me.X, me.Z, _pendingNpc.X, _pendingNpc.Z) < 4f) { OpenNpc(_pendingNpc); _pendingNpc = null; }

            // clique na interface não vaza para o mundo (nem no quadro seguinte)
            if (UI.UiRoot.Instance != null && Time.frameCount - UI.UiRoot.Instance.LastUiPointerFrame <= 1) { _holdMove = false; return; }
            var hand = UI.UiRoot.Instance?.Drag;
            if (hand != null && hand.Holding)
            {   // item no cursor: cliques não andam nem atacam; clique no chão larga, clique direito/Esc devolve
                _holdMove = false;
                if (!PointerOverUi && InputK.MouseDown(0)) hand.DropOnGround();
                if (InputK.MouseDown(1) && !PointerOverUi) hand.Cancel();
                if (InputK.KeyDown(KeyCode.Escape)) hand.Cancel();
                return;
            }
            if (!PointerOverUi)
            {
                if (InputK.MouseDown(0))
                {
                    _pendingNpc = null;
                    if (_hoverNpc != null)
                    {
                        if (MathK.Dist(me.X, me.Z, _hoverNpc.X, _hoverNpc.Z) < 4f) OpenNpc(_hoverNpc);
                        else
                        {   // anda até um ponto ao lado do NPC, do lado da praça (onde não há balcão, forja nem parede)
                            _pendingNpc = _hoverNpc;
                            var dir = new Vector2(_map.SafeX - _hoverNpc.X, _map.SafeZ - _hoverNpc.Z);
                            if (_map.SafeRadius <= 0 || dir.sqrMagnitude < 0.01f) dir = new Vector2(0, -1);
                            dir.Normalize();
                            Send($"MOVE {Protocol.F(_hoverNpc.X + dir.x * 3.2f)} {Protocol.F(_hoverNpc.Z + dir.y * 3.2f)}");
                        }
                    }
                    else if (_hoverId != 0 && _hoverKind == EntityKind.Drop) Send($"PICK {_hoverId}");
                    else if (_hoverId != 0 && TryOpenShopOf(_hoverId)) { }
                    else if (_hoverId != 0) { _selectedTarget = _hoverId; Send($"ATTACK {_hoverId}"); }
                    else _holdMove = true;
                }
                if (InputK.MouseDown(1))
                {
                    int target = _hoverId != 0 && _hoverKind != EntityKind.Drop ? _hoverId : _selectedTarget;
                    var skills = ClassSkills();
                    if (skills.Count > 0)
                    {
                        var sk = skills[Mathf.Clamp(_skillSlot, 0, skills.Count - 1)];
                        if (!sk.NeedsTarget) Send($"SKILL {sk.Id} 0");          // buff, estandarte ou invocação
                        else if (target != 0) { _selectedTarget = target; Send($"SKILL {sk.Id} {target}"); }
                    }
                }
            }
            if (InputK.MouseUp(0)) _holdMove = false;
            if (_holdMove && Time.time >= _nextMoveSend && TryGround(InputK.MousePosition, out var g))
            {
                if (Vector3.Distance(g, _lastMoveSent) > 0.3f || Time.time - _nextMoveSend > 0.5f)
                {
                    Send($"MOVE {Protocol.F(g.x)} {Protocol.F(g.z)}");
                    _lastMoveSent = g;
                }
                _nextMoveSend = Time.time + 0.1f;   // segurando o botão: novo destino a cada 0,1 s (o servidor refaz o A*)
            }

            if (GUIUtility.keyboardControl != 0) return; // digitando em algum campo
            for (int i = 0; i < 6; i++) if (InputK.KeyDown(KeyCode.Alpha1 + i) && i < ClassSkills().Count) _skillSlot = i;
            if (InputK.KeyDown(KeyCode.Q)) UseQuick(0);      // atalhos de consumível Q W E R (HUD v4)
            if (InputK.KeyDown(KeyCode.W)) UseQuick(1);
            if (InputK.KeyDown(KeyCode.E)) UseQuick(2);
            if (InputK.KeyDown(KeyCode.R)) UseQuick(3);
            if (InputK.KeyDown(KeyCode.Space)) PickNearest();
            if (InputK.KeyDown(KeyCode.C)) _showChar = !_showChar;
            if (InputK.KeyDown(KeyCode.I) || InputK.KeyDown(KeyCode.V)) _showInv = !_showInv;
            if (InputK.KeyDown(KeyCode.P)) _showParty = !_showParty;
            if (InputK.KeyDown(KeyCode.H) || InputK.KeyDown(KeyCode.F1)) _showHelp = !_showHelp;
            if (InputK.KeyDown(KeyCode.M)) _showWorldMap = !_showWorldMap;
            if (InputK.KeyDown(KeyCode.O)) _showOptions = !_showOptions;
            if (InputK.KeyDown(KeyCode.Home)) _iso.ResetView();   // única forma de voltar a câmera ao padrão
            if (InputK.KeyDown(KeyCode.A)) Toggle(UiPanel.Ascension);   // Árvore da Ascensão
            if (InputK.KeyDown(KeyCode.X)) Send("MOUNT"); // monta/desmonta (o servidor valida); R virou atalho de poção
            TryTradeHotkey();
            if (InputK.KeyDown(KeyCode.G)) { _showGuild = !_showGuild; if (_showGuild) Send("GUILD INFO"); }
            if (InputK.KeyDown(KeyCode.K)) { _showRanking = !_showRanking; if (_showRanking) OpenRanking(_rankTab); }
            if (InputK.KeyDown(KeyCode.B)) { _showMyShop = !_showMyShop; if (_showMyShop) { _showInv = true; Send("SHOP MINE"); } }
            if (InputK.KeyDown(KeyCode.Escape)) CloseAll();
        }

        public List<SkillDefinition> ClassSkills() =>
            SkillCatalog.All.Where(s => s.Id != 0 && (s.Classes & ClassDefinition.MaskOf(State.Me.Class)) != 0).ToList();

        internal void UsePotion(PotionType t)
        {
            for (int i = 0; i < State.Inventory.Length; i++)
                if (State.Inventory[i]?.Def.Potion == t) { Send($"USE {i}"); return; }
            State.AddLog("err", t == PotionType.Health ? "Sem Poções Rubras (compre com a Mercadora Tália)." : "Sem Poções Cerúleas.");
        }

        private void PickNearest()
        {
            EntityState? best = null; float bd = 8f;
            foreach (var e in State.Entities.Values)
            {
                if (e.Kind != EntityKind.Drop) continue;
                float d = MathK.Dist(e.X, e.Z, State.Me.X, State.Me.Z);
                if (d < bd) { bd = d; best = e; }
            }
            if (best != null) Send($"PICK {best.Id}");
        }

        internal void OpenNpc(NpcDefinition n)
        {
            switch (n.Role)
            {
                case "shop": _showShop = true; _showInv = true; break;
                case "forge": _showForge = true; _showInv = true; _forgeSel.Clear(); _forgePreview = "Selecione os itens no inventário."; break;
                case "vault": _showInv = true; Send("VAULT"); break;
                case "guild": _showGuild = true; Send("GUILD INFO"); break;
                default: State.AddLog("say", $"{n.Name}: {(n.Greeting != "" ? n.Greeting : "As cinzas rondam o portão sul. Fique atento aos avisos de invasão!")}"); break;
            }
        }

        internal void CloseAll()
        {
            _showChar = _showInv = _showShop = _showForge = _showParty = _showHelp = _showGuild = _showMyShop = _showRanking = _showWorldMap = _showOptions = _showAscension = false;
            if (State.TradeOpen) Send("TRADE CANCEL");
            State.ShopVisible = false;
            if (State.VaultOpen) { Send("VCLOSE"); State.VaultOpen = false; }
            _runeMode = -1; _selSlot = -1;
        }
    }
}
