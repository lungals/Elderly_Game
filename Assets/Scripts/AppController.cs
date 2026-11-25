using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
[RequireComponent(typeof(AudioSource))]
public class AppController : MonoBehaviour
{
    
    [Serializable] public class Foto { public string id; public string arquivo; public string audio; public bool curtida; }
    [Serializable] public class Dados
    {
        public bool logado;
        public string login = "", email = "", nome = "NOME";
        public bool whatsApp;
        public List<Foto> fotos = new List<Foto>();
    }

    enum Tela { Menu, CriarConta, Principal, Camera, Perfil, FotosCurtidas, Galeria }

    const string LinkApp = "https://seusite.com/compartilhando-historias"; 

    AppViewModel vm;
    Dados dados;
    VisualElement root, popup, gradeCurtidas, gradeMinhas, iconeCoracao;
    Image imgCamera;
    readonly Dictionary<Tela, VisualElement> telas = new Dictionary<Tela, VisualElement>();
    readonly Dictionary<string, Texture2D> cacheTex = new Dictionary<string, Texture2D>();

    AudioSource fonteAudio;
    WebCamTexture webcam;
    AudioClip clipGravando;
    Foto ultimaFotoSessao;
    string audioPendente;
    int indiceFeed;

    string CaminhoDados => Path.Combine(Application.persistentDataPath, "dados.json");

  
    void OnEnable()
    {
        fonteAudio = GetComponent<AudioSource>();
        root = GetComponent<UIDocument>().rootVisualElement.Q("root");

        vm = new AppViewModel();
        root.dataSource = vm;            

        CarregarDados();
        vm.Login = dados.login; vm.Email = dados.email; vm.Nome = dados.nome; vm.WhatsAppVinculado = dados.whatsApp;

        telas[Tela.Menu]          = root.Q("tela-menu");
        telas[Tela.CriarConta]    = root.Q("tela-criar-conta");
        telas[Tela.Principal]     = root.Q("tela-principal");
        telas[Tela.Camera]        = root.Q("tela-camera");
        telas[Tela.Perfil]        = root.Q("tela-perfil");
        telas[Tela.FotosCurtidas] = root.Q("tela-fotos-curtidas");
        telas[Tela.Galeria]       = root.Q("tela-galeria");
        popup         = root.Q("popup-compartilhar");
        gradeCurtidas = root.Q("grade-curtidas");
        gradeMinhas   = root.Q("grade-minhas-fotos");
        iconeCoracao  = root.Q("icone-coracao");
        imgCamera     = root.Q<Image>("img-camera");

        //Menu
        Clique("btn-entrar",       () => Ir(Tela.Principal));
        Clique("btn-criar-conta",  () => Ir(Tela.CriarConta));
        Clique("btn-sair-app",     SairDoApp);

        //Criar conta
        Clique("btn-vincular-whatsapp", VincularWhatsApp);
        Clique("btn-confirmar-conta",   ConfirmarConta);
        Clique("btn-voltar-conta",      () => Ir(Tela.Menu));

        //Principal
        Clique("btn-configurar",     () => Ir(Tela.Perfil));
        Clique("btn-gostei",         Curtir);
        Clique("btn-tocar-audio",    AlternarAudio);
        Clique("btn-abrir-camera",   () => Ir(Tela.Camera));
        Clique("btn-abrir-galeria",  () => Ir(Tela.Galeria));
        root.Q("img-principal").RegisterCallback<ClickEvent>(_ => ProximaImagemFeed());

        //Câmera
        Clique("btn-voltar-camera", () => Ir(Tela.Principal));
        Clique("btn-fotografar",    Fotografar);
        Clique("btn-gravar-audio",  AlternarGravacao);

        //Perfil
        Clique("btn-voltar-perfil",      () => Ir(Tela.Principal));
        Clique("btn-fotos-curtidas",     () => Ir(Tela.FotosCurtidas));
        Clique("btn-adicionar-pessoas",  () => popup.style.display = DisplayStyle.Flex);
        Clique("btn-sair-conta",         SairDaConta);

        //Fotos curtidas & Galeria
        Clique("btn-voltar-curtidas",     () => Ir(Tela.Perfil));  
        Clique("btn-voltar-galeria",      () => Ir(Tela.Principal));
        Clique("btn-configurar-galeria",  () => Ir(Tela.Perfil));

        //Popup adicionar pessoas
        Clique("btn-convidar-whatsapp", () => Application.OpenURL("https://wa.me/?text=" + UnityWebRequest.EscapeURL($"Venha compartilhar histórias comigo! {LinkApp}")));
        Clique("btn-convidar-facebook", () => Application.OpenURL("https://www.facebook.com/sharer/sharer.php?u=" + UnityWebRequest.EscapeURL(LinkApp)));
        Clique("btn-convidar-outros",   () => GUIUtility.systemCopyBuffer = LinkApp);
        Clique("btn-fechar-popup",      () => popup.style.display = DisplayStyle.None);

        Ir(Tela.Menu);
    }

    void OnDisable() { PararCamera(); }

    void Clique(string nome, Action acao)
    {
        var b = root.Q<Button>(nome);
        if (b == null) { Debug.LogWarning($"Botão '{nome}' não encontrado no UXML"); return; }
        b.clicked += acao;
    }

   
    //NAVEGAÇÃO
    
    void Ir(Tela destino)
    {
        PararAudio();
        if (destino != Tela.Camera) PararCamera();
        popup.style.display = DisplayStyle.None;
        vm.MensagemErro = "";

        foreach (var kv in telas)
            kv.Value.style.display = kv.Key == destino ? DisplayStyle.Flex : DisplayStyle.None;

        switch (destino)
        {
            case Tela.Principal:      AtualizarFeed(); break;
            case Tela.Camera:         StartCoroutine(IniciarCamera()); break;
            case Tela.FotosCurtidas:  PreencherGrade(gradeCurtidas, dados.fotos.FindAll(f => f.curtida)); break;
            case Tela.Galeria:        PreencherGrade(gradeMinhas, dados.fotos); break;
        }
    }

    void SairDoApp()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void SairDaConta()
    {
        dados.logado = false;
        SalvarDados();
        Ir(Tela.Menu);
    }

    
    //CONTA
  
    void VincularWhatsApp()
    {
        
        Application.OpenURL("https://wa.me/");
        vm.WhatsAppVinculado = true;
    }

    void ConfirmarConta()
    {
        if (string.IsNullOrWhiteSpace(vm.Login)) { vm.MensagemErro = "Informe um login."; return; }
        if (!vm.Email.Contains("@") || !vm.Email.Contains(".")) { vm.MensagemErro = "Informe um email válido."; return; }

        dados.login = vm.Login.Trim();
        dados.email = vm.Email.Trim();
        dados.nome = dados.login.ToUpper();
        dados.whatsApp = vm.WhatsAppVinculado;
        dados.logado = true;
        vm.Nome = dados.nome;
        SalvarDados();
        Ir(Tela.Principal);
    }

    
    //TELA PRINCIPAL & FEED
    
    void AtualizarFeed()
    {
        PararAudio();
        if (dados.fotos.Count == 0)
        {
            vm.ImagemAtual = null; vm.CurtidaAtual = false; vm.TemAudio = false;
            iconeCoracao?.RemoveFromClassList("curtido");
            return;
        }
        indiceFeed = Mathf.Clamp(indiceFeed, 0, dados.fotos.Count - 1);
        var f = dados.fotos[indiceFeed];
        vm.ImagemAtual = CarregarTextura(f.arquivo);
        vm.CurtidaAtual = f.curtida;
        vm.TemAudio = !string.IsNullOrEmpty(f.audio) && File.Exists(f.audio);
        iconeCoracao?.EnableInClassList("curtido", f.curtida);
    }

    void ProximaImagemFeed()
    {
        if (dados.fotos.Count == 0) return;
        indiceFeed = (indiceFeed + 1) % dados.fotos.Count;
        AtualizarFeed();
    }

    void Curtir()
    {
        if (dados.fotos.Count == 0) return;
        var f = dados.fotos[indiceFeed];
        f.curtida = !f.curtida;          
        SalvarDados();
        vm.CurtidaAtual = f.curtida;
        iconeCoracao?.EnableInClassList("curtido", f.curtida);
    }

    //Áudio
    void AlternarAudio()
    {
        if (!vm.TemAudio) return;
        if (vm.TocandoAudio) { PararAudio(); return; }
        StartCoroutine(TocarAudio(dados.fotos[indiceFeed].audio));
    }

    IEnumerator TocarAudio(string caminho)
    {
        using var req = UnityWebRequestMultimedia.GetAudioClip("file://" + caminho, AudioType.WAV);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) { Debug.LogWarning(req.error); yield break; }

        fonteAudio.clip = DownloadHandlerAudioClip.GetContent(req);
        fonteAudio.Play();
        vm.TocandoAudio = true;
        yield return new WaitWhile(() => fonteAudio.isPlaying);
        vm.TocandoAudio = false;
    }

    void PararAudio()
    {
        if (fonteAudio != null) fonteAudio.Stop();
        if (vm != null) vm.TocandoAudio = false;
    }


    //CÂMERA & GRAVAÇÃO
   
    IEnumerator IniciarCamera()
    {
        ultimaFotoSessao = null;
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam | UserAuthorization.Microphone);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam) || WebCamTexture.devices.Length == 0)
        {
            Debug.LogWarning("Sem permissão ou sem câmera.");
            yield break;
        }
        webcam = new WebCamTexture(WebCamTexture.devices[0].name, 1280, 720);
        webcam.Play();
        vm.CameraTexture = webcam;
    }

    void PararCamera()
    {
        if (Microphone.IsRecording(null)) Microphone.End(null);
        if (vm != null) vm.Gravando = false;
        if (webcam != null) { webcam.Stop(); Destroy(webcam); webcam = null; }
        if (vm != null) vm.CameraTexture = null;
    }

    void Update()
    {
       
        if (webcam != null && webcam.isPlaying && imgCamera != null && webcam.width > 16)
        {
            imgCamera.style.rotate = new Rotate(new Angle(webcam.videoRotationAngle, AngleUnit.Degree));
            imgCamera.style.scale = new Scale(new Vector3(1, webcam.videoVerticallyMirrored ? -1 : 1, 1));
            imgCamera.MarkDirtyRepaint();
        }
    }

    void Fotografar()
    {
        if (webcam == null || !webcam.isPlaying || webcam.width < 16) return;

        var tex = new Texture2D(webcam.width, webcam.height, TextureFormat.RGB24, false);
        tex.SetPixels32(webcam.GetPixels32());
        tex.Apply();
        tex = Rotacionar(tex, webcam.videoRotationAngle, webcam.videoVerticallyMirrored);

        string id = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string arq = Path.Combine(Application.persistentDataPath, $"foto_{id}.jpg");
        File.WriteAllBytes(arq, tex.EncodeToJPG(90));
        Destroy(tex);

        var f = new Foto { id = id, arquivo = arq, audio = audioPendente };
        audioPendente = null;
        dados.fotos.Insert(0, f);
        ultimaFotoSessao = f;
        indiceFeed = 0;
        SalvarDados();
    }

    void AlternarGravacao()
    {
        if (!vm.Gravando)
        {
            if (Microphone.devices.Length == 0) return;
            clipGravando = Microphone.Start(null, false, 120, 44100);
            vm.Gravando = true;
            return;
        }

        int amostras = Microphone.GetPosition(null);
        Microphone.End(null);
        vm.Gravando = false;
        if (clipGravando == null || amostras <= 0) return;

        string arq = Path.Combine(Application.persistentDataPath, $"audio_{DateTime.Now:yyyyMMdd_HHmmss_fff}.wav");
        SalvarWav(arq, clipGravando, amostras);

      
        if (ultimaFotoSessao != null) { ultimaFotoSessao.audio = arq; SalvarDados(); }
        else audioPendente = arq;
    }

    
    //GRADES (Galeria & Fotos curtidas)
    
    void PreencherGrade(VisualElement grade, List<Foto> fotos)
    {
        grade.Clear();
        int total = Mathf.Max(9, Mathf.CeilToInt(fotos.Count / 3f) * 3); 
        for (int i = 0; i < total; i++)
        {
            var celula = new VisualElement();
            celula.AddToClassList("celula");
            if (i < fotos.Count)
            {
                var foto = fotos[i];
                celula.style.backgroundImage = new StyleBackground(CarregarTextura(foto.arquivo));
                celula.RegisterCallback<ClickEvent>(_ =>
                {
                    indiceFeed = dados.fotos.IndexOf(foto);
                    Ir(Tela.Principal);         
                });
            }
            grade.Add(celula);
        }
    }

    //UTILITÁRIOS  
    Texture2D CarregarTextura(string caminho)
    {
        if (cacheTex.TryGetValue(caminho, out var t) && t != null) return t;
        if (!File.Exists(caminho)) return null;
        t = new Texture2D(2, 2);
        t.LoadImage(File.ReadAllBytes(caminho));
        cacheTex[caminho] = t;
        return t;
    }

    static Texture2D Rotacionar(Texture2D src, int graus, bool espelhadoVert)
    {
        graus = ((graus % 360) + 360) % 360;
        int w = src.width, h = src.height;
        var s = src.GetPixels32();
        bool troca = graus == 90 || graus == 270;
        int nw = troca ? h : w, nh = troca ? w : h;
        var d = new Color32[s.Length];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int yy = espelhadoVert ? h - 1 - y : y;
                int nx, ny;
                switch (graus)
                {
                    case 90:  nx = yy;         ny = w - 1 - x; break;
                    case 180: nx = w - 1 - x;  ny = h - 1 - yy; break;
                    case 270: nx = h - 1 - yy; ny = x;         break;
                    default:  nx = x;          ny = yy;        break;
                }
                d[ny * nw + nx] = s[y * w + x];
            }

        var r = new Texture2D(nw, nh, TextureFormat.RGB24, false);
        r.SetPixels32(d); r.Apply();
        UnityEngine.Object.Destroy(src);
        return r;
    }

    static void SalvarWav(string caminho, AudioClip clip, int amostras)
    {
        var dadosFloat = new float[amostras * clip.channels];
        clip.GetData(dadosFloat, 0);
        using var fs = new FileStream(caminho, FileMode.Create);
        using var bw = new BinaryWriter(fs);
        int bytes = dadosFloat.Length * 2;
        bw.Write(new[] { 'R', 'I', 'F', 'F' }); bw.Write(36 + bytes);
        bw.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
        bw.Write(16); bw.Write((short)1); bw.Write((short)clip.channels);
        bw.Write(clip.frequency); bw.Write(clip.frequency * clip.channels * 2);
        bw.Write((short)(clip.channels * 2)); bw.Write((short)16);
        bw.Write(new[] { 'd', 'a', 't', 'a' }); bw.Write(bytes);
        foreach (var f in dadosFloat) bw.Write((short)(Mathf.Clamp(f, -1f, 1f) * short.MaxValue));
    }

    void CarregarDados()
    {
        try { dados = File.Exists(CaminhoDados) ? JsonUtility.FromJson<Dados>(File.ReadAllText(CaminhoDados)) : new Dados(); }
        catch { dados = new Dados(); }
        dados ??= new Dados();
    }

    void SalvarDados() => File.WriteAllText(CaminhoDados, JsonUtility.ToJson(dados));
}
