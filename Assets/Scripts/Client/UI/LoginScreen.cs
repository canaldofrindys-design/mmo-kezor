using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Kezor.Core.Net;
using Kezor.Core.Server;

namespace Kezor.Client.UI
{
    /// <summary>
    /// Login com lista de servidores: fundo 3D cinematográfico (a câmera passeia pela vila à noite), logotipo e um painel
    /// pequeno com o "Servidor deste PC" e os servidores do servers.json (canais, status por ping TCP com o comando
    /// STATUS, população e latência). Só multiplayer: servidor → conta e senha → seleção em fileira → mundo com os outros
    /// jogadores. Lembra o último servidor, canal e conta (nunca a senha). Menu com Opções, Créditos e Sair.
    /// </summary>
    public sealed class LoginScreen
    {
        public const string PrefServer = "kz.lastServer", PrefChannel = "kz.lastChannel", PrefAccount = "kz.lastAccount", PrefHostPort = "kz.hostPort",
                            PrefDirectHost = "kz.directHost", PrefDirectPort = "kz.directPort";
        private const float StatusEvery = 15f;

        private readonly UiRoot _ui;
        private KezorGame G => _ui.Game;
        public readonly VisualElement Root;
        private readonly VisualElement _serverPanel, _list, _channels, _lanRow, _accountPanel, _menu, _credits;
        private readonly Label _serverError, _accError, _accTitle, _accSub, _hostHint;
        private readonly TextField _lanPort, _account, _password, _ipHost, _ipPort;
        private readonly VisualElement _ipRow;
        private readonly Toggle _register;
        private readonly Button _continue, _login;
        private List<ServerEntry> _servers = new List<ServerEntry>();
        private int _sel, _chan;
        private bool _accountStep;
        private readonly List<(VisualElement row, VisualElement dot, Label pop, Label ping)> _rows = new List<(VisualElement, VisualElement, Label, Label)>();
        private readonly List<Button> _chanBtns = new List<Button>();
        private string _listError = "";

        private sealed class Probe { public ServerStatus? Status; public float At = -999; public Task<ServerStatus>? Task; }
        private readonly Dictionary<string, Probe> _probes = new Dictionary<string, Probe>();

        public LoginScreen(UiRoot ui)
        {
            _ui = ui;
            Root = Kz.El("login", ui.Root, pick: true);
            Kz.El("entry__vignette", Root);
            Kz.El("login__fade", Root);
            var logo = Kz.El("login__logo", Root);
            Kz.Text("KEZOR", "kz-logo", logo);
            Kz.Text("Crônicas de Brasalume", "entry__tagline", logo);

            // ---------------- servidores
            _serverPanel = Kz.El("kz-panel login__panel", Root, pick: true);
            var head = Kz.El("login__head", _serverPanel, pick: true);
            Kz.Text("Escolha o servidor", "kz-h2", head);
            Kz.Btn("Atualizar", () => RefreshAll(true), "kz-btn kz-btn--small", head);
            _list = Kz.El("login__list", _serverPanel, pick: true);
            _channels = Kz.El("login__channels", _serverPanel, pick: true);
            _lanRow = Kz.El("win-row login__lan", _serverPanel, pick: true);
            Kz.Text("Porta", "kz-text-2", _lanRow);
            _lanPort = Field(_lanRow, 6, Protocol.DefaultPort.ToString()); _lanPort.style.width = 90;
            _ipRow = Kz.El("win-row login__lan", _serverPanel, pick: true);
            Kz.Text("IP", "kz-text-2", _ipRow);
            _ipHost = Field(_ipRow, 64, "192.168.0.10"); _ipHost.style.width = 170;
            _ipPort = Field(_ipRow, 6, Protocol.DefaultPort.ToString()); _ipPort.style.width = 80;
            _hostHint = Kz.Text("", "kz-text-3", _serverPanel);
            _serverError = Kz.Text("", "entry__error", _serverPanel);
            _continue = Kz.Btn("Continuar", Continue, "kz-btn kz-btn--primary entry__enter", _serverPanel);

            // ---------------- conta
            _accountPanel = Kz.El("kz-panel login__panel login__panel--account", Root, pick: true);
            _accTitle = Kz.Text("", "kz-h2", _accountPanel);
            _accSub = Kz.Text("", "kz-text-3", _accountPanel);
            Kz.Text("Conta", "entry__label", _accountPanel);
            _account = Field(_accountPanel, 16, "conta");
            Kz.Text("Senha", "entry__label", _accountPanel);
            _password = Field(_accountPanel, 64, "senha");
            _password.isPasswordField = true; _password.maskChar = '•';
            _register = new Toggle("Criar conta nova (senha de 6+ caracteres)"); _register.AddToClassList("kz-toggle");
            _accountPanel.Add(_register);
            Kz.Text("A senha só vai para o servidor, que guarda apenas o hash. O jogo lembra a conta, nunca a senha.", "kz-text-3", _accountPanel);
            _accError = Kz.Text("", "entry__error", _accountPanel);
            var accBtns = Kz.El("win-row", _accountPanel, pick: true);
            Kz.Btn("Voltar", () => { _accountStep = false; G._loginError = ""; }, "kz-btn", accBtns);
            _login = Kz.Btn("Entrar", Login, "kz-btn kz-btn--primary login__grow", accBtns);

            // ---------------- menu e créditos
            Kz.Btn("Menu", () => _menu.Show(_menu.style.display == DisplayStyle.None), "kz-btn login__menu-btn", Root);
            _menu = Kz.El("kz-panel login__menu", Root, pick: true);
            Kz.Btn("Opções", () => { G.Toggle(KezorGame.UiPanel.Options); _menu.Show(false); }, "kz-btn", _menu);
            Kz.Btn("Créditos", () => { _credits.Show(true); _menu.Show(false); }, "kz-btn", _menu);
            Kz.Btn("Sair do jogo", Quit, "kz-btn", _menu);
            _menu.Show(false);
            _credits = Kz.El("kz-panel login__credits", Root, pick: true);
            Kz.Text("Créditos", "kz-title", _credits);
            Kz.Text(CreditsText(), "kz-text login__credits-text", _credits);
            Kz.Btn("Fechar", () => _credits.Show(false), "kz-btn", _credits);
            _credits.Show(false);
            Kz.Text($"Kezor {Protocol.GameVersion}  ·  fantasia sombria low poly  ·  servidor autoritativo", "entry__footer", Root);

            _account.value = PlayerPrefs.GetString(PrefAccount, "");
            _lanPort.value = PlayerPrefs.GetString(PrefHostPort, Protocol.DefaultPort.ToString());
            _ipHost.value = PlayerPrefs.GetString(PrefDirectHost, "");
            _ipPort.value = PlayerPrefs.GetString(PrefDirectPort, Protocol.DefaultPort.ToString());
            LoadServers();
            Root.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter) return;
                if (G._connecting) return;
                if (_accountStep) Login(); else Continue();
            }, TrickleDown.TrickleDown);
        }

        private static string CreditsText() =>
            "<b>Kezor</b> — jogo original: código, regras, modelos 3D (Blender), ícones e interface feitos para o projeto.\n\n" +
            "<b>Fontes</b>\n" +
            "· Cinzel — Natanael Gama. SIL Open Font License 1.1 (Resources/Fonts/Cinzel_OFL.txt).\n" +
            "· Inter — Rasmus Andersson. SIL Open Font License 1.1 (Resources/Fonts/Inter_OFL.txt).\n\n" +
            "<b>Tecnologia</b>\n" +
            "· Unity 6 (UI Toolkit, Input System). · .NET (servidor dedicado sem Unity).\n" +
            "· Blender (modelos, assados de oclusão e cor por vértice).\n\n" +
            "As fontes OFL podem ser usadas, estudadas, modificadas e redistribuídas com o jogo; não podem ser vendidas sozinhas.";

        private static TextField Field(VisualElement parent, int max, string placeholder)
        {
            var f = new TextField { maxLength = max };
            f.AddToClassList("kz-field");
            f.textEdition.placeholder = placeholder;
            f.RegisterCallback<FocusInEvent>(_ => KezorGame.Instance!.ChatFocused = true);
            f.RegisterCallback<FocusOutEvent>(_ => KezorGame.Instance!.ChatFocused = false);
            parent.Add(f);
            return f;
        }

        // ------------------------------------------------------------------ lista
        /// <summary>servers.json: primeiro o do usuário (persistentDataPath/kezor), senão o que vem com o jogo (StreamingAssets).</summary>
        public static string ServersPath()
        {
            var user = Path.Combine(Application.persistentDataPath, "kezor", "servers.json");
            return File.Exists(user) ? user : Path.Combine(Application.streamingAssetsPath, "servers.json");
        }

        private void LoadServers()
        {
            var remote = ServerList.Load(ServersPath(), out _listError);
            int.TryParse(G._port, out var lanPort);
            _servers = ServerList.WithBuiltins(remote, lanPort > 0 ? lanPort : Protocol.DefaultPort);
            _list.Clear(); _rows.Clear();
            for (int i = 0; i < _servers.Count; i++)
            {
                int idx = i;
                var s = _servers[i];
                var row = Kz.El("login__row", _list, pick: true);
                var dot = Kz.El("login__dot", row);
                var col = Kz.El("login__row-text", row);
                Kz.Text(s.Name, "login__name", col);
                Kz.Text(s.Kind == ServerKind.Remote ? $"{s.Region}{(s.Region != "" ? " · " : "")}{s.Host}" : s.Region, "login__sub", col);
                var pop = Kz.Text("", "login__pop", row);
                var ping = Kz.Text("", "login__ping", row);
                row.RegisterCallback<ClickEvent>(e => { Select(idx); if (e.clickCount >= 2) Continue(); });
                _rows.Add((row, dot, pop, ping));
            }
            var last = PlayerPrefs.GetString(PrefServer, ServerList.HostId);
            _sel = Mathf.Max(0, _servers.FindIndex(x => x.Id == last));
            _chan = Mathf.Clamp(PlayerPrefs.GetInt(PrefChannel, 0), 0, _servers[_sel].Channels.Count - 1);
            BuildChannels();
            RefreshAll(false);
        }

        private void Select(int i)
        {
            if (i == _sel) return;
            _sel = i; _chan = 0; G._loginError = "";
            BuildChannels();
        }

        private void BuildChannels()
        {
            _channels.Clear(); _chanBtns.Clear();
            var s = _servers[_sel];
            if (s.Kind != ServerKind.Remote) return;
            Kz.Text("Canal", "kz-text-2 login__chan-label", _channels);
            for (int i = 0; i < s.Channels.Count; i++)
            {
                int c = i;
                _chanBtns.Add(Kz.Btn(s.Channels[i].Name, () => _chan = c, "kz-btn kz-btn--small login__chan", _channels));
            }
        }

        private string Key(ServerEntry s, ChannelEntry c) => s.Host + ":" + (s.Kind == ServerKind.Host ? HostPortValue() : c.Port);

        private int DirectPortValue() => int.TryParse(_ipPort.value.Trim(), out var p) && p > 0 && p < 65536 ? p : -1;

        private int HostPortValue() => int.TryParse(_lanPort.value.Trim(), out var p) && p > 0 && p < 65536 ? p : Protocol.DefaultPort;

        private void RefreshAll(bool force)
        {
            foreach (var s in _servers)
            {
                if (s.Kind == ServerKind.Host && G.Hosting) continue;   // o nosso: os números vêm direto do servidor
                if (s.Kind == ServerKind.Direct) continue;
                foreach (var c in s.Channels)
                {
                    var key = Key(s, c);
                    if (!_probes.TryGetValue(key, out var p)) _probes[key] = p = new Probe();
                    if (p.Task != null && !p.Task.IsCompleted) continue;
                    if (!force && Time.unscaledTime - p.At < StatusEvery) continue;
                    string host = s.Host; int port = s.Kind == ServerKind.Host ? HostPortValue() : c.Port;
                    p.Task = Task.Run(() => ServerStatusProbe.Probe(host, port, 1500));
                    p.At = Time.unscaledTime;
                }
            }
        }

        /// <summary>Melhor status entre os canais (para a linha) ou o do canal escolhido.</summary>
        private ServerStatus? StatusOf(ServerEntry s, int channel = -1)
        {
            ServerStatus? best = null;
            for (int i = 0; i < s.Channels.Count; i++)
            {
                if (channel >= 0 && i != channel) continue;
                if (!_probes.TryGetValue(Key(s, s.Channels[i]), out var p) || p.Status == null) continue;
                if (best == null || (p.Status.Online && (!best.Online || p.Status.LatencyMs < best.LatencyMs))) best = p.Status;
            }
            return best;
        }

        private bool Probing(ServerEntry s)
        {
            foreach (var c in s.Channels) if (_probes.TryGetValue(Key(s, c), out var p) && p.Task != null && !p.Task.IsCompleted) return true;
            return false;
        }

        // ------------------------------------------------------------------ ações
        private void Continue()
        {
            var s = _servers[_sel];
            G._loginError = "";
            PlayerPrefs.SetString(PrefServer, s.Id); PlayerPrefs.SetInt(PrefChannel, _chan); PlayerPrefs.Save();
            if (s.Kind == ServerKind.Host)
            {   // abre o servidor aqui (ou usa o que já está aberto nesta porta) e entra nele com conta, como os outros
                int port = HostPortValue();
                if (!G.EnsureHost(port, out _, out var err)) { G._loginError = err; return; }
                G._port = port.ToString();
                PlayerPrefs.SetString(PrefHostPort, port.ToString());
            }
            else if (s.Kind == ServerKind.Direct)
            {
                var ip = _ipHost.value.Trim();
                if (ip.Length == 0 || ip.Contains(" ")) { G._loginError = "Digite o IP do servidor (ex.: 192.168.0.10)."; return; }
                if (DirectPortValue() < 0) { G._loginError = "Porta inválida."; return; }
                PlayerPrefs.SetString(PrefDirectHost, ip); PlayerPrefs.SetString(PrefDirectPort, DirectPortValue().ToString());
            }
            else
            {
                var st = StatusOf(s, _chan);
                if (st != null && st.Online && st.Load == "lotado") { G._loginError = "Este canal está lotado. Escolha outro."; return; }
            }
            _accountStep = true;
            _password.value = "";
            _password.schedule.Execute(() => (_account.value == "" ? _account : _password).Focus()).StartingIn(30);
        }

        private void Login()
        {
            var s = _servers[_sel];
            var c = s.Channels[Mathf.Clamp(_chan, 0, s.Channels.Count - 1)];
            if (_account.value.Trim().Length < 3) { G._loginError = "Digite a conta."; return; }
            if (_password.value.Length == 0) { G._loginError = "Digite a senha."; return; }
            PlayerPrefs.SetString(PrefAccount, _account.value.Trim()); PlayerPrefs.Save();   // a senha nunca é salva
            G._host = s.Kind == ServerKind.Direct ? _ipHost.value.Trim() : s.Host;
            G._port = (s.Kind == ServerKind.Host ? HostPortValue() : s.Kind == ServerKind.Direct ? DirectPortValue() : c.Port).ToString();
            G._serverLabel = s.Kind == ServerKind.Host ? "Servidor deste PC" : s.Kind == ServerKind.Direct ? $"{G._host}:{G._port}" : $"{s.Name} · {c.Name}";
            G._account = _account.value.Trim(); G._password = _password.value; G._registering = _register.value;
            _password.value = "";
            G.StartGame();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private string? _ips;
        /// <summary>Endereços deste PC na rede (para os amigos digitarem no servers.json ou no "Conectar").</summary>
        private string HostHint()
        {
            if (_ips == null)
            {
                try
                {
                    var list = new List<string>();
                    foreach (var a in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()))
                        if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(a)) list.Add(a.ToString());
                    _ips = list.Count > 0 ? string.Join(", ", list) : "";
                }
                catch { _ips = ""; }
            }
            var who = _ips != "" ? $"Amigos na sua rede entram pelo IP <b>{_ips}</b> e porta {HostPortValue()}." : "Amigos na sua rede entram pelo seu IP e por esta porta.";
            return "Todo mundo entra com conta e senha, inclusive você. Na primeira vez marque \"Criar conta nova\". " + who +
                   " Para testar sozinho com 2 heróis, abra uma segunda janela do jogo e entre aqui também.";
        }

        // ------------------------------------------------------------------ quadro
        public void Tick()
        {
            bool connected = G.Connected && !G._connecting;
            bool show = !G.InWorld && !connected;
            Root.Show(show);
            if (!show) return;
            if (Root.style.display == DisplayStyle.Flex) RefreshAll(false);
            foreach (var p in _probes.Values)
                if (p.Task != null && p.Task.IsCompleted && p.Status != p.Task.Result) p.Status = p.Task.Result;

            _serverPanel.Show(!_accountStep);
            _accountPanel.Show(_accountStep);
            var sel = _servers[_sel];
            for (int i = 0; i < _rows.Count; i++)
            {
                var (row, dot, pop, ping) = _rows[i];
                var s = _servers[i];
                row.SetClass("login__row--on", i == _sel);
                if (s.Kind == ServerKind.Host && G.Hosting)
                {
                    dot.SetClass("login__dot--on", true); dot.SetClass("login__dot--off", false); dot.SetClass("login__dot--busy", false);
                    pop.Set($"{G.HostPlayers} online"); ping.Set("aberto");
                    continue;
                }
                if (s.Kind == ServerKind.Direct)
                {
                    dot.SetClass("login__dot--on", false); dot.SetClass("login__dot--off", false); dot.SetClass("login__dot--busy", false);
                    pop.Set(""); ping.Set("");
                    continue;
                }
                var st = StatusOf(s);
                if (s.Kind == ServerKind.Host && (st == null || !st.Online))
                {   // ainda fechado: abre quando o jogador entrar
                    dot.SetClass("login__dot--on", false); dot.SetClass("login__dot--off", false); dot.SetClass("login__dot--busy", true);
                    pop.Set("abre ao entrar"); ping.Set("");
                    continue;
                }
                bool probing = Probing(s) && st == null;
                dot.SetClass("login__dot--on", st != null && st.Online && st.Load == "livre");
                dot.SetClass("login__dot--busy", st != null && st.Online && st.Load != "livre");
                dot.SetClass("login__dot--off", st != null && !st.Online);
                pop.Set(probing ? "..." : st == null ? "" : st.Online ? $"{st.Players}/{st.MaxPlayers}" : "offline");
                ping.Set(st != null && st.Online ? $"{st.LatencyMs} ms" : "");
            }
            for (int i = 0; i < _chanBtns.Count; i++)
            {
                _chanBtns[i].SetClass("kz-btn--on", i == _chan);
                var st = StatusOf(sel, i);
                _chanBtns[i].text = sel.Channels[i].Name + (st == null ? "" : st.Online ? $"  {st.Players}/{st.MaxPlayers}" : "  offline");
            }
            _lanRow.Show(sel.Kind == ServerKind.Host);
            _ipRow.Show(sel.Kind == ServerKind.Direct);
            _lanPort.SetEnabled(!G.Hosting);
            _hostHint.Show(sel.Kind != ServerKind.Remote);
            if (sel.Kind == ServerKind.Host) _hostHint.Set(HostHint());
            else if (sel.Kind == ServerKind.Direct) _hostHint.Set("Digite o IP e a porta que o seu amigo vê na tela dele em \"Servidor deste PC\". Fora da mesma rede, ele precisa liberar a porta no roteador.");
            _continue.text = G._connecting ? "Conectando..." : "Continuar";
            _continue.SetEnabled(!G._connecting);
            string err = G._loginError != "" ? G._loginError : _listError;
            _serverError.Set(err != "" ? $"<color=#ff7a6a>{err}</color>" : "");
            var ch = sel.Channels[Mathf.Clamp(_chan, 0, sel.Channels.Count - 1)];
            _accTitle.Set(sel.Name);
            var cs = StatusOf(sel, _chan);
            _accSub.Set(sel.Kind == ServerKind.Direct ? $"{_ipHost.value.Trim()}:{_ipPort.value.Trim()}"
                : sel.Kind == ServerKind.Host
                ? $"127.0.0.1:{HostPortValue()} · {(G.Hosting ? G.HostPlayers + " online · hospedado por este jogo" : "aberto em outra janela")}"
                : $"{ch.Name} · {sel.Host}:{ch.Port}" + (cs != null && cs.Online ? $" · {cs.Players}/{cs.MaxPlayers} online · {cs.LatencyMs} ms" : ""));
            _accError.Set(G._loginError != "" ? $"<color=#ff7a6a>{G._loginError}</color>" : "");
            _login.text = G._connecting ? "Conectando..." : _register.value ? "Criar conta e entrar" : "Entrar";
            _login.SetEnabled(!G._connecting);
        }
    }
}
